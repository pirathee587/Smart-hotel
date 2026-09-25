'use client'

import type { RequestStatusUpdate } from '@/types/chat'
import * as signalR from '@microsoft/signalr'
import { useCallback,useEffect,useRef,useState } from 'react'

const HUB_URL = `${process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'}/hubs/smarthotel`

interface UseChatSocketOptions {
  token: string
  sessionId: string | null
  onRequestStatusUpdate?: (update: RequestStatusUpdate) => void
}

interface UseChatSocketReturn {
  /** Current streaming token delta (accumulates during stream) */
  streamingContent: string
  /** Whether LLM is currently streaming */
  isStreaming: boolean
  /** Connection state */
  isConnected: boolean
  /** Join a chat session group to receive its stream events */
  joinSession: (sessionId: string) => Promise<void>
  /** Leave a chat session group */
  leaveSession: (sessionId: string) => Promise<void>
  /** Reset streaming content (call after stream completes and message is committed) */
  resetStreaming: () => void
}

export function useChatSocket({
  token,
  sessionId,
  onRequestStatusUpdate,
}: UseChatSocketOptions): UseChatSocketReturn {
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const [isConnected, setIsConnected] = useState(false)
  const [isStreaming, setIsStreaming] = useState(false)
  const [streamingContent, setStreamingContent] = useState('')

  // ── Build + start connection ───────────────────────────────────────────────
  useEffect(() => {
    if (!token) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => token,
        transport: signalR.HttpTransportType.WebSockets |
                   signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(
        process.env.NODE_ENV === 'development'
          ? signalR.LogLevel.Information
          : signalR.LogLevel.Warning,
      )
      .build()

    // ── Event handlers ─────────────────────────────────────────────────────
    connection.on('ChatTokenDelta', (token: string) => {
      setIsStreaming(true)
      setStreamingContent(prev => prev + token)
    })

    connection.on('ChatStreamComplete', () => {
      setIsStreaming(false)
      // Content is intentionally NOT reset here — the parent commits it to bubbles
    })

    connection.on('RequestStatusUpdated', (update: RequestStatusUpdate) => {
      onRequestStatusUpdate?.(update)
    })

    connection.onreconnecting(() => setIsConnected(false))
    connection.onreconnected(() => setIsConnected(true))
    connection.onclose(() => setIsConnected(false))

    connectionRef.current = connection

    // Start
    connection.start()
      .then(() => {
        setIsConnected(true)
        // Auto-join session group if we already have a sessionId
        if (sessionId) {
          connection.invoke('JoinChatSession', sessionId).catch(console.error)
        }
      })
      .catch(err => console.error('SignalR connection error:', err))

    return () => {
      connection.stop()
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  // ── Auto-join session group when sessionId changes ────────────────────────
  useEffect(() => {
    if (sessionId && connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      connectionRef.current.invoke('JoinChatSession', sessionId).catch(console.error)
    }
  }, [sessionId])

  const joinSession = useCallback(async (sid: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('JoinChatSession', sid)
    }
  }, [])

  const leaveSession = useCallback(async (sid: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('LeaveChatSession', sid)
    }
  }, [])

  const resetStreaming = useCallback(() => {
    setStreamingContent('')
    setIsStreaming(false)
  }, [])

  return { streamingContent, isStreaming, isConnected, joinSession, leaveSession, resetStreaming }
}
