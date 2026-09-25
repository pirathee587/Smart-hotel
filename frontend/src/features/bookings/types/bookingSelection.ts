/**
 * SmartHotel Booking Selection Types & Data Contracts
 * OTA-style room selection and rate plan interfaces.
 */

export interface MealPlan {
  id: string;
  name: string;
  fromPrice: number;
  tagline?: string;
}

export interface RoomVariant {
  id: string;
  label: string;
  bedDescription?: string;
}

export interface RatePlan {
  id: string;
  name: string;
  inclusionsLabel: string;
  bullets: string[];
  originalPrice?: number;
  price: number;
  currency: string;
  isMemberRate?: boolean;
}

import type { RoomTypeDto,RoomTypeImageDto } from "@/types/roomTypes";

export interface RoomListing {
  id: string;
  name: string;
  imageUrl: string;
  galleryImages?: string[];
  galleryCount: number;
  amenities: string[];
  variants: RoomVariant[];
  bedCount: number;
  sleeps: number;
  description: string;
  fullDescription?: string;
  roomSizeSqFt?: number;
  badge?: string;
  ratePlans: RatePlan[];
  variantRatePlans?: Record<string, RatePlan[]>;
  roomType?: RoomTypeDto;
  images?: RoomTypeImageDto[];
}

export interface CartItem {
  id: string;
  roomId: string;
  roomName: string;
  variantId: string;
  variantLabel: string;
  ratePlanId: string;
  ratePlanName: string;
  price: number;
  currency: string;
  nights: number;
  checkIn: string;
  checkOut: string;
  guests: number;
  rooms: number;
}

export interface BookingSearchParams {
  checkIn: string;
  checkOut: string;
  guests: number;
  rooms: number;
  promoCode?: string;
  selectedMealPlanId?: string;
  accessibleOnly?: boolean;
  viewBy?: "rooms" | "suites" | "villas";
  sortBy?: "lowest_price" | "highest_price" | "sleeps" | "rating";
}
