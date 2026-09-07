'use client'

import { useCallback, useEffect, useRef, useState } from 'react'
import { portalApi } from '@/services/portalApi'
import { useChatSocket } from '@/hooks/useChatSocket'
import type { ChatBubble, SendMessageRequest } from '@/types/chat'

interface GuestChatWidgetProps {
  token: string
  bookingId: string
  roomId: string
  roomFloor: number
  roomNumber: string
  customerName: string
}

const generateId = () => (typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).substring(2, 11))

const GOLD = '#e2b96f'
const GOLD_DIM = 'rgba(226,185,111,0.15)'

const QUICK_ACTIONS = [
  { label: '🧹 Room Cleaning', message: 'I need room cleaning service please.' },
  { label: '🛁 Extra Towels', message: 'Could I get extra towels delivered to my room?' },
  { label: '❄️ AC Issue', message: 'The air conditioning in my room is not working properly.' },
  { label: '📶 WiFi Help', message: 'What is the WiFi network name and how do I connect?' },
  { label: '🕒 Checkout Time', message: 'What is the checkout time?' },
  { label: '🗣 Talk to Staff', message: null },  // triggers escalation
]

export default function GuestChatWidget({
  token, bookingId, roomId, roomFloor, roomNumber, customerName,
}: GuestChatWidgetProps) {
  const [isOpen, setIsOpen]       = useState(false)
  const [input, setInput]         = useState('')
  const [isSending, setIsSending] = useState(false)
  const [isEscalated, setIsEscalated] = useState(false)
  const [sessionId, setSessionId] = useState<string | null>(null)
  const [bubbles, setBubbles]     = useState<ChatBubble[]>([])
  const [unreadCount, setUnreadCount] = useState(0)
  const bottomRef = useRef<HTMLDivElement>(null)
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  const { streamingContent, isStreaming, isConnected, joinSession, resetStreaming } = useChatSocket({
    token,
    sessionId,
  })

  // ── Hydrate history on open ───────────────────────────────────────────────
  useEffect(() => {
    if (!isOpen || bubbles.length > 0) return
    portalApi.getChatHistory(token, bookingId).then(msgs => {
      const hydrated: ChatBubble[] = msgs
        .filter(m => m.role === 'User' || m.role === 'Assistant')
        .map(m => ({
          id:        m.id,
          role:      m.role === 'User' ? 'user' : 'assistant',
          content:   m.content,
          createdAt: new Date(m.createdAt),
        }))
      if (hydrated.length > 0) setBubbles(hydrated)
    }).catch(() => {
      // Silent — chat starts fresh
    })
  }, [isOpen])

  // ── Add greeting on first open ────────────────────────────────────────────
  useEffect(() => {
    if (isOpen && bubbles.length === 0 && !isSending) {
      setBubbles([{
        id:        'greeting',
        role:      'assistant',
        content:   `Hello ${customerName}! 👋 I'm Aria, your SmartHotel AI concierge. How can I help you today? You can ask me about hotel facilities, report an issue, or request a service.`,
        createdAt: new Date(),
      }])
    }
  }, [isOpen])

  // ── Scroll to bottom on new message ──────────────────────────────────────
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
    if (!isOpen && bubbles.length > 0) setUnreadCount(c => c + 1)
  }, [bubbles])

  useEffect(() => {
    if (isOpen) setUnreadCount(0)
  }, [isOpen])

  // ── Commit streaming message to bubbles once stream completes ─────────────
  const streamingBubbleIdRef = useRef<string | null>(null)

  useEffect(() => {
    if (isStreaming && streamingContent && !streamingBubbleIdRef.current) {
      const id = generateId()
      streamingBubbleIdRef.current = id
      setBubbles(prev => [...prev, {
        id, role: 'assistant', content: streamingContent, isStreaming: true, createdAt: new Date(),
      }])
    } else if (isStreaming && streamingContent && streamingBubbleIdRef.current) {
      setBubbles(prev => prev.map(b =>
        b.id === streamingBubbleIdRef.current
          ? { ...b, content: streamingContent }
          : b,
      ))
    } else if (!isStreaming && streamingBubbleIdRef.current) {
      setBubbles(prev => prev.map(b =>
        b.id === streamingBubbleIdRef.current
          ? { ...b, isStreaming: false }
          : b,
      ))
      streamingBubbleIdRef.current = null
      resetStreaming()
    }
  }, [streamingContent, isStreaming])

  // ── Send message ──────────────────────────────────────────────────────────
  const sendMessage = useCallback(async (text: string) => {
    if (!text.trim() || isSending) return
    setIsSending(true)
    setInput('')

    const userBubble: ChatBubble = {
      id: generateId(), role: 'user', content: text.trim(), createdAt: new Date(),
    }
    setBubbles(prev => [...prev, userBubble])

    try {
      const req: SendMessageRequest = {
        message: text.trim(), bookingId, roomId, roomFloor, roomNumber, customerName,
      }
      const result = await portalApi.sendChatMessage(token, req)

      // If not streaming (fallback / non-streaming provider), add reply bubble directly
      if (!isStreaming && !streamingBubbleIdRef.current) {
        const replyBubble: ChatBubble = {
          id:               generateId(),
          role:             'assistant',
          content:          result.replyText,
          createdAt:        new Date(),
          ticketRef:        result.ticketReference ?? undefined,
          serviceRequestId: result.serviceRequestId ?? undefined,
          isEscalated:      result.isEscalated,
          isFallback:       result.isFallback,
        }
        setBubbles(prev => [...prev, replyBubble])
      }

      if (result.isEscalated) setIsEscalated(true)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Something went wrong. Please try again.'
      setBubbles(prev => [...prev, {
        id: generateId(), role: 'assistant',
        content: `⚠️ ${msg}`,
        createdAt: new Date(), isFallback: true,
      }])
    } finally {
      setIsSending(false)
    }
  }, [token, bookingId, roomId, roomFloor, roomNumber, customerName, isSending, isStreaming])

  // ── Escalate to human ─────────────────────────────────────────────────────
  const handleEscalate = useCallback(async () => {
    try {
      await portalApi.escalateChatSession(token, { bookingId, roomFloor, roomNumber })
      setIsEscalated(true)
      setBubbles(prev => [...prev, {
        id: generateId(), role: 'assistant',
        content: '✅ A front desk team member has been notified and will reach out to you shortly.',
        createdAt: new Date(),
      }])
    } catch {
      setBubbles(prev => [...prev, {
        id: generateId(), role: 'assistant',
        content: '⚠️ Could not reach front desk right now. Please call reception directly.',
        createdAt: new Date(),
      }])
    }
  }, [token, bookingId, roomFloor, roomNumber])

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      sendMessage(input)
    }
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <>
      {/* ── Floating trigger button ─────────────────────────────────────────── */}
      <button
        id="chat-widget-trigger"
        onClick={() => setIsOpen(o => !o)}
        aria-label="Open AI Concierge Chat"
        style={{
          position: 'fixed', bottom: 28, right: 28, zIndex: 1000,
          width: 60, height: 60, borderRadius: '50%',
          background: `linear-gradient(135deg, ${GOLD}, #c8914a)`,
          border: 'none', cursor: 'pointer', boxShadow: '0 4px 20px rgba(226,185,111,0.4)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          transition: 'transform 0.2s, box-shadow 0.2s',
        }}
        onMouseEnter={e => {
          (e.currentTarget as HTMLElement).style.transform = 'scale(1.1)'
          ;(e.currentTarget as HTMLElement).style.boxShadow = '0 6px 28px rgba(226,185,111,0.6)'
        }}
        onMouseLeave={e => {
          (e.currentTarget as HTMLElement).style.transform = 'scale(1)'
          ;(e.currentTarget as HTMLElement).style.boxShadow = '0 4px 20px rgba(226,185,111,0.4)'
        }}
      >
        <span style={{ fontSize: 26 }}>{isOpen ? '✕' : '💬'}</span>
        {unreadCount > 0 && !isOpen && (
          <span style={{
            position: 'absolute', top: 0, right: 0,
            background: '#e53e3e', color: '#fff', fontSize: 11, fontWeight: 700,
            borderRadius: '50%', width: 20, height: 20,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>{unreadCount}</span>
        )}
      </button>

      {/* ── Chat drawer ─────────────────────────────────────────────────────── */}
      {isOpen && (
        <div
          id="chat-widget-drawer"
          style={{
            position: 'fixed', bottom: 100, right: 28, zIndex: 999,
            width: 420, maxWidth: 'calc(100vw - 32px)',
            height: 580, maxHeight: 'calc(100vh - 140px)',
            borderRadius: 20, overflow: 'hidden',
            background: 'linear-gradient(180deg, #1a1730 0%, #0f0c29 100%)',
            border: '1px solid rgba(226,185,111,0.25)',
            boxShadow: '0 20px 60px rgba(0,0,0,0.5), 0 0 0 1px rgba(226,185,111,0.1)',
            display: 'flex', flexDirection: 'column',
            animation: 'chat-slide-in 0.25s ease-out',
          }}
        >
          {/* Header */}
          <div style={{
            padding: '14px 18px',
            background: 'linear-gradient(135deg, rgba(226,185,111,0.12), rgba(226,185,111,0.05))',
            borderBottom: '1px solid rgba(226,185,111,0.15)',
            display: 'flex', alignItems: 'center', gap: 12, flexShrink: 0,
          }}>
            <div style={{
              width: 40, height: 40, borderRadius: '50%',
              background: `linear-gradient(135deg, ${GOLD}, #c8914a)`,
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 20, flexShrink: 0,
            }}>✨</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <p style={{ margin: 0, color: '#fff', fontWeight: 700, fontSize: 15 }}>Aria</p>
              <p style={{ margin: 0, color: GOLD, fontSize: 12 }}>
                SmartHotel AI Concierge
                {' '}
                <span style={{
                  display: 'inline-block', width: 7, height: 7, borderRadius: '50%',
                  background: isConnected ? '#48bb78' : '#718096',
                  verticalAlign: 'middle', marginLeft: 4,
                }} />
              </p>
            </div>
            {!isEscalated && (
              <button
                id="chat-escalate-btn"
                onClick={handleEscalate}
                title="Talk to a Human"
                style={{
                  background: GOLD_DIM, border: `1px solid rgba(226,185,111,0.3)`,
                  borderRadius: 8, color: GOLD, fontSize: 12, padding: '5px 10px',
                  cursor: 'pointer', whiteSpace: 'nowrap', transition: 'background 0.2s',
                }}
              >🗣 Staff</button>
            )}
            {isEscalated && (
              <span style={{ fontSize: 11, color: '#48bb78', fontWeight: 600 }}>👥 Staff Notified</span>
            )}
          </div>

          {/* Quick actions */}
          {bubbles.length <= 1 && (
            <div style={{
              padding: '10px 14px 0',
              display: 'flex', flexWrap: 'wrap', gap: 6, flexShrink: 0,
            }}>
              {QUICK_ACTIONS.map(action => (
                <button
                  key={action.label}
                  onClick={() => action.message ? sendMessage(action.message) : handleEscalate()}
                  style={{
                    background: GOLD_DIM, border: `1px solid rgba(226,185,111,0.2)`,
                    borderRadius: 20, color: '#e2e8f0', fontSize: 12, padding: '5px 11px',
                    cursor: 'pointer', transition: 'all 0.15s', whiteSpace: 'nowrap',
                  }}
                  onMouseEnter={e => {
                    (e.currentTarget as HTMLElement).style.background = 'rgba(226,185,111,0.25)'
                  }}
                  onMouseLeave={e => {
                    (e.currentTarget as HTMLElement).style.background = GOLD_DIM
                  }}
                >
                  {action.label}
                </button>
              ))}
            </div>
          )}

          {/* Message list */}
          <div style={{ flex: 1, overflowY: 'auto', padding: '14px 16px', display: 'flex', flexDirection: 'column', gap: 12 }}>
            {bubbles.map(bubble => (
              <ChatBubbleItem key={bubble.id} bubble={bubble} />
            ))}
            {isSending && !isStreaming && (
              <TypingIndicator />
            )}
            <div ref={bottomRef} />
          </div>

          {/* Input area */}
          <div style={{
            borderTop: '1px solid rgba(226,185,111,0.12)',
            padding: '12px 14px',
            display: 'flex', gap: 10, alignItems: 'flex-end', flexShrink: 0,
            background: 'rgba(255,255,255,0.02)',
          }}>
            <textarea
              ref={textareaRef}
              id="chat-message-input"
              value={input}
              onChange={e => setInput(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Type a message… (Enter to send)"
              disabled={isSending || isEscalated}
              rows={1}
              style={{
                flex: 1, background: 'rgba(255,255,255,0.07)',
                border: '1px solid rgba(226,185,111,0.2)', borderRadius: 12,
                color: '#e2e8f0', fontSize: 14, padding: '10px 14px',
                resize: 'none', outline: 'none', maxHeight: 100, overflowY: 'auto',
                transition: 'border-color 0.2s', lineHeight: 1.5,
              }}
              onFocus={e => { (e.target as HTMLElement).style.borderColor = GOLD }}
              onBlur={e => { (e.target as HTMLElement).style.borderColor = 'rgba(226,185,111,0.2)' }}
            />
            <button
              id="chat-send-btn"
              onClick={() => sendMessage(input)}
              disabled={isSending || !input.trim() || isEscalated}
              style={{
                width: 42, height: 42, borderRadius: '50%',
                background: input.trim() && !isSending
                  ? `linear-gradient(135deg, ${GOLD}, #c8914a)`
                  : 'rgba(255,255,255,0.08)',
                border: 'none', cursor: input.trim() && !isSending ? 'pointer' : 'not-allowed',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontSize: 18, transition: 'all 0.2s', flexShrink: 0,
              }}
            >
              {isSending ? '⟳' : '➤'}
            </button>
          </div>
        </div>
      )}

      <style>{`
        @keyframes chat-slide-in {
          from { opacity: 0; transform: translateY(16px) scale(0.97); }
          to   { opacity: 1; transform: translateY(0)    scale(1);    }
        }
        @keyframes blink {
          0%, 100% { opacity: 1; } 50% { opacity: 0; }
        }
        @keyframes dot-bounce {
          0%, 100% { transform: translateY(0); }
          50%       { transform: translateY(-4px); }
        }
      `}</style>
    </>
  )
}

// ── Sub-components ────────────────────────────────────────────────────────────

function ChatBubbleItem({ bubble }: { bubble: ChatBubble }) {
  const isUser = bubble.role === 'user'
  return (
    <div style={{
      display: 'flex', flexDirection: isUser ? 'row-reverse' : 'row',
      gap: 8, alignItems: 'flex-end',
    }}>
      {!isUser && (
        <div style={{
          width: 28, height: 28, borderRadius: '50%', flexShrink: 0,
          background: 'linear-gradient(135deg, #e2b96f, #c8914a)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 14,
        }}>✨</div>
      )}
      <div style={{ maxWidth: '78%', display: 'flex', flexDirection: 'column', gap: 4 }}>
        <div style={{
          background:   isUser ? 'linear-gradient(135deg, #e2b96f, #c8914a)' : 'rgba(255,255,255,0.08)',
          color:        isUser ? '#1a1730' : '#e2e8f0',
          borderRadius: isUser ? '18px 18px 4px 18px' : '18px 18px 18px 4px',
          padding: '10px 14px', fontSize: 14, lineHeight: 1.55, wordBreak: 'break-word',
        }}>
          {bubble.content}
          {bubble.isStreaming && (
            <span style={{ animation: 'blink 0.7s infinite', marginLeft: 2 }}>▌</span>
          )}
        </div>
        {/* Ticket card */}
        {bubble.ticketRef && (
          <div style={{
            background: 'rgba(72,187,120,0.1)', border: '1px solid rgba(72,187,120,0.3)',
            borderRadius: 10, padding: '8px 12px', fontSize: 12, color: '#48bb78',
          }}>
            🎫 Ticket logged: <strong>{bubble.ticketRef}</strong>
            <span style={{
              display: 'inline-block', marginLeft: 8,
              background: 'rgba(72,187,120,0.15)', borderRadius: 10, padding: '1px 7px',
              fontSize: 11,
            }}>Pending</span>
          </div>
        )}
        {bubble.isFallback && (
          <div style={{ fontSize: 11, color: '#ed8936', marginLeft: 4 }}>
            ⚠️ AI offline — front desk notified
          </div>
        )}
      </div>
    </div>
  )
}

function TypingIndicator() {
  return (
    <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end' }}>
      <div style={{
        width: 28, height: 28, borderRadius: '50%',
        background: 'linear-gradient(135deg, #e2b96f, #c8914a)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 14, flexShrink: 0,
      }}>✨</div>
      <div style={{
        background: 'rgba(255,255,255,0.08)', borderRadius: '18px 18px 18px 4px',
        padding: '12px 18px', display: 'flex', gap: 5, alignItems: 'center',
      }}>
        {[0, 1, 2].map(i => (
          <div key={i} style={{
            width: 7, height: 7, borderRadius: '50%', background: '#e2b96f',
            animation: `dot-bounce 1.2s ease-in-out ${i * 0.2}s infinite`,
          }} />
        ))}
      </div>
    </div>
  )
}
