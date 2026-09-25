"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { CheckoutResultDto,publicApi } from "@/services/publicApi";
import {
AlertCircle,
ArrowLeft,
Award,
Calendar,
CheckCircle2,
ChevronRight,
Clock,
CreditCard,
HelpCircle,
Lock,
ShieldCheck,
Sparkles,
Users
} from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import React,{ useEffect,useMemo,useState } from "react";
import { MOCK_ROOM_LISTINGS } from "../services/mockBookingData";

export function CheckoutPageContent() {
  const searchParams = useSearchParams();
  const { isAuthenticated, user, token, initialize } = useAuthStore();

  useEffect(() => {
    initialize();
  }, [initialize]);

  // Read search parameters from URL
  const roomId = searchParams.get("roomId") || "room-premium-terrace";
  const roomNameParam = searchParams.get("roomName") || "Premium Terrace";
  const variantLabel = searchParams.get("variantLabel") || "King Bed";
  const ratePlanName = searchParams.get("ratePlanName") || "Member Rate — All Meals Included";
  const checkIn = searchParams.get("checkIn") || new Date().toISOString().split("T")[0];
  const checkOut = searchParams.get("checkOut") || "";
  const guests = parseInt(searchParams.get("guests") || "2", 10);
  const roomsCount = parseInt(searchParams.get("rooms") || "1", 10);
  const nights = parseInt(searchParams.get("nights") || "2", 10);
  const pricePerNight = parseFloat(searchParams.get("price") || "315");
  const isUpgraded = searchParams.get("isUpgraded") === "true";
  const priceDelta = parseFloat(searchParams.get("priceDelta") || "0");
  const draftId = searchParams.get("draftId") || "";

  // Lookup matching listing for image
  const roomListing = useMemo(() => {
    return MOCK_ROOM_LISTINGS.find((r) => r.id === roomId || r.name.toLowerCase().includes(roomNameParam.toLowerCase())) || MOCK_ROOM_LISTINGS[0];
  }, [roomId, roomNameParam]);

  // Form State
  const [formData, setFormData] = useState({
    firstName: "",
    lastName: "",
    email: "",
    phone: "",
    addressLine1: "42 Sanctuary Ridge Way",
    addressLine2: "",
    city: "Maskeliya",
    country: "Sri Lanka",
    postalCode: "22070",
    sameAddress: true,
    specialRequests: "",
    loyaltyNumber: "",
    // Payment fields (simulated tokenization)
    cardholderName: "",
    cardNumber: "",
    cardExpiry: "",
    cardCvv: "",
    termsAccepted: true,
  });

  // Pre-fill authenticated member info
  useEffect(() => {
    queueMicrotask(() => {
      if (user) {
      const nameParts = (user.name || "").trim().split(" ");
      const fName = nameParts[0] || "John";
      const lName = nameParts.slice(1).join(" ") || "Smith";

        setFormData((prev) => ({
        ...prev,
        firstName: prev.firstName || fName,
        lastName: prev.lastName || lName,
        email: prev.email || user.email || "member@smarthotel.com",
        phone: prev.phone || "+94 77 123 4567",
        loyaltyNumber: prev.loyaltyNumber || `SH-${user.id ? user.id.substring(0, 6).toUpperCase() : "MEMBER"}`,
        cardholderName: prev.cardholderName || user.name || "Member Guest",
        }));
      } else {
        setFormData((prev) => ({
        ...prev,
        firstName: prev.firstName || "Guest",
        lastName: prev.lastName || "Member",
        email: prev.email || "guest@smarthotel.com",
        phone: prev.phone || "+94 77 123 4567",
        cardholderName: prev.cardholderName || "Guest Member",
        }));
      }
    });
  }, [user]);

  // Tooltip state for CVV
  const [showCvvTooltip, setShowCvvTooltip] = useState(false);

  // Submitting state
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [confirmedBooking, setConfirmedBooking] = useState<CheckoutResultDto | null>(null);

  // Price calculations
  const subtotal = pricePerNight * nights * roomsCount;
  const serviceCharge = Math.round(subtotal * 0.1);
  const governmentTax = Math.round(subtotal * 0.15);
  const totalAmount = subtotal + serviceCharge + governmentTax;
  const memberPointsEarned = Math.round(subtotal);

  // Card formatting helpers
  const handleCardNumberChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = e.target.value.replace(/\D/g, "").slice(0, 16);
    const formatted = raw.replace(/(\d{4})(?=\d)/g, "$1 ");
    setFormData((prev) => ({ ...prev, cardNumber: formatted }));
  };

  const handleExpiryChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    let raw = e.target.value.replace(/\D/g, "").slice(0, 4);
    if (raw.length >= 3) {
      raw = `${raw.slice(0, 2)}/${raw.slice(2)}`;
    }
    setFormData((prev) => ({ ...prev, cardExpiry: raw }));
  };

  const handleCvvChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = e.target.value.replace(/\D/g, "").slice(0, 4);
    setFormData((prev) => ({ ...prev, cardCvv: raw }));
  };

  // Submit checkout
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.termsAccepted) {
      setErrorMessage("Please accept the terms and cancellation policy to proceed.");
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    // TODO: integrate real payment provider SDK for PCI DSS tokenization
    const simulatedPaymentToken = `tok_test_${Math.random().toString(36).substring(2, 12)}_${Date.now()}`;

    try {
      const activeDraftId = draftId || "00000000-0000-0000-0000-000000000001";

      const payload = {
        draftId: activeDraftId,
        contact: {
          firstName: formData.firstName,
          lastName: formData.lastName,
          email: formData.email,
          mobile: formData.phone,
        },
        address: {
          addressType: "Home",
          country: formData.country,
          addressLine1: formData.addressLine1,
          city: formData.city,
        },
        specialRequests: formData.specialRequests || undefined,
        loyalty: formData.loyaltyNumber ? { program: "Slowhouse", loyaltyId: formData.loyaltyNumber } : undefined,
        paymentToken: simulatedPaymentToken,
        couponCode: undefined,
      };

      const result = await publicApi.checkoutBooking(payload, token || undefined);
      setConfirmedBooking(result);
    } catch (err: unknown) {
      let message = "Payment and checkout could not be completed. Your card has not been charged. Please check your reservation details and try again.";
      if (err && typeof err === "object") {
        const errorObj = err as {
          response?: { data?: { message?: string; error?: string } };
          message?: string;
        };
        if (errorObj.response?.data?.message) {
          message = String(errorObj.response.data.message);
        } else if (errorObj.response?.data?.error) {
          message = String(errorObj.response.data.error);
        } else if (errorObj.message) {
          message = String(errorObj.message);
        }
      }
      setErrorMessage(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  // If confirmed, show confirmation view
  if (confirmedBooking) {
    return (
      <div className="min-h-screen bg-[#F7F6F2] py-12 px-4 sm:px-6 lg:px-8">
        <div className="max-w-2xl mx-auto bg-white rounded-3xl shadow-xl border border-[#0F1B1A]/10 p-8 sm:p-10 text-center animate-in zoom-in-95 duration-300">
          <div className="w-16 h-16 rounded-full bg-emerald-100 text-emerald-600 flex items-center justify-center mx-auto mb-5 shadow-xs">
            <CheckCircle2 className="w-9 h-9" />
          </div>

          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-[#2F5C52]/10 text-[#2F5C52] text-xs font-semibold uppercase tracking-wider mb-2">
            Reservation Confirmed
          </div>

          <h1 className="font-serif text-3xl font-semibold text-[#0F1B1A] mb-2">
            Thank you, {confirmedBooking.guestName}!
          </h1>
          <p className="text-sm text-[#0F1B1A]/70 mb-6 max-w-md mx-auto">
            Your stay at SmartHotel Maskeliya has been booked. A confirmation email with keyless mobile check-in instructions has been sent to{" "}
            <strong className="text-[#0F1B1A]">{confirmedBooking.guestEmail}</strong>.
          </p>

          {/* Booking Summary Box */}
          <div className="p-6 rounded-2xl bg-[#F7F6F2] border border-[#0F1B1A]/8 text-left space-y-4 mb-8">
            <div className="flex items-center justify-between border-b border-[#0F1B1A]/10 pb-4">
              <div>
                <span className="text-xs text-[#0F1B1A]/60 font-medium">Booking Reference</span>
                <div className="font-mono text-xl font-bold text-[#E07A3E]">
                  {confirmedBooking.bookingReference}
                </div>
              </div>
              <span className="px-3 py-1 rounded-full bg-emerald-100 text-emerald-800 text-xs font-semibold">
                Guaranteed
              </span>
            </div>

            <div className="grid grid-cols-2 gap-4 text-xs sm:text-sm">
              <div>
                <span className="text-[#0F1B1A]/60 block mb-0.5">Room & Suite</span>
                <span className="font-semibold text-[#0F1B1A]">{confirmedBooking.roomName}</span>
              </div>
              <div>
                <span className="text-[#0F1B1A]/60 block mb-0.5">Stay Dates</span>
                <span className="font-semibold text-[#0F1B1A]">
                  {confirmedBooking.checkInDate} to {confirmedBooking.checkOutDate || "Next day"} ({nights} nights)
                </span>
              </div>
              <div>
                <span className="text-[#0F1B1A]/60 block mb-0.5">Total Paid</span>
                <span className="font-semibold text-[#0F1B1A]">
                  {confirmedBooking.currency} {confirmedBooking.totalAmount}
                </span>
              </div>
              <div>
                <span className="text-[#0F1B1A]/60 block mb-0.5">Points Earned</span>
                <span className="font-semibold text-emerald-700 flex items-center gap-1">
                  <Sparkles className="w-3.5 h-3.5 text-[#E07A3E]" />
                  +{memberPointsEarned} SmartPoints
                </span>
              </div>
            </div>
          </div>

          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <Link
              href="/rooms"
              className="px-6 py-3 rounded-xl bg-[#0F1B1A] hover:bg-[#2F5C52] text-white text-sm font-semibold transition-all shadow-md"
            >
              Browse More Rooms
            </Link>
            <Link
              href="/"
              className="px-6 py-3 rounded-xl border border-[#0F1B1A]/20 hover:bg-[#0F1B1A]/5 text-[#0F1B1A] text-sm font-semibold transition-all"
            >
              Return to Home
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#F7F6F2] py-8 sm:py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-7xl mx-auto">
        {/* Navigation Breadcrumb */}
        <div className="mb-6 flex items-center gap-2 text-xs text-[#0F1B1A]/60">
          <Link href="/rooms" className="hover:text-[#0F1B1A] flex items-center gap-1 transition-colors">
            <ArrowLeft className="w-3.5 h-3.5" />
            Back to Room Selection
          </Link>
          <span>/</span>
          <span className="text-[#0F1B1A] font-medium">Checkout</span>
        </div>

        {/* Page Title */}
        <div className="mb-8">
          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-[#2F5C52]/10 text-[#2F5C52] text-xs font-semibold uppercase tracking-wider mb-2">
            <Lock className="w-3.5 h-3.5 text-[#E07A3E]" />
            Secure Checkout
          </div>
          <h1 className="font-serif text-3xl sm:text-4xl font-semibold text-[#0F1B1A]">
            Review & Finalize Your Reservation
          </h1>
          <p className="mt-1 text-sm text-[#0F1B1A]/70 font-sans">
            Guaranteed best rate. Direct boutique reservation at SmartHotel Maskeliya.
          </p>
        </div>

        {/* 2-Column Checkout Layout */}
        <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
          {/* ── LEFT COLUMN: Guest & Payment Details (lg:col-span-7) ── */}
          <div className="lg:col-span-7 space-y-6">
            {/* 1. Contact Information */}
            <div className="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-[#0F1B1A]/8">
              <div className="flex items-center justify-between mb-5">
                <div className="flex items-center gap-2.5">
                  <div className="w-8 h-8 rounded-full bg-[#2F5C52]/10 text-[#2F5C52] flex items-center justify-center font-bold text-sm">
                    1
                  </div>
                  <h2 className="font-serif text-xl font-semibold text-[#0F1B1A]">
                    Guest Contact Information
                  </h2>
                </div>

                {isAuthenticated && (
                  <div className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-emerald-50 text-emerald-800 text-xs font-medium border border-emerald-200/60">
                    <Award className="w-3.5 h-3.5 text-emerald-600" />
                    <span>Member Logged In</span>
                  </div>
                )}
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                    First Name *
                  </label>
                  <input
                    type="text"
                    required
                    value={formData.firstName}
                    onChange={(e) => setFormData({ ...formData, firstName: e.target.value })}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                    placeholder="First name"
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                    Last Name *
                  </label>
                  <input
                    type="text"
                    required
                    value={formData.lastName}
                    onChange={(e) => setFormData({ ...formData, lastName: e.target.value })}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                    placeholder="Last name"
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                    Email Address (for confirmation) *
                  </label>
                  <input
                    type="email"
                    required
                    value={formData.email}
                    onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                    placeholder="you@domain.com"
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                    Phone Number (for keyless entry pin) *
                  </label>
                  <input
                    type="tel"
                    required
                    value={formData.phone}
                    onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                    placeholder="+94 77 123 4567"
                  />
                </div>
              </div>
            </div>

            {/* 2. Billing Address */}
            <div className="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-[#0F1B1A]/8">
              <div className="flex items-center gap-2.5 mb-5">
                <div className="w-8 h-8 rounded-full bg-[#2F5C52]/10 text-[#2F5C52] flex items-center justify-center font-bold text-sm">
                  2
                </div>
                <h2 className="font-serif text-xl font-semibold text-[#0F1B1A]">
                  Billing Address
                </h2>
              </div>

              <div className="space-y-4">
                <div>
                  <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                    Street Address *
                  </label>
                  <input
                    type="text"
                    required
                    value={formData.addressLine1}
                    onChange={(e) => setFormData({ ...formData, addressLine1: e.target.value })}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                    placeholder="House / Building name, street"
                  />
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                      City *
                    </label>
                    <input
                      type="text"
                      required
                      value={formData.city}
                      onChange={(e) => setFormData({ ...formData, city: e.target.value })}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                      placeholder="City"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                      Country *
                    </label>
                    <input
                      type="text"
                      required
                      value={formData.country}
                      onChange={(e) => setFormData({ ...formData, country: e.target.value })}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                      placeholder="Country"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                      Postal Code *
                    </label>
                    <input
                      type="text"
                      required
                      value={formData.postalCode}
                      onChange={(e) => setFormData({ ...formData, postalCode: e.target.value })}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                      placeholder="Postal code"
                    />
                  </div>
                </div>

                <label className="flex items-center gap-2 text-xs text-[#0F1B1A]/70 cursor-pointer pt-1">
                  <input
                    type="checkbox"
                    checked={formData.sameAddress}
                    onChange={(e) => setFormData({ ...formData, sameAddress: e.target.checked })}
                    className="w-4 h-4 rounded text-[#E07A3E] focus:ring-[#E07A3E] border-[#0F1B1A]/20"
                  />
                  <span>Billing address matches primary contact address</span>
                </label>
              </div>
            </div>

            {/* 3. Reservation Details & Preferences */}
            <div className="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-[#0F1B1A]/8">
              <div className="flex items-center gap-2.5 mb-5">
                <div className="w-8 h-8 rounded-full bg-[#2F5C52]/10 text-[#2F5C52] flex items-center justify-center font-bold text-sm">
                  3
                </div>
                <h2 className="font-serif text-xl font-semibold text-[#0F1B1A]">
                  Reservation Preferences
                </h2>
              </div>

              <div className="space-y-4">
                <div>
                  <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                    Special Requests (Optional)
                  </label>
                  <textarea
                    rows={3}
                    value={formData.specialRequests}
                    onChange={(e) => setFormData({ ...formData, specialRequests: e.target.value })}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A] placeholder:text-[#0F1B1A]/40"
                    placeholder="e.g. Quiet room, high floor, early arrival estimate, dietary restrictions (vegan, gluten-free), anniversary surprise..."
                  />
                  <span className="text-[11px] text-[#0F1B1A]/50">
                    Special requests cannot be guaranteed but our concierge team will do their utmost to accommodate.
                  </span>
                </div>

                <div>
                  <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                    SmartHotel Loyalty Member Number
                  </label>
                  <input
                    type="text"
                    value={formData.loyaltyNumber}
                    onChange={(e) => setFormData({ ...formData, loyaltyNumber: e.target.value })}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                    placeholder="SH-XXXXXX"
                  />
                </div>
              </div>
            </div>

            {/* 4. Payment Information (Tokenized) */}
            <div className="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-[#0F1B1A]/8">
              <div className="flex items-center justify-between mb-5">
                <div className="flex items-center gap-2.5">
                  <div className="w-8 h-8 rounded-full bg-[#2F5C52]/10 text-[#2F5C52] flex items-center justify-center font-bold text-sm">
                    4
                  </div>
                  <h2 className="font-serif text-xl font-semibold text-[#0F1B1A]">
                    Payment Details
                  </h2>
                </div>

                {/* Card Icons */}
                <div className="flex items-center gap-2 text-xs font-semibold text-[#0F1B1A]/60">
                  <span className="px-2 py-0.5 rounded-md bg-[#0F1B1A]/5 border border-[#0F1B1A]/10 text-[11px]">
                    VISA
                  </span>
                  <span className="px-2 py-0.5 rounded-md bg-[#0F1B1A]/5 border border-[#0F1B1A]/10 text-[11px]">
                    MC
                  </span>
                  <span className="px-2 py-0.5 rounded-md bg-[#0F1B1A]/5 border border-[#0F1B1A]/10 text-[11px]">
                    AMEX
                  </span>
                </div>
              </div>

              <div className="p-4 rounded-2xl bg-[#F7F6F2] border border-[#0F1B1A]/8 mb-5 flex items-start gap-3 text-xs text-[#0F1B1A]/70">
                <Lock className="w-4 h-4 text-[#2F5C52] shrink-0 mt-0.5" />
                <span>
                  <strong>Tokenized & Encrypted:</strong> Your payment information is securely tokenized using end-to-end 256-bit encryption. We never store raw card numbers on our servers.
                </span>
              </div>

              <div className="space-y-4">
                <div>
                  <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                    Cardholder Name *
                  </label>
                  <input
                    type="text"
                    required
                    value={formData.cardholderName}
                    onChange={(e) => setFormData({ ...formData, cardholderName: e.target.value })}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                    placeholder="Name as it appears on card"
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                    Card Number *
                  </label>
                  <div className="relative">
                    <input
                      type="text"
                      required
                      value={formData.cardNumber}
                      onChange={handleCardNumberChange}
                      maxLength={19}
                      className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm font-mono focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                      placeholder="4111 2222 3333 4444"
                    />
                    <CreditCard className="w-4 h-4 text-[#0F1B1A]/40 absolute left-3.5 top-1/2 -translate-y-1/2" />
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-[#0F1B1A]/70 mb-1">
                      Expiry Date *
                    </label>
                    <input
                      type="text"
                      required
                      value={formData.cardExpiry}
                      onChange={handleExpiryChange}
                      maxLength={5}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm font-mono focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                      placeholder="MM/YY"
                    />
                  </div>

                  <div className="relative">
                    <div className="flex items-center justify-between mb-1">
                      <label className="block text-xs font-medium text-[#0F1B1A]/70">
                        CVV / CVC *
                      </label>
                      <button
                        type="button"
                        onClick={() => setShowCvvTooltip(!showCvvTooltip)}
                        className="text-[#0F1B1A]/50 hover:text-[#0F1B1A] transition-colors"
                        aria-label="CVV information"
                      >
                        <HelpCircle className="w-3.5 h-3.5" />
                      </button>
                    </div>

                    <input
                      type="password"
                      required
                      value={formData.cardCvv}
                      onChange={handleCvvChange}
                      maxLength={4}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-[#0F1B1A]/15 text-sm font-mono focus:border-[#E07A3E] focus:ring-1 focus:ring-[#E07A3E] outline-hidden text-[#0F1B1A]"
                      placeholder="123"
                    />

                    {showCvvTooltip && (
                      <div className="absolute right-0 bottom-full mb-2 w-48 p-2 bg-[#0F1B1A] text-white text-[11px] rounded-lg shadow-lg z-30 animate-in fade-in duration-150">
                        3-digit number on the back of your Visa/Mastercard, or 4 digits on the front of Amex.
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>

            {/* 5. Policies & Guarantees */}
            <div className="bg-white rounded-3xl p-6 sm:p-8 shadow-sm border border-[#0F1B1A]/8 space-y-4">
              <h2 className="font-serif text-lg font-semibold text-[#0F1B1A] flex items-center gap-2">
                <ShieldCheck className="w-5 h-5 text-[#2F5C52]" />
                Policies & Booking Guarantees
              </h2>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs text-[#0F1B1A]/80">
                <div className="p-3.5 rounded-xl bg-[#F7F6F2] border border-[#0F1B1A]/8">
                  <div className="font-semibold text-[#0F1B1A] mb-1 flex items-center gap-1.5">
                    <Clock className="w-3.5 h-3.5 text-[#2F5C52]" />
                    Check-in & Check-out
                  </div>
                  <div>Check-in: 2:00 PM</div>
                  <div>Check-out: 11:00 AM</div>
                </div>

                <div className="p-3.5 rounded-xl bg-emerald-50/70 border border-emerald-200/60">
                  <div className="font-semibold text-emerald-900 mb-1 flex items-center gap-1.5">
                    <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
                    Flexible Cancellation
                  </div>
                  <div>Free cancellation up to 48 hours prior to arrival date.</div>
                </div>
              </div>

              <label className="flex items-start gap-2.5 text-xs text-[#0F1B1A]/80 cursor-pointer pt-2 select-none">
                <input
                  type="checkbox"
                  required
                  checked={formData.termsAccepted}
                  onChange={(e) => setFormData({ ...formData, termsAccepted: e.target.checked })}
                  className="w-4 h-4 rounded text-[#E07A3E] focus:ring-[#E07A3E] border-[#0F1B1A]/20 mt-0.5"
                />
                <span>
                  I agree to the hotel rules, terms and conditions, and understand that my card will be authorized for this boutique reservation.
                </span>
              </label>

              {errorMessage && (
                <div className="p-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
                  <AlertCircle className="w-4 h-4 shrink-0" />
                  <span>{errorMessage}</span>
                </div>
              )}
            </div>
          </div>

          {/* ── RIGHT COLUMN: Sticky Price Summary (lg:col-span-5) ── */}
          <div className="lg:col-span-5 sticky top-24 space-y-6">
            <div className="bg-white rounded-3xl p-6 sm:p-7 shadow-sm border border-[#0F1B1A]/8 overflow-hidden">
              <h3 className="font-serif text-xl font-semibold text-[#0F1B1A] mb-4">
                Reservation Summary
              </h3>

              {/* Room Card Preview */}
              <div className="flex gap-3.5 pb-4 border-b border-[#0F1B1A]/10">
                <div className="relative w-20 h-20 rounded-xl overflow-hidden shrink-0 bg-[#0F1B1A]/5 shadow-xs">
                  <Image
                    src={roomListing.imageUrl || "/images/slowhouse-hero.jpg"}
                    alt={roomListing.name}
                    fill
                    className="object-cover"
                  />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <h4 className="font-serif text-base font-semibold text-[#0F1B1A] truncate">
                      {roomNameParam}
                    </h4>
                    {isUpgraded && (
                      <span className="px-2 py-0.5 rounded-full bg-[#E07A3E]/10 text-[#E07A3E] text-[10px] font-bold shrink-0">
                        Upgraded
                      </span>
                    )}
                  </div>
                  <div className="text-xs text-[#0F1B1A]/60 mt-0.5 truncate">{variantLabel}</div>
                  <div className="text-xs font-medium text-[#2F5C52] mt-1 line-clamp-1">{ratePlanName}</div>
                </div>
              </div>

              {/* Dates & Guests summary */}
              <div className="py-4 border-b border-[#0F1B1A]/10 space-y-2 text-xs text-[#0F1B1A]/75">
                <div className="flex items-center justify-between">
                  <span className="flex items-center gap-1.5 text-[#0F1B1A]/60">
                    <Calendar className="w-3.5 h-3.5" />
                    Dates
                  </span>
                  <span className="font-semibold text-[#0F1B1A]">
                    {checkIn} → {checkOut || "Next day"} ({nights} night{nights > 1 ? "s" : ""})
                  </span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="flex items-center gap-1.5 text-[#0F1B1A]/60">
                    <Users className="w-3.5 h-3.5" />
                    Occupancy
                  </span>
                  <span className="font-semibold text-[#0F1B1A]">
                    {guests} Guest{guests > 1 ? "s" : ""}, {roomsCount} Room{roomsCount > 1 ? "s" : ""}
                  </span>
                </div>
              </div>

              {/* Price Breakdown */}
              <div className="py-4 space-y-2.5 text-sm border-b border-[#0F1B1A]/10">
                <div className="flex justify-between text-[#0F1B1A]/75 text-xs">
                  <span>Room rate (${pricePerNight} × {nights} nights)</span>
                  <span className="font-medium text-[#0F1B1A]">${subtotal}</span>
                </div>

                {isUpgraded && priceDelta > 0 && (
                  <div className="flex justify-between text-xs text-emerald-700 font-medium">
                    <span className="flex items-center gap-1">
                      <Sparkles className="w-3 h-3 text-[#E07A3E]" />
                      Room upgrade difference
                    </span>
                    <span>+${priceDelta * nights}</span>
                  </div>
                )}

                <div className="flex justify-between text-[#0F1B1A]/75 text-xs">
                  <span>Property service fee (10%)</span>
                  <span>${serviceCharge}</span>
                </div>

                <div className="flex justify-between text-[#0F1B1A]/75 text-xs">
                  <span>Tourism VAT & Taxes (15%)</span>
                  <span>${governmentTax}</span>
                </div>
              </div>

              {/* Total Due */}
              <div className="py-4 flex items-baseline justify-between">
                <div>
                  <span className="text-xs text-[#0F1B1A]/60 block font-medium">Total Amount Due</span>
                  <span className="text-[11px] text-[#0F1B1A]/50">Includes all taxes & fees</span>
                </div>
                <div className="text-right">
                  <div className="font-serif text-2xl font-bold text-[#0F1B1A]">
                    ${totalAmount} <span className="text-xs font-sans font-normal text-[#0F1B1A]/60">USD</span>
                  </div>
                </div>
              </div>

              {/* SmartPoints Loyalty callout */}
              <div className="p-3 rounded-2xl bg-[#2F5C52]/10 border border-[#2F5C52]/20 flex items-center justify-between text-xs mb-5">
                <div className="flex items-center gap-2">
                  <Sparkles className="w-4 h-4 text-[#E07A3E]" />
                  <span className="font-medium text-[#2F5C52]">SmartPoints Earned</span>
                </div>
                <span className="font-bold text-[#2F5C52]">+{memberPointsEarned} pts</span>
              </div>

              {/* Error banner in summary column */}
              {errorMessage && (
                <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-start gap-2.5 mb-4 animate-in fade-in-50">
                  <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
                  <span>{errorMessage}</span>
                </div>
              )}

              {/* Complete Checkout CTA Button */}
              <button
                type="submit"
                disabled={isSubmitting}
                className="w-full py-3.5 px-4 rounded-xl bg-[#E07A3E] hover:bg-[#C4622D] active:scale-98 text-white font-medium text-sm shadow-md hover:shadow-lg transition-all flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isSubmitting ? (
                  <>
                    <div className="w-4 h-4 rounded-full border-2 border-white border-t-transparent animate-spin" />
                    <span>Processing Secure Reservation...</span>
                  </>
                ) : (
                  <>
                    <span>Continue</span>
                    <ChevronRight className="w-4 h-4" />
                  </>
                )}
              </button>

              <div className="mt-4 text-center">
                <span className="text-[11px] text-[#0F1B1A]/50 flex items-center justify-center gap-1">
                  <Lock className="w-3 h-3 text-emerald-600" />
                  Guaranteed safe checkout with 256-bit SSL encryption
                </span>
              </div>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
