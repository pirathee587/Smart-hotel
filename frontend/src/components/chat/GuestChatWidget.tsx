'use client'

import { useConciergeStream } from '@/hooks/useConciergeStream'
import type { ConciergeServiceRequestDto } from '@/types/chat'
import {
  AlertCircle,
  Bot,
  CheckCircle2,
  Clock,
  Maximize2,
  Minimize2,
  RotateCcw,
  Send,
  Sparkles,
  User,
  X,
} from 'lucide-react'
import React, { useCallback, useEffect, useRef, useState } from 'react'

interface GuestChatWidgetProps {
  token: string
  bookingId?: string
  roomId?: string
  roomFloor?: number
  roomNumber?: string
  customerName?: string
}

const QUICK_ACTIONS = [
  { label: '🧹 Extra Towels', message: 'Could you please send extra bath towels to my room?' },
  { label: '🔧 AC Temperature', message: 'The air conditioning in my room is running too cold. Could maintenance check it?' },
  { label: '🏊 Pool & Spa Hours', message: 'What are the operating hours for the infinity pool and spa?' },
  { label: '🍽 Breakfast Times', message: 'What time is breakfast served and where is the dining hall located?' },
  { label: '🕒 Checkout Time', message: 'What is the checkout time for today?' },
]

export default function GuestChatWidget({
  token,
  customerName = 'Valued Guest',
}: GuestChatWidgetProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [isExpanded, setIsExpanded] = useState(false)
  const [input, setInput] = useState('')

  const messagesEndRef = useRef<HTMLDivElement>(null)
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  const {
    messages,
    isStreaming,
    error,
    context,
    activeRequests,
    sendMessage,
    cancelStream,
    resetChat,
  } = useConciergeStream({
    token,
    customerName,
  })

  // Derived unread count for guest when drawer is closed
  const unreadCount = isOpen
    ? 0
    : Math.max(0, messages.filter((m) => m.role === 'assistant' && m.id !== 'greeting').length)

  // Auto-scroll on new messages or streaming tokens
  useEffect(() => {
    if (isOpen) {
      messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
    }
  }, [messages, isOpen])


  // Handle message submission
  const handleSend = useCallback(async () => {
    const text = input.trim()
    if (!text || isStreaming) return
    setInput('')
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto'
    }
    await sendMessage(text)
  }, [input, isStreaming, sendMessage])

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  // Adjust textarea height dynamically
  const handleInputChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setInput(e.target.value)
    e.target.style.height = 'auto'
    e.target.style.height = `${Math.min(e.target.scrollHeight, 120)}px`
  }

  // Render status badge for tracked request
  const renderRequestStatusBadge = (req: ConciergeServiceRequestDto) => {
    switch (req.status) {
      case 'REQUEST_PENDING':
        return (
          <div className="mt-2.5 rounded-xl border border-amber-500/30 bg-amber-950/40 p-2.5 text-xs text-amber-200">
            <div className="flex items-center justify-between">
              <span className="flex items-center gap-1.5 font-medium">
                <Clock className="h-3.5 w-3.5 animate-spin text-amber-400" />
                Queued with Concierge
              </span>
              <span className="text-[10px] text-amber-300/70 font-mono">
                Ref #{req.requestId.substring(0, 8)}
              </span>
            </div>
            <p className="mt-1 text-[11px] text-amber-200/80">
              {req.requestType} • Room {req.roomNumber}: &quot;{req.description}&quot;
            </p>
          </div>
        )
      case 'TASK_CREATED':
        return (
          <div className="mt-2.5 rounded-xl border border-sky-500/30 bg-sky-950/40 p-2.5 text-xs text-sky-200">
            <div className="flex items-center justify-between">
              <span className="flex items-center gap-1.5 font-medium">
                <CheckCircle2 className="h-3.5 w-3.5 text-sky-400" />
                Confirmed with {req.requestType}
              </span>
              <span className="text-[10px] text-sky-300/70 font-mono">
                Ref #{req.requestId.substring(0, 8)}
              </span>
            </div>
            <p className="mt-1 text-[11px] text-sky-200/80">
              Awaiting manager assignment for Room {req.roomNumber}.
            </p>
          </div>
        )
      case 'ASSIGNED':
      case 'IN_PROGRESS':
        return (
          <div className="mt-2.5 rounded-xl border border-indigo-500/30 bg-indigo-950/40 p-2.5 text-xs text-indigo-200">
            <div className="flex items-center justify-between">
              <span className="flex items-center gap-1.5 font-medium">
                <User className="h-3.5 w-3.5 text-indigo-400" />
                Staff Assigned &amp; In Progress
              </span>
              <span className="text-[10px] text-indigo-300/70 font-mono">
                Task #{req.taskId ? req.taskId.substring(0, 8) : req.requestId.substring(0, 8)}
              </span>
            </div>
            <p className="mt-1 text-[11px] text-indigo-200/80">
              Our {req.requestType} specialist is currently attending to Room {req.roomNumber}.
            </p>
          </div>
        )
      case 'COMPLETED':
        return (
          <div className="mt-2.5 rounded-xl border border-emerald-500/30 bg-emerald-950/40 p-2.5 text-xs text-emerald-200">
            <div className="flex items-center justify-between">
              <span className="flex items-center gap-1.5 font-medium">
                <CheckCircle2 className="h-3.5 w-3.5 text-emerald-400" />
                Service Completed
              </span>
              <span className="text-[10px] text-emerald-300/70 font-mono">
                Ref #{req.requestId.substring(0, 8)}
              </span>
            </div>
            <p className="mt-1 text-[11px] text-emerald-200/80">
              Your request for Room {req.roomNumber} has been fulfilled.
            </p>
          </div>
        )
      case 'FAILED':
        return (
          <div className="mt-2.5 rounded-xl border border-rose-500/30 bg-rose-950/40 p-2.5 text-xs text-rose-200">
            <div className="flex items-center justify-between">
              <span className="flex items-center gap-1.5 font-medium">
                <AlertCircle className="h-3.5 w-3.5 text-rose-400" />
                Dispatch Issue
              </span>
            </div>
            <p className="mt-1 text-[11px] text-rose-200/80">
              Unable to queue request automatically. Please dial 0 from your room phone.
            </p>
          </div>
        )
      default:
        return null
    }
  }

  return (
    <>
      {/* Floating Concierge Launcher Button */}
      {!isOpen && (
        <button
          onClick={() => setIsOpen(true)}
          className="fixed bottom-6 right-6 z-50 flex items-center gap-3 rounded-full bg-[#16302C] px-5 py-3.5 text-[#F6F1E6] shadow-2xl border border-[#2F5C52]/50 hover:bg-[#1C3D38] hover:border-[#E07A3E]/60 transition-all duration-300 group focus:outline-none focus:ring-2 focus:ring-[#E07A3E]"
          aria-label="Open AI Concierge chat"
        >
          <div className="relative">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-[#E07A3E] text-white shadow-md group-hover:scale-105 transition-transform">
              <Bot className="h-5 w-5" />
            </div>
            {unreadCount > 0 && (
              <span className="absolute -top-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full bg-rose-500 text-[10px] font-bold text-white shadow">
                {unreadCount}
              </span>
            )}
          </div>
          <div className="text-left pr-1">
            <p className="text-xs uppercase tracking-wider text-[#E07A3E] font-semibold">
              AI Concierge
            </p>
            <p className="text-sm font-medium text-[#F6F1E6]">
              {context.verifiedRoom ? `Room ${context.verifiedRoom}` : 'Ask Aria'}
            </p>
          </div>
        </button>
      )}

      {/* Main Chat Drawer Window */}
      {isOpen && (
        <aside
          className={`fixed bottom-6 right-6 z-50 flex flex-col overflow-hidden rounded-2xl bg-[#0F1B1A]/95 backdrop-blur-xl border border-[#2F5C52]/40 shadow-2xl text-[#F6F1E6] transition-all duration-300 ${
            isExpanded
              ? 'w-[92vw] h-[85vh] sm:w-[680px] sm:h-[750px]'
              : 'w-[94vw] h-[580px] sm:w-[420px]'
          }`}
          aria-label="AI Concierge dialogue"
          role="dialog"
          aria-modal="true"
        >
          {/* Header */}
          <header className="flex items-center justify-between border-b border-[#2F5C52]/30 bg-[#16302C]/80 px-4 py-3.5 backdrop-blur-md">
            <div className="flex items-center gap-3">
              <div className="flex h-9 w-9 items-center justify-center rounded-full bg-[#E07A3E] text-white shadow-sm">
                <Sparkles className="h-4 w-4" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-sm font-semibold text-[#F6F1E6]">Aria · AI Concierge</h3>
                  <span className="flex h-2 w-2 rounded-full bg-emerald-400 animate-pulse" />
                </div>
                <p className="text-[11px] text-[#A2B5AF]">
                  {context.verifiedRoom ? (
                    <span className="text-[#E07A3E] font-medium">
                      Room {context.verifiedRoom} • Verified Stay
                    </span>
                  ) : (
                    'SmartHotel Maskeliya Guest Assistant'
                  )}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-1 text-[#A2B5AF]">
              <button
                onClick={resetChat}
                className="rounded-lg p-1.5 hover:bg-[#244741] hover:text-white transition-colors"
                title="Reset conversation"
                aria-label="Reset conversation"
              >
                <RotateCcw className="h-4 w-4" />
              </button>
              <button
                onClick={() => setIsExpanded(!isExpanded)}
                className="rounded-lg p-1.5 hover:bg-[#244741] hover:text-white transition-colors hidden sm:block"
                title={isExpanded ? 'Restore window size' : 'Expand window'}
                aria-label={isExpanded ? 'Restore window size' : 'Expand window'}
              >
                {isExpanded ? (
                  <Minimize2 className="h-4 w-4" />
                ) : (
                  <Maximize2 className="h-4 w-4" />
                )}
              </button>
              <button
                onClick={() => setIsOpen(false)}
                className="rounded-lg p-1.5 hover:bg-[#244741] hover:text-white transition-colors"
                title="Close chat"
                aria-label="Close chat"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
          </header>

          {/* Active Requests Status Banner (if any open) */}
          {activeRequests.some(
            (r) => r.status === 'REQUEST_PENDING' || r.status === 'TASK_CREATED' || r.status === 'ASSIGNED',
          ) && (
            <div className="border-b border-[#2F5C52]/30 bg-[#142824] px-4 py-2 text-xs flex items-center justify-between text-[#F6F1E6]">
              <span className="flex items-center gap-2">
                <span className="h-2 w-2 rounded-full bg-[#E07A3E] animate-ping" />
                <span className="font-medium text-[#E07A3E]">
                  {activeRequests.filter(
                    (r) => r.status !== 'COMPLETED' && r.status !== 'FAILED',
                  ).length}{' '}
                  Active Operational Request(s)
                </span>
              </span>
              <span className="text-[11px] text-[#A2B5AF]">Live Status Connected</span>
            </div>
          )}

          {/* Messages Scroll Area */}
          <div
            className="flex-1 overflow-y-auto p-4 space-y-4 text-sm"
            role="log"
            aria-live="polite"
            aria-relevant="additions text"
          >
            {messages.map((msg) => {
              const isUser = msg.role === 'user'
              // Find any corresponding active service request
              const matchingReq = msg.serviceRequestId
                ? activeRequests.find((r) => r.requestId === msg.serviceRequestId)
                : null

              return (
                <div
                  key={msg.id}
                  className={`flex flex-col ${isUser ? 'items-end' : 'items-start'}`}
                >
                  <div
                    className={`max-w-[85%] rounded-2xl px-4 py-3 leading-relaxed shadow-sm ${
                      isUser
                        ? 'bg-[#E07A3E] text-white rounded-br-none'
                        : 'bg-[#16302C] text-[#F6F1E6] rounded-bl-none border border-[#2F5C52]/30'
                    }`}
                  >
                    <p className="whitespace-pre-wrap">{msg.content}</p>

                    {/* Associated service request card */}
                    {matchingReq && renderRequestStatusBadge(matchingReq)}
                  </div>

                  <span className="mt-1 px-1 text-[10px] text-[#7C9188]">
                    {msg.createdAt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                  </span>
                </div>
              )
            })}

            {/* Streaming Indicator */}
            {isStreaming && (
              <div className="flex items-center gap-2 text-xs text-[#A2B5AF] italic px-2">
                <span className="h-2 w-2 rounded-full bg-[#E07A3E] animate-bounce" />
                Aria is typing...
                <button
                  onClick={cancelStream}
                  className="ml-2 text-[11px] text-rose-300 underline hover:text-rose-200 not-italic"
                >
                  Stop
                </button>
              </div>
            )}

            {error && (
              <div className="rounded-xl border border-rose-500/30 bg-rose-950/40 p-3 text-xs text-rose-200 flex items-start gap-2">
                <AlertCircle className="h-4 w-4 text-rose-400 mt-0.5 shrink-0" />
                <span>{error}</span>
              </div>
            )}

            <div ref={messagesEndRef} />
          </div>

          {/* Quick Action Suggestion Chips */}
          <div className="px-3 py-2 bg-[#122420]/80 border-t border-[#2F5C52]/20 overflow-x-auto flex items-center gap-2 no-scrollbar">
            {QUICK_ACTIONS.map((action, i) => (
              <button
                key={i}
                disabled={isStreaming}
                onClick={() => sendMessage(action.message)}
                className="whitespace-nowrap rounded-full border border-[#2F5C52]/40 bg-[#16302C] px-3 py-1.5 text-xs text-[#A2B5AF] hover:border-[#E07A3E]/70 hover:text-white transition-all disabled:opacity-40"
              >
                {action.label}
              </button>
            ))}
          </div>

          {/* Chat Input Footer */}
          <footer className="border-t border-[#2F5C52]/30 bg-[#16302C]/90 p-3">
            <form
              onSubmit={(e) => {
                e.preventDefault()
                handleSend()
              }}
              className="flex items-end gap-2"
            >
              <textarea
                ref={textareaRef}
                rows={1}
                value={input}
                onChange={handleInputChange}
                onKeyDown={handleKeyDown}
                placeholder={
                  isStreaming
                    ? 'Aria is answering...'
                    : 'Ask about facilities or request housekeeping / maintenance...'
                }
                disabled={isStreaming}
                className="flex-1 resize-none rounded-xl border border-[#2F5C52]/50 bg-[#0F1B1A] px-3.5 py-2.5 text-sm text-[#F6F1E6] placeholder-[#657D74] focus:border-[#E07A3E] focus:outline-none focus:ring-1 focus:ring-[#E07A3E] transition-all disabled:opacity-50 max-h-28"
                aria-label="Your message to AI concierge"
              />
              <button
                type="submit"
                disabled={!input.trim() || isStreaming}
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[#E07A3E] text-white transition-transform hover:scale-105 disabled:opacity-40 disabled:hover:scale-100 shadow-md"
                aria-label="Send message"
              >
                <Send className="h-4 w-4" />
              </button>
            </form>
            <p className="mt-2 text-center text-[10px] text-[#7C9188]">
              Grounding powered by SmartHotel Concierge · Room service verified via booking record
            </p>
          </footer>
        </aside>
      )}
    </>
  )
}
