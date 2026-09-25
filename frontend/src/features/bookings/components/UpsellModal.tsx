"use client";

import { publicApi,UpsellOptionDto } from "@/services/publicApi";
import {
ArrowRight,
CheckCircle2,
Sparkles,
X
} from "lucide-react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import React,{ useEffect,useMemo,useState } from "react";
import { RatePlan,RoomListing,RoomVariant } from "../types/bookingSelection";
import { RoomDetailsPanel } from "./RoomDetailsPanel";

export const UPSELL_SUPPRESS_KEY = "smarthotel_suppress_upsell";

interface UpsellModalProps {
  isOpen: boolean;
  onClose: () => void;
  room: RoomListing;
  variant: RoomVariant;
  ratePlan: RatePlan;
  checkIn: string;
  checkOut: string;
  guests: number;
  roomsCount: number;
  nights: number;
  promoCode?: string;
  draftId?: string;
}

export function UpsellModal({
  isOpen,
  onClose,
  room,
  variant,
  ratePlan,
  checkIn,
  checkOut,
  guests,
  roomsCount,
  nights,
  promoCode,
  draftId,
}: UpsellModalProps) {
  const router = useRouter();

  const [upsellOptions, setUpsellOptions] = useState<UpsellOptionDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [suppressUpsell, setSuppressUpsell] = useState(() =>
    typeof window !== "undefined" && sessionStorage.getItem(UPSELL_SUPPRESS_KEY) === "true",
  );

  // State for slide-over RoomDetailsPanel
  const [inspectingOption, setInspectingOption] = useState<UpsellOptionDto | null>(null);

  // Fetch or compute upsell options
  useEffect(() => {
    if (!isOpen) return;

    let isMounted = true;
    queueMicrotask(() => {
      if (isMounted) setLoading(true);
    });
    publicApi
      .getUpsellOptions(room.id, checkIn, checkOut, guests)
      .then((data) => {
        if (!isMounted) return;
        if (data && data.length > 0) {
          setUpsellOptions(data);
        } else setUpsellOptions([]);
      })
      .catch(() => {
        if (!isMounted) return;
        setUpsellOptions([]);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [isOpen, room.id, ratePlan.currency, ratePlan.price, checkIn, checkOut, guests]);

  // Handle suppression toggle
  const handleSuppressToggle = (e: React.ChangeEvent<HTMLInputElement>) => {
    const checked = e.target.checked;
    setSuppressUpsell(checked);
    if (typeof window !== "undefined") {
      if (checked) {
        sessionStorage.setItem(UPSELL_SUPPRESS_KEY, "true");
      } else {
        sessionStorage.removeItem(UPSELL_SUPPRESS_KEY);
      }
    }
  };

  // Navigate to checkout with original room
  const handleProceedWithCurrent = () => {
    onClose();
    const query = new URLSearchParams({
      roomId: room.id,
      roomName: room.name,
      roomTypeId: room.id,
      variantId: variant.id,
      variantLabel: variant.label,
      ratePlanId: ratePlan.id,
      ratePlanName: ratePlan.name,
      checkIn,
      checkOut,
      guests: guests.toString(),
      rooms: roomsCount.toString(),
      price: ratePlan.price.toString(),
      nights: nights.toString(),
      isUpgraded: "false",
    });
    if (promoCode) query.set("promo", promoCode);
    if (draftId) query.set("draftId", draftId);

    router.push(`/booking/checkout?${query.toString()}`);
  };

  // Navigate to checkout with upgraded room
  const handleUpgradeSelection = (option: UpsellOptionDto) => {
    // If we have a draftId, optionally notify backend
    if (draftId) {
      publicApi.upgradeBookingDraft(draftId, option.roomId).catch(() => {});
    }

    const roomName = option.roomTypeName || option.roomName || "Upgraded Suite";
    const tier = option.tier ?? 3;
    const price = option.pricePerNight ?? option.upgradePrice ?? 0;

    onClose();
    const query = new URLSearchParams({
      roomId: option.roomId,
      roomName: roomName,
      roomTypeId: option.roomTypeId || option.roomId,
      variantId: variant.id,
      variantLabel: `${roomName} (${variant.label})`,
      ratePlanId: ratePlan.id,
      ratePlanName: ratePlan.name,
      checkIn,
      checkOut,
      guests: guests.toString(),
      rooms: roomsCount.toString(),
      price: price.toString(),
      originalPrice: ratePlan.price.toString(),
      priceDelta: (option.priceDelta ?? 0).toString(),
      nights: nights.toString(),
      isUpgraded: "true",
      upgradeTier: tier.toString(),
    });
    if (promoCode) query.set("promo", promoCode);
    if (draftId) query.set("draftId", draftId);

    router.push(`/booking/checkout?${query.toString()}`);
  };

  // Find matching listing for detailed panel
  const inspectingRoomListing = useMemo(() => {
    if (!inspectingOption) return null;
    return {
      id: inspectingOption.roomId,
      name: inspectingOption.roomTypeName,
      imageUrl: inspectingOption.imageUrl || "/images/slowhouse-hero.jpg",
      galleryImages: inspectingOption.galleryImages,
      galleryCount: inspectingOption.galleryImages?.length ?? (inspectingOption.imageUrl ? 1 : 0),
      amenities: inspectingOption.amenities ?? inspectingOption.keyFeatures ?? [],
      variants: [{ id: inspectingOption.roomTypeId, label: inspectingOption.roomNumber || "Available room" }],
      bedCount: 1,
      sleeps: inspectingOption.maxOccupancy ?? 2,
      description: inspectingOption.shortDescription || inspectingOption.description || "",
      fullDescription: inspectingOption.fullDescription || inspectingOption.description || "",
      ratePlans: [{
        id: `upgrade-${inspectingOption.roomTypeId}`,
        name: "Upgrade offer",
        inclusionsLabel: "Authoritative upgrade rate",
        bullets: inspectingOption.keyFeatures ?? [],
        price: inspectingOption.pricePerNight,
        currency: inspectingOption.currency ?? ratePlan.currency,
      }],
    } satisfies RoomListing;
  }, [inspectingOption, ratePlan.currency]);

  if (!isOpen) return null;

  return (
    <>
      <div
        className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 overflow-y-auto"
        aria-labelledby="upsell-modal-title"
        role="dialog"
        aria-modal="true"
      >
        {/* Backdrop */}
        <div
          className="fixed inset-0 bg-black/60 backdrop-blur-sm transition-opacity"
          onClick={handleProceedWithCurrent}
          aria-hidden="true"
        />

        {/* Modal Container */}
        <div className="relative w-full max-w-4xl bg-white rounded-3xl shadow-2xl border border-[#0F1B1A]/10 overflow-hidden z-10 my-8 animate-in fade-in zoom-in-95 duration-200">
          {/* Close button */}
          <button
            type="button"
            onClick={handleProceedWithCurrent}
            className="absolute top-5 right-5 z-20 p-2 rounded-full bg-white/80 hover:bg-white text-[#0F1B1A]/60 hover:text-[#0F1B1A] shadow-xs hover:shadow transition-all"
            aria-label="Close modal"
          >
            <X className="w-5 h-5" />
          </button>

          {/* Modal Header */}
          <div className="pt-8 px-6 sm:px-8 pb-4 text-center">
            <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-[#E07A3E]/10 text-[#E07A3E] text-xs font-semibold uppercase tracking-wider mb-2">
              <Sparkles className="w-3.5 h-3.5" />
              Special Upgrade Offer
            </div>
            <h2
              id="upsell-modal-title"
              className="font-serif text-2xl sm:text-3xl font-semibold text-[#0F1B1A]"
            >
              Check out other great options!
            </h2>
            <p className="mt-1 text-sm text-[#0F1B1A]/70 max-w-xl mx-auto font-sans">
              Enhance your stay at SmartHotel Maskeliya. Treat yourself to elevated panoramic lake views, geothermal soak tubs, and bespoke perks.
            </p>
          </div>

          {/* Currently Selected Room Row */}
          <div className="mx-6 sm:mx-8 mb-6 p-3.5 sm:p-4 rounded-2xl bg-[#F7F6F2] border border-[#0F1B1A]/8 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
            <div className="flex items-center gap-3.5">
              <div className="relative w-16 h-16 rounded-xl overflow-hidden shrink-0 bg-[#0F1B1A]/5 shadow-xs">
                <Image
                  src={room.imageUrl || "/images/slowhouse-hero.jpg"}
                  alt={room.name}
                  fill
                  className="object-cover"
                />
              </div>
              <div>
                <span className="text-[11px] font-semibold text-[#2F5C52] uppercase tracking-wider">
                  Currently Selected Room
                </span>
                <h3 className="font-serif text-base font-semibold text-[#0F1B1A]">
                  {room.name} <span className="text-xs font-sans font-normal text-[#0F1B1A]/60">({variant.label})</span>
                </h3>
                <div className="flex items-center gap-3 text-xs text-[#0F1B1A]/70 mt-0.5">
                  <span>{ratePlan.name}</span>
                  <span>•</span>
                  <span>{nights} night{nights > 1 ? "s" : ""}</span>
                  <span>•</span>
                  <span>{guests} guest{guests > 1 ? "s" : ""}</span>
                </div>
              </div>
            </div>

            <div className="sm:text-right shrink-0">
              <div className="text-xs text-[#0F1B1A]/60">Current Selection</div>
              <div className="text-lg font-bold text-[#0F1B1A] font-serif">
                ${ratePlan.price}
                <span className="text-xs font-normal text-[#0F1B1A]/60 font-sans"> / night</span>
              </div>
              <div className="text-xs text-[#0F1B1A]/70">
                ${ratePlan.price * nights} total
              </div>
            </div>
          </div>

          {/* Upgrade Cards List / Grid */}
          <div className="px-6 sm:px-8 pb-6">
            <div className="text-xs font-semibold text-[#0F1B1A]/50 uppercase tracking-wider mb-3">
              Recommended Upgrades Available
            </div>

            {loading ? (
              <div className="py-12 flex flex-col items-center justify-center gap-3 text-center">
                <div className="w-8 h-8 rounded-full border-2 border-[#E07A3E] border-t-transparent animate-spin" />
                <span className="text-sm text-[#0F1B1A]/70">
                  Checking real-time suite upgrades...
                </span>
              </div>
            ) : upsellOptions.length === 0 ? (
              <div className="p-8 text-center bg-[#F7F6F2] rounded-2xl border border-[#0F1B1A]/8">
                <p className="text-sm text-[#0F1B1A]/70">
                  You already have our best available room option selected for your dates!
                </p>
                <button
                  type="button"
                  onClick={handleProceedWithCurrent}
                  className="mt-4 px-6 py-2.5 rounded-xl bg-[#E07A3E] hover:bg-[#C4622D] text-white font-medium text-sm transition-all"
                >
                  Proceed to Checkout
                </button>
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {upsellOptions.map((option) => {
                  const priceDelta = option.priceDelta ?? 0;
                  const totalDelta = priceDelta * nights;
                  const roomName = option.roomTypeName || option.roomName || "Upgraded Suite";
                  const tier = option.tier ?? 3;
                  const description = option.description || option.shortDescription || "";
                  const keyHighlights = (option.keyFeatures && option.keyFeatures.length > 0)
                    ? option.keyFeatures
                    : (option.amenities && option.amenities.length > 0)
                    ? option.amenities
                    : ["Panoramic View", "Complimentary Refreshments"];

                  return (
                    <div
                      key={option.roomId}
                      className="group relative flex flex-col justify-between rounded-2xl bg-white border border-[#0F1B1A]/10 hover:border-[#E07A3E]/50 shadow-xs hover:shadow-md transition-all duration-200 overflow-hidden"
                    >
                      {/* Image Banner */}
                      <div className="relative aspect-[16/10] w-full overflow-hidden bg-[#0F1B1A]/5">
                        <Image
                          src={option.imageUrl || "/images/slowhouse-interior.jpg"}
                          alt={roomName}
                          fill
                          className="object-cover group-hover:scale-105 transition-transform duration-300"
                          sizes="(max-width: 768px) 100vw, 320px"
                        />
                        <div className="absolute top-2.5 left-2.5">
                          <span className="px-2.5 py-1 rounded-full bg-black/60 backdrop-blur-md text-white text-[11px] font-semibold tracking-wide">
                            {tier >= 4 ? "Premier Villa" : tier >= 3 ? "Panorama Suite" : "Signature Suite"}
                          </span>
                        </div>
                        <div className="absolute bottom-2.5 right-2.5">
                          <span className="px-2.5 py-1 rounded-full bg-emerald-700/90 backdrop-blur-md text-white text-xs font-bold shadow-xs">
                            +${priceDelta} / night
                          </span>
                        </div>
                      </div>

                      {/* Content */}
                      <div className="p-4 flex-1 flex flex-col justify-between">
                        <div>
                          <h4 className="font-serif text-lg font-semibold text-[#0F1B1A] group-hover:text-[#2F5C52] transition-colors line-clamp-1">
                            {roomName}
                          </h4>
                          <p className="text-xs text-[#0F1B1A]/70 line-clamp-2 mt-1 leading-relaxed">
                            {description}
                          </p>

                          {/* Key Highlights */}
                          <ul className="mt-3 space-y-1.5">
                            {keyHighlights.slice(0, 2).map((feat, i) => (
                              <li
                                key={i}
                                className="flex items-start gap-2 text-xs text-[#0F1B1A]/85"
                              >
                                <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600 shrink-0 mt-0.5" />
                                <span className="line-clamp-1">{feat}</span>
                              </li>
                            ))}
                          </ul>
                        </div>

                        {/* Pricing & Actions */}
                        <div className="mt-4 pt-3 border-t border-[#0F1B1A]/8">
                          <div className="flex items-center justify-between mb-3">
                            <div>
                              <div className="text-[11px] text-[#0F1B1A]/60">Upgrade Cost</div>
                              <div className="text-xs font-bold text-emerald-700">
                                +${totalDelta} total ({nights} night{nights > 1 ? "s" : ""})
                              </div>
                            </div>
                            <button
                              type="button"
                              onClick={() => setInspectingOption(option)}
                              className="text-xs font-semibold text-[#2F5C52] hover:text-[#0F1B1A] underline underline-offset-4 hover:no-underline transition-colors"
                            >
                              Learn more
                            </button>
                          </div>

                          <button
                            type="button"
                            onClick={() => handleUpgradeSelection(option)}
                            className="w-full py-2.5 px-3 rounded-xl bg-[#E07A3E] hover:bg-[#C4622D] text-white text-xs font-semibold shadow-xs hover:shadow-sm active:scale-98 transition-all flex items-center justify-center gap-1.5"
                          >
                            <span>Upgrade</span>
                            <ArrowRight className="w-3.5 h-3.5" />
                          </button>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* Modal Footer Controls */}
          <div className="px-6 sm:px-8 py-4 bg-[#F7F6F2] border-t border-[#0F1B1A]/8 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
            <label className="flex items-center gap-2.5 text-xs text-[#0F1B1A]/70 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={suppressUpsell}
                onChange={handleSuppressToggle}
                className="w-4 h-4 rounded text-[#E07A3E] focus:ring-[#E07A3E] border-[#0F1B1A]/20 cursor-pointer"
              />
              <span>Do not show me this again for this reservation</span>
            </label>

            <div className="flex items-center gap-4 w-full sm:w-auto justify-end">
              <button
                type="button"
                onClick={handleProceedWithCurrent}
                className="text-xs font-semibold text-[#0F1B1A]/60 hover:text-[#0F1B1A] underline underline-offset-4 transition-colors"
              >
                No, Thanks — Keep Current Room
              </button>
              <button
                type="button"
                onClick={handleProceedWithCurrent}
                className="px-5 py-2 rounded-xl bg-[#0F1B1A] hover:bg-[#2F5C52] text-white text-xs font-semibold transition-colors"
              >
                Proceed to Checkout
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Room Details Slide-Over Drawer */}
      <RoomDetailsPanel
        isOpen={!!inspectingOption}
        onClose={() => setInspectingOption(null)}
        upsellOption={inspectingOption}
        roomListing={inspectingRoomListing}
        nights={nights}
        onSelectUpgrade={(opt) => {
          setInspectingOption(null);
          handleUpgradeSelection(opt);
        }}
      />
    </>
  );
}
