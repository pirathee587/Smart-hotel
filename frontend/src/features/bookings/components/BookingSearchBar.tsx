"use client";

import {
ArrowRight,
Calendar,
ChevronDown,
ChevronUp,
ShoppingCart,
Sparkles,
Tag,
Trash2,
Users,
X,
} from "lucide-react";
import React,{ useEffect,useRef,useState } from "react";
import { CartItem } from "../types/bookingSelection";

interface BookingSearchBarProps {
  checkIn: string;
  checkOut: string;
  adults: number;
  childrenCount: number;
  rooms: number;
  promoCode: string;
  cart: CartItem[];
  onUpdateParams: (updates: {
    checkIn?: string;
    checkOut?: string;
    adults?: number;
    childrenCount?: number;
    rooms?: number;
    promoCode?: string;
  }) => void;
  onRemoveCartItem: (id: string) => void;
  onCheckout: () => void;
}

export function BookingSearchBar({
  checkIn,
  checkOut,
  adults,
  childrenCount,
  rooms,
  promoCode,
  cart,
  onUpdateParams,
  onRemoveCartItem,
  onCheckout,
}: BookingSearchBarProps) {
  const [guestsOpen, setGuestsOpen] = useState(false);
  const [specialCodesOpen, setSpecialCodesOpen] = useState(false);
  const [cartOpen, setCartOpen] = useState(false);
  const [localPromo, setLocalPromo] = useState(promoCode);
  const [promoApplied, setPromoApplied] = useState(Boolean(promoCode));

  const guestsRef = useRef<HTMLDivElement>(null);
  const cartRef = useRef<HTMLDivElement>(null);

  const totalGuests = adults + childrenCount;

  // Calculate cart total
  const cartTotal = cart.reduce((sum, item) => sum + item.price * item.nights, 0);

  // Close popovers on click outside
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (guestsRef.current && !guestsRef.current.contains(e.target as Node)) {
        setGuestsOpen(false);
      }
      if (cartRef.current && !cartRef.current.contains(e.target as Node)) {
        setCartOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleApplyPromo = (e: React.FormEvent) => {
    e.preventDefault();
    onUpdateParams({ promoCode: localPromo.trim() });
    setPromoApplied(Boolean(localPromo.trim()));
  };

  return (
    <section className="sticky top-20 z-40 bg-[#0F1B1A]/95 backdrop-blur-md border-b border-[#2F5C52]/30 text-[#F6F1E6] shadow-xl">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-3.5 sm:py-4">
        <div className="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-4">
          {/* Main Search Controls */}
          <div className="w-full lg:flex-1">
            {/* Field Row: Guests, Check-in, Check-out */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-2.5 sm:gap-3 bg-[#16302C]/90 p-2 sm:p-2.5 rounded-2xl border border-[#2F5C52]/50 shadow-inner">
              {/* Field 1: Guests Stepper Popover */}
              <div className="relative" ref={guestsRef}>
                <label className="block text-[10px] font-semibold uppercase tracking-wider text-[#7C9188] px-3 pt-1">
                  Guests &amp; Rooms
                </label>
                <button
                  type="button"
                  suppressHydrationWarning
                  onClick={() => setGuestsOpen((prev) => !prev)}
                  className="w-full flex items-center justify-between px-3 py-2 text-left hover:bg-[#2F5C52]/20 rounded-xl transition-colors cursor-pointer group"
                  aria-expanded={guestsOpen}
                  aria-label="Select guests and rooms"
                >
                  <div className="flex items-center gap-2.5 min-w-0">
                    <Users className="w-4 h-4 text-[#2F5C52] group-hover:text-[#E07A3E] transition-colors shrink-0" />
                    <span className="text-xs sm:text-sm font-medium text-[#F6F1E6] truncate">
                      {totalGuests} guest{totalGuests > 1 ? "s" : ""}, {rooms} room{rooms > 1 ? "s" : ""}
                    </span>
                  </div>
                  <ChevronDown
                    className={`w-4 h-4 text-[#7C9188] transition-transform duration-200 ${
                      guestsOpen ? "rotate-180 text-[#E07A3E]" : ""
                    }`}
                  />
                </button>

                {/* Guests Popover */}
                {guestsOpen && (
                  <div className="absolute top-full mt-2 left-0 w-72 sm:w-80 bg-[#16302C] border border-[#2F5C52] rounded-2xl shadow-2xl p-4 z-50 animate-in fade-in zoom-in-95 duration-150">
                    <div className="flex items-center justify-between pb-3 border-b border-[#2F5C52]/40">
                      <span className="text-xs font-serif font-semibold text-[#F6F1E6]">
                        Occupancy Details
                      </span>
                      <button
                        type="button"
                        onClick={() => setGuestsOpen(false)}
                        className="text-[#7C9188] hover:text-white p-1 rounded-md"
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>

                    <div className="py-3 space-y-3.5">
                      {/* Adults */}
                      <div className="flex items-center justify-between">
                        <div>
                          <div className="text-xs font-medium text-[#F6F1E6]">Adults</div>
                          <div className="text-[10px] text-[#7C9188]">Ages 13 and above</div>
                        </div>
                        <div className="flex items-center gap-2">
                          <button
                            type="button"
                            disabled={adults <= 1}
                            onClick={() => onUpdateParams({ adults: Math.max(1, adults - 1) })}
                            className="w-7 h-7 rounded-lg border border-[#2F5C52] text-sm text-[#F6F1E6] hover:bg-[#2F5C52]/40 disabled:opacity-30 disabled:hover:bg-transparent transition-colors flex items-center justify-center cursor-pointer"
                          >
                            -
                          </button>
                          <span className="w-6 text-center text-xs font-semibold text-[#F6F1E6]">
                            {adults}
                          </span>
                          <button
                            type="button"
                            disabled={adults >= 8}
                            onClick={() => onUpdateParams({ adults: Math.min(8, adults + 1) })}
                            className="w-7 h-7 rounded-lg border border-[#2F5C52] text-sm text-[#F6F1E6] hover:bg-[#2F5C52]/40 disabled:opacity-30 disabled:hover:bg-transparent transition-colors flex items-center justify-center cursor-pointer"
                          >
                            +
                          </button>
                        </div>
                      </div>

                      {/* Children */}
                      <div className="flex items-center justify-between">
                        <div>
                          <div className="text-xs font-medium text-[#F6F1E6]">Children</div>
                          <div className="text-[10px] text-[#7C9188]">Ages 0 to 12</div>
                        </div>
                        <div className="flex items-center gap-2">
                          <button
                            type="button"
                            disabled={childrenCount <= 0}
                            onClick={() => onUpdateParams({ childrenCount: Math.max(0, childrenCount - 1) })}
                            className="w-7 h-7 rounded-lg border border-[#2F5C52] text-sm text-[#F6F1E6] hover:bg-[#2F5C52]/40 disabled:opacity-30 disabled:hover:bg-transparent transition-colors flex items-center justify-center cursor-pointer"
                          >
                            -
                          </button>
                          <span className="w-6 text-center text-xs font-semibold text-[#F6F1E6]">
                            {childrenCount}
                          </span>
                          <button
                            type="button"
                            disabled={childrenCount >= 6}
                            onClick={() => onUpdateParams({ childrenCount: Math.min(6, childrenCount + 1) })}
                            className="w-7 h-7 rounded-lg border border-[#2F5C52] text-sm text-[#F6F1E6] hover:bg-[#2F5C52]/40 disabled:opacity-30 disabled:hover:bg-transparent transition-colors flex items-center justify-center cursor-pointer"
                          >
                            +
                          </button>
                        </div>
                      </div>

                      {/* Rooms */}
                      <div className="flex items-center justify-between">
                        <div>
                          <div className="text-xs font-medium text-[#F6F1E6]">Rooms</div>
                          <div className="text-[10px] text-[#7C9188]">Number of suites</div>
                        </div>
                        <div className="flex items-center gap-2">
                          <button
                            type="button"
                            disabled={rooms <= 1}
                            onClick={() => onUpdateParams({ rooms: Math.max(1, rooms - 1) })}
                            className="w-7 h-7 rounded-lg border border-[#2F5C52] text-sm text-[#F6F1E6] hover:bg-[#2F5C52]/40 disabled:opacity-30 disabled:hover:bg-transparent transition-colors flex items-center justify-center cursor-pointer"
                          >
                            -
                          </button>
                          <span className="w-6 text-center text-xs font-semibold text-[#F6F1E6]">
                            {rooms}
                          </span>
                          <button
                            type="button"
                            disabled={rooms >= 4}
                            onClick={() => onUpdateParams({ rooms: Math.min(4, rooms + 1) })}
                            className="w-7 h-7 rounded-lg border border-[#2F5C52] text-sm text-[#F6F1E6] hover:bg-[#2F5C52]/40 disabled:opacity-30 disabled:hover:bg-transparent transition-colors flex items-center justify-center cursor-pointer"
                          >
                            +
                          </button>
                        </div>
                      </div>
                    </div>

                    <div className="pt-3 border-t border-[#2F5C52]/40 flex justify-end">
                      <button
                        type="button"
                        onClick={() => setGuestsOpen(false)}
                        className="px-4 py-1.5 bg-[#2F5C52] hover:bg-[#2F5C52]/80 text-[#F6F1E6] rounded-xl text-xs font-semibold tracking-wide transition-colors cursor-pointer"
                      >
                        Apply
                      </button>
                    </div>
                  </div>
                )}
              </div>

              {/* Field 2: Check-in */}
              <div className="relative">
                <label className="block text-[10px] font-semibold uppercase tracking-wider text-[#7C9188] px-3 pt-1">
                  Check-in
                </label>
                <div className="flex items-center gap-2.5 px-3 py-2 bg-transparent hover:bg-[#2F5C52]/20 rounded-xl transition-colors">
                  <Calendar className="w-4 h-4 text-[#2F5C52] shrink-0" />
                  <input
                    type="date"
                    value={checkIn}
                    min={new Date().toISOString().split("T")[0]}
                    onChange={(e) => onUpdateParams({ checkIn: e.target.value })}
                    className="w-full bg-transparent text-xs sm:text-sm font-medium text-[#F6F1E6] focus:outline-none cursor-pointer scheme-dark"
                  />
                </div>
              </div>

              {/* Field 3: Check-out */}
              <div className="relative">
                <label className="block text-[10px] font-semibold uppercase tracking-wider text-[#7C9188] px-3 pt-1">
                  Check-out
                </label>
                <div className="flex items-center gap-2.5 px-3 py-2 bg-transparent hover:bg-[#2F5C52]/20 rounded-xl transition-colors">
                  <Calendar className="w-4 h-4 text-[#2F5C52] shrink-0" />
                  <input
                    type="date"
                    value={checkOut}
                    min={checkIn || new Date().toISOString().split("T")[0]}
                    onChange={(e) => onUpdateParams({ checkOut: e.target.value })}
                    className="w-full bg-transparent text-xs sm:text-sm font-medium text-[#F6F1E6] focus:outline-none cursor-pointer scheme-dark"
                  />
                </div>
              </div>
            </div>

            {/* Below the field row, right-aligned: Special Codes or Rates */}
            <div className="mt-2 flex items-center justify-end">
              <button
                type="button"
                suppressHydrationWarning
                onClick={() => setSpecialCodesOpen((prev) => !prev)}
                className="inline-flex items-center gap-1.5 text-xs font-medium text-[#7C9188] hover:text-[#E07A3E] transition-colors cursor-pointer"
              >
                <Tag className="w-3.5 h-3.5 text-[#2F5C52]" />
                <span className="underline underline-offset-4 decoration-[#2F5C52]/60 hover:decoration-[#E07A3E]">
                  Special Codes or Rates
                </span>
                {specialCodesOpen ? (
                  <ChevronUp className="w-3 h-3" />
                ) : (
                  <ChevronDown className="w-3 h-3" />
                )}
              </button>
            </div>

            {/* Expandable Special Codes Input */}
            {specialCodesOpen && (
              <form
                onSubmit={handleApplyPromo}
                className="mt-2.5 p-3 bg-[#16302C]/70 border border-[#2F5C52]/40 rounded-xl flex flex-col sm:flex-row items-stretch sm:items-center justify-end gap-2.5 animate-in fade-in slide-in-from-top-2 duration-150"
              >
                <span className="text-[11px] text-[#7C9188]">
                  Promo Code / Corporate ID:
                </span>
                <div className="flex items-center gap-2">
                  <input
                    type="text"
                    placeholder="Enter code (e.g. MASKELIYA26)"
                    value={localPromo}
                    onChange={(e) => setLocalPromo(e.target.value)}
                    className="bg-[#0F1B1A] border border-[#2F5C52]/60 rounded-lg px-3 py-1.5 text-xs text-[#F6F1E6] placeholder-[#7C9188]/60 focus:outline-none focus:border-[#E07A3E] uppercase tracking-wider"
                  />
                  <button
                    type="submit"
                    className="px-3 py-1.5 bg-[#2F5C52] hover:bg-[#E07A3E] text-white rounded-lg text-xs font-medium transition-colors cursor-pointer"
                  >
                    Apply
                  </button>
                </div>
                {promoApplied && (
                  <span className="inline-flex items-center gap-1 text-[11px] text-[#C4622D] font-semibold">
                    <Sparkles className="w-3 h-3" /> Code Active
                  </span>
                )}
              </form>
            )}
          </div>

          {/* Separate floating card, top-right: "Your Cart: N Items" summary */}
          <div className="relative shrink-0 hidden md:block" ref={cartRef}>
            <button
              type="button"
              suppressHydrationWarning
              onClick={() => setCartOpen((prev) => !prev)}
              className="flex items-center gap-3 bg-[#16302C] hover:bg-[#16302C]/90 border border-[#2F5C52] rounded-2xl px-4 py-2.5 shadow-lg transition-all hover:border-[#C4622D]/60 cursor-pointer text-left"
              aria-expanded={cartOpen}
              aria-label="View booking cart"
            >
              <div className="relative flex items-center justify-center w-10 h-10 rounded-xl bg-[#2F5C52]/30 text-[#C4622D]">
                <ShoppingCart className="w-5 h-5" />
                {cart.length > 0 && (
                  <span className="absolute -top-1.5 -right-1.5 w-5 h-5 rounded-full bg-[#C4622D] text-white text-[10px] font-bold flex items-center justify-center animate-pulse">
                    {cart.length}
                  </span>
                )}
              </div>
              <div>
                <div className="text-[11px] font-medium uppercase tracking-wider text-[#7C9188]">
                  Your Cart
                </div>
                <div className="text-xs sm:text-sm font-semibold text-[#F6F1E6] flex items-center gap-1.5">
                  <span>
                    {cart.length === 0
                      ? "0 Items"
                      : `${cart.length} Item${cart.length > 1 ? "s" : ""}`}
                  </span>
                  {cart.length > 0 && (
                    <span className="text-[#C4622D] font-serif font-bold">
                      · ${cartTotal.toLocaleString()}
                    </span>
                  )}
                </div>
              </div>
              <ChevronDown
                className={`w-4 h-4 text-[#7C9188] ml-1 transition-transform duration-200 ${
                  cartOpen ? "rotate-180 text-[#C4622D]" : ""
                }`}
              />
            </button>

            {/* Desktop Cart Dropdown Preview */}
            {cartOpen && (
              <div className="absolute right-0 top-full mt-2 w-88 bg-[#16302C] border border-[#2F5C52] rounded-2xl shadow-2xl p-4 z-50 animate-in fade-in zoom-in-95 duration-150">
                <div className="flex items-center justify-between pb-3 border-b border-[#2F5C52]/40">
                  <div className="flex items-center gap-2">
                    <ShoppingCart className="w-4 h-4 text-[#C4622D]" />
                    <span className="font-serif text-sm font-semibold text-[#F6F1E6]">
                      Selected Stays ({cart.length})
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() => setCartOpen(false)}
                    className="text-[#7C9188] hover:text-white p-1"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>

                {cart.length === 0 ? (
                  <div className="py-8 text-center text-[#7C9188] text-xs">
                    <p>Your booking cart is empty.</p>
                    <p className="mt-1 text-[11px]">
                      Select a room and rate plan below to get started.
                    </p>
                  </div>
                ) : (
                  <div className="py-3 space-y-3 max-h-72 overflow-y-auto">
                    {cart.map((item) => (
                      <div
                        key={item.id}
                        className="p-3 bg-[#0F1B1A]/80 border border-[#2F5C52]/30 rounded-xl space-y-1.5 relative group"
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div>
                            <h4 className="font-serif text-xs font-bold text-[#F6F1E6]">
                              {item.roomName}
                            </h4>
                            <p className="text-[10px] text-[#7C9188]">
                              {item.variantLabel}
                            </p>
                          </div>
                          <button
                            type="button"
                            onClick={() => onRemoveCartItem(item.id)}
                            className="text-[#7C9188] hover:text-rose-400 p-1 transition-colors"
                            title="Remove stay"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        </div>
                        <div className="text-[11px] text-[#2F5C52] font-medium truncate">
                          {item.ratePlanName}
                        </div>
                        <div className="flex items-center justify-between pt-1 text-[11px] text-[#7C9188]">
                          <span>
                            {item.nights} night{item.nights > 1 ? "s" : ""}
                          </span>
                          <span className="font-serif font-bold text-[#C4622D]">
                            ${(item.price * item.nights).toLocaleString()}{" "}
                            <span className="text-[9px] font-sans text-[#7C9188]">
                              {item.currency}
                            </span>
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                )}

                {cart.length > 0 && (
                  <div className="pt-3 border-t border-[#2F5C52]/40 space-y-3">
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-[#7C9188]">Estimated Total</span>
                      <span className="font-serif font-bold text-base text-[#C4622D]">
                        ${cartTotal.toLocaleString()}
                      </span>
                    </div>
                    <button
                      type="button"
                      onClick={() => {
                        setCartOpen(false);
                        onCheckout();
                      }}
                      className="w-full py-2.5 bg-[#C4622D] hover:bg-[#E07A3E] text-white rounded-xl text-xs font-semibold tracking-wider uppercase transition-all shadow-lg hover:shadow-[#C4622D]/30 flex items-center justify-center gap-2 cursor-pointer"
                    >
                      <span>Proceed to Confirmation</span>
                      <ArrowRight className="w-3.5 h-3.5" />
                    </button>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Mobile Cart Trigger Bottom Bar */}
      {cart.length > 0 && (
        <div className="md:hidden fixed bottom-0 left-0 right-0 z-50 bg-[#0F1B1A]/95 backdrop-blur-md border-t border-[#2F5C52] px-4 py-3 shadow-[0_-10px_25px_rgba(0,0,0,0.5)]">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-2.5">
              <span className="flex items-center justify-center w-8 h-8 rounded-lg bg-[#C4622D] text-white text-xs font-bold">
                {cart.length}
              </span>
              <div>
                <div className="text-[10px] uppercase tracking-wider text-[#7C9188]">
                  Cart Total
                </div>
                <div className="text-sm font-serif font-bold text-[#C4622D]">
                  ${cartTotal.toLocaleString()}
                </div>
              </div>
            </div>
            <button
              type="button"
              onClick={onCheckout}
              className="px-5 py-2.5 bg-[#C4622D] hover:bg-[#E07A3E] text-white rounded-xl text-xs font-bold tracking-wider uppercase flex items-center gap-1.5 cursor-pointer"
            >
              <span>Confirm</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>
      )}
    </section>
  );
}
