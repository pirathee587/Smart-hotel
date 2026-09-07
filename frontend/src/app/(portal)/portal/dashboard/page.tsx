'use client'

import { useEffect, useState, Suspense, lazy } from 'react'
import { useSearchParams, useRouter } from 'next/navigation'
import axios from 'axios'
import dynamic from 'next/dynamic'

// Chat components loaded client-side only (they use SignalR / browser APIs)
const GuestChatWidget = dynamic(() => import('@/components/chat/GuestChatWidget'), { ssr: false })
const LiveRequestDashboard = dynamic(() => import('@/components/chat/LiveRequestDashboard'), { ssr: false })

// ── Types ──────────────────────────────────────────────────────────────────────
interface ServiceRequest {
  id: string
  title: string
  status: string
  createdAt: string
}

interface BookingDto {
  bookingId: string
  referenceCode: string
  roomNumber?: string
  roomTypeName?: string
  checkInDate: string
  checkOutDate: string
  status: string
  totalAmount: number
  otaSource?: string
  paymentStatus?: string
  refundAmount?: number
  serviceRequests: ServiceRequest[]
}

interface PortalData {
  customerName: string
  email: string
  hasPassword: boolean
  bookings: BookingDto[]
}

const API_BASE = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'

function createApiClient(token: string) {
  return axios.create({
    baseURL: API_BASE,
    headers: { Authorization: `Bearer ${token}` },
  })
}

// ── Sub-components ─────────────────────────────────────────────────────────────
function StatusBadge({ status }: { status: string }) {
  const colors: Record<string, { bg: string; text: string }> = {
    Confirmed: { bg: 'rgba(72,187,120,0.15)', text: '#68d391' },
    CheckedIn: { bg: 'rgba(66,153,225,0.15)', text: '#63b3ed' },
    CheckedOut: { bg: 'rgba(160,174,192,0.15)', text: '#a0aec0' },
    Cancelled: { bg: 'rgba(245,101,101,0.15)', text: '#fc8181' },
    Paid: { bg: 'rgba(72,187,120,0.15)', text: '#68d391' },
    Refunded: { bg: 'rgba(214,188,250,0.15)', text: '#d6bcfa' },
    PartiallyRefunded: { bg: 'rgba(237,179,28,0.15)', text: '#f6e05e' },
  }
  const color = colors[status] || { bg: 'rgba(160,174,192,0.1)', text: '#a0aec0' }
  return (
    <span className="px-2 py-0.5 rounded-full text-xs font-medium" style={{ background: color.bg, color: color.text }}>
      {status}
    </span>
  )
}

function BookingCard({ booking, token }: { booking: BookingDto; token: string }) {
  const [showServiceForm, setShowServiceForm] = useState(false)
  const [serviceTitle, setServiceTitle] = useState('')
  const [serviceLoading, setServiceLoading] = useState(false)
  const [serviceMessage, setServiceMessage] = useState('')
  const [refundLoading, setRefundLoading] = useState(false)
  const [refundMessage, setRefundMessage] = useState('')

  const nights = Math.ceil(
    (new Date(booking.checkOutDate).getTime() - new Date(booking.checkInDate).getTime()) / (1000 * 60 * 60 * 24)
  )

  const handleServiceRequest = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!serviceTitle.trim()) return
    setServiceLoading(true)
    try {
      const api = createApiClient(token)
      await api.post('/api/v1/bookings/service-requests', {
        bookingId: booking.bookingId,
        title: serviceTitle,
        description: '',
      })
      setServiceMessage('✓ Service request submitted!')
      setServiceTitle('')
      setShowServiceForm(false)
    } catch {
      setServiceMessage('Failed to submit. Please try again.')
    } finally {
      setServiceLoading(false)
    }
  }

  const handleRefundRequest = async () => {
    if (!confirm('Request a refund for this booking? Refund amount depends on the cancellation policy.')) return
    setRefundLoading(true)
    try {
      const api = createApiClient(token)
      const res = await api.post(`/api/v1/bookings/${booking.bookingId}/refund`, {
        reason: 'Guest-initiated cancellation',
      })
      const data = res.data
      if (data.status === 'NoRefund') {
        setRefundMessage('Cancellation policy: no refund applicable within this time window.')
      } else {
        setRefundMessage(`✓ Refund of ${data.refundAmount?.toFixed(2)} processed successfully (${data.status}).`)
      }
    } catch (err: unknown) {
      const message = axios.isAxiosError(err) ? err.response?.data?.message : 'Refund request failed.'
      setRefundMessage(message || 'Refund request failed.')
    } finally {
      setRefundLoading(false)
    }
  }

  return (
    <div className="rounded-2xl overflow-hidden mb-4" style={{ background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.08)' }}>
      {/* Card Header */}
      <div className="p-6 border-b" style={{ borderColor: 'rgba(255,255,255,0.06)' }}>
        <div className="flex items-start justify-between flex-wrap gap-3">
          <div>
            <div className="flex items-center gap-3 mb-1">
              <span className="font-mono font-bold text-lg" style={{ color: '#e2b96f' }}>{booking.referenceCode}</span>
              <StatusBadge status={booking.status} />
              {booking.otaSource && (
                <span className="text-xs px-2 py-0.5 rounded-full" style={{ background: 'rgba(102,126,234,0.15)', color: '#a78bfa' }}>
                  via {booking.otaSource}
                </span>
              )}
            </div>
            <p className="text-sm" style={{ color: '#718096' }}>
              {booking.roomTypeName || 'Room Type TBD'}
              {booking.roomNumber && ` — Room ${booking.roomNumber}`}
            </p>
          </div>
          <div className="text-right">
            <p className="text-xl font-bold text-white">${booking.totalAmount.toFixed(2)}</p>
            {booking.paymentStatus && <StatusBadge status={booking.paymentStatus} />}
          </div>
        </div>
      </div>

      {/* Date info */}
      <div className="grid grid-cols-3 gap-0 border-b" style={{ borderColor: 'rgba(255,255,255,0.06)' }}>
        {[
          { label: 'Check-in', value: new Date(booking.checkInDate).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' }) },
          { label: 'Nights', value: String(nights) },
          { label: 'Check-out', value: new Date(booking.checkOutDate).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' }) },
        ].map((item, i) => (
          <div key={i} className="p-4 text-center" style={{ borderRight: i < 2 ? '1px solid rgba(255,255,255,0.06)' : 'none' }}>
            <p className="text-xs mb-1" style={{ color: '#718096' }}>{item.label}</p>
            <p className="text-sm font-semibold text-white">{item.value}</p>
          </div>
        ))}
      </div>

      {/* Refund info */}
      {booking.refundAmount && booking.refundAmount > 0 && (
        <div className="px-6 py-3 border-b" style={{ borderColor: 'rgba(255,255,255,0.06)', background: 'rgba(214,188,250,0.05)' }}>
          <p className="text-sm" style={{ color: '#d6bcfa' }}>
            💜 Refund of <strong>${booking.refundAmount.toFixed(2)}</strong> has been processed.
          </p>
        </div>
      )}

      {/* Actions */}
      <div className="p-4 flex flex-wrap gap-2 items-center">
        {booking.status === 'CheckedIn' && (
          <button
            id={`service-request-${booking.bookingId}`}
            onClick={() => setShowServiceForm(!showServiceForm)}
            className="px-4 py-2 rounded-lg text-sm font-medium transition-all"
            style={{ background: 'rgba(226,185,111,0.1)', color: '#e2b96f', border: '1px solid rgba(226,185,111,0.2)' }}
          >
            🛎 Request Service
          </button>
        )}

        {(booking.status === 'Confirmed' || booking.status === 'CheckedIn') &&
          !booking.refundAmount && (
          <button
            id={`refund-${booking.bookingId}`}
            onClick={handleRefundRequest}
            disabled={refundLoading}
            className="px-4 py-2 rounded-lg text-sm font-medium transition-all"
            style={{ background: 'rgba(245,101,101,0.1)', color: '#fc8181', border: '1px solid rgba(245,101,101,0.2)', cursor: refundLoading ? 'not-allowed' : 'pointer' }}
          >
            {refundLoading ? '…' : '↩ Cancel & Refund'}
          </button>
        )}

        {serviceMessage && <span className="text-sm ml-2" style={{ color: serviceMessage.startsWith('✓') ? '#68d391' : '#fc8181' }}>{serviceMessage}</span>}
        {refundMessage && <span className="text-sm ml-2" style={{ color: refundMessage.startsWith('✓') ? '#68d391' : '#f6e05e' }}>{refundMessage}</span>}
      </div>

      {/* Service request form */}
      {showServiceForm && (
        <form onSubmit={handleServiceRequest} className="px-4 pb-4 flex gap-2">
          <input
            id={`service-title-${booking.bookingId}`}
            type="text"
            value={serviceTitle}
            onChange={e => setServiceTitle(e.target.value)}
            placeholder="e.g., Extra towels, room cleaning, dining order…"
            className="flex-1 px-4 py-2 rounded-lg text-white text-sm outline-none"
            style={{ background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.12)' }}
          />
          <button
            type="submit"
            disabled={serviceLoading}
            className="px-4 py-2 rounded-lg text-sm font-medium"
            style={{ background: 'linear-gradient(135deg, #e2b96f, #d4a054)', color: '#1a1a2e' }}
          >
            {serviceLoading ? '…' : 'Send'}
          </button>
        </form>
      )}

      {/* Service requests list */}
      {booking.serviceRequests.length > 0 && (
        <div className="px-6 pb-4">
          <p className="text-xs font-medium mb-2" style={{ color: '#718096' }}>Service Requests</p>
          <div className="space-y-2">
            {booking.serviceRequests.map(req => (
              <div key={req.id} className="flex items-center justify-between text-sm">
                <span style={{ color: '#a0aec0' }}>{req.title}</span>
                <StatusBadge status={req.status} />
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

// ── Main Dashboard ─────────────────────────────────────────────────────────────
function DashboardContent() {
  const searchParams = useSearchParams()
  const router = useRouter()
  const sessionToken = searchParams.get('session')

  const [portalData, setPortalData] = useState<PortalData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [showPasswordForm, setShowPasswordForm] = useState(false)
  const [newPassword, setNewPassword] = useState('')
  const [passwordMsg, setPasswordMsg] = useState('')
  const [passwordLoading, setPasswordLoading] = useState(false)
  const [activeTab, setActiveTab] = useState<'current' | 'past'>('current')

  useEffect(() => {
    if (!sessionToken) {
      router.push('/portal/access?error=missing_token')
      return
    }

    // Store session in sessionStorage for subsequent page loads
    sessionStorage.setItem('portal_session', sessionToken)

    const api = createApiClient(sessionToken)
    api.get('/api/v1/portal/me')
      .then(res => setPortalData(res.data))
      .catch(() => setError('Failed to load your portal data. Please try again.'))
      .finally(() => setLoading(false))
  }, [sessionToken, router])

  const handleSetPassword = async (e: React.FormEvent) => {
    e.preventDefault()
    if (newPassword.length < 8) {
      setPasswordMsg('Password must be at least 8 characters.')
      return
    }
    setPasswordLoading(true)
    try {
      const api = createApiClient(sessionToken!)
      await api.post('/api/v1/portal/set-password', { newPassword })
      setPasswordMsg('✓ Password set! You can now log in directly at any time.')
      setNewPassword('')
      setShowPasswordForm(false)
      if (portalData) setPortalData({ ...portalData, hasPassword: true })
    } catch {
      setPasswordMsg('Failed to set password. Please try again.')
    } finally {
      setPasswordLoading(false)
    }
  }

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-96 gap-4">
        <div className="w-12 h-12 rounded-full border-4 animate-spin" style={{ borderColor: 'rgba(226,185,111,0.2)', borderTopColor: '#e2b96f' }} />
        <p style={{ color: '#a0aec0' }}>Loading your portal…</p>
      </div>
    )
  }

  if (error || !portalData) {
    return (
      <div className="text-center py-16">
        <p className="text-4xl mb-4">⚠️</p>
        <p style={{ color: '#fc8181' }}>{error || 'Unable to load portal data.'}</p>
      </div>
    )
  }

  const now = new Date()
  const currentBookings = portalData.bookings.filter(b =>
    ['Confirmed', 'CheckedIn'].includes(b.status) || new Date(b.checkOutDate) >= now
  )
  const pastBookings = portalData.bookings.filter(b =>
    !['Confirmed', 'CheckedIn'].includes(b.status) && new Date(b.checkOutDate) < now
  )

  return (
    <div>
      {/* Welcome Banner */}
      <div className="mb-8 p-6 rounded-2xl" style={{ background: 'linear-gradient(135deg, rgba(226,185,111,0.08), rgba(212,160,84,0.04))', border: '1px solid rgba(226,185,111,0.15)' }}>
        <div className="flex items-center justify-between flex-wrap gap-4">
          <div>
            <h1 className="text-2xl font-bold text-white mb-1">Welcome, {portalData.customerName.split(' ')[0]}! 👋</h1>
            <p className="text-sm" style={{ color: '#718096' }}>{portalData.email}</p>
          </div>
          <div className="flex gap-3 flex-wrap">
            {!portalData.hasPassword && (
              <button
                id="set-password-btn"
                onClick={() => setShowPasswordForm(!showPasswordForm)}
                className="px-4 py-2 rounded-lg text-sm font-medium transition-all"
                style={{ background: 'rgba(226,185,111,0.1)', color: '#e2b96f', border: '1px solid rgba(226,185,111,0.25)' }}
              >
                🔐 Set a Password
              </button>
            )}
            {portalData.hasPassword && (
              <span className="px-3 py-2 rounded-lg text-xs" style={{ background: 'rgba(72,187,120,0.1)', color: '#68d391', border: '1px solid rgba(72,187,120,0.2)' }}>
                ✓ Password Set
              </span>
            )}
          </div>
        </div>

        {/* Set password form */}
        {showPasswordForm && (
          <form onSubmit={handleSetPassword} className="mt-4 flex gap-3 items-center flex-wrap">
            <input
              id="new-password-input"
              type="password"
              value={newPassword}
              onChange={e => setNewPassword(e.target.value)}
              placeholder="Choose a password (min. 8 characters)"
              className="flex-1 px-4 py-2 rounded-lg text-white text-sm outline-none min-w-48"
              style={{ background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.12)' }}
              required
            />
            <button
              id="save-password-btn"
              type="submit"
              disabled={passwordLoading}
              className="px-4 py-2 rounded-lg text-sm font-medium"
              style={{ background: 'linear-gradient(135deg, #e2b96f, #d4a054)', color: '#1a1a2e' }}
            >
              {passwordLoading ? 'Saving…' : 'Save Password'}
            </button>
            {passwordMsg && <span className="text-sm w-full" style={{ color: passwordMsg.startsWith('✓') ? '#68d391' : '#fc8181' }}>{passwordMsg}</span>}
          </form>
        )}
      </div>

      {/* Booking Tabs */}
      <div className="flex gap-1 mb-6 p-1 rounded-xl w-fit" style={{ background: 'rgba(255,255,255,0.04)' }}>
        {([['current', 'Current Bookings'], ['past', 'Past Stays']] as const).map(([key, label]) => (
          <button
            key={key}
            id={`tab-${key}`}
            onClick={() => setActiveTab(key)}
            className="px-4 py-2 rounded-lg text-sm font-medium transition-all"
            style={{
              background: activeTab === key ? 'rgba(226,185,111,0.15)' : 'transparent',
              color: activeTab === key ? '#e2b96f' : '#718096',
            }}
          >
            {label}
            <span className="ml-2 px-1.5 py-0.5 rounded-full text-xs" style={{ background: 'rgba(255,255,255,0.08)', color: '#a0aec0' }}>
              {key === 'current' ? currentBookings.length : pastBookings.length}
            </span>
          </button>
        ))}
      </div>

      {/* Bookings List */}
      {activeTab === 'current' ? (
        currentBookings.length === 0 ? (
          <div className="text-center py-12" style={{ color: '#4a5568' }}>
            <p className="text-4xl mb-3">🛏️</p>
            <p>No active bookings.</p>
          </div>
        ) : (
          currentBookings.map(b => <BookingCard key={b.bookingId} booking={b} token={sessionToken!} />)
        )
      ) : (
        pastBookings.length === 0 ? (
          <div className="text-center py-12" style={{ color: '#4a5568' }}>
            <p className="text-4xl mb-3">📋</p>
            <p>No past stays found.</p>
          </div>
        ) : (
          pastBookings.map(b => <BookingCard key={b.bookingId} booking={b} token={sessionToken!} />)
        )
      )}

      {/* ── Live Request Tracking ─────────────────────────────────────────── */}
      {(() => {
        const checkedInBooking = currentBookings.find(b => b.status === 'CheckedIn')
        if (!checkedInBooking || !sessionToken) return null
        return (
          <div style={{ marginTop: 32 }}>
            <div style={{
              display: 'flex', alignItems: 'center', gap: 10, marginBottom: 16,
            }}>
              <div style={{
                width: 8, height: 8, borderRadius: '50%', background: '#48bb78',
                boxShadow: '0 0 8px #48bb78', animation: 'pulse 2s infinite',
              }} />
              <h2 style={{ margin: 0, color: '#e2e8f0', fontSize: 17, fontWeight: 700 }}>
                Live Request Tracking
              </h2>
              <span style={{
                fontSize: 11, padding: '2px 8px', borderRadius: 10,
                background: 'rgba(72,187,120,0.1)', color: '#48bb78',
                border: '1px solid rgba(72,187,120,0.25)',
              }}>Real-time</span>
            </div>
            <LiveRequestDashboard
              token={sessionToken}
              bookingId={checkedInBooking.bookingId}
            />
            <style>{`
              @keyframes pulse {
                0%, 100% { opacity: 1; transform: scale(1); }
                50%        { opacity: 0.5; transform: scale(1.3); }
              }
            `}</style>
          </div>
        )
      })()}

      {/* ── AI Concierge Chat Widget ──────────────────────────────────────── */}
      {(() => {
        const checkedInBooking = currentBookings.find(b => b.status === 'CheckedIn')
        if (!checkedInBooking || !sessionToken) return null
        return (
          <GuestChatWidget
            token={sessionToken}
            bookingId={checkedInBooking.bookingId}
            roomId={checkedInBooking.bookingId}  // fallback to bookingId — roomId not in PortalData yet
            roomFloor={1}
            roomNumber={checkedInBooking.roomNumber ?? 'Unknown'}
            customerName={portalData.customerName}
          />
        )
      })()}
    </div>
  )
}

export default function PortalDashboardPage() {
  return (
    <Suspense fallback={
      <div className="flex items-center justify-center min-h-96">
        <div className="w-10 h-10 rounded-full border-4 animate-spin" style={{ borderColor: 'rgba(226,185,111,0.2)', borderTopColor: '#e2b96f' }} />
      </div>
    }>
      <DashboardContent />
    </Suspense>
  )
}
