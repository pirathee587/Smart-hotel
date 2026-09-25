'use client'

import { portalApi } from '@/services/portalApi'
import type { ActiveRequestDto } from '@/types/chat'
import { useState } from 'react'

interface CsatWidgetProps {
  token: string
  request: ActiveRequestDto
  onSubmitted: (serviceRequestId: string) => void
}

const GOLD = '#e2b96f'

export default function CsatWidget({ token, request, onSubmitted }: CsatWidgetProps) {
  const [rating, setRating]     = useState(0)
  const [hovered, setHovered]   = useState(0)
  const [comment, setComment]   = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [submitted, setSubmitted]   = useState(false)
  const [error, setError]           = useState<string | null>(null)

  if (request.csatSubmitted || submitted) {
    return (
      <div style={{
        background: 'rgba(72,187,120,0.08)',
        border: '1px solid rgba(72,187,120,0.25)',
        borderRadius: 12, padding: '10px 14px',
        display: 'flex', alignItems: 'center', gap: 8,
        fontSize: 13, color: '#48bb78',
      }}>
        ✅ Thank you for your feedback!
      </div>
    )
  }

  const handleSubmit = async () => {
    if (rating === 0) return
    setSubmitting(true)
    setError(null)
    try {
      await portalApi.submitCsatFeedback(token, {
        serviceRequestId: request.serviceRequestId,
        rating,
        comment: comment.trim() || undefined,
      })
      setSubmitted(true)
      onSubmitted(request.serviceRequestId)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to submit rating.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div style={{
      background: 'rgba(226,185,111,0.06)',
      border: '1px solid rgba(226,185,111,0.2)',
      borderRadius: 12, padding: '12px 14px',
      marginTop: 8,
    }}>
      <p style={{ margin: '0 0 8px', fontSize: 13, color: '#e2e8f0', fontWeight: 600 }}>
        ⭐ How was your experience?
      </p>
      {/* Star row */}
      <div style={{ display: 'flex', gap: 4, marginBottom: 10 }}>
        {[1, 2, 3, 4, 5].map(star => (
          <button
            key={star}
            id={`csat-star-${star}-${request.serviceRequestId}`}
            onClick={() => setRating(star)}
            onMouseEnter={() => setHovered(star)}
            onMouseLeave={() => setHovered(0)}
            style={{
              background: 'none', border: 'none', cursor: 'pointer',
              fontSize: 24, padding: 2, transition: 'transform 0.15s',
              transform: (hovered || rating) >= star ? 'scale(1.15)' : 'scale(1)',
              color: (hovered || rating) >= star ? GOLD : 'rgba(255,255,255,0.2)',
            }}
          >
            ★
          </button>
        ))}
      </div>
      {/* Comment */}
      <textarea
        placeholder="Optional: tell us more…"
        value={comment}
        onChange={e => setComment(e.target.value)}
        rows={2}
        style={{
          width: '100%', background: 'rgba(255,255,255,0.06)',
          border: '1px solid rgba(226,185,111,0.15)', borderRadius: 8,
          color: '#e2e8f0', fontSize: 13, padding: '8px 10px',
          resize: 'none', outline: 'none', boxSizing: 'border-box',
        }}
      />
      {error && <p style={{ color: '#e53e3e', fontSize: 12, margin: '4px 0 0' }}>{error}</p>}
      <button
        id={`csat-submit-${request.serviceRequestId}`}
        onClick={handleSubmit}
        disabled={rating === 0 || submitting}
        style={{
          marginTop: 8, padding: '7px 18px', borderRadius: 8, border: 'none',
          background: rating > 0
            ? `linear-gradient(135deg, ${GOLD}, #c8914a)`
            : 'rgba(255,255,255,0.08)',
          color: rating > 0 ? '#1a1730' : '#718096',
          fontSize: 13, fontWeight: 700, cursor: rating > 0 ? 'pointer' : 'not-allowed',
          transition: 'all 0.2s',
        }}
      >
        {submitting ? 'Submitting…' : 'Submit Rating'}
      </button>
    </div>
  )
}
