/**
 * SmartHotel Public API — unauthenticated client
 * Used for public-facing calls: room types, availability, etc.
 * No auth token required.
 */

import type {
BookingDto,
CreateBookingPayload,
RoomAvailabilityDto,
RoomTypeDto,
} from '@/types/roomTypes'
import axios from 'axios'

export type { BookingDto,CreateBookingPayload,RoomAvailabilityDto,RoomTypeDto }

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

const client = axios.create({
  baseURL: API_BASE,
  timeout: 12_000,
  headers: { 'Content-Type': 'application/json' },
})

export const publicApi = {
  // ── Room Types ─────────────────────────────────────────────────────────────

  /**
   * Fetch all published, active room types.
   * Ordered by price ascending.
   */
  async getRoomTypes(signal?: AbortSignal): Promise<RoomTypeDto[]> {
    const { data } = await client.get<RoomTypeDto[]>('/api/v1/room-types', { signal })
    return data
  },

  /**
   * Fetch a single room type by ID.
   */
  async getRoomTypeById(id: string, signal?: AbortSignal): Promise<RoomTypeDto> {
    const { data } = await client.get<RoomTypeDto>(`/api/v1/room-types/${id}`, { signal })
    return data
  },

  // ── Availability ───────────────────────────────────────────────────────────

  /**
   * Check room availability for all published room types (or a specific one)
   * for the given date range.
   */
  async checkAvailability(
    checkIn: string,
    checkOut: string,
    roomTypeId?: string,
    signal?: AbortSignal,
  ): Promise<RoomAvailabilityDto[]> {
    const params: Record<string, string> = { checkIn, checkOut }
    if (roomTypeId) params.roomTypeId = roomTypeId

    const { data } = await client.get<RoomAvailabilityDto[]>(
      '/api/v1/rooms/availability',
      { params, signal },
    )
    return data
  },

  // ── Booking (requires auth token) ─────────────────────────────────────────

  /**
   * Create a new booking. Requires Supabase JWT bearer token.
   */
  async createBooking(
    payload: CreateBookingPayload,
    token: string,
  ): Promise<BookingDto> {
    const { data } = await client.post<BookingDto>(
      '/api/v1/bookings',
      payload,
      { headers: { Authorization: `Bearer ${token}` } },
    )
    return data
  },

  /**
   * Fetch a single booking by ID. Requires auth token.
   */
  async getBookingById(
    bookingId: string,
    token: string,
    signal?: AbortSignal,
  ): Promise<BookingDto> {
    const { data } = await client.get<BookingDto>(`/api/v1/bookings/${bookingId}`, {
      headers: { Authorization: `Bearer ${token}` },
      signal,
    })
    return data
  },

  // ── PayHere Payments ───────────────────────────────────────────────────────

  /**
   * Generate a signed PayHere checkout order for a confirmed booking.
   * Returns form fields to POST to PayHere sandbox/live checkout URL.
   */
  async createPayHereOrder(
    bookingId: string,
    customerFirstName: string,
    customerPhone: string,
    token: string,
    options?: { returnUrl?: string; cancelUrl?: string; notifyUrl?: string },
  ): Promise<PayHereOrderResponse> {
    const baseUrl = typeof window !== 'undefined' ? window.location.origin : ''
    const { data } = await client.post<PayHereOrderResponse>(
      '/api/v1/payments/payhere/order',
      {
        bookingId,
        customerFirstName,
        customerPhone,
        returnUrl: options?.returnUrl ?? `${baseUrl}/booking/payment-success`,
        cancelUrl: options?.cancelUrl ?? `${baseUrl}/booking/payment-cancel`,
        notifyUrl:
          options?.notifyUrl ??
          `${API_BASE}/api/v1/payments/payhere/notify`,
      },
      { headers: { Authorization: `Bearer ${token}` } },
    )
    return data
  },

  // ── Upsell & Upgrades ──────────────────────────────────────────────────────

  /**
   * Fetch room upgrade options for a given room and stay parameters.
   */
  async getUpsellOptions(
    roomId: string,
    checkIn?: string,
    checkOut?: string,
    guests?: number,
    signal?: AbortSignal,
  ): Promise<UpsellOptionDto[]> {
    try {
      const params: Record<string, string | number> = {}
      if (checkIn) params.checkIn = checkIn
      if (checkOut) params.checkOut = checkOut
      if (guests) params.guests = guests

      const { data } = await client.get<UpsellOptionDto[]>(
        `/api/rooms/${roomId}/upsell-options`,
        { params, signal }
      )
      return (data || []).map((item) => {
        const roomName = item.roomTypeName || item.roomName || 'Selected Suite'
        const description = item.description || item.shortDescription || item.fullDescription || ''
        const amenities: string[] = Array.isArray(item.amenities) ? item.amenities : []
        const keyFeatures: string[] = Array.isArray(item.keyFeatures) && item.keyFeatures.length > 0
          ? item.keyFeatures
          : amenities.length > 0
          ? amenities.slice(0, 3)
          : ['Panoramic View', 'Complimentary Amenities']

        return {
          ...item,
          roomId: item.roomId,
          roomTypeId: item.roomTypeId || item.roomId,
          roomTypeName: roomName,
          roomName,
          roomNumber: item.roomNumber || '',
          tier: typeof item.tier === 'number' ? item.tier : 3,
          description,
          shortDescription: item.shortDescription || description,
          fullDescription: item.fullDescription || description,
          pricePerNight: item.pricePerNight ?? item.upgradePrice ?? 0,
          upgradePrice: item.upgradePrice ?? item.pricePerNight ?? 0,
          priceDelta: item.priceDelta ?? 0,
          currency: item.currency || 'USD',
          imageUrl: item.imageUrl || '',
          galleryImages: item.galleryImages || (item.imageUrl ? [item.imageUrl] : []),
          maxOccupancy: item.maxOccupancy ?? item.sleeps ?? 2,
          sleeps: item.sleeps ?? item.maxOccupancy ?? 2,
          keyFeatures,
          amenities,
        }
      })
    } catch (err) {
      if (axios.isCancel(err)) {
        throw err
      }
      console.error(`[publicApi] Failed to fetch upsell options for room ${roomId}:`, err)
      return []
    }
  },

  // ── Booking Draft & Checkout ────────────────────────────────────────────────

  /**
   * Create a temporary booking draft holding the room & rate plan.
   */
  async createBookingDraft(
    payload: CreateBookingDraftPayload
  ): Promise<BookingDraftDto> {
    const { data } = await client.post<BookingDraftDto>(
      '/api/bookings/draft',
      payload
    )
    return data
  },

  /**
   * Upgrade an existing draft booking to a higher tier room.
   */
  async upgradeBookingDraft(
    draftId: string,
    upgradeOptionId: string
  ): Promise<BookingDraftDto> {
    const { data } = await client.post<BookingDraftDto>(
      `/api/bookings/draft/${draftId}/upgrade`,
      { upgradeOptionId }
    )
    return data
  },

  /**
   * Complete checkout and tokenize payment.
   */
  async checkoutBooking(
    payload: CheckoutBookingPayload,
    token?: string
  ): Promise<CheckoutResultDto> {
    const headers: Record<string, string> = {}
    if (token) headers.Authorization = `Bearer ${token}`

    const { data } = await client.post<CheckoutResultDto>(
      '/api/bookings/checkout',
      payload,
      { headers }
    )
    return data
  },
}

// ── Additional types ──────────────────────────────────────────────────────────

export interface PayHereOrderResponse {
  merchantId: string
  orderId: string
  amount: number
  currency: string
  hash: string
  checkoutUrl: string
  formFields: Record<string, string>
}

export interface UpsellOptionDto {
  roomId: string
  roomTypeId: string
  roomTypeName: string
  roomName?: string
  roomNumber?: string
  tier?: number
  badge?: string
  description?: string
  shortDescription?: string
  fullDescription?: string
  pricePerNight: number
  upgradePrice?: number
  currentPrice?: number
  priceDelta: number
  currency?: string
  imageUrl: string
  galleryImages?: string[]
  maxOccupancy?: number
  sleeps?: number
  bedCount?: number
  bedDescription?: string
  roomSizeSqFt?: number
  keyFeatures: string[]
  amenities: string[]
}

export interface CreateBookingDraftPayload {
  roomId: string
  roomName: string
  roomTypeId: string
  ratePlanId: string
  ratePlanName: string
  checkInDate: string
  checkOutDate: string
  guestCount: number
  roomsCount: number
  pricePerNight: number
  appliedPromoCode?: string | null
}

export interface BookingDraftDto {
  id: string
  roomId: string
  roomName: string
  roomTypeId: string
  ratePlanId: string
  ratePlanName: string
  checkInDate: string
  checkOutDate: string
  guestCount: number
  roomsCount: number
  pricePerNight: number
  nights: number
  taxesAndFees: number
  totalAmount: number
  appliedPromoCode?: string | null
  expiresAt: string
}

export interface ContactInfoDto {
  firstName: string
  lastName: string
  mobile: string
  email: string
}

export interface AddressInfoDto {
  addressType: string
  country: string
  addressLine1: string
  city: string
}

export interface LoyaltyInfoDto {
  program?: string
  loyaltyId?: string
}

export interface CheckoutBookingPayload {
  draftId: string
  contact: ContactInfoDto
  address?: AddressInfoDto
  specialRequests?: string
  loyalty?: LoyaltyInfoDto
  paymentToken: string
  couponCode?: string
}

export interface CheckoutResultDto {
  bookingId: string
  bookingReference: string
  status: string
  roomName: string
  checkInDate: string
  checkOutDate: string
  totalAmount: number
  currency: string
  guestName: string
  guestEmail: string
}
