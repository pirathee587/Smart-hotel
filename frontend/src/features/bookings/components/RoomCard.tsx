"use client";

import type { RoomTypeDto } from "@/types/roomTypes";
import {
Bath,
Bed,
Camera,
Check,
ChevronDown,
ChevronUp,
Clock,
Coffee,
Flame,
Key,
Sparkles,
Users,
Utensils,
Wifi,
Wind,
X
} from "lucide-react";
import React,{ useState } from "react";
import { RatePlan,RoomListing,RoomVariant } from "../types/bookingSelection";

interface RoomCardProps {
  room: RoomListing;
  roomType?: RoomTypeDto;
  nights: number;
  checkIn: string;
  checkOut: string;
  guests: number;
  roomsCount: number;
  onBookRatePlan: (room: RoomListing, variant: RoomVariant, ratePlan: RatePlan) => void;
}

// ── Amenity Icon Helper ───────────────────────────────────────────────────────
function getAmenityIcon(name: string): React.ElementType {
  const lower = name.toLowerCase();
  if (lower.includes("wifi") || lower.includes("internet")) return Wifi;
  if (lower.includes("air") || lower.includes("wind") || lower.includes("dryer")) return Wind;
  if (lower.includes("shower") || lower.includes("bath") || lower.includes("tub") || lower.includes("pool")) return Bath;
  if (lower.includes("coffee") || lower.includes("espresso")) return Coffee;
  if (lower.includes("dining") || lower.includes("food") || lower.includes("meal")) return Utensils;
  if (lower.includes("fire")) return Flame;
  if (lower.includes("key")) return Key;
  return Sparkles;
}

export function RoomCard({
  room,
  roomType,
  onBookRatePlan,
}: RoomCardProps) {
  // Selected variant state (defaults to first variant)
  const [selectedVariantId, setSelectedVariantId] = useState<string>(
    room.variants[0]?.id ?? ""
  );
  // Expandable details state
  const [detailsOpen, setDetailsOpen] = useState(false);
  // Gallery modal state
  const [galleryOpen, setGalleryOpen] = useState(false);
  const [activeGalleryIdx, setActiveGalleryIdx] = useState(0);

  const currentVariant =
    room.variants.find((v) => v.id === selectedVariantId) ?? room.variants[0];

  // Dynamic rate plans according to selected variant
  const activeRatePlans: RatePlan[] =
    room.variantRatePlans && currentVariant
      ? room.variantRatePlans[currentVariant.id] ?? room.ratePlans
      : room.ratePlans;

  // Active room type and primary image resolution:
  // 1. Use roomType.images.find(i => i.isPrimary)?.imageUrl
  // 2. Fall back to room.imageUrl
  // 3. Fall back to existing curated placeholder image (/images/slowhouse-hero.jpg)
  const activeType = roomType ?? room.roomType;
  const primaryUploadedImage =
    activeType?.images?.find((i) => i.isPrimary)?.imageUrl ||
    room.images?.find((i) => i.isPrimary)?.imageUrl;

  const displayImageUrl =
    primaryUploadedImage || room.imageUrl || "/images/slowhouse-hero.jpg";

  const galleryImages =
    activeType?.images && activeType.images.length > 0
      ? activeType.images.map((i) => i.imageUrl)
      : room.images && room.images.length > 0
      ? room.images.map((i) => i.imageUrl)
      : room.galleryImages?.length
      ? room.galleryImages
      : [displayImageUrl];

  return (
    <article
      className="bg-[#16302C]/90 border border-[#2F5C52]/40 rounded-3xl overflow-hidden shadow-2xl transition-all duration-300 hover:border-[#2F5C52] flex flex-col lg:flex-row mb-10 text-[#F6F1E6]"
      aria-label={room.name}
    >
      {/* ─────────────────────────────────────────────────────────────
          LEFT COLUMN: Room Photo + Amenity 2-Column Grid
      ───────────────────────────────────────────────────────────── */}
      <div className="w-full lg:w-[380px] xl:w-[420px] bg-[#0F1B1A]/80 flex flex-col border-b lg:border-b-0 lg:border-r border-[#2F5C52]/30 shrink-0">
        {/* Photo Container */}
        <div className="relative h-64 sm:h-72 w-full overflow-hidden group cursor-pointer" onClick={() => setGalleryOpen(true)}>
          {/* Room Image */}
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={displayImageUrl}
            alt={room.name}
            className="w-full h-full object-cover transition-transform duration-700 group-hover:scale-105"
            loading="lazy"
            onError={(e) => {
              // Fallback to existing curated placeholder if image fails to load
              e.currentTarget.src = "/images/slowhouse-hero.jpg";
            }}
          />
          <div className="absolute inset-0 bg-gradient-to-t from-[#0F1B1A]/80 via-transparent to-transparent pointer-events-none" />

          {/* Small Gallery / Image-Count Badge */}
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              setGalleryOpen(true);
            }}
            className="absolute bottom-3 left-3 inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-black/70 backdrop-blur-md text-white text-[11px] font-medium border border-white/20 hover:bg-black/90 transition-all cursor-pointer"
            aria-label={`View ${room.galleryCount} photos of ${room.name}`}
          >
            <Camera className="w-3.5 h-3.5 text-[#E07A3E]" />
            <span>{room.galleryCount} Photos</span>
          </button>

          {/* Room Size Badge */}
          {room.roomSizeSqFt && (
            <span className="absolute top-3 left-3 px-2.5 py-1 rounded-full bg-[#0F1B1A]/80 backdrop-blur-sm text-[10px] font-semibold text-[#EEE7D6] tracking-wide border border-[#2F5C52]/30">
              {room.roomSizeSqFt.toLocaleString()} sq ft
            </span>
          )}
        </div>

        {/* Amenity Icon List Below Photo (2-column grid: Hair Dryer, Free WiFi, etc.) */}
        <div className="p-5 flex-1 flex flex-col justify-center">
          <div className="text-[11px] uppercase tracking-wider font-semibold text-[#7C9188] mb-3">
            Key In-Room Amenities
          </div>
          <div className="grid grid-cols-2 gap-2.5">
            {room.amenities.slice(0, 6).map((amenity) => {
              const Icon = getAmenityIcon(amenity);
              return (
                <div
                  key={amenity}
                  className="flex items-center gap-2 text-xs text-[#EEE7D6]/90"
                >
                  <Icon className="w-3.5 h-3.5 text-[#2F5C52] shrink-0" />
                  <span className="truncate">{amenity}</span>
                </div>
              );
            })}
          </div>
        </div>
      </div>

      {/* ─────────────────────────────────────────────────────────────
          RIGHT COLUMN: Room Info, Variants, Details & Rate Plans
      ───────────────────────────────────────────────────────────── */}
      <div className="flex-1 p-6 sm:p-7 flex flex-col justify-between">
        <div>
          {/* Header Row: Room name & Bed-type variant toggle */}
          <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-3">
            <div>
              {/* Room name (Fraunces, ink-teal tone) */}
              <h3 className="font-serif font-bold text-2xl sm:text-3xl text-[#F6F1E6] tracking-tight">
                {room.name}
              </h3>

              {/* Meta row: optional urgency badge, bed count, Sleeps N */}
              <div className="flex flex-wrap items-center gap-3 mt-2 text-xs text-[#7C9188]">
                {room.badge && (
                  <span className="inline-flex items-center gap-1 text-[#C4622D] font-semibold bg-[#C4622D]/10 px-2 py-0.5 rounded-md border border-[#C4622D]/20">
                    <Clock className="w-3 h-3 text-[#C4622D]" />
                    {room.badge}
                  </span>
                )}
                <span className="flex items-center gap-1 text-[#EEE7D6]">
                  <Bed className="w-3.5 h-3.5 text-[#2F5C52]" />
                  {currentVariant?.bedDescription ?? `${room.bedCount} Bed`}
                </span>
                <span className="flex items-center gap-1 text-[#EEE7D6]">
                  <Users className="w-3.5 h-3.5 text-[#2F5C52]" />
                  Sleeps {room.sleeps}
                </span>
              </div>
            </div>

            {/* Bed-type variant toggle as pill buttons */}
            <div className="flex flex-wrap items-center gap-1.5 p-1 bg-[#0F1B1A]/80 rounded-2xl border border-[#2F5C52]/40 self-start">
              {room.variants.map((variant) => {
                const isActive = selectedVariantId === variant.id;
                return (
                  <button
                    key={variant.id}
                    type="button"
                    onClick={() => setSelectedVariantId(variant.id)}
                    className={`px-3 py-1.5 rounded-xl text-xs font-medium transition-all cursor-pointer ${
                      isActive
                        ? "bg-[#2F5C52] text-white shadow-md font-semibold"
                        : "text-[#7C9188] hover:text-[#F6F1E6] hover:bg-[#2F5C52]/20"
                    }`}
                  >
                    {variant.label}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Short description (1–2 lines) */}
          <p className="text-xs sm:text-sm text-[#7C9188] mt-3 leading-relaxed">
            {room.description}
          </p>

          {/* "Room Details" expandable link (underline, pine) */}
          <div className="mt-2.5">
            <button
              type="button"
              onClick={() => setDetailsOpen((prev) => !prev)}
              className="inline-flex items-center gap-1 text-xs font-semibold text-[#2F5C52] hover:text-[#E07A3E] transition-colors cursor-pointer"
            >
              <span className="underline underline-offset-4 decoration-[#2F5C52]/60 hover:decoration-[#E07A3E]">
                {detailsOpen ? "Hide Room Details" : "Room Details"}
              </span>
              {detailsOpen ? (
                <ChevronUp className="w-3.5 h-3.5" />
              ) : (
                <ChevronDown className="w-3.5 h-3.5" />
              )}
            </button>

            {/* Expandable Room Details Section */}
            {detailsOpen && (
              <div className="mt-3.5 p-4 bg-[#0F1B1A]/70 border border-[#2F5C52]/30 rounded-2xl text-xs text-[#7C9188] space-y-3 animate-in fade-in duration-200">
                <p className="text-xs text-[#EEE7D6] leading-relaxed">
                  {room.fullDescription || room.description}
                </p>
                <div className="grid grid-cols-2 sm:grid-cols-3 gap-2 pt-2 border-t border-[#2F5C52]/20">
                  <div>
                    <span className="block text-[10px] uppercase tracking-wider text-[#7C9188]">
                      Living Area
                    </span>
                    <span className="text-xs text-[#F6F1E6] font-medium">
                      {room.roomSizeSqFt} sq ft / {Math.round((room.roomSizeSqFt || 0) * 0.0929)} m²
                    </span>
                  </div>
                  <div>
                    <span className="block text-[10px] uppercase tracking-wider text-[#7C9188]">
                      Max Capacity
                    </span>
                    <span className="text-xs text-[#F6F1E6] font-medium">
                      {room.sleeps} Guests
                    </span>
                  </div>
                  <div>
                    <span className="block text-[10px] uppercase tracking-wider text-[#7C9188]">
                      Bed Configuration
                    </span>
                    <span className="text-xs text-[#F6F1E6] font-medium">
                      {currentVariant?.bedDescription ?? "1 Luxury Bed"}
                    </span>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Divider before rate plans */}
        <div className="my-5 border-t border-[#2F5C52]/30" />

        {/* ─────────────────────────────────────────────────────────────
            RATE PLANS (One row per rate plan, stacked vertically)
        ───────────────────────────────────────────────────────────── */}
        <div className="space-y-4">
          {activeRatePlans.map((ratePlan, idx) => (
            <div key={ratePlan.id}>
              {idx > 0 && <div className="my-4 border-t border-[#2F5C52]/20" />}
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 p-4 rounded-2xl bg-[#0F1B1A]/40 border border-[#2F5C52]/25 hover:border-[#2F5C52]/60 transition-colors">
                {/* Left side of rate plan: Name, Inclusions badge, Bullets */}
                <div className="space-y-2 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    {/* Rate plan name (underlined link style) */}
                    <span className="font-serif font-bold text-sm sm:text-base text-[#F6F1E6] underline underline-offset-4 decoration-[#2F5C52]/50">
                      {ratePlan.name}
                    </span>
                    {/* Inclusion Badge with fork/knife or spark icon */}
                    <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-[#2F5C52] bg-[#2F5C52]/15 px-2 py-0.5 rounded-full border border-[#2F5C52]/30">
                      <Utensils className="w-3 h-3 text-[#2F5C52]" />
                      <span>{ratePlan.inclusionsLabel}</span>
                    </span>
                  </div>

                  {/* Bullet list of inclusions */}
                  <ul className="space-y-1 text-xs text-[#7C9188]">
                    {ratePlan.bullets.map((bullet, bIdx) => (
                      <li key={bIdx} className="flex items-start gap-1.5">
                        <Check className="w-3.5 h-3.5 text-[#2F5C52] shrink-0 mt-0.5" />
                        <span>{bullet}</span>
                      </li>
                    ))}
                  </ul>
                </div>

                {/* Right-aligned price block & Book Now button */}
                <div className="flex flex-row md:flex-col items-end justify-between md:justify-center gap-3 shrink-0 pt-2 md:pt-0 border-t md:border-t-0 border-[#2F5C52]/20">
                  <div className="text-right">
                    {/* MEMBER RATE label */}
                    {ratePlan.isMemberRate && (
                      <span className="text-[10px] font-bold tracking-wider uppercase text-[#C4622D] block">
                        MEMBER RATE
                      </span>
                    )}

                    {/* Strikethrough original price & bold discounted price */}
                    <div className="flex items-baseline justify-end gap-2">
                      {ratePlan.originalPrice && (
                        <span className="text-xs text-[#7C9188] line-through">
                          ${ratePlan.originalPrice}
                        </span>
                      )}
                      <span className="font-serif font-bold text-xl sm:text-2xl text-[#C4622D]">
                        ${ratePlan.price}
                      </span>
                      <span className="text-[11px] text-[#7C9188]">
                        / night
                      </span>
                    </div>

                    {/* Excluding taxes and fees caption */}
                    <span className="text-[10px] text-[#7C9188]/70 block">
                      Excluding taxes and fees
                    </span>
                  </div>

                  {/* Solid Book Now button (light orange / ember-bright fill, white text) */}
                  <button
                    type="button"
                    onClick={() => onBookRatePlan(room, currentVariant, ratePlan)}
                    className="px-6 py-2.5 rounded-xl bg-[#E07A3E] hover:bg-[#C4622D] text-white border border-[#E07A3E] text-xs font-bold tracking-wider uppercase shadow-md hover:shadow-lg hover:shadow-[#E07A3E]/30 transition-all hover:scale-[1.02] cursor-pointer whitespace-nowrap active:scale-95"
                    aria-label={`Book ${room.name} with ${ratePlan.name}`}
                  >
                    Book Now
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* ─────────────────────────────────────────────────────────────
          LIGHTWEIGHT GALLERY MODAL
      ───────────────────────────────────────────────────────────── */}
      {galleryOpen && (
        <div
          className="fixed inset-0 z-50 bg-black/90 backdrop-blur-md flex items-center justify-center p-4 animate-in fade-in duration-200"
          onClick={() => setGalleryOpen(false)}
        >
          <div
            className="relative max-w-4xl w-full bg-[#0F1B1A] border border-[#2F5C52]/40 rounded-3xl overflow-hidden shadow-2xl p-4"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between pb-3 border-b border-[#2F5C52]/40">
              <div>
                <h4 className="font-serif font-bold text-lg text-[#F6F1E6]">
                  {room.name}
                </h4>
                <p className="text-xs text-[#7C9188]">
                  Photo {activeGalleryIdx + 1} of {galleryImages.length}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setGalleryOpen(false)}
                className="p-2 text-[#7C9188] hover:text-white rounded-full bg-[#16302C]"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Main Preview */}
            <div className="relative h-80 sm:h-96 w-full mt-3 rounded-2xl overflow-hidden bg-black">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={galleryImages[activeGalleryIdx]}
                alt={`${room.name} preview`}
                className="w-full h-full object-contain"
              />
            </div>

            {/* Thumbnail Row */}
            <div className="flex items-center gap-2 mt-3 overflow-x-auto pb-2">
              {galleryImages.map((img, i) => (
                <button
                  key={i}
                  type="button"
                  onClick={() => setActiveGalleryIdx(i)}
                  className={`w-20 h-14 rounded-xl overflow-hidden border-2 shrink-0 transition-all ${
                    activeGalleryIdx === i
                      ? "border-[#C4622D] scale-105"
                      : "border-transparent opacity-60 hover:opacity-100"
                  }`}
                >
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={img} alt="thumb" className="w-full h-full object-cover" />
                </button>
              ))}
            </div>
          </div>
        </div>
      )}
    </article>
  );
}
