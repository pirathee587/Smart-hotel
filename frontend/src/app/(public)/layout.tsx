"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import GuestSettingsSheet from "@/features/settings/components/GuestSettingsSheet";
import GuestChatWidget from "@/components/chat/GuestChatWidget";
import {
ChevronDown,
Mail,
MapPin,
Menu,
Phone,
ShoppingCart,
X,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import React from "react";

export default function PublicLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const pathname = usePathname();
  const [mobileOpen, setMobileOpen] = React.useState(false);
  const [aboutOpen, setAboutOpen] = React.useState(false);
  const [currencyOpen, setCurrencyOpen] = React.useState(false);
  const [langOpen, setLangOpen] = React.useState(false);
  const [currency, setCurrency] = React.useState("USD");
  const [language, setLanguage] = React.useState("ENGLISH - US");
  const [isSettingsOpen, setIsSettingsOpen] = React.useState(false);
  const { isAuthenticated, user, token, logout, initialize } = useAuthStore();

  React.useEffect(() => {
    initialize();
  }, [initialize]);

  // Click outside to close dropdowns
  React.useEffect(() => {
    const handleOutsideClick = () => {
      setAboutOpen(false);
      setCurrencyOpen(false);
      setLangOpen(false);
    };
    if (aboutOpen || currencyOpen || langOpen) {
      window.addEventListener("click", handleOutsideClick);
      return () => window.removeEventListener("click", handleOutsideClick);
    }
  }, [aboutOpen, currencyOpen, langOpen]);

  const isHomePage = pathname === "/";

  return (
    <div className="min-h-screen bg-[#0F1B1A] text-[#F6F1E6] flex flex-col font-sans antialiased">

      {/* ── NAVBAR (Matching booking engine specification) ──────────── */}
      {!isHomePage && (
        <header
          className="sticky top-0 z-50 w-full bg-[#0F1B1A]/95 backdrop-blur-md border-b border-[#2F5C52]/30 transition-all duration-300"
          role="banner"
        >
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-20 flex items-center justify-between gap-4">

            {/* Wordmark — Smart + Hotel italic */}
            <Link
              href="/"
              className="group flex items-baseline gap-1 text-white focus-visible:ring-2 focus-visible:ring-[#C4622D] focus-visible:outline-none rounded-sm shrink-0"
            >
              <span className="font-serif font-medium text-2xl tracking-tight text-white">
                Smart
              </span>
              <span className="font-serif italic text-2xl tracking-tight text-[#E07A3E]">
                Hotel
              </span>
            </Link>

            {/* Desktop Navigation: HOME, ROOMS, ABOUT, MY BOOKINGS, USD, ENGLISH - US, SIGN IN, Cart */}
            <nav className="hidden md:flex items-center gap-4 lg:gap-6 xl:gap-8 text-xs lg:text-[13px] font-semibold tracking-wider uppercase">
              {/* HOME */}
              <Link
                href="/"
                className={`transition-colors py-1 ${pathname === "/"
                    ? "text-[#E07A3E] font-bold"
                    : "text-[#EEE7D6]/90 hover:text-[#E07A3E]"
                  }`}
              >
                HOME
              </Link>

              {/* ROOMS */}
              <Link
                href="/rooms"
                className={`transition-colors py-1 ${pathname === "/rooms"
                    ? "text-[#E07A3E] font-bold"
                    : "text-[#EEE7D6]/90 hover:text-[#E07A3E]"
                  }`}
              >
                ROOMS
              </Link>

              {/* ABOUT ▼ */}
              <div className="relative" onClick={(e) => e.stopPropagation()}>
                <button
                  type="button"
                  suppressHydrationWarning
                  onClick={() => {
                    setAboutOpen(!aboutOpen);
                    setCurrencyOpen(false);
                    setLangOpen(false);
                  }}
                  className={`inline-flex items-center gap-1 transition-colors py-1 cursor-pointer ${aboutOpen
                      ? "text-[#E07A3E] font-bold"
                      : "text-[#EEE7D6]/90 hover:text-[#E07A3E]"
                    }`}
                  aria-expanded={aboutOpen}
                >
                  <span>ABOUT</span>
                  <ChevronDown
                    className={`w-3.5 h-3.5 transition-transform duration-200 ${aboutOpen ? "rotate-180 text-[#E07A3E]" : "text-[#EEE7D6]/60"
                      }`}
                  />
                </button>

                {aboutOpen && (
                  <div className="absolute top-full mt-3 left-0 w-52 bg-[#16302C] border border-[#2F5C52]/70 rounded-xl shadow-2xl py-2 z-50 animate-in fade-in slide-in-from-top-2">
                    <Link
                      href="/#residences"
                      onClick={() => setAboutOpen(false)}
                      className="block px-4 py-2 text-xs text-[#EEE7D6] hover:text-white hover:bg-[#2F5C52]/40 transition-colors normal-case tracking-normal"
                    >
                      Our Story &amp; Sanctuary
                    </Link>
                    <Link
                      href="/#experiences"
                      onClick={() => setAboutOpen(false)}
                      className="block px-4 py-2 text-xs text-[#EEE7D6] hover:text-white hover:bg-[#2F5C52]/40 transition-colors normal-case tracking-normal"
                    >
                      Experiences &amp; Dining
                    </Link>
                    <Link
                      href="/#contact"
                      onClick={() => setAboutOpen(false)}
                      className="block px-4 py-2 text-xs text-[#EEE7D6] hover:text-white hover:bg-[#2F5C52]/40 transition-colors normal-case tracking-normal"
                    >
                      Contact Concierge
                    </Link>
                  </div>
                )}
              </div>

              {/* MY BOOKINGS */}
              <Link
                href="/booking/checkout"
                className={`transition-colors py-1 whitespace-nowrap ${pathname.startsWith("/booking")
                    ? "text-[#E07A3E] font-bold"
                    : "text-[#EEE7D6]/90 hover:text-[#E07A3E]"
                  }`}
              >
                MY BOOKINGS
              </Link>

              {/* USD ▼ */}
              <div className="relative" onClick={(e) => e.stopPropagation()}>
                <button
                  type="button"
                  suppressHydrationWarning
                  onClick={() => {
                    setCurrencyOpen(!currencyOpen);
                    setAboutOpen(false);
                    setLangOpen(false);
                  }}
                  className={`inline-flex items-center gap-1 transition-colors py-1 cursor-pointer ${currencyOpen ? "text-[#E07A3E]" : "text-[#EEE7D6]/90 hover:text-[#E07A3E]"
                    }`}
                  aria-expanded={currencyOpen}
                >
                  <span>{currency}</span>
                  <ChevronDown
                    className={`w-3.5 h-3.5 transition-transform duration-200 ${currencyOpen ? "rotate-180 text-[#E07A3E]" : "text-[#EEE7D6]/60"
                      }`}
                  />
                </button>

                {currencyOpen && (
                  <div className="absolute top-full mt-3 right-0 w-36 bg-[#16302C] border border-[#2F5C52]/70 rounded-xl shadow-2xl py-1.5 z-50 animate-in fade-in slide-in-from-top-2">
                    {[
                      { code: "USD", symbol: "$" },
                      { code: "EUR", symbol: "€" },
                      { code: "GBP", symbol: "£" },
                      { code: "LKR", symbol: "Rs" },
                    ].map((item) => (
                      <button
                        key={item.code}
                        type="button"
                        suppressHydrationWarning
                        onClick={() => {
                          setCurrency(item.code);
                          setCurrencyOpen(false);
                        }}
                        className={`w-full flex items-center justify-between px-3.5 py-2 text-xs transition-colors cursor-pointer ${currency === item.code
                            ? "text-[#E07A3E] font-bold bg-[#2F5C52]/40"
                            : "text-[#EEE7D6] hover:bg-[#2F5C52]/30"
                          }`}
                      >
                        <span>{item.code}</span>
                        <span className="text-[#7C9188] text-[11px]">{item.symbol}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {/* ENGLISH - US ▼ */}
              <div className="relative" onClick={(e) => e.stopPropagation()}>
                <button
                  type="button"
                  suppressHydrationWarning
                  onClick={() => {
                    setLangOpen(!langOpen);
                    setAboutOpen(false);
                    setCurrencyOpen(false);
                  }}
                  className={`inline-flex items-center gap-1 transition-colors py-1 cursor-pointer whitespace-nowrap ${langOpen ? "text-[#E07A3E]" : "text-[#EEE7D6]/90 hover:text-[#E07A3E]"
                    }`}
                  aria-expanded={langOpen}
                >
                  <span>{language}</span>
                  <ChevronDown
                    className={`w-3.5 h-3.5 transition-transform duration-200 ${langOpen ? "rotate-180 text-[#E07A3E]" : "text-[#EEE7D6]/60"
                      }`}
                  />
                </button>

                {langOpen && (
                  <div className="absolute top-full mt-3 right-0 w-44 bg-[#16302C] border border-[#2F5C52]/70 rounded-xl shadow-2xl py-1.5 z-50 animate-in fade-in slide-in-from-top-2">
                    {[
                      { id: "ENGLISH - US", label: "English (US)" },
                      { id: "ENGLISH - UK", label: "English (UK)" },
                      { id: "TAMIL", label: "தமிழ் (Tamil)" },
                      { id: "SINHALA", label: "සිංහල (Sinhala)" },
                    ].map((item) => (
                      <button
                        key={item.id}
                        type="button"
                        suppressHydrationWarning
                        onClick={() => {
                          setLanguage(item.id);
                          setLangOpen(false);
                        }}
                        className={`w-full text-left px-3.5 py-2 text-xs transition-colors cursor-pointer normal-case tracking-normal ${language === item.id
                            ? "text-[#E07A3E] font-bold bg-[#2F5C52]/40"
                            : "text-[#EEE7D6] hover:bg-[#2F5C52]/30"
                          }`}
                      >
                        {item.label}
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {/* SIGN IN / ACCOUNT */}
              {isAuthenticated ? (
                <div className="flex items-center">
                  <button
                    type="button"
                    onClick={() => setIsSettingsOpen(true)}
                    className="text-xs text-[#EEE7D6]/90 hover:text-[#E07A3E] transition-colors font-semibold tracking-wider cursor-pointer flex items-center gap-1.5 px-2 py-1 rounded-md hover:bg-[#16302C]"
                    title="Open Guest Sanctuary Settings"
                  >
                    <span>{user?.name?.split(" ")[0] || "PIRATHEEPAN"}</span>
                  </button>
                </div>
              ) : (
                <Link
                  href="/login"
                  className={`transition-colors py-1 whitespace-nowrap ${pathname === "/login"
                      ? "text-[#E07A3E] font-bold"
                      : "text-[#EEE7D6]/90 hover:text-[#E07A3E]"
                    }`}
                >
                  SIGN IN
                </Link>
              )}

              {/* CART ICON 🛒 */}
              <Link
                href="/rooms"
                className="relative p-1.5 text-[#EEE7D6] hover:text-[#E07A3E] transition-colors cursor-pointer"
                title="Browse Rooms & Suites"
                aria-label="Booking cart"
              >
                <ShoppingCart className="w-4 h-4 lg:w-5 lg:h-5 stroke-[1.8]" />
              </Link>
            </nav>

            {/* Mobile Actions: Cart + Menu toggle */}
            <div className="flex md:hidden items-center gap-3">
              <Link
                href="/rooms"
                className="p-1.5 text-[#EEE7D6] hover:text-[#E07A3E] transition-colors"
                aria-label="Browse Rooms"
              >
                <ShoppingCart className="w-5 h-5 stroke-[1.8]" />
              </Link>
              <button
                type="button"
                suppressHydrationWarning
                onClick={() => setMobileOpen(!mobileOpen)}
                className="p-2 text-white hover:text-[#E07A3E] rounded-md focus-visible:ring-2 focus-visible:ring-[#C4622D] focus-visible:outline-none"
                aria-label="Toggle navigation menu"
                aria-expanded={mobileOpen}
              >
                {mobileOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
              </button>
            </div>
          </div>

          {/* Mobile drawer with complete menu */}
          {mobileOpen && (
            <div className="md:hidden bg-[#0F1B1A] border-b border-[#2F5C52]/40 px-6 py-6 space-y-4 animate-in fade-in duration-200 text-xs font-semibold tracking-wider uppercase">
              <Link
                href="/"
                onClick={() => setMobileOpen(false)}
                className={`block py-1.5 ${pathname === "/" ? "text-[#E07A3E]" : "text-[#EEE7D6] hover:text-[#E07A3E]"
                  }`}
              >
                HOME
              </Link>
              <Link
                href="/rooms"
                onClick={() => setMobileOpen(false)}
                className={`block py-1.5 ${pathname === "/rooms" ? "text-[#E07A3E] font-bold" : "text-[#EEE7D6] hover:text-[#E07A3E]"
                  }`}
              >
                ROOMS
              </Link>
              <div className="py-1">
                <span className="text-[#7C9188] text-[10px] block mb-1">ABOUT</span>
                <div className="pl-3 space-y-1.5 normal-case font-normal text-xs">
                  <Link
                    href="/#residences"
                    onClick={() => setMobileOpen(false)}
                    className="block text-[#EEE7D6] hover:text-[#E07A3E]"
                  >
                    Our Story &amp; Sanctuary
                  </Link>
                  <Link
                    href="/#experiences"
                    onClick={() => setMobileOpen(false)}
                    className="block text-[#EEE7D6] hover:text-[#E07A3E]"
                  >
                    Experiences &amp; Dining
                  </Link>
                  <Link
                    href="/#contact"
                    onClick={() => setMobileOpen(false)}
                    className="block text-[#EEE7D6] hover:text-[#E07A3E]"
                  >
                    Contact Concierge
                  </Link>
                </div>
              </div>
              <Link
                href="/booking/checkout"
                onClick={() => setMobileOpen(false)}
                className={`block py-1.5 ${pathname.startsWith("/booking")
                    ? "text-[#E07A3E] font-bold"
                    : "text-[#EEE7D6] hover:text-[#E07A3E]"
                  }`}
              >
                MY BOOKINGS
              </Link>
              {isAuthenticated ? (
                <div className="flex items-center justify-between py-1.5 text-xs">
                  <button
                    type="button"
                    onClick={() => {
                      setIsSettingsOpen(true);
                      setMobileOpen(false);
                    }}
                    className="text-[#EEE7D6] hover:text-[#E07A3E] font-medium text-left"
                  >
                    {user?.name || "Guest Account"} (Settings)
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      logout();
                      setMobileOpen(false);
                    }}
                    className="text-xs text-[#E07A3E] font-bold cursor-pointer"
                  >
                    SIGN OUT
                  </button>
                </div>
              ) : (
                <Link
                  href="/login"
                  onClick={() => setMobileOpen(false)}
                  className={`block py-1.5 ${pathname === "/login" ? "text-[#E07A3E] font-bold" : "text-[#EEE7D6] hover:text-[#E07A3E]"
                    }`}
                >
                  SIGN IN →
                </Link>
              )}
              <div className="pt-3 border-t border-[#2F5C52]/30 flex items-center justify-between text-[11px] text-[#7C9188] normal-case">
                <span>Currency: <strong className="text-[#EEE7D6]">{currency}</strong></span>
                <span>Language: <strong className="text-[#EEE7D6]">{language}</strong></span>
              </div>
            </div>
          )}
        </header>
      )}

      {/* Main content */}
      <main className="flex-1 flex flex-col relative overflow-hidden">
        {children}
      </main>

      {/* ── FOOTER (matches homepage footer exactly) ──────────────── */}
      {!isHomePage && (
        <footer id="contact" className="bg-[#0F1B1A] text-[#F6F1E6] pt-16 pb-12 border-t border-[#16302C]">
          <div className="max-w-7xl mx-auto px-6 md:px-12">
            {/* 4-Column Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-10 pb-16">

              {/* Brand column */}
              <div className="space-y-4">
                <Link href="/" className="inline-flex items-baseline gap-1 text-white">
                  <span className="font-serif font-medium text-2xl tracking-tight text-white">Smart</span>
                  <span className="font-serif italic text-2xl tracking-tight text-[#E07A3E]">Hotel</span>
                </Link>
                <p className="text-xs text-[#7C9188] leading-relaxed max-w-xs font-light">
                  A sanctuary of slow living and quiet intelligence set within the tea ridges of Maskeliya, Sri Lanka.
                </p>
                <div className="text-xs text-[#7C9188] space-y-1 pt-2">
                  <div className="flex items-center gap-2">
                    <MapPin className="w-3.5 h-3.5 text-[#C4622D] shrink-0" />
                    <span>Maskeliya, Central Highlands, Sri Lanka</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Mail className="w-3.5 h-3.5 text-[#C4622D] shrink-0" />
                    <span>reservations@smarthotel.com</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Phone className="w-3.5 h-3.5 text-[#C4622D] shrink-0" />
                    <span>+94 51 222 3456</span>
                  </div>
                </div>
              </div>

              {/* Guest Access */}
              <div className="space-y-3">
                <h4 className="font-serif text-sm font-semibold tracking-wider uppercase text-[#EEE7D6] border-b border-[#16302C] pb-2">
                  Guest Access
                </h4>
                <ul className="space-y-2 text-xs text-[#7C9188]">
                  <li><Link href="/rooms" className="hover:text-white transition-colors">Find My Stay</Link></li>
                  <li><Link href="/login" className="hover:text-white transition-colors">Sign In Credentials</Link></li>
                  <li><Link href="/rooms" className="hover:text-white transition-colors">Browse Rooms</Link></li>
                  <li><Link href="/rooms" className="hover:text-white transition-colors">Manage Booking</Link></li>
                </ul>
              </div>

              {/* Account */}
              <div className="space-y-3">
                <h4 className="font-serif text-sm font-semibold tracking-wider uppercase text-[#EEE7D6] border-b border-[#16302C] pb-2">
                  Account
                </h4>
                <ul className="space-y-2 text-xs text-[#7C9188]">
                  <li><Link href="/login" className="hover:text-white transition-colors">Guest Sign In</Link></li>
                  <li><Link href="/staff/login" className="hover:text-[#E07A3E] transition-colors font-medium">Staff &amp; Admin Portal</Link></li>
                  <li><Link href="/rooms" className="hover:text-[#E07A3E] transition-colors">Book a Stay</Link></li>
                </ul>
              </div>

              {/* Policies */}
              <div className="space-y-3">
                <h4 className="font-serif text-sm font-semibold tracking-wider uppercase text-[#EEE7D6] border-b border-[#16302C] pb-2">
                  Policies
                </h4>
                <ul className="space-y-2 text-xs text-[#7C9188]">
                  <li><Link href="/#contact" className="hover:text-white transition-colors">Cancellation Policy</Link></li>
                  <li><Link href="/#contact" className="hover:text-white transition-colors">Data Privacy &amp; GDPR</Link></li>
                  <li><Link href="/#contact" className="hover:text-white transition-colors">Terms of Stay</Link></li>
                  <li><Link href="/#contact" className="hover:text-white transition-colors">Contact Concierge</Link></li>
                </ul>
              </div>
            </div>

            {/* Bottom bar */}
            <div className="pt-8 border-t border-[#16302C] flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-[#7C9188]">
              <p>© {new Date().getFullYear()} SmartHotel Maskeliya. All rights reserved.</p>
              <p className="text-[11px] tracking-wide text-[#7C9188]/80">
                Slow living · Intelligent hospitality · Maskeliya, Central Highlands
              </p>
            </div>
          </div>
        </footer>
      )}

      {/* Guest Sanctuary Settings Half-Page Drawer */}
      <GuestSettingsSheet
        isOpen={isSettingsOpen}
        onClose={() => setIsSettingsOpen(false)}
      />

      {/* AI Concierge Chat Widget — Strictly Authenticated Customer Guests */}
      {isAuthenticated &&
        (user?.userType === "customer" ||
          String(user?.role).toLowerCase() === "customer") && (
          <GuestChatWidget
            token={token || ""}
            customerName={user?.name || "Guest"}
          />
        )}
    </div>
  );
}
