"use client";

import { UpsellOptionDto } from "@/services/publicApi";
import {
Bed,
CheckCircle2,
ChevronLeft,
ChevronRight,
Maximize2,
ShieldCheck,
Sparkles,
Users,
X
} from "lucide-react";
import Image from "next/image";
import { useState } from "react";
import { RoomListing } from "../types/bookingSelection";

interface RoomDetailsPanelProps {
  isOpen: boolean;
  onClose: () => void;
  upsellOption: UpsellOptionDto | null;
  roomListing?: RoomListing | null;
  nights: number;
  onSelectUpgrade: (option: UpsellOptionDto) => void;
}

export function RoomDetailsPanel({
  isOpen,
  onClose,
  upsellOption,
  roomListing,
  nights,
  onSelectUpgrade,
}: RoomDetailsPanelProps) {
  const [activePhotoIndex, setActivePhotoIndex] = useState(0);

  if (!isOpen || !upsellOption) return null;

  // Aggregate gallery photos
  const gallery = roomListing?.galleryImages && roomListing.galleryImages.length > 0
    ? roomListing.galleryImages
    : [upsellOption.imageUrl || "/images/slowhouse-interior.jpg", "/images/hero-view.jpg", "/images/slowhouse-hero.jpg"];

  const currentPhoto = gallery[activePhotoIndex % gallery.length];

  const totalUpgradePrice = upsellOption.priceDelta * nights;

  return (
    <div className="fixed inset-0 z-[60] flex justify-end" aria-labelledby="room-details-title" role="dialog" aria-modal="true">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/50 backdrop-blur-xs transition-opacity animate-in fade-in duration-300"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Drawer */}
      <div className="relative w-full max-w-xl bg-white shadow-2xl flex flex-col h-full z-10 animate-in slide-in-from-right duration-300">
        {/* Drawer Header */}
        <div className="sticky top-0 z-20 flex items-center justify-between px-6 py-4 bg-white/95 backdrop-blur-md border-b border-[#0F1B1A]/10">
          <div>
            <div className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-[#2F5C52]/10 text-[#2F5C52] text-xs font-semibold uppercase tracking-wider mb-1">
              <Sparkles className="w-3.5 h-3.5 text-[#E07A3E]" />
              {(upsellOption.tier ?? 3) >= 4 ? "Premier Villa" : (upsellOption.tier ?? 3) >= 3 ? "Panorama Suite" : "Signature Suite"}
            </div>
            <h2 id="room-details-title" className="font-serif text-2xl font-semibold text-[#0F1B1A]">
              {upsellOption.roomTypeName || upsellOption.roomName || "Room Upgrade"}
            </h2>
          </div>

          <button
            onClick={onClose}
            className="p-2 rounded-full text-[#0F1B1A]/60 hover:text-[#0F1B1A] hover:bg-[#0F1B1A]/5 transition-colors"
            aria-label="Close details"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Drawer Scrollable Body */}
        <div className="flex-1 overflow-y-auto px-6 py-6 space-y-6">
          {/* Main Photo Gallery */}
          <div className="relative rounded-2xl overflow-hidden aspect-[16/10] bg-[#0F1B1A]/5 shadow-sm group">
            <Image
              src={currentPhoto}
              alt={upsellOption.roomTypeName}
              fill
              className="object-cover transition-transform duration-500 group-hover:scale-105"
              sizes="(max-width: 768px) 100vw, 600px"
              priority
            />
            <div className="absolute inset-0 bg-gradient-to-t from-black/40 via-transparent to-transparent pointer-events-none" />

            {/* Gallery Navigation Arrows */}
            {gallery.length > 1 && (
              <div className="absolute inset-x-3 top-1/2 -translate-y-1/2 flex justify-between pointer-events-none">
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    setActivePhotoIndex((prev) => (prev > 0 ? prev - 1 : gallery.length - 1));
                  }}
                  className="pointer-events-auto p-1.5 rounded-full bg-white/90 text-[#0F1B1A] shadow-md hover:bg-white hover:scale-105 transition-all"
                  aria-label="Previous image"
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    setActivePhotoIndex((prev) => (prev + 1) % gallery.length);
                  }}
                  className="pointer-events-auto p-1.5 rounded-full bg-white/90 text-[#0F1B1A] shadow-md hover:bg-white hover:scale-105 transition-all"
                  aria-label="Next image"
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            )}

            {/* Photo counter */}
            <div className="absolute bottom-3 right-3 px-3 py-1 rounded-full bg-black/60 backdrop-blur-md text-white text-xs font-medium">
              {activePhotoIndex + 1} of {gallery.length} Photos
            </div>
          </div>

          {/* Gallery Thumbnails */}
          {gallery.length > 1 && (
            <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-thin">
              {gallery.map((img, idx) => (
                <button
                  key={idx}
                  type="button"
                  onClick={() => setActivePhotoIndex(idx)}
                  className={`relative w-20 h-14 rounded-lg overflow-hidden shrink-0 border-2 transition-all ${
                    idx === activePhotoIndex
                      ? "border-[#E07A3E] ring-2 ring-[#E07A3E]/30"
                      : "border-transparent opacity-70 hover:opacity-100"
                  }`}
                >
                  <Image src={img} alt={`Thumb ${idx + 1}`} fill className="object-cover" />
                </button>
              ))}
            </div>
          )}

          {/* Quick Specs Grid */}
          <div className="grid grid-cols-3 gap-3 p-4 rounded-xl bg-[#F7F6F2] border border-[#0F1B1A]/8 text-center">
            <div className="flex flex-col items-center">
              <Maximize2 className="w-4 h-4 text-[#2F5C52] mb-1" />
              <span className="text-xs text-[#0F1B1A]/60 uppercase tracking-wider font-medium">Size</span>
              <span className="text-sm font-semibold text-[#0F1B1A]">
                {roomListing?.roomSizeSqFt ? `${roomListing.roomSizeSqFt} sq ft` : "780 sq ft"}
              </span>
            </div>
            <div className="flex flex-col items-center border-x border-[#0F1B1A]/10">
              <Users className="w-4 h-4 text-[#2F5C52] mb-1" />
              <span className="text-xs text-[#0F1B1A]/60 uppercase tracking-wider font-medium">Sleeps</span>
              <span className="text-sm font-semibold text-[#0F1B1A]">
                Up to {upsellOption.maxOccupancy || 3} Guests
              </span>
            </div>
            <div className="flex flex-col items-center">
              <Bed className="w-4 h-4 text-[#2F5C52] mb-1" />
              <span className="text-xs text-[#0F1B1A]/60 uppercase tracking-wider font-medium">Bedding</span>
              <span className="text-sm font-semibold text-[#0F1B1A]">
                {roomListing?.variants?.[0]?.bedDescription || "King Bed + Lounge"}
              </span>
            </div>
          </div>

          {/* Description */}
          <div>
            <h3 className="font-serif text-lg font-semibold text-[#0F1B1A] mb-2">About this Sanctuary</h3>
            <p className="text-sm text-[#0F1B1A]/80 leading-relaxed font-sans">
              {roomListing?.fullDescription || upsellOption.fullDescription || upsellOption.description || upsellOption.shortDescription || ""}
            </p>
          </div>

          {/* Key Upgrade Highlights */}
          <div>
            <h3 className="font-serif text-lg font-semibold text-[#0F1B1A] mb-3 flex items-center gap-2">
              <Sparkles className="w-4 h-4 text-[#E07A3E]" />
              Signature Upgrade Highlights
            </h3>
            <div className="grid grid-cols-1 gap-2.5">
              {(
                (upsellOption.keyFeatures && upsellOption.keyFeatures.length > 0)
                  ? upsellOption.keyFeatures
                  : (upsellOption.amenities && upsellOption.amenities.length > 0)
                  ? upsellOption.amenities.slice(0, 3)
                  : ["Panoramic View", "Complimentary Refreshments"]
              ).map((feat, i) => (
                <div
                  key={i}
                  className="flex items-start gap-3 p-3 rounded-xl bg-emerald-50/70 border border-emerald-200/50 text-[#0F1B1A]"
                >
                  <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0 mt-0.5" />
                  <span className="text-sm font-medium">{feat}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Full Amenities */}
          <div>
            <h3 className="font-serif text-lg font-semibold text-[#0F1B1A] mb-3">Room Amenities & Features</h3>
            <div className="grid grid-cols-2 gap-2.5">
              {(upsellOption.amenities && upsellOption.amenities.length > 0
                ? upsellOption.amenities
                : roomListing?.amenities || [
                    "Starlink High-Speed WiFi",
                    "Geothermal Soak Tub",
                    "Acoustic Timber Insulation",
                    "Espresso & Artisanal Tea",
                    "Climate Control",
                    "Mountain View Glazing",
                    "Keyless Digital Entry",
                    "Fireplace Lounge",
                  ]
              ).map((amenity, i) => (
                <div key={i} className="flex items-center gap-2 text-sm text-[#0F1B1A]/80">
                  <div className="w-1.5 h-1.5 rounded-full bg-[#2F5C52]" />
                  <span>{amenity}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Policies Note */}
          <div className="flex items-start gap-3 p-3.5 rounded-xl bg-[#0F1B1A]/5 border border-[#0F1B1A]/10 text-xs text-[#0F1B1A]/70">
            <ShieldCheck className="w-4 h-4 text-[#2F5C52] shrink-0 mt-0.5" />
            <p>
              Upgrading applies seamlessly to your selected rate plan. All existing inclusions (meal plans, cancellation window, and member discounts) are preserved with enhanced luxury space.
            </p>
          </div>
        </div>

        {/* Bottom Action Footer */}
        <div className="sticky bottom-0 z-20 px-6 py-4 bg-white border-t border-[#0F1B1A]/10 shadow-[0_-4px_20px_rgba(0,0,0,0.05)]">
          <div className="flex items-center justify-between gap-4">
            <div>
              <div className="text-xs text-[#0F1B1A]/60 font-medium">Upgrade Difference</div>
              <div className="flex items-baseline gap-1.5">
                <span className="text-2xl font-bold text-[#0F1B1A] font-serif">
                  +${upsellOption.priceDelta}
                </span>
                <span className="text-xs text-[#0F1B1A]/60">/ night</span>
              </div>
              <div className="text-xs text-emerald-700 font-medium">
                +${totalUpgradePrice} total for {nights} night{nights > 1 ? "s" : ""}
              </div>
            </div>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={onClose}
                className="px-4 py-2.5 rounded-xl text-sm font-medium text-[#0F1B1A]/70 hover:text-[#0F1B1A] hover:bg-[#0F1B1A]/5 transition-colors"
              >
                Back
              </button>
              <button
                type="button"
                onClick={() => onSelectUpgrade(upsellOption)}
                className="px-6 py-2.5 rounded-xl bg-[#E07A3E] hover:bg-[#C4622D] text-white font-medium text-sm shadow-md hover:shadow-lg transition-all flex items-center gap-1.5 active:scale-98"
              >
                <span>Upgrade to this Room</span>
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
