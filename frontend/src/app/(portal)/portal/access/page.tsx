'use client'

import { useEffect, useState, Suspense } from 'react'
import { useSearchParams, useRouter } from 'next/navigation'

interface BookingLookupResult {
  bookingId: string
  customerId: string
  referenceCode: string
  checkInDate: string
  checkOutDate: string
  bookingStatus: string
  totalAmount: number
  paymentStatus?: string
}

function PortalAccessContent() {
  const searchParams = useSearchParams()
  const router = useRouter()
  const token = searchParams.get('token')
  const error = searchParams.get('error')

  const [status, setStatus] = useState<'loading' | 'error' | 'lookup'>('loading')
  const [errorMessage, setErrorMessage] = useState('')
  const [lookupRef, setLookupRef] = useState('')
  const [lookupEmail, setLookupEmail] = useState('')
  const [lookupLoading, setLookupLoading] = useState(false)
  const [lookupResult, setLookupResult] = useState<BookingLookupResult | null>(null)
  const [lookupError, setLookupError] = useState('')

  useEffect(() => {
    if (error) {
      const messages: Record<string, string> = {
        token_expired: 'This access link has expired. Links are valid for 48 hours. Please use the booking lookup form below.',
        token_used: 'This access link has already been used. If you need to access your portal again, please use the booking lookup form.',
        invalid_token: 'This access link is invalid. Please check your email or use the booking lookup form below.',
        missing_token: 'No access token was provided.',
      }
      setErrorMessage(messages[error] || 'An unknown error occurred. Please try again.')
      setStatus('lookup')
      return
    }

    if (token) {
      // The backend redirect should have already handled the token — if we land here with a token, call directly
      const apiBase = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'
      window.location.href = `${apiBase}/api/v1/auth/portal-access?token=${encodeURIComponent(token)}`
    } else {
      setStatus('lookup')
    }
  }, [token, error, router])

  const handleLookup = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!lookupRef.trim() || !lookupEmail.trim()) return

    setLookupLoading(true)
    setLookupError('')
    setLookupResult(null)

    try {
      const apiBase = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'
      const res = await fetch(
        `${apiBase}/api/v1/auth/portal-lookup?referenceCode=${encodeURIComponent(lookupRef.toUpperCase())}&email=${encodeURIComponent(lookupEmail)}`,
      )
      if (!res.ok) {
        const data = await res.json()
        setLookupError(data.message || 'Booking not found. Please check your details.')
      } else {
        const data = await res.json()
        setLookupResult(data)
      }
    } catch {
      setLookupError('Network error. Please try again.')
    } finally {
      setLookupLoading(false)
    }
  }

  if (status === 'loading') {
    return (
      <div className="flex flex-col items-center justify-center min-h-96 gap-4">
        <div className="w-12 h-12 rounded-full border-4 animate-spin" style={{ borderColor: 'rgba(226,185,111,0.2)', borderTopColor: '#e2b96f' }} />
        <p style={{ color: '#a0aec0' }}>Verifying your access link…</p>
      </div>
    )
  }

  return (
    <div className="max-w-md mx-auto pt-8">
      {/* Error Banner */}
      {errorMessage && (
        <div className="mb-6 p-4 rounded-xl" style={{ background: 'rgba(245,101,101,0.1)', border: '1px solid rgba(245,101,101,0.3)' }}>
          <div className="flex gap-3">
            <span className="text-xl">⚠️</span>
            <p className="text-sm" style={{ color: '#fc8181' }}>{errorMessage}</p>
          </div>
        </div>
      )}

      {/* Lookup Card */}
      <div className="rounded-2xl p-8" style={{ background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.08)', backdropFilter: 'blur(12px)' }}>
        <div className="text-center mb-6">
          <div className="w-14 h-14 rounded-xl mx-auto mb-4 flex items-center justify-center" style={{ background: 'linear-gradient(135deg, #e2b96f, #d4a054)' }}>
            <span className="text-2xl">🔍</span>
          </div>
          <h1 className="text-xl font-bold text-white mb-2">Find Your Booking</h1>
          <p className="text-sm" style={{ color: '#718096' }}>
            Enter your booking reference and email to access your stay details.
          </p>
        </div>

        <form onSubmit={handleLookup} className="space-y-4">
          <div>
            <label className="block text-sm font-medium mb-2" style={{ color: '#a0aec0' }}>
              Booking Reference
            </label>
            <input
              id="portal-reference-code"
              type="text"
              value={lookupRef}
              onChange={e => setLookupRef(e.target.value.toUpperCase())}
              placeholder="SH-2025-XXXXXX"
              className="w-full px-4 py-3 rounded-xl text-white placeholder-gray-500 outline-none transition-all"
              style={{ background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.12)', fontFamily: 'monospace', letterSpacing: '2px' }}
              required
            />
          </div>

          <div>
            <label className="block text-sm font-medium mb-2" style={{ color: '#a0aec0' }}>
              Email Address
            </label>
            <input
              id="portal-email"
              type="email"
              value={lookupEmail}
              onChange={e => setLookupEmail(e.target.value)}
              placeholder="guest@example.com"
              className="w-full px-4 py-3 rounded-xl text-white placeholder-gray-500 outline-none transition-all"
              style={{ background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.12)' }}
              required
            />
          </div>

          {lookupError && (
            <p className="text-sm text-center" style={{ color: '#fc8181' }}>{lookupError}</p>
          )}

          <button
            id="portal-lookup-submit"
            type="submit"
            disabled={lookupLoading}
            className="w-full py-3 px-6 rounded-xl font-semibold transition-all"
            style={{ background: lookupLoading ? 'rgba(226,185,111,0.4)' : 'linear-gradient(135deg, #e2b96f, #d4a054)', color: '#1a1a2e', cursor: lookupLoading ? 'not-allowed' : 'pointer' }}
          >
            {lookupLoading ? 'Looking up…' : 'Find My Booking'}
          </button>
        </form>

        {/* Lookup Result */}
        {lookupResult && (
          <div className="mt-6 p-4 rounded-xl" style={{ background: 'rgba(72,187,120,0.08)', border: '1px solid rgba(72,187,120,0.25)' }}>
            <div className="flex items-center gap-2 mb-3">
              <span className="text-green-400">✓</span>
              <span className="font-semibold text-white">Booking Found</span>
              <span className="ml-auto font-mono text-sm px-2 py-0.5 rounded" style={{ background: 'rgba(226,185,111,0.15)', color: '#e2b96f' }}>
                {lookupResult.referenceCode}
              </span>
            </div>
            <div className="space-y-1 text-sm" style={{ color: '#a0aec0' }}>
              <p>Check-in: <span className="text-white font-medium">{new Date(lookupResult.checkInDate).toLocaleDateString()}</span></p>
              <p>Check-out: <span className="text-white font-medium">{new Date(lookupResult.checkOutDate).toLocaleDateString()}</span></p>
              <p>Status: <span style={{ color: lookupResult.bookingStatus === 'Confirmed' ? '#68d391' : '#fc8181' }}>{lookupResult.bookingStatus}</span></p>
              {lookupResult.paymentStatus && (
                <p>Payment: <span className="text-white">{lookupResult.paymentStatus}</span></p>
              )}
            </div>
            <p className="mt-3 text-xs" style={{ color: '#718096' }}>
              To access all portal features, please use the link from your original confirmation email, or contact the hotel directly.
            </p>
          </div>
        )}
      </div>

      <p className="text-center mt-6 text-sm" style={{ color: '#4a5568' }}>
        Having trouble? Contact us at <a href="mailto:support@smarthotel.com" style={{ color: '#e2b96f' }}>support@smarthotel.com</a>
      </p>
    </div>
  )
}

export default function PortalAccessPage() {
  return (
    <Suspense fallback={
      <div className="flex items-center justify-center min-h-96">
        <div className="w-10 h-10 rounded-full border-4 animate-spin" style={{ borderColor: 'rgba(226,185,111,0.2)', borderTopColor: '#e2b96f' }} />
      </div>
    }>
      <PortalAccessContent />
    </Suspense>
  )
}
