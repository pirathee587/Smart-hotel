/**
 * SmartHotel Guest Portal — typed API client
 * Wraps all portal-facing backend calls with Axios.
 * Token is passed in explicitly to keep the service stateless (no global interceptor).
 */

import axios, { type AxiosInstance } from 'axios'
import type {
  ChatMessageDto,
  SendMessageRequest,
  SendMessageResult,
  ActiveRequestDto,
  EscalateRequest,
  EscalateResult,
  SubmitCsatRequest,
} from '@/types/chat'

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

// ── Types ──────────────────────────────────────────────────────────────────────

export interface ServiceRequest {
  id: string
  title: string
  status: string
  createdAt: string
}

export interface BookingDto {
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

export interface PortalData {
  customerName: string
  email: string
  hasPassword: boolean
  bookings: BookingDto[]
}

export interface BookingLookupResult {
  bookingId: string
  customerId: string
  referenceCode: string
  checkInDate: string
  checkOutDate: string
  bookingStatus: string
  totalAmount: number
  paymentStatus?: string
}

export interface RefundResult {
  refundAmount: number
  status: 'FullRefund' | 'PartialRefund' | 'NoRefund'
  gatewayRefundId?: string
}

// ── Client factory ─────────────────────────────────────────────────────────────

function createClient(token: string): AxiosInstance {
  return axios.create({
    baseURL: API_BASE,
    headers: { Authorization: `Bearer ${token}` },
    timeout: 15_000,
  })
}

// ── Portal API ─────────────────────────────────────────────────────────────────

export const portalApi = {
  /**
   * Fetches all bookings, payment status, and service requests for the authenticated guest.
   */
  async getPortalData(token: string): Promise<PortalData> {
    const { data } = await createClient(token).get<PortalData>('/api/v1/portal/me')
    return data
  },

  /**
   * Fallback lookup: find a booking by reference code + email (no JWT needed).
   * Used when the magic link has expired.
   */
  async lookupBooking(
    referenceCode: string,
    email: string,
  ): Promise<BookingLookupResult> {
    const { data } = await axios.get<BookingLookupResult>(
      `${API_BASE}/api/v1/auth/portal-lookup`,
      { params: { referenceCode: referenceCode.toUpperCase(), email } },
    )
    return data
  },

  /**
   * Guest-initiated refund. Cancellation policy evaluated server-side.
   */
  async requestRefund(
    token: string,
    bookingId: string,
    reason = 'Guest-initiated cancellation',
  ): Promise<RefundResult> {
    const { data } = await createClient(token).post<RefundResult>(
      `/api/v1/bookings/${bookingId}/refund`,
      { reason },
    )
    return data
  },

  /**
   * Submits a service request (room service, housekeeping, dining, etc).
   */
  async submitServiceRequest(
    token: string,
    bookingId: string,
    title: string,
    description = '',
  ): Promise<void> {
    await createClient(token).post('/api/v1/bookings/service-requests', {
      bookingId,
      title,
      description,
    })
  },

  /**
   * Sets a password for an OTA guest account (enables direct login for future stays).
   */
  async setPassword(token: string, newPassword: string): Promise<void> {
    await createClient(token).post('/api/v1/portal/set-password', { newPassword })
  },

  /**
   * Builds the magic-link URL that the backend redirects through.
   * Useful if the frontend wants to display a "click here" button for the verification step.
   */
  getMagicLinkUrl(rawToken: string): string {
    return `${API_BASE}/api/v1/auth/portal-access?token=${encodeURIComponent(rawToken)}`
  },

  // ── AI Concierge Chat API ──────────────────────────────────────────────────

  /**
   * Sends a guest message to the AI concierge.
   * The LLM response is streamed via SignalR (ChatTokenDelta events).
   * This call returns the final assembled reply + any ticket metadata.
   */
  async sendChatMessage(
    token: string,
    req: SendMessageRequest,
  ): Promise<SendMessageResult> {
    const { data } = await createClient(token).post<SendMessageResult>(
      '/api/v1/chat/message',
      req,
    )
    return data
  },

  /**
   * Fetches the chat history for the guest's active session (for hydrating on refresh).
   */
  async getChatHistory(
    token: string,
    bookingId: string,
    limit = 50,
  ): Promise<ChatMessageDto[]> {
    const { data } = await createClient(token).get<ChatMessageDto[]>(
      '/api/v1/chat/history',
      { params: { bookingId, limit } },
    )
    return data
  },

  /**
   * Triggers "Talk to a Human" — escalates the session and notifies Front Desk.
   */
  async escalateChatSession(
    token: string,
    req: EscalateRequest,
  ): Promise<EscalateResult> {
    const { data } = await createClient(token).post<EscalateResult>(
      '/api/v1/chat/escalate',
      req,
    )
    return data
  },

  /**
   * Returns all service requests for the live request tracking dashboard.
   * Updated in real-time via SignalR (RequestStatusUpdated events).
   */
  async getActiveRequests(
    token: string,
    bookingId: string,
  ): Promise<ActiveRequestDto[]> {
    const { data } = await createClient(token).get<ActiveRequestDto[]>(
      '/api/v1/chat/requests',
      { params: { bookingId } },
    )
    return data
  },

  /**
   * Submits a CSAT rating (1–5 stars) after a service request is Completed.
   */
  async submitCsatFeedback(
    token: string,
    req: SubmitCsatRequest,
  ): Promise<void> {
    await createClient(token).post('/api/v1/chat/csat', req)
  },
}
