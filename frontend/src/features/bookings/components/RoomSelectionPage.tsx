"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { publicApi } from "@/services/publicApi";
import type { RoomTypeDto } from "@/types/roomTypes";
import {
AlertCircle,
BedDouble,
CalendarX,
CheckCircle2,
Loader2,
MessageSquare,
Phone,
RefreshCw,
} from "lucide-react";
import Link from "next/link";
import { useRouter,useSearchParams } from "next/navigation";
import { useCallback,useEffect,useMemo,useState } from "react";
import { MOCK_MEAL_PLANS } from "../services/mockBookingData";
import {
CartItem,
MealPlan,
RatePlan,
RoomListing,
RoomVariant,
} from "../types/bookingSelection";
import { BookingSearchBar } from "./BookingSearchBar";
import { MealPlanCarousel } from "./MealPlanCarousel";
import { MemberSignInModal,PendingBookingContext } from "./MemberSignInModal";
import { RoomCard } from "./RoomCard";
import { RoomFilterBar } from "./RoomFilterBar";
import { UPSELL_SUPPRESS_KEY,UpsellModal } from "./UpsellModal";

function mapRoomTypeToListing(type: RoomTypeDto): RoomListing {
  const primaryImg = type.images?.find((i) => i.isPrimary)?.imageUrl;
  const gallery = type.images?.map((i) => i.imageUrl) ?? [];
  const displayImage = primaryImg || gallery[0] || "/images/slowhouse-hero.jpg";

  const basePrice = Number(type.pricePerNight) || 280;
  const allInclusivePrice = basePrice + 150;

  return {
    id: type.id,
    name: type.title || type.name,
    imageUrl: displayImage,
    galleryImages: gallery.length > 0 ? gallery : [displayImage],
    galleryCount: gallery.length > 0 ? gallery.length : 1,
    amenities:
      type.amenities && type.amenities.length > 0
        ? type.amenities
        : [
            "Free High-Speed WiFi",
            "Tea Tasting Station",
            "Rainfall Shower",
            "Air Conditioning",
          ],
    variants: [
      {
        id: `${type.id}-std`,
        label: type.bedType || "1 King Bed",
        bedDescription: type.bedType || "Standard King Bed",
      },
    ],
    bedCount: 1,
    sleeps: type.capacity || 2,
    description:
      type.longDescription && type.longDescription.length > 0
        ? type.longDescription.slice(0, 160) +
          (type.longDescription.length > 160 ? "..." : "")
        : "Luxury Ceylon tea estate residence overlooking misty valley slopes.",
    fullDescription: type.longDescription || type.cancellationPolicyText || "",
    roomSizeSqFt: type.roomSizeSqFt || 450,
    ratePlans: [
      {
        id: `${type.id}-sanctuary`,
        name: "Estate Sanctuary Rate",
        inclusionsLabel: "Includes artisan Ceylon breakfast and estate Wi-Fi",
        bullets: [
          "Flexible cancellation up to 48 hours before check-in",
          "Daily afternoon Ceylon tea tasting included",
        ],
        price: basePrice,
        currency: "USD",
      },
      {
        id: `${type.id}-all-inclusive`,
        name: "All-Inclusive Estate Experience",
        inclusionsLabel: "Breakfast, lunch, tea tasting & 4-course dinner",
        bullets: [
          "Complete estate culinary & tea pairing program",
          "Priority reservations at Sanctuary spa",
        ],
        price: allInclusivePrice,
        currency: "USD",
      },
    ],
    roomType: type,
    images: type.images,
  };
}

export function RoomSelectionPage() {
  const router = useRouter();
  const searchParams = useSearchParams();

  // ── 1. Search Parameters from URL ─────────────────────────────────────────
  const getDefaultCheckIn = () => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return d.toISOString().split("T")[0];
  };

  const getDefaultCheckOut = () => {
    const d = new Date();
    d.setDate(d.getDate() + 3);
    return d.toISOString().split("T")[0];
  };

  const [checkIn, setCheckIn] = useState<string>(
    searchParams.get("checkIn") || getDefaultCheckIn()
  );
  const [checkOut, setCheckOut] = useState<string>(
    searchParams.get("checkOut") || getDefaultCheckOut()
  );

  const parsedGuests = parseInt(searchParams.get("guests") || "2", 10);
  const [adults, setAdults] = useState<number>(Math.max(1, parsedGuests));
  const [childrenCount, setChildrenCount] = useState<number>(0);
  const [rooms, setRooms] = useState<number>(
    parseInt(searchParams.get("rooms") || "1", 10)
  );
  const [promoCode, setPromoCode] = useState<string>(
    searchParams.get("promo") || ""
  );

  // ── 2. Filter & Selection States ──────────────────────────────────────────
  const [mealPlans] = useState<MealPlan[]>(MOCK_MEAL_PLANS);
  const [selectedMealPlanId, setSelectedMealPlanId] = useState<string | null>(
    "meal-all-inclusive"
  );
  const [accessibleOnly, setAccessibleOnly] = useState<boolean>(false);
  const [viewBy, setViewBy] = useState<"rooms" | "suites" | "villas">("rooms");
  const [sortBy, setSortBy] = useState<
    "lowest_price" | "highest_price" | "sleeps" | "rating"
  >("lowest_price");
  const [selectedAmenities, setSelectedAmenities] = useState<string[]>([]);
  const [roomsData, setRoomsData] = useState<RoomListing[]>([]);
  const [roomTypes, setRoomTypes] = useState<RoomTypeDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // ── 3. Auth & Cart States ────────────────────────────────────────────────
  const { isAuthenticated, user, initialize } = useAuthStore();
  const [cart, setCart] = useState<CartItem[]>([]);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Modal state for Member Sign In on Book Now
  const [isSignInModalOpen, setIsSignInModalOpen] = useState(false);
  const [pendingBooking, setPendingBooking] = useState<PendingBookingContext | null>(null);

  // Modal state for Post-Login Upsell Offer
  const [isUpsellModalOpen, setIsUpsellModalOpen] = useState(false);
  const [upsellBookingContext, setUpsellBookingContext] = useState<PendingBookingContext | null>(null);

  useEffect(() => {
    initialize();
  }, [initialize]);

  // Initial fetch on mount: updates state asynchronously on response without cascading sync renders
  useEffect(() => {
    let active = true;

    publicApi
      .getRoomTypes()
      .then((types) => {
        if (!active) return;
        if (types && types.length > 0) {
          setRoomTypes(types);
          const mapped = types.map(mapRoomTypeToListing);
          setRoomsData(mapped);
        } else {
          setRoomTypes([]);
          setRoomsData([]);
        }
        setIsLoading(false);
      })
      .catch((err: unknown) => {
        if (!active) return;
        console.error("[RoomSelectionPage] Failed to load live room availability:", err);
        setLoadError(
          "We couldn't load live availability right now. Please check your connection, try again, or contact our reservations desk directly."
        );
        setRoomTypes([]);
        setRoomsData([]);
        setIsLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  // Manual retry handler triggered by user interaction
  const fetchRooms = useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);

    try {
      const types = await publicApi.getRoomTypes();
      if (types && types.length > 0) {
        setRoomTypes(types);
        const mapped = types.map(mapRoomTypeToListing);
        setRoomsData(mapped);
      } else {
        setRoomTypes([]);
        setRoomsData([]);
      }
    } catch (err: unknown) {
      console.error("[RoomSelectionPage] Failed to load live room availability:", err);
      setLoadError(
        "We couldn't load live availability right now. Please check your connection, try again, or contact our reservations desk directly."
      );
      setRoomTypes([]);
      setRoomsData([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Compute stay night count
  const nights = useMemo(() => {
    try {
      const start = new Date(checkIn).getTime();
      const end = new Date(checkOut).getTime();
      const diff = Math.ceil((end - start) / (1000 * 60 * 60 * 24));
      return diff > 0 ? diff : 2;
    } catch {
      return 2;
    }
  }, [checkIn, checkOut]);

  // Aggregate all unique amenities for filter menu
  const allAmenities = useMemo(() => {
    const set = new Set<string>();
    roomsData.forEach((r) => r.amenities.forEach((a) => set.add(a)));
    return Array.from(set);
  }, [roomsData]);

  // ── Update URL when search changes ────────────────────────────────────────
  const handleUpdateParams = (updates: {
    checkIn?: string;
    checkOut?: string;
    adults?: number;
    childrenCount?: number;
    rooms?: number;
    promoCode?: string;
  }) => {
    if (updates.checkIn !== undefined) setCheckIn(updates.checkIn);
    if (updates.checkOut !== undefined) setCheckOut(updates.checkOut);
    if (updates.adults !== undefined) setAdults(updates.adults);
    if (updates.childrenCount !== undefined) setChildrenCount(updates.childrenCount);
    if (updates.rooms !== undefined) setRooms(updates.rooms);
    if (updates.promoCode !== undefined) setPromoCode(updates.promoCode);

    const newCheckIn = updates.checkIn ?? checkIn;
    const newCheckOut = updates.checkOut ?? checkOut;
    const totalGuests = (updates.adults ?? adults) + (updates.childrenCount ?? childrenCount);
    const newRooms = updates.rooms ?? rooms;
    const newPromo = updates.promoCode ?? promoCode;

    const params = new URLSearchParams();
    params.set("checkIn", newCheckIn);
    params.set("checkOut", newCheckOut);
    params.set("guests", totalGuests.toString());
    params.set("rooms", newRooms.toString());
    if (newPromo) params.set("promo", newPromo);

    router.replace(`/rooms?${params.toString()}`, { scroll: false });
  };

  // ── Proceed to Checkout Helper ────────────────────────────────────────────
  const proceedToCheckout = (pending: PendingBookingContext) => {
    const query = new URLSearchParams({
      roomId: pending.room.id,
      roomName: pending.room.name,
      roomTypeId: pending.room.id,
      variantId: pending.variant.id,
      variantLabel: pending.variant.label,
      ratePlanId: pending.ratePlan.id,
      ratePlanName: pending.ratePlan.name,
      checkIn: pending.checkIn,
      checkOut: pending.checkOut,
      guests: pending.guests.toString(),
      rooms: pending.rooms.toString(),
      price: pending.ratePlan.price.toString(),
      nights: nights.toString(),
    });
    if (pending.promoCode) query.set("promo", pending.promoCode);

    router.push(`/booking/checkout?${query.toString()}`);
  };

  // Check suppression and trigger UpsellModal or direct Checkout
  const handleTriggerUpsellOrCheckout = (pending: PendingBookingContext) => {
    const isSuppressed =
      typeof window !== "undefined" &&
      sessionStorage.getItem(UPSELL_SUPPRESS_KEY) === "true";

    if (isSuppressed) {
      proceedToCheckout(pending);
    } else {
      setUpsellBookingContext(pending);
      setIsUpsellModalOpen(true);
    }
  };

  // ── Add to Cart / Book Rate Plan (Opens Sign In Modal if not authenticated) ──
  const handleBookRatePlan = (
    room: RoomListing,
    variant: RoomVariant,
    ratePlan: RatePlan
  ) => {
    const newItem: CartItem = {
      id: `cart-${room.id}-${variant.id}-${ratePlan.id}-${Date.now()}`,
      roomId: room.id,
      roomName: room.name,
      variantId: variant.id,
      variantLabel: variant.label,
      ratePlanId: ratePlan.id,
      ratePlanName: ratePlan.name,
      price: ratePlan.price,
      currency: ratePlan.currency,
      nights,
      checkIn,
      checkOut,
      guests: adults + childrenCount,
      rooms,
    };

    setCart((prev) => [...prev, newItem]);
    setToastMessage(`Selected "${room.name} (${variant.label})"`);
    setTimeout(() => setToastMessage(null), 4000);

    const pendingContext: PendingBookingContext = {
      room,
      variant,
      ratePlan,
      checkIn,
      checkOut,
      guests: adults + childrenCount,
      rooms,
      promoCode,
    };

    // If authenticated, trigger upsell modal (or direct checkout if suppressed)
    if (isAuthenticated && user) {
      handleTriggerUpsellOrCheckout(pendingContext);
      return;
    }

    // If not authenticated, open Member Sign In modal
    setPendingBooking(pendingContext);
    setIsSignInModalOpen(true);
  };

  const handleSignInSuccess = (pending: PendingBookingContext | null) => {
    setIsSignInModalOpen(false);
    if (pending) {
      handleTriggerUpsellOrCheckout(pending);
    }
  };

  const handleRemoveCartItem = (id: string) => {
    setCart((prev) => prev.filter((item) => item.id !== id));
  };

  // ── Checkout / Confirm Booking from Cart ───────────────────────────────────
  const handleCheckout = () => {
    if (cart.length === 0) return;
    const primary = cart[cart.length - 1];
    const pending: PendingBookingContext = {
      room: roomsData.find((r) => r.id === primary.roomId) || roomsData[0],
      variant: { id: primary.variantId, label: primary.variantLabel },
      ratePlan: {
        id: primary.ratePlanId,
        name: primary.ratePlanName,
        inclusionsLabel: "Included",
        bullets: [],
        price: primary.price,
        currency: primary.currency,
      },
      checkIn: primary.checkIn,
      checkOut: primary.checkOut,
      guests: primary.guests,
      rooms: primary.rooms,
      promoCode,
    };

    if (isAuthenticated && user) {
      handleTriggerUpsellOrCheckout(pending);
    } else {
      setPendingBooking(pending);
      setIsSignInModalOpen(true);
    }
  };

  // ── Filter & Sort Rooms ───────────────────────────────────────────────────
  const filteredRooms = useMemo(() => {
    let result = [...roomsData];

    // Filter by viewBy
    if (viewBy === "suites") {
      result = result.filter(
        (r) => r.name.toLowerCase().includes("suite")
      );
    } else if (viewBy === "villas") {
      result = result.filter(
        (r) => r.name.toLowerCase().includes("villa")
      );
    }

    // Filter by amenities
    if (selectedAmenities.length > 0) {
      result = result.filter((r) =>
        selectedAmenities.every((amenity) => r.amenities.includes(amenity))
      );
    }

    // Sort
    result.sort((a, b) => {
      const getMinPrice = (room: RoomListing) =>
        Math.min(...room.ratePlans.map((rp) => rp.price));

      if (sortBy === "lowest_price") {
        return getMinPrice(a) - getMinPrice(b);
      }
      if (sortBy === "highest_price") {
        return getMinPrice(b) - getMinPrice(a);
      }
      if (sortBy === "sleeps") {
        return b.sleeps - a.sleeps;
      }
      return 0; // Default rating / order
    });

    return result;
  }, [roomsData, viewBy, selectedAmenities, sortBy]);

  const formatDateDisplay = (iso: string) => {
    if (!iso) return "";
    try {
      const [y, m, d] = iso.split("-").map(Number);
      return new Date(y, m - 1, d).toLocaleDateString("en-US", {
        month: "short",
        day: "numeric",
      });
    } catch {
      return iso;
    }
  };

  return (
    <div className="min-h-screen bg-[#0F1B1A] text-[#F6F1E6]">
      {/* ─────────────────────────────────────────────────────────────
          HERO BANNER: Maskeliya Sanctuary Retreat Context
      ───────────────────────────────────────────────────────────── */}
      <section className="relative h-64 sm:h-72 flex items-end overflow-hidden">
        <div
          className="absolute inset-0 bg-cover bg-center"
          style={{ backgroundImage: "url('/images/hero-view.jpg')" }}
        />
        <div className="absolute inset-0 bg-gradient-to-t from-[#0F1B1A] via-[#0F1B1A]/60 to-transparent" />
        <div className="relative z-10 max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pb-8 w-full">
          <span className="text-[#C4622D] text-[11px] font-semibold tracking-[0.25em] uppercase block mb-1">
            OTA Booking Engine · SmartHotel Maskeliya
          </span>
          <h1 className="font-serif font-bold text-3xl sm:text-4xl lg:text-5xl text-[#F6F1E6]">
            Choose Your Sanctuary Residence
          </h1>
          <p className="text-xs sm:text-sm text-[#7C9188] mt-2 flex flex-wrap items-center gap-2">
            <span>{nights} night{nights > 1 ? "s" : ""} stay</span>
            <span>·</span>
            <span>{adults + childrenCount} guest{adults + childrenCount > 1 ? "s" : ""}</span>
            <span>·</span>
            <span>{rooms} room{rooms > 1 ? "s" : ""}</span>
            {checkIn && checkOut && (
              <>
                <span>·</span>
                <span className="text-[#F6F1E6]">
                  {formatDateDisplay(checkIn)} — {formatDateDisplay(checkOut)}
                </span>
              </>
            )}
            {promoCode && (
              <span className="text-[#C4622D] font-medium bg-[#C4622D]/10 px-2 py-0.5 rounded-full border border-[#C4622D]/30 ml-1">
                Promo: {promoCode}
              </span>
            )}
          </p>
        </div>
      </section>

      {/* ─────────────────────────────────────────────────────────────
          1. STICKY BOOKING SEARCH BAR
      ───────────────────────────────────────────────────────────── */}
      <BookingSearchBar
        checkIn={checkIn}
        checkOut={checkOut}
        adults={adults}
        childrenCount={childrenCount}
        rooms={rooms}
        promoCode={promoCode}
        cart={cart}
        onUpdateParams={handleUpdateParams}
        onRemoveCartItem={handleRemoveCartItem}
        onCheckout={handleCheckout}
      />

      {/* ─────────────────────────────────────────────────────────────
          MAIN CONTENT AREA
      ───────────────────────────────────────────────────────────── */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 sm:py-12">
        {/* 2a. Select a Room — Available Meal Plans carousel */}
        <MealPlanCarousel
          mealPlans={mealPlans}
          selectedPlanId={selectedMealPlanId}
          onSelectPlan={(id) => setSelectedMealPlanId(id)}
        />

        {/* 2b. Filters & sort bar */}
        <RoomFilterBar
          accessibleOnly={accessibleOnly}
          onToggleAccessible={() => setAccessibleOnly((prev) => !prev)}
          viewBy={viewBy}
          onChangeViewBy={setViewBy}
          sortBy={sortBy}
          onChangeSortBy={setSortBy}
          selectedAmenities={selectedAmenities}
          onToggleAmenity={(amenity) =>
            setSelectedAmenities((prev) =>
              prev.includes(amenity)
                ? prev.filter((a) => a !== amenity)
                : [...prev, amenity]
            )
          }
          allAmenities={allAmenities}
        />

        {/* 2c. Room card listing */}
        <div className="space-y-8">
          {/* State 1: Active Loading State */}
          {isLoading && (
            <div className="py-20 text-center bg-[#16302C]/40 border border-[#2F5C52]/30 rounded-3xl p-10 flex flex-col items-center justify-center">
              <Loader2 className="w-8 h-8 text-[#C4622D] animate-spin mb-3" />
              <h3 className="font-serif text-lg text-[#F6F1E6] font-medium">
                Checking residence availability...
              </h3>
              <p className="text-xs text-[#7C9188] mt-1 max-w-sm">
                Retrieving real-time room types, rates, and imagery for your stay.
              </p>
            </div>
          )}

          {/* State 2: Genuine API Error (Network / 500 / Timeout) */}
          {!isLoading && loadError && (
            <div className="py-16 text-center bg-[#16302C]/60 border border-rose-500/30 rounded-3xl p-8 max-w-2xl mx-auto shadow-2xl">
              <AlertCircle className="w-10 h-10 text-rose-400 mx-auto mb-3" />
              <h3 className="font-serif text-xl text-[#F6F1E6] font-semibold">
                Unable to Load Availability
              </h3>
              <p className="text-xs sm:text-sm text-[#7C9188] mt-2 leading-relaxed max-w-md mx-auto">
                {loadError}
              </p>
              <div className="flex flex-wrap items-center justify-center gap-3 mt-6">
                <button
                  type="button"
                  onClick={() => fetchRooms()}
                  className="inline-flex items-center gap-2 px-5 py-2.5 bg-[#C4622D] hover:bg-[#A84E1F] text-white text-xs font-semibold rounded-xl transition-all shadow-lg cursor-pointer"
                >
                  <RefreshCw className="w-4 h-4" /> Try Again
                </button>
                <Link
                  href="/contact"
                  className="inline-flex items-center gap-2 px-5 py-2.5 bg-[#16302C] hover:bg-[#2F5C52] border border-[#2F5C52] text-xs font-semibold rounded-xl text-[#F6F1E6] transition-colors"
                >
                  <Phone className="w-4 h-4 text-emerald-400" /> Contact Concierge
                </Link>
              </div>
            </div>
          )}

          {/* State 3: Legitimate Empty Response (API returned [] with no error) */}
          {!isLoading && !loadError && roomsData.length === 0 && (
            <div className="py-20 text-center bg-[#16302C]/40 border border-[#2F5C52]/30 rounded-3xl p-10 max-w-2xl mx-auto">
              <CalendarX className="w-10 h-10 text-[#7C9188] mx-auto mb-3" />
              <h3 className="font-serif text-xl text-[#F6F1E6] font-semibold">
                No rooms currently available for these dates
              </h3>
              <p className="text-xs sm:text-sm text-[#7C9188] mt-2 max-w-md mx-auto leading-relaxed">
                All residences are committed for your selected stay dates. Please try adjusting your arrival or departure dates, or contact our reservations desk for waitlist options.
              </p>
              <div className="flex flex-wrap items-center justify-center gap-3 mt-6">
                <button
                  type="button"
                  onClick={() => {
                    window.scrollTo({ top: 150, behavior: "smooth" });
                  }}
                  className="inline-flex items-center gap-2 px-5 py-2.5 bg-[#2F5C52] hover:bg-[#16302C] border border-[#2F5C52] text-xs font-semibold rounded-xl text-white transition-colors cursor-pointer"
                >
                  Adjust Stay Dates
                </button>
                <Link
                  href="/contact"
                  className="inline-flex items-center gap-2 px-5 py-2.5 bg-[#16302C] hover:bg-[#2F5C52] border border-[#2F5C52] text-xs font-semibold rounded-xl text-[#F6F1E6] transition-colors"
                >
                  <Phone className="w-4 h-4 text-emerald-400" /> Inquire for Waitlist
                </Link>
              </div>
            </div>
          )}

          {/* State 4: Available Room Cards */}
          {!isLoading &&
            !loadError &&
            filteredRooms.length > 0 &&
            filteredRooms.map((room) => {
              const matchedType =
                roomTypes.find(
                  (t) =>
                    t.id === room.id ||
                    t.name.toLowerCase() === room.name.toLowerCase()
                ) ?? room.roomType;

              return (
                <RoomCard
                  key={room.id}
                  room={room}
                  roomType={matchedType}
                  nights={nights}
                  checkIn={checkIn}
                  checkOut={checkOut}
                  guests={adults + childrenCount}
                  roomsCount={rooms}
                  onBookRatePlan={handleBookRatePlan}
                />
              );
            })}

          {/* State 5: Filter Mismatch (Residences exist, but none match amenity filters) */}
          {!isLoading &&
            !loadError &&
            roomsData.length > 0 &&
            filteredRooms.length === 0 && (
              <div className="py-20 text-center bg-[#16302C]/40 border border-[#2F5C52]/30 rounded-3xl p-8">
                <BedDouble className="w-12 h-12 text-[#2F5C52] mx-auto mb-3" />
                <h3 className="font-serif text-lg text-[#F6F1E6] font-semibold">
                  No residences match your filter criteria
                </h3>
                <p className="text-xs text-[#7C9188] mt-1 max-w-md mx-auto">
                  Try clearing active amenity filters or switching the residence view to see all available room types.
                </p>
                <button
                  type="button"
                  onClick={() => {
                    setSelectedAmenities([]);
                    setViewBy("rooms");
                    setAccessibleOnly(false);
                  }}
                  className="mt-4 px-4 py-2 bg-[#2F5C52] hover:bg-[#16302C] border border-[#2F5C52] text-xs font-semibold rounded-xl text-white transition-colors cursor-pointer"
                >
                  Reset All Filters
                </button>
              </div>
            )}
        </div>
      </main>

      {/* ─────────────────────────────────────────────────────────────
          TOAST NOTIFICATION (Appears when Book Now is clicked)
      ───────────────────────────────────────────────────────────── */}
      {toastMessage && (
        <div className="fixed bottom-6 right-6 z-50 bg-[#16302C] border border-[#C4622D] text-[#F6F1E6] px-5 py-3 rounded-2xl shadow-2xl flex items-center gap-3 animate-in fade-in slide-in-from-bottom-5 duration-300">
          <CheckCircle2 className="w-5 h-5 text-[#C4622D] shrink-0" />
          <div>
            <div className="text-xs font-semibold">{toastMessage}</div>
            <div className="text-[11px] text-[#7C9188]">
              Ready in your cart for checkout.
            </div>
          </div>
          <button
            type="button"
            onClick={() => setToastMessage(null)}
            className="text-[#7C9188] hover:text-white ml-2 text-xs"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* ─────────────────────────────────────────────────────────────
          FOOTER CONCIERGE ASSISTANCE
      ───────────────────────────────────────────────────────────── */}
      <section className="border-t border-[#2F5C52]/30 bg-[#0a1412] py-12 px-4 sm:px-6 lg:px-8 text-center mt-16">
        <div className="max-w-3xl mx-auto space-y-4">
          <span className="text-[10px] font-semibold uppercase tracking-widest text-[#7C9188]">
            Maskeliya Guest Relations
          </span>
          <h2 className="font-serif font-bold text-2xl text-[#F6F1E6]">
            Need personal guidance selecting your sanctuary?
          </h2>
          <p className="text-xs text-[#7C9188] max-w-xl mx-auto leading-relaxed">
            Our resident concierge team coordinates private helicopter transfers, custom dietary requests, and private Adams Peak pilgrimage guides.
          </p>
          <div className="flex flex-wrap items-center justify-center gap-4 pt-2">
            <Link
              href="/contact"
              className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl border border-[#2F5C52] hover:border-[#C4622D] text-xs font-semibold text-[#F6F1E6] hover:text-[#C4622D] transition-colors"
            >
              <MessageSquare className="w-3.5 h-3.5" />
              <span>Contact Concierge</span>
            </Link>
            <a
              href="tel:+94522223456"
              className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[#2F5C52]/30 hover:bg-[#2F5C52]/50 text-xs font-semibold text-[#EEE7D6] transition-colors"
            >
              <Phone className="w-3.5 h-3.5" />
              <span>+94 52 222 3456</span>
            </a>
          </div>
        </div>
      </section>

      {/* ─────────────────────────────────────────────────────────────
          MEMBER SIGN IN MODAL (Triggered on Book Now)
      ───────────────────────────────────────────────────────────── */}
      <MemberSignInModal
        isOpen={isSignInModalOpen}
        onClose={() => setIsSignInModalOpen(false)}
        pendingBooking={pendingBooking}
        onSuccess={handleSignInSuccess}
      />

      {/* ─────────────────────────────────────────────────────────────
          POST-LOGIN UPSELL MODAL (Upgrade offers before checkout)
      ───────────────────────────────────────────────────────────── */}
      {upsellBookingContext && (
        <UpsellModal
          isOpen={isUpsellModalOpen}
          onClose={() => setIsUpsellModalOpen(false)}
          room={upsellBookingContext.room}
          variant={upsellBookingContext.variant}
          ratePlan={upsellBookingContext.ratePlan}
          checkIn={upsellBookingContext.checkIn}
          checkOut={upsellBookingContext.checkOut}
          guests={upsellBookingContext.guests}
          roomsCount={upsellBookingContext.rooms}
          nights={nights}
          promoCode={upsellBookingContext.promoCode}
        />
      )}
    </div>
  );
}
