'use client'

import { portalApi, CONCIERGE_BASE } from '@/services/portalApi'
import type {
  ChatBubble,
  ConciergeServiceRequestDto,
  ConciergeStreamContext,
} from '@/types/chat'
import { useCallback, useEffect, useRef, useState } from 'react'

const generateId = () =>
  typeof crypto !== 'undefined' && crypto.randomUUID
    ? crypto.randomUUID()
    : Math.random().toString(36).substring(2, 11)

interface UseConciergeStreamOptions {
  token: string
  initialConversationId?: string | null
  customerName?: string
  onConversationCreated?: (conversationId: string) => void
}

interface UseConciergeStreamReturn {
  messages: ChatBubble[]
  isStreaming: boolean
  error: string | null
  context: ConciergeStreamContext
  activeRequests: ConciergeServiceRequestDto[]
  conversationId: string | null
  sendMessage: (messageText: string) => Promise<void>
  loadHistory: (cid?: string) => Promise<void>
  refreshRequests: () => Promise<void>
  cancelStream: () => void
  resetChat: () => void
}

export function useConciergeStream({
  token,
  initialConversationId = null,
  customerName = 'Guest',
  onConversationCreated,
}: UseConciergeStreamOptions): UseConciergeStreamReturn {
  const [messages, setMessages] = useState<ChatBubble[]>(() => [
    {
      id: 'greeting',
      role: 'assistant',
      content: `Hello ${customerName}! 👋 I'm Aria, your SmartHotel AI concierge. How can I help you today? You can ask me about hotel facilities, request housekeeping or maintenance, or check your stay details.`,
      createdAt: new Date(),
    },
  ])
  const [isStreaming, setIsStreaming] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [conversationId, setConversationId] = useState<string | null>(initialConversationId)
  const [context, setContext] = useState<ConciergeStreamContext>({})
  const [activeRequests, setActiveRequests] = useState<ConciergeServiceRequestDto[]>([])

  const abortControllerRef = useRef<AbortController | null>(null)
  const conversationIdRef = useRef<string | null>(initialConversationId)

  useEffect(() => {
    conversationIdRef.current = conversationId
  }, [conversationId])

  // Hydrate conversation history
  const loadHistory = useCallback(
    async (cid?: string) => {
      const targetCid = cid || conversationIdRef.current
      if (!token || !targetCid) return

      try {
        const historyData = await portalApi.getConciergeHistory(token, targetCid)
        if (historyData?.messages && historyData.messages.length > 0) {
          const loaded: ChatBubble[] = historyData.messages.map((m) => {
            let metadata: Record<string, string> = {}
            if (m.metadataJson) {
              try {
                metadata = JSON.parse(m.metadataJson) as Record<string, string>
              } catch {
                // ignore parsing error
              }
            }
            return {
              id: m.id,
              role: (m.role.toLowerCase() === 'user' ? 'user' : 'assistant') as 'user' | 'assistant',
              content: m.content,
              createdAt: new Date(m.createdAt),
              ticketRef: metadata.requestId ? metadata.requestId.substring(0, 8) : undefined,
              serviceRequestId: metadata.requestId,
            }
          })
          setMessages(loaded)
        }
      } catch {
        // Fallback to initial greeting on network error
      }
    },
    [token],
  )

  // Poll / refresh active requests
  const refreshRequests = useCallback(async () => {
    if (!token) return
    try {
      const reqs = await portalApi.getConciergeRequests(token)
      if (reqs) {
        setActiveRequests(reqs)
      }
    } catch {
      // Silently continue if polling encounters network hiccup
    }
  }, [token])

  // Periodically refresh active requests when token is present
  useEffect(() => {
    if (!token) return
    let isMounted = true

    portalApi
      .getConciergeRequests(token)
      .then((reqs) => {
        if (isMounted && reqs) {
          setActiveRequests(reqs)
        }
      })
      .catch(() => {})

    const interval = setInterval(() => {
      portalApi
        .getConciergeRequests(token)
        .then((reqs) => {
          if (isMounted && reqs) {
            setActiveRequests(reqs)
          }
        })
        .catch(() => {})
    }, 10000)

    return () => {
      isMounted = false
      clearInterval(interval)
    }
  }, [token])

  // Load history if initialConversationId provided
  useEffect(() => {
    if (initialConversationId) {
      loadHistory(initialConversationId)
    }
  }, [initialConversationId, loadHistory])

  // Cancel active stream
  const cancelStream = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort()
      abortControllerRef.current = null
    }
    setIsStreaming(false)
  }, [])

  // Reset chat session
  const resetChat = useCallback(() => {
    cancelStream()
    setConversationId(null)
    conversationIdRef.current = null
    setContext({})
    setMessages([
      {
        id: 'greeting',
        role: 'assistant',
        content: `Hello ${customerName}! 👋 I'm Aria, your SmartHotel AI concierge. How can I help you today? You can ask me about hotel facilities, request housekeeping or maintenance, or check your stay details.`,
        createdAt: new Date(),
      },
    ])
  }, [cancelStream, customerName])

  // Send message and stream response via SSE
  const sendMessage = useCallback(
    async (messageText: string) => {
      const trimmed = messageText.trim()
      if (!trimmed || isStreaming) return

      setError(null)
      const userBubbleId = generateId()
      const assistantBubbleId = generateId()

      // Optimistically append user message
      const userBubble: ChatBubble = {
        id: userBubbleId,
        role: 'user',
        content: trimmed,
        createdAt: new Date(),
      }

      // Placeholder streaming assistant message
      const assistantBubble: ChatBubble = {
        id: assistantBubbleId,
        role: 'assistant',
        content: '',
        isStreaming: true,
        createdAt: new Date(),
      }

      setMessages((prev) => [...prev, userBubble, assistantBubble])
      setIsStreaming(true)

      const controller = new AbortController()
      abortControllerRef.current = controller

      try {
        const response = await fetch(`${CONCIERGE_BASE}/api/v1/concierge/chat/stream`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            message: trimmed,
            conversationId: conversationIdRef.current || undefined,
          }),
          signal: controller.signal,
        })

        if (!response.ok) {
          let errorMsg = 'Failed to connect to AI Concierge service.'
          if (response.status === 429) {
            errorMsg = 'You are sending messages too quickly. Please wait a moment.'
          } else if (response.status === 401) {
            errorMsg = 'Session expired. Please log in again to speak with the concierge.'
          } else if (response.status === 403) {
            errorMsg = 'Access denied. The concierge is available for verified guests.'
          }
          throw new Error(errorMsg)
        }

        const reader = response.body?.getReader()
        if (!reader) {
          throw new Error('Response stream not readable.')
        }

        const decoder = new TextDecoder('utf-8')
        let buffer = ''
        let accumulatedContent = ''

        while (true) {
          const { value, done } = await reader.read()
          if (done) break

          buffer += decoder.decode(value, { stream: true })
          const lines = buffer.split('\n')
          buffer = lines.pop() ?? ''

          for (const line of lines) {
            const trimmedLine = line.trim()
            if (!trimmedLine || !trimmedLine.startsWith('data: ')) continue

            const dataStr = trimmedLine.replace('data: ', '').trim()
            if (!dataStr) continue

            try {
              const event = JSON.parse(dataStr)

              if (event.type === 'context') {
                if (event.conversationId && !conversationIdRef.current) {
                  setConversationId(event.conversationId)
                  conversationIdRef.current = event.conversationId
                  onConversationCreated?.(event.conversationId)
                }
                setContext((prev) => ({
                  ...prev,
                  verifiedRoom: event.verifiedRoom || prev.verifiedRoom,
                  bookingReference: event.bookingReference || prev.bookingReference,
                }))
              } else if (event.type === 'chunk') {
                accumulatedContent += event.content || ''
                setMessages((prev) =>
                  prev.map((b) =>
                    b.id === assistantBubbleId
                      ? { ...b, content: accumulatedContent }
                      : b,
                  ),
                )
              } else if (event.type === 'tool_call') {
                if (event.requestId) {
                  const ticketRef = event.requestId.substring(0, 8)
                  setMessages((prev) =>
                    prev.map((b) =>
                      b.id === assistantBubbleId
                        ? {
                            ...b,
                            ticketRef,
                            serviceRequestId: event.requestId,
                          }
                        : b,
                    ),
                  )
                  refreshRequests()
                }
              } else if (event.type === 'error') {
                accumulatedContent += `\n⚠️ ${event.message || 'Service request dispatch notice.'}`
                setMessages((prev) =>
                  prev.map((b) =>
                    b.id === assistantBubbleId
                      ? { ...b, content: accumulatedContent }
                      : b,
                  ),
                )
              } else if (event.type === 'done') {
                setMessages((prev) =>
                  prev.map((b) =>
                    b.id === assistantBubbleId
                      ? { ...b, isStreaming: false }
                      : b,
                  ),
                )
              }
            } catch {
              // Ignore partial JSON parse errors
            }
          }
        }

        setMessages((prev) =>
          prev.map((b) =>
            b.id === assistantBubbleId
              ? { ...b, isStreaming: false }
              : b,
          ),
        )
      } catch (err: unknown) {
        const errorObj = err instanceof Error ? err : new Error(String(err))
        if (errorObj.name === 'AbortError') {
          setMessages((prev) =>
            prev.map((b) =>
              b.id === assistantBubbleId
                ? { ...b, isStreaming: false, content: b.content || 'Request cancelled.' }
                : b,
            ),
          )
        } else {
          const failureMsg =
            errorObj.message ||
            'Unable to reach AI Concierge. Please try again or contact the front desk.'
          setError(failureMsg)
          setMessages((prev) =>
            prev.map((b) =>
              b.id === assistantBubbleId
                ? {
                    ...b,
                    isStreaming: false,
                    content:
                      b.content ||
                      `I apologize, but I encountered an error connecting to our concierge system. Please try again or reach the front desk directly.`,
                  }
                : b,
            ),
          )
        }
      } finally {
        setIsStreaming(false)
        abortControllerRef.current = null
      }
    },
    [token, isStreaming, refreshRequests, onConversationCreated],
  )

  return {
    messages,
    isStreaming,
    error,
    context,
    activeRequests,
    conversationId,
    sendMessage,
    loadHistory,
    refreshRequests,
    cancelStream,
    resetChat,
  }
}
