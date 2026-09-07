"use client";

import React, { useState, useEffect, Suspense } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import Link from "next/link";
import {
  Search,
  CheckCircle2,
  Calendar,
  User,
  Mail,
  Phone,
  BedDouble,
  CreditCard,
  Copy,
  Check,
  ArrowRight,
  ShieldCheck,
  AlertCircle,
  Sparkles,
  ExternalLink,
  HelpCircle,
  Lock,
} from "lucide-react";
import api from "@/lib/axios";
import { useAuthStore } from "@/features/auth/store/useAuthStore";

interface BookingLookupData {
  bookingReference: string;
  status: string;
  checkInDate: string;
  checkOutDate: string;
  nights: number;
  roomTypeName: string;
  guestCount: number;
  maskedGuestName: string;
  maskedEmail: string;
  maskedPhone: string | null;
  source: string;
  paymentStatus: string;
  totalAmount: number;
  isClaimed: boolean;
}

function BookingLookupContent() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const { isAuthenticated, user } = useAuthStore();

  const [bookingReference, setBookingReference] = useState("");
  const [email, setEmail] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [booking, setBooking] = useState<BookingLookupData | null>(null);
  const [copied, setCopied] = useState(false);
  const [isClaiming, setIsClaiming] = useState(false);
  const [claimSuccess, setClaimSuccess] = useState<string | null>(null);

  // Auto-fill from URL query param ?ref= and optional ?email=
  useEffect(() => {
    const refParam = searchParams.get("ref");
    const emailParam = searchParams.get("email");

    if (refParam) {
      setBookingReference(refParam.toUpperCase().trim());
    }
    if (emailParam) {
      setEmail(emailParam.trim());
    }
  }, [searchParams]);

  const handleLookup = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setClaimSuccess(null);

    const ref = bookingReference.trim().toUpperCase();
    const mail = email.trim().toLowerCase();

    if (!ref || !mail) {
      setError("Please enter both your booking reference number and email address.");
      return;
    }

    setIsLoading(true);
    try {
      const response = await api.post("/api/v1/kiosk/lookup", {
        bookingReference: ref,
        lastName: mail,
      });

      const d = response.data;
      const nights = Math.max(
        1,
        Math.round(
          (new Date(d.checkOutDate).getTime() - new Date(d.checkInDate).getTime()) / (1000 * 3600 * 24)
        )
      );

      setBooking({
        bookingReference: d.bookingReference,
        status: typeof d.status === "number" ? (d.status === 0 ? "Confirmed" : d.status === 1 ? "CheckedIn" : d.status === 2 ? "CheckedOut" : "Cancelled") : String(d.status || "Confirmed"),
        checkInDate: d.checkInDate,
        checkOutDate: d.checkOutDate,
        nights,
        roomTypeName: d.roomTypeName || "Standard Residence",
        guestCount: d.guestCount || 2,
        maskedGuestName: d.customerLastName ? `Guest ${d.customerLastName.charAt(0)}***` : "Valued Guest",
        maskedEmail: d.customerEmail ? `${d.customerEmail.charAt(0)}***@${d.customerEmail.split('@')[1] || '***'}` : mail,
        maskedPhone: null,
        source: "SmartHotel Direct",
        paymentStatus: "Paid",
        totalAmount: Number(d.totalAmount || 0),
        isClaimed: false,
      });
    } catch (err: any) {
      setBooking(null);
      if (err.response?.status === 429) {
        setError("Too many lookup attempts. Please wait a minute and try again.");
      } else if (err.response?.data?.message) {
        setError(err.response.data.message);
      } else {
        setError("No matching booking found. Please verify your reference number and email address.");
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleCopyReference = () => {
    if (!booking) return;
    navigator.clipboard.writeText(booking.bookingReference);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDirectClaim = async () => {
    if (!booking || !isAuthenticated) return;
    setIsClaiming(true);
    setError(null);
    setClaimSuccess(null);

    try {
      await api.post("/api/bookings/claim", {
        bookingReference: booking.bookingReference,
        email: email.trim(),
      });

      setClaimSuccess("Reservation successfully linked to your account!");
      setBooking((prev) => (prev ? { ...prev, isClaimed: true } : null));
    } catch (err: any) {
      setError(
        err.response?.data?.message ||
          "Could not link this reservation. Please ensure your logged-in account email matches the reservation."
      );
    } finally {
      setIsClaiming(false);
    }
  };

  const getSourceBadge = (source: string) => {
    switch (source.toLowerCase()) {
      case "bookingcom":
        return { label: "Booking.com", bg: "bg-blue-900/40 text-blue-300 border-blue-700/50" };
      case "agoda":
        return { label: "Agoda", bg: "bg-emerald-900/40 text-emerald-300 border-emerald-700/50" };
      case "expedia":
        return { label: "Expedia", bg: "bg-amber-900/40 text-amber-300 border-amber-700/50" };
      default:
        return { label: "SmartHotel Direct", bg: "bg-[#C4622D]/20 text-[#E07A3E] border-[#C4622D]/40" };
    }
  };

  return (
    <div className="min-h-screen bg-[#0F1B1A] text-[#E7EFEC] py-12 px-4 sm:px-6 lg:px-8 selection:bg-[#C4622D]/30">
      <div className="max-w-4xl mx-auto flex flex-col gap-10">
        {/* Header Hero Section */}
        <div className="text-center flex flex-col items-center gap-3">
          <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-[#16302C] border border-[rgba(231,239,236,0.12)] text-xs text-[#E07A3E] font-medium shadow-sm">
            <Sparkles className="w-3.5 h-3.5" />
            <span>OTA & Direct Reservation Portal</span>
          </div>
          <h1
            className="font-serif text-3xl sm:text-4xl md:text-5xl font-normal text-[#E7EFEC] tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            Find Your Reservation
          </h1>
          <p className="max-w-2xl text-sm sm:text-base text-[#9BAFA9] leading-relaxed">
            Whether you booked directly or through{" "}
            <span className="text-[#E7EFEC] font-medium">Booking.com, Agoda, or Expedia</span>, access your stay
            details, check your booking status, or link it to your SmartHotel account.
          </p>
        </div>

        {/* Search Card */}
        <div className="bg-[#162725]/80 backdrop-blur-xl border border-[rgba(231,239,236,0.12)] rounded-2xl p-6 sm:p-8 shadow-2xl shadow-black/40">
          <form onSubmit={handleLookup} className="flex flex-col gap-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
              {/* Booking Reference */}
              <div className="flex flex-col gap-1.5">
                <label
                  htmlFor="bookingReference"
                  className="text-xs sm:text-sm font-medium text-[#E7EFEC] flex items-center justify-between"
                >
                  <span>Booking Reference Number</span>
                  <span className="text-xs text-[#9BAFA9] font-mono">e.g. TH-2026-XXXXXX</span>
                </label>
                <div className="relative">
                  <input
                    id="bookingReference"
                    type="text"
                    required
                    value={bookingReference}
                    onChange={(e) => setBookingReference(e.target.value.toUpperCase())}
                    placeholder="TH-2026-483920"
                    className="w-full px-4 py-3 rounded-xl bg-[#0F1B1A]/80 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#7C9188] text-sm focus:outline-none focus:ring-2 focus:ring-[#C4622D] focus:border-transparent transition-all font-mono tracking-wider uppercase"
                  />
                  <div className="absolute right-3.5 top-1/2 -translate-y-1/2 text-[#7C9188]">
                    <ShieldCheck className="w-4 h-4" />
                  </div>
                </div>
              </div>

              {/* Guest Email */}
              <div className="flex flex-col gap-1.5">
                <label
                  htmlFor="guestEmail"
                  className="text-xs sm:text-sm font-medium text-[#E7EFEC] flex items-center justify-between"
                >
                  <span>Guest Email Address</span>
                  <span className="text-xs text-[#9BAFA9]">Used during booking</span>
                </label>
                <div className="relative">
                  <input
                    id="guestEmail"
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="guest@example.com"
                    className="w-full px-4 py-3 rounded-xl bg-[#0F1B1A]/80 border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] placeholder-[#7C9188] text-sm focus:outline-none focus:ring-2 focus:ring-[#C4622D] focus:border-transparent transition-all"
                  />
                  <div className="absolute right-3.5 top-1/2 -translate-y-1/2 text-[#7C9188]">
                    <Mail className="w-4 h-4" />
                  </div>
                </div>
              </div>
            </div>

            {/* Error Message */}
            {error && (
              <div className="flex items-start gap-3 p-3.5 rounded-xl bg-red-950/40 border border-red-800/60 text-red-200 text-xs sm:text-sm">
                <AlertCircle className="w-5 h-5 text-red-400 shrink-0 mt-0.5" />
                <div className="flex-1">{error}</div>
              </div>
            )}

            {/* Claim Success Message */}
            {claimSuccess && (
              <div className="flex items-start gap-3 p-3.5 rounded-xl bg-emerald-950/40 border border-emerald-800/60 text-emerald-200 text-xs sm:text-sm">
                <CheckCircle2 className="w-5 h-5 text-emerald-400 shrink-0 mt-0.5" />
                <div className="flex-1">{claimSuccess}</div>
              </div>
            )}

            {/* Submit Button */}
            <button
              type="submit"
              disabled={isLoading}
              className="w-full sm:w-auto self-end px-8 py-3.5 rounded-xl bg-[#C4622D] hover:bg-[#A84F22] active:scale-[0.99] text-[#E7EFEC] font-medium text-sm tracking-wide transition-all shadow-lg shadow-[#C4622D]/20 flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
            >
              {isLoading ? (
                <>
                  <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                  <span>Searching Reservation...</span>
                </>
              ) : (
                <>
                  <Search className="w-4 h-4" />
                  <span>Look Up Reservation</span>
                </>
              )}
            </button>
          </form>
        </div>

        {/* Search Results Details Card */}
        {booking && (
          <div className="bg-[#162725] border border-[rgba(231,239,236,0.18)] rounded-2xl overflow-hidden shadow-2xl shadow-black/50 animate-in fade-in slide-in-from-bottom-4 duration-300">
            {/* Top Bar with Reference & Badges */}
            <div className="p-6 sm:p-8 bg-[#1A332F]/50 border-b border-[rgba(231,239,236,0.1)] flex flex-wrap items-center justify-between gap-4">
              <div className="flex flex-col gap-1">
                <span className="text-xs text-[#9BAFA9] uppercase tracking-wider font-semibold">
                  Reservation Reference
                </span>
                <div className="flex items-center gap-2.5">
                  <span className="font-mono text-xl sm:text-2xl font-bold text-[#E7EFEC] tracking-wider">
                    {booking.bookingReference}
                  </span>
                  <button
                    onClick={handleCopyReference}
                    title="Copy Reference"
                    className="p-1.5 rounded-lg bg-[#0F1B1A]/60 hover:bg-[#0F1B1A] border border-[rgba(231,239,236,0.1)] text-[#9BAFA9] hover:text-[#E7EFEC] transition-all cursor-pointer"
                  >
                    {copied ? <Check className="w-4 h-4 text-emerald-400" /> : <Copy className="w-4 h-4" />}
                  </button>
                </div>
              </div>

              <div className="flex items-center gap-2 flex-wrap">
                {/* Source Badge */}
                {(() => {
                  const badge = getSourceBadge(booking.source);
                  return (
                    <span
                      className={`px-3 py-1 rounded-full text-xs font-medium border ${badge.bg}`}
                    >
                      {badge.label}
                    </span>
                  );
                })()}

                {/* Status Badge */}
                <span
                  className={`px-3 py-1 rounded-full text-xs font-medium border ${
                    booking.status.toLowerCase() === "confirmed"
                      ? "bg-emerald-900/40 text-emerald-300 border-emerald-700/50"
                      : booking.status.toLowerCase() === "checkedin"
                      ? "bg-blue-900/40 text-blue-300 border-blue-700/50"
                      : "bg-amber-900/40 text-amber-300 border-amber-700/50"
                  }`}
                >
                  {booking.status}
                </span>

                {/* Claim Status Badge */}
                <span
                  className={`px-3 py-1 rounded-full text-xs font-medium border ${
                    booking.isClaimed
                      ? "bg-purple-900/40 text-purple-300 border-purple-700/50"
                      : "bg-zinc-800/70 text-zinc-300 border-zinc-700/50"
                  }`}
                >
                  {booking.isClaimed ? "Account Linked" : "Unclaimed"}
                </span>
              </div>
            </div>

            {/* Details Grid */}
            <div className="p-6 sm:p-8 grid grid-cols-1 md:grid-cols-2 gap-8">
              {/* Stay & Room Details */}
              <div className="flex flex-col gap-5">
                <h3 className="text-xs uppercase tracking-widest text-[#E07A3E] font-semibold flex items-center gap-2">
                  <Calendar className="w-4 h-4" />
                  <span>Stay Itinerary</span>
                </h3>

                <div className="bg-[#0F1B1A]/60 rounded-xl p-4.5 border border-[rgba(231,239,236,0.08)] flex flex-col gap-4">
                  <div className="flex items-center justify-between pb-3 border-b border-[rgba(231,239,236,0.08)]">
                    <div>
                      <span className="text-[11px] text-[#9BAFA9] uppercase">Check-in</span>
                      <p className="text-sm font-semibold text-[#E7EFEC]">
                        {new Date(booking.checkInDate).toLocaleDateString(undefined, {
                          weekday: "short",
                          year: "numeric",
                          month: "short",
                          day: "numeric",
                        })}
                      </p>
                    </div>
                    <div className="text-right">
                      <span className="text-[11px] text-[#9BAFA9] uppercase">Check-out</span>
                      <p className="text-sm font-semibold text-[#E7EFEC]">
                        {new Date(booking.checkOutDate).toLocaleDateString(undefined, {
                          weekday: "short",
                          year: "numeric",
                          month: "short",
                          day: "numeric",
                        })}
                      </p>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-3 text-xs">
                    <div>
                      <span className="text-[#9BAFA9]">Duration</span>
                      <p className="font-medium text-[#E7EFEC]">
                        {booking.nights} {booking.nights === 1 ? "Night" : "Nights"}
                      </p>
                    </div>
                    <div>
                      <span className="text-[#9BAFA9]">Guests</span>
                      <p className="font-medium text-[#E7EFEC]">
                        {booking.guestCount} {booking.guestCount === 1 ? "Guest" : "Guests"}
                      </p>
                    </div>
                    <div className="col-span-2">
                      <span className="text-[#9BAFA9]">Room Type</span>
                      <p className="font-medium text-[#E7EFEC] flex items-center gap-1.5 mt-0.5">
                        <BedDouble className="w-3.5 h-3.5 text-[#E07A3E]" />
                        <span>{booking.roomTypeName}</span>
                      </p>
                    </div>
                  </div>
                </div>
              </div>

              {/* Guest & Payment Overview */}
              <div className="flex flex-col gap-5">
                <h3 className="text-xs uppercase tracking-widest text-[#E07A3E] font-semibold flex items-center gap-2">
                  <ShieldCheck className="w-4 h-4" />
                  <span>Guest & Payment Information</span>
                </h3>

                <div className="bg-[#0F1B1A]/60 rounded-xl p-4.5 border border-[rgba(231,239,236,0.08)] flex flex-col gap-3 text-xs">
                  <div className="flex items-center justify-between py-1 border-b border-[rgba(231,239,236,0.08)]">
                    <span className="text-[#9BAFA9] flex items-center gap-1.5">
                      <User className="w-3.5 h-3.5" />
                      <span>Guest Name</span>
                    </span>
                    <span className="font-mono text-[#E7EFEC]">{booking.maskedGuestName}</span>
                  </div>

                  <div className="flex items-center justify-between py-1 border-b border-[rgba(231,239,236,0.08)]">
                    <span className="text-[#9BAFA9] flex items-center gap-1.5">
                      <Mail className="w-3.5 h-3.5" />
                      <span>Contact Email</span>
                    </span>
                    <span className="font-mono text-[#E7EFEC]">{booking.maskedEmail}</span>
                  </div>

                  {booking.maskedPhone && (
                    <div className="flex items-center justify-between py-1 border-b border-[rgba(231,239,236,0.08)]">
                      <span className="text-[#9BAFA9] flex items-center gap-1.5">
                        <Phone className="w-3.5 h-3.5" />
                        <span>Phone</span>
                      </span>
                      <span className="font-mono text-[#E7EFEC]">{booking.maskedPhone}</span>
                    </div>
                  )}

                  <div className="flex items-center justify-between pt-1">
                    <span className="text-[#9BAFA9] flex items-center gap-1.5">
                      <CreditCard className="w-3.5 h-3.5" />
                      <span>Payment Status</span>
                    </span>
                    <div className="text-right">
                      <span className="font-semibold text-emerald-400">
                        {booking.paymentStatus}
                      </span>
                      <p className="text-[11px] text-[#7C9188]">
                        Total: {booking.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                      </p>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Bottom Claim / Portal Action Banner */}
            <div className="p-6 sm:p-8 bg-[#16302C]/60 border-t border-[rgba(231,239,236,0.1)] flex flex-col sm:flex-row items-center justify-between gap-4">
              <div className="flex flex-col gap-1 text-center sm:text-left">
                <h4 className="text-sm font-semibold text-[#E7EFEC]">
                  {booking.isClaimed
                    ? "Manage this reservation from your Guest Portal"
                    : "Link this reservation to your SmartHotel account"}
                </h4>
                <p className="text-xs text-[#9BAFA9]">
                  {booking.isClaimed
                    ? "Access digital key, request housekeeping, order dining, and chat with your AI Concierge."
                    : "Create a free account or sign in to manage room preferences, view invoice, and unlock guest perks."}
                </p>
              </div>

              <div className="flex items-center gap-3 shrink-0 w-full sm:w-auto">
                {booking.isClaimed ? (
                  <Link
                    href="/portal"
                    className="w-full sm:w-auto px-6 py-2.5 rounded-xl bg-[#C4622D] hover:bg-[#A84F22] text-[#E7EFEC] font-medium text-xs sm:text-sm transition-all flex items-center justify-center gap-2"
                  >
                    <span>Open Guest Portal</span>
                    <ArrowRight className="w-4 h-4" />
                  </Link>
                ) : isAuthenticated ? (
                  <button
                    onClick={handleDirectClaim}
                    disabled={isClaiming}
                    className="w-full sm:w-auto px-6 py-2.5 rounded-xl bg-[#C4622D] hover:bg-[#A84F22] text-[#E7EFEC] font-medium text-xs sm:text-sm transition-all flex items-center justify-center gap-2 disabled:opacity-50 cursor-pointer"
                  >
                    {isClaiming ? "Linking..." : "Claim to My Account"}
                  </button>
                ) : (
                  <div className="flex items-center gap-2.5 w-full sm:w-auto">
                    <Link
                      href={`/register?ref=${encodeURIComponent(booking.bookingReference)}&email=${encodeURIComponent(
                        email.trim()
                      )}`}
                      className="flex-1 sm:flex-initial px-5 py-2.5 rounded-xl bg-[#C4622D] hover:bg-[#A84F22] text-[#E7EFEC] font-medium text-xs sm:text-sm transition-all text-center"
                    >
                      Create Account to Claim
                    </Link>
                    <Link
                      href={`/login?ref=${encodeURIComponent(booking.bookingReference)}`}
                      className="flex-1 sm:flex-initial px-4 py-2.5 rounded-xl bg-[#0F1B1A]/80 hover:bg-[#0F1B1A] border border-[rgba(231,239,236,0.15)] text-[#E7EFEC] font-medium text-xs sm:text-sm transition-all text-center"
                    >
                      Sign In
                    </Link>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {/* FAQ & Information Section */}
        <div className="bg-[#162725]/40 border border-[rgba(231,239,236,0.08)] rounded-2xl p-6 sm:p-8 flex flex-col gap-6">
          <div className="flex items-center gap-2 text-[#E07A3E] text-sm font-semibold">
            <HelpCircle className="w-4 h-4" />
            <span>Frequently Asked Questions</span>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-xs sm:text-sm text-[#9BAFA9]">
            <div className="flex flex-col gap-1.5">
              <h4 className="font-semibold text-[#E7EFEC]">Where can I find my booking reference?</h4>
              <p className="leading-relaxed">
                Your booking reference format is <span className="font-mono text-[#E07A3E]">TH-YYYY-XXXXXX</span>.
                You can find it in your SmartHotel confirmation email, or in the confirmation sent by Booking.com,
                Agoda, or Expedia.
              </p>
            </div>

            <div className="flex flex-col gap-1.5">
              <h4 className="font-semibold text-[#E7EFEC]">I booked on an OTA. Can I still use the Guest Portal?</h4>
              <p className="leading-relaxed">
                Yes! Simply look up your reservation above, then click &quot;Create Account to Claim&quot; using the same
                email address. Your reservation will automatically link to your profile.
              </p>
            </div>

            <div className="flex flex-col gap-1.5">
              <h4 className="font-semibold text-[#E7EFEC]">How is my privacy protected?</h4>
              <p className="leading-relaxed">
                Public lookups require matching both the exact booking reference and the reservation email. All
                sensitive personal information is masked to protect guest privacy.
              </p>
            </div>

            <div className="flex flex-col gap-1.5">
              <h4 className="font-semibold text-[#E7EFEC]">Need help or need to modify your stay?</h4>
              <p className="leading-relaxed">
                Reach out to our 24/7 Maskeliya concierge desk via{" "}
                <Link href="/contact" className="text-[#E07A3E] underline hover:text-[#C4622D]">
                  Contact Us
                </Link>{" "}
                or call +94 52 222 3456.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default function BookingLookupPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen bg-[#0F1B1A] flex items-center justify-center text-[#9BAFA9]">
          <div className="w-6 h-6 border-2 border-[#C4622D]/30 border-t-[#C4622D] rounded-full animate-spin" />
        </div>
      }
    >
      <BookingLookupContent />
    </Suspense>
  );
}
