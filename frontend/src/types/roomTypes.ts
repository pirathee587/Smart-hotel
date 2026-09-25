// ── Room Types ──────────────────────────────────────────────────────────────

export interface RoomTypeImageDto {
  id: string;
  roomTypeId: string;
  imageUrl: string;
  displayOrder: number;
  isPrimary: boolean;
}

export interface RoomTypeDto {
  id: string;
  hotelId: string;
  name: string;
  title: string;
  bedType: string;
  capacity: number;
  roomSizeSqFt: number;
  pricePerNight: number;
  cleaningFee: number;
  amenitiesFee: number;
  longDescription: string;
  highlights: string[];
  amenities: string[];
  cancellationPolicyText: string;
  isPublished: boolean;
  isActive: boolean;
  images: RoomTypeImageDto[];
}

// ── Availability ─────────────────────────────────────────────────────────────

export interface RoomAvailabilityDto {
  roomTypeId: string;
  roomTypeName: string;
  totalRooms: number;
  availableCount: number;
  firstAvailableRoomId: string | null;
}

// ── Booking ───────────────────────────────────────────────────────────────────

export interface CreateBookingPayload {
  customerLastName: string;
  customerEmail: string;
  roomId: string;
  roomTypeId: string;
  checkInDate: string;   // ISO date: "2026-09-15"
  checkOutDate: string;
  guestCount: number;
}

export interface BookingDto {
  id: string;
  bookingReference: string;
  customerId: string;
  customerLastName: string;
  customerEmail: string;
  roomId: string;
  roomNumber: string;
  roomTypeId: string;
  checkInDate: string;
  checkOutDate: string;
  guestCount: number;
  totalAmount: number;
  status: string;
  paymentReference?: string;
  createdAtUtc: string;
}
