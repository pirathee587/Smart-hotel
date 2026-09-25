"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import {
AlertCircle,
Check,
Eye,
EyeOff,
Loader2,
Shield,
User,
X
} from "lucide-react";
import Link from "next/link";
import React,{ useEffect,useRef,useState } from "react";
import { RatePlan,RoomListing,RoomVariant } from "../types/bookingSelection";

export interface PendingBookingContext {
  room: RoomListing;
  variant: RoomVariant;
  ratePlan: RatePlan;
  checkIn: string;
  checkOut: string;
  guests: number;
  rooms: number;
  promoCode?: string;
}

interface MemberSignInModalProps {
  isOpen: boolean;
  onClose: () => void;
  pendingBooking: PendingBookingContext | null;
  onSuccess: (pendingBooking: PendingBookingContext | null) => void;
}

export function MemberSignInModal({
  isOpen,
  onClose,
  pendingBooking,
  onSuccess,
}: MemberSignInModalProps) {
  const { loginMember, isLoading } = useAuthStore();

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [useGHA, setUseGHA] = useState(false);

  // Field validation errors
  const [errors, setErrors] = useState<{ username?: string; password?: string }>({});
  // API error banner (e.g. invalid_credentials)
  const [apiError, setApiError] = useState<string | null>(null);

  const modalRef = useRef<HTMLDivElement>(null);
  const usernameInputRef = useRef<HTMLInputElement>(null);

  // Focus trap & Esc listener
  useEffect(() => {
    if (!isOpen) return;

    // Autofocus username input
    setTimeout(() => {
      usernameInputRef.current?.focus();
    }, 100);

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }

      // Basic focus trap
      if (e.key === "Tab" && modalRef.current) {
        const focusableElements = modalRef.current.querySelectorAll<HTMLElement>(
          'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
        );
        const first = focusableElements[0];
        const last = focusableElements[focusableElements.length - 1];

        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  // Build guest checkout or sign-in link preserving booking context
  const buildRegisterUrl = () => {
    if (!pendingBooking) return "/login";
    const params = new URLSearchParams({
      roomId: pendingBooking.room.id,
      roomName: pendingBooking.room.name,
      roomTypeId: pendingBooking.room.id,
      variantId: pendingBooking.variant.id,
      variantLabel: `${pendingBooking.room.name} (${pendingBooking.variant.label})`,
      ratePlanId: pendingBooking.ratePlan.id,
      ratePlanName: pendingBooking.ratePlan.name,
      checkIn: pendingBooking.checkIn,
      checkOut: pendingBooking.checkOut,
      guests: pendingBooking.guests.toString(),
      rooms: pendingBooking.rooms.toString(),
      price: pendingBooking.ratePlan.price.toString(),
    });
    if (pendingBooking.promoCode) {
      params.set("promo", pendingBooking.promoCode);
    }
    return `/booking/checkout?${params.toString()}`;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setApiError(null);

    const newErrors: { username?: string; password?: string } = {};
    if (!username.trim()) {
      newErrors.username = "Username or email is required.";
    }
    if (!password) {
      newErrors.password = "Password is required.";
    }

    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors);
      return;
    }

    setErrors({});

    const result = await loginMember(username, password);

    if (result.success) {
      onSuccess(pendingBooking);
    } else {
      // Invalid credentials: error banner above inputs, inputs keep values, password field clears
      setApiError(
        result.error === "invalid_credentials"
          ? "Invalid username or password. Please check your credentials and try again."
          : result.error || "Unable to sign in. Please verify your details."
      );
      setPassword("");
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-[4px] animate-in fade-in duration-200"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-labelledby="member-signin-title"
    >
      <div
        ref={modalRef}
        onClick={(e) => e.stopPropagation()}
        className="relative w-full max-w-md bg-[#0F1B1A] border border-[#2F5C52]/50 rounded-3xl p-6 sm:p-8 shadow-[0_25px_60px_-15px_rgba(0,0,0,0.8)] text-[#F6F1E6] animate-in zoom-in-95 duration-200"
      >
        {/* Close Button */}
        <button
          type="button"
          onClick={onClose}
          className="absolute top-5 right-5 p-2 text-[#7C9188] hover:text-white rounded-full bg-[#16302C]/60 hover:bg-[#16302C] transition-colors cursor-pointer"
          aria-label="Close modal"
        >
          <X className="w-4 h-4" />
        </button>

        {/* Header: Member Sign In (Fraunces, ink-teal) */}
        <div className="mb-6">
          <div className="flex items-center gap-2 text-[#C4622D] text-xs font-semibold uppercase tracking-[0.2em] mb-1">
            <Shield className="w-3.5 h-3.5 text-[#C4622D]" />
            <span>SmartHotel Member Access</span>
          </div>
          <h2
            id="member-signin-title"
            className="font-serif font-bold text-2xl sm:text-3xl text-[#F6F1E6] tracking-tight"
          >
            Member Sign In
          </h2>
          {/* Subtext */}
          <p className="text-xs sm:text-sm text-[#7C9188] mt-1">
            Sign in with your username and password.
          </p>
        </div>

        {/* Selected Room Context Banner (preserved behind modal) */}
        {pendingBooking && (
          <div className="mb-5 p-3 rounded-xl bg-[#16302C]/70 border border-[#2F5C52]/40 text-xs">
            <span className="text-[#7C9188] block text-[10px] uppercase tracking-wider">
              Pending Stay Reservation
            </span>
            <div className="font-serif font-bold text-[#F6F1E6] text-sm truncate">
              {pendingBooking.room.name} · {pendingBooking.variant.label}
            </div>
            <div className="text-[11px] text-[#C4622D] font-medium mt-0.5">
              {pendingBooking.ratePlan.name} · ${pendingBooking.ratePlan.price}/night
            </div>
          </div>
        )}

        {/* Error Banner with aria-live */}
        {apiError && (
          <div
            role="alert"
            aria-live="polite"
            className="mb-5 p-3.5 rounded-xl bg-rose-950/40 border border-rose-800/60 text-rose-300 text-xs flex items-start gap-2.5 animate-in fade-in slide-in-from-top-2 duration-150"
          >
            <AlertCircle className="w-4 h-4 text-rose-400 shrink-0 mt-0.5" />
            <div className="flex-1 font-medium leading-relaxed">{apiError}</div>
          </div>
        )}

        {/* Sign In Form */}
        <form onSubmit={handleSubmit} className="space-y-4" noValidate>
          {/* Checkbox: Use Global Hotel Alliance */}
          {/* // TODO: wire to loyalty/partner login once that integration is scoped */}
          <div className="flex items-center gap-2.5 pt-1 pb-1 text-xs text-[#EEE7D6] select-none">
            <label className="inline-flex items-center gap-2.5 cursor-pointer">
              <input
                type="checkbox"
                checked={useGHA}
                onChange={(e) => setUseGHA(e.target.checked)}
                className="sr-only"
              />
              <div
                className={`w-4 h-4 rounded border flex items-center justify-center transition-all ${
                  useGHA
                    ? "bg-[#2F5C52] border-[#2F5C52] text-white"
                    : "border-[#2F5C52] bg-[#16302C]"
                }`}
              >
                {useGHA && <Check className="w-3 h-3 stroke-[3]" />}
              </div>
              <span className="text-xs text-[#7C9188] hover:text-[#F6F1E6] transition-colors">
                Use Global Hotel Alliance
              </span>
            </label>
          </div>

          {/* Username Input */}
          <div>
            <label
              htmlFor="modal-username"
              className="block text-xs font-medium text-[#EEE7D6] mb-1.5"
            >
              Username
            </label>
            <div className="relative">
              <input
                ref={usernameInputRef}
                id="modal-username"
                type="text"
                value={username}
                onChange={(e) => {
                  setUsername(e.target.value);
                  if (errors.username) setErrors({ ...errors, username: undefined });
                }}
                placeholder="Enter your username or email"
                autoComplete="username"
                className={`w-full bg-[#16302C] border rounded-xl px-3.5 py-2.5 text-xs sm:text-sm text-[#F6F1E6] placeholder-[#7C9188]/60 focus:outline-none transition-colors ${
                  errors.username
                    ? "border-rose-500 focus:border-rose-400"
                    : "border-[#2F5C52] focus:border-[#C4622D]"
                }`}
              />
              <User className="w-4 h-4 text-[#7C9188] absolute right-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
            </div>
            {errors.username && (
              <p className="mt-1 text-[11px] text-rose-400">{errors.username}</p>
            )}
          </div>

          {/* Password Input */}
          <div>
            <label
              htmlFor="modal-password"
              className="block text-xs font-medium text-[#EEE7D6] mb-1.5"
            >
              Password
            </label>
            <div className="relative">
              <input
                id="modal-password"
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value);
                  if (errors.password) setErrors({ ...errors, password: undefined });
                }}
                placeholder="Enter your password"
                autoComplete="current-password"
                className={`w-full bg-[#16302C] border rounded-xl px-3.5 py-2.5 text-xs sm:text-sm text-[#F6F1E6] placeholder-[#7C9188]/60 focus:outline-none transition-colors ${
                  errors.password
                    ? "border-rose-500 focus:border-rose-400"
                    : "border-[#2F5C52] focus:border-[#C4622D]"
                }`}
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="p-1 text-[#7C9188] hover:text-white absolute right-3 top-1/2 -translate-y-1/2"
                aria-label={showPassword ? "Hide password" : "Show password"}
              >
                {showPassword ? (
                  <EyeOff className="w-4 h-4" />
                ) : (
                  <Eye className="w-4 h-4" />
                )}
              </button>
            </div>
            {errors.password && (
              <p className="mt-1 text-[11px] text-rose-400">{errors.password}</p>
            )}
          </div>

          {/* Below inputs: "Forgot Password?" link (pine, underlined) */}
          <div className="text-right pt-0.5">
            <Link
              href="/account/forgot-password"
              className="text-xs font-medium text-[#7C9188] hover:text-[#C4622D] underline underline-offset-4 decoration-[#2F5C52]/60 hover:decoration-[#C4622D] transition-colors"
            >
              Forgot Password?
            </Link>
          </div>

          {/* Bottom Row: Continue to guest checkout (left) and solid Sign in button (right) */}
          <div className="pt-4 flex flex-col sm:flex-row items-center justify-between gap-4 border-t border-[#2F5C52]/30">
            <div className="text-xs text-[#7C9188] text-center sm:text-left">
              <span>Prefer not to sign in? </span>
              <Link
                href={buildRegisterUrl()}
                className="font-bold text-[#F6F1E6] hover:text-[#C4622D] underline underline-offset-2 transition-colors"
              >
                Continue as guest →
              </Link>
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="w-full sm:w-auto px-7 py-2.5 bg-[#E07A3E] hover:bg-[#C4622D] text-white border border-[#E07A3E] rounded-xl text-xs font-bold tracking-wider uppercase transition-all shadow-lg hover:shadow-[#E07A3E]/30 disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer flex items-center justify-center gap-2 min-w-[120px]"
            >
              {isLoading && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
              <span>{isLoading ? "Signing In…" : "Sign in"}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
