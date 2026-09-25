'use client'

import { useChatSocket } from '@/hooks/useChatSocket'
import { portalApi } from '@/services/portalApi'
import type { ActiveRequestDto,RequestStatusUpdate } from '@/types/chat'
import { useCallback,useEffect,useState } from 'react'
import CsatWidget from './CsatWidget'

interface LiveRequestDashboardProps {
  token: string
  bookingId: string
}

const GOLD = '#e2b96f'

const STATUS_COLOR: Record<string, string> = {
  Pending:    '#ed8936',
  InProgress: '#4299e1',
  Completed:  '#48bb78',
  Cancelled:  '#718096',
  Assigned:   '#9f7aea',
  Accepted:   '#667eea',
}

const PRIORITY_COLOR: Record<string, string> = {
  Low:    '#48bb78',
  Medium: '#ed8936',
  High:   '#e53e3e',
  Urgent: '#9b2c2c',
}

export default function LiveRequestDashboard({ token, bookingId }: LiveRequestDashboardProps) {
  const [requests, setRequests]   = useState<ActiveRequestDto[]>([])
  const [isLoading, setIsLoading] = useState(true)

  // ── Initial load ──────────────────────────────────────────────────────────
  const loadRequests = useCallback(async () => {
    try {
      const data = await portalApi.getActiveRequests(token, bookingId)
      setRequests(data)
    } catch {
      // Silent — no requests is a valid state
    } finally {
      setIsLoading(false)
    }
  }, [token, bookingId])

  useEffect(() => { queueMicrotask(() => void loadRequests()) }, [loadRequests])

  // ── Real-time updates via SignalR ─────────────────────────────────────────
  const handleStatusUpdate = useCallback((update: RequestStatusUpdate) => {
    setRequests(prev => prev.map(r =>
      r.serviceRequestId === update.serviceRequestId
        ? {
            ...r,
            serviceRequestStatus: update.newStatus,
            taskStatus: update.taskStatus ?? r.taskStatus,
            assignedEmployeeName: update.assignedEmployeeName ?? r.assignedEmployeeName,
          }
        : r,
    ))
  }, [])

  // useChatSocket for real-time updates only (no chat sessionId)
  useChatSocket({ token, sessionId: null, onRequestStatusUpdate: handleStatusUpdate })

  const handleCsatSubmitted = useCallback((srId: string) => {
    setRequests(prev => prev.map(r =>
      r.serviceRequestId === srId ? { ...r, csatSubmitted: true } : r,
    ))
  }, [])

  if (isLoading) {
    return (
      <div style={{ padding: '24px 0', textAlign: 'center', color: '#718096', fontSize: 14 }}>
        Loading your requests…
      </div>
    )
  }

  if (requests.length === 0) {
    return (
      <div style={{
        background: 'rgba(255,255,255,0.03)',
        border: '1px solid rgba(226,185,111,0.1)',
        borderRadius: 14, padding: '28px 20px', textAlign: 'center',
      }}>
        <p style={{ margin: 0, fontSize: 32, lineHeight: 1 }}>🛎️</p>
        <p style={{ margin: '10px 0 4px', color: '#e2e8f0', fontWeight: 600 }}>No active requests</p>
        <p style={{ margin: 0, color: '#718096', fontSize: 13 }}>
          Use the AI Concierge chat to request services or report issues.
        </p>
      </div>
    )
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      {requests.map(req => (
        <RequestCard
          key={req.serviceRequestId}
          request={req}
          token={token}
          onCsatSubmitted={handleCsatSubmitted}
        />
      ))}
    </div>
  )
}

// ── Request Card ──────────────────────────────────────────────────────────────

function RequestCard({
  request, token, onCsatSubmitted,
}: {
  request: ActiveRequestDto
  token: string
  onCsatSubmitted: (id: string) => void
}) {
  const isComplete = request.serviceRequestStatus === 'Completed'
  const showCsat   = isComplete && !request.csatSubmitted

  return (
    <div style={{
      background: 'rgba(255,255,255,0.04)',
      border: `1px solid ${isComplete ? 'rgba(72,187,120,0.25)' : 'rgba(226,185,111,0.15)'}`,
      borderRadius: 14, padding: '16px 18px',
      transition: 'border-color 0.3s',
    }}>
      {/* Header row */}
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10, flexWrap: 'wrap' }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <p style={{ margin: '0 0 4px', color: '#e2e8f0', fontWeight: 700, fontSize: 15, wordBreak: 'break-word' }}>
            {request.title}
          </p>
          <p style={{ margin: 0, color: '#718096', fontSize: 12 }}>
            {new Date(request.createdAt).toLocaleString()}
          </p>
        </div>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', flexShrink: 0 }}>
          <StatusBadge label={request.serviceRequestStatus} color={STATUS_COLOR[request.serviceRequestStatus] ?? '#718096'} />
          <StatusBadge label={request.priority} color={PRIORITY_COLOR[request.priority] ?? '#718096'} />
        </div>
      </div>

      {/* Progress timeline */}
      <RequestTimeline request={request} />

      {/* Assigned employee */}
      {request.assignedEmployeeName && (
        <p style={{ margin: '10px 0 0', fontSize: 13, color: '#a0aec0' }}>
          👤 Assigned to <strong style={{ color: GOLD }}>{request.assignedEmployeeName}</strong>
        </p>
      )}

      {/* CSAT widget — appears when Completed */}
      {showCsat && (
        <CsatWidget token={token} request={request} onSubmitted={onCsatSubmitted} />
      )}
    </div>
  )
}

// ── Status timeline ───────────────────────────────────────────────────────────

const STAGES = ['Pending', 'Assigned', 'InProgress', 'Completed']

function RequestTimeline({ request }: { request: ActiveRequestDto }) {
  const taskStage = request.taskStatus ?? request.serviceRequestStatus
  const currentIdx = STAGES.findIndex(s =>
    s === taskStage || s === request.serviceRequestStatus,
  )

  return (
    <div style={{ marginTop: 14, display: 'flex', alignItems: 'center', gap: 0 }}>
      {STAGES.map((stage, i) => {
        const done    = i < currentIdx
        const active  = i === currentIdx
        const future  = i > currentIdx
        const color   = done || active
          ? (STATUS_COLOR[stage] ?? GOLD)
          : 'rgba(255,255,255,0.15)'

        return (
          <div key={stage} style={{ display: 'flex', alignItems: 'center', flex: 1 }}>
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flexShrink: 0 }}>
              <div style={{
                width: 12, height: 12, borderRadius: '50%',
                background: color,
                boxShadow: active ? `0 0 8px ${color}` : 'none',
                transition: 'all 0.4s',
              }} />
              <span style={{
                fontSize: 10, marginTop: 4,
                color: future ? 'rgba(255,255,255,0.3)' : color,
                whiteSpace: 'nowrap',
              }}>
                {stage === 'InProgress' ? 'In Progress' : stage}
              </span>
            </div>
            {i < STAGES.length - 1 && (
              <div style={{
                flex: 1, height: 2, margin: '0 4px',
                background: done ? (STATUS_COLOR[STAGES[i + 1]] ?? GOLD) : 'rgba(255,255,255,0.1)',
                marginBottom: 18,
                transition: 'background 0.4s',
              }} />
            )}
          </div>
        )
      })}
    </div>
  )
}

function StatusBadge({ label, color }: { label: string; color: string }) {
  return (
    <span style={{
      fontSize: 11, fontWeight: 700, padding: '2px 9px', borderRadius: 20,
      background: `${color}22`, color, border: `1px solid ${color}44`,
    }}>
      {label}
    </span>
  )
}
