"use client";

import React, { useState, useRef, useEffect } from "react";
import Link from "next/link";
import Image from "next/image";

const NAV_ITEMS = [
  { label: "Residences", href: "#residences" },
  { label: "Wellness", href: "#wellness" },
  { label: "Experiences", href: "#experiences" },
  { label: "The Ridge", href: "#ridge" },
  { label: "Contact", href: "#contact" },
];
import {
  Menu,
  X,
  Plus,
  ArrowRight,
  ChevronLeft,
  ChevronRight,
  Check,
  Calendar as CalendarIcon,
  Users as UsersIcon,
  Sparkles,
  MapPin,
  Compass,
  Phone,
  Mail,
  Flame,
  Key,
  Utensils,
  Bot,
  Layers,
  Globe,
  Navigation
} from "lucide-react";
import { Map, MapMarker, MarkerContent, MarkerTooltip, MarkerLabel } from "@/components/ui/mapcn-marker-content";
import { CoverflowCarousel } from "@/components/ui/coverflow-carousel";
import { ReviewSection } from "@/components/ui/review-section";

// Suite types definition
interface Suite {
  id: string;
  name: string;
  price: number;
  image: string;
  specs: string;
  amenities: string[];
  description: string;
}

const SUITES: Suite[] = [
  {
    id: "standard-slowhouse",
    name: "Standard Slowhouse",
    price: 240,
    // TODO: replace with client photo
    image: "/images/slowhouse-interior.jpg",
    specs: "580 sq ft · 2 Guests · King Bed · Tea Terrace View",
    amenities: ["IoT Climate", "Rain Shower", "Highland Tea Bar", "Keyless Digital Key", "Forest Glass"],
    description: "Intimate timber slowhouse elevated above the tea contours, tailored for couples seeking silent retreat."
  },
  {
    id: "deluxe-slowhouse",
    name: "Deluxe Slowhouse",
    price: 290,
    // TODO: replace with client photo
    image: "/images/pool-thumb.jpg",
    specs: "820 sq ft · 2 Guests · Super King · Valley Panorama",
    amenities: ["Wood Hearth", "Soaking Tub", "Acoustic Silence", "IoT Climate", "Private Balcony"],
    description: "Expanded residence with an open-flame stone hearth and deep soaking tub overlooking the Maskeliya reservoir."
  },
  {
    id: "master-slowhouse",
    name: "Master Slowhouse",
    price: 340,
    // TODO: replace with client photo
    image: "/images/slowhouse-interior.jpg",
    specs: "1,250 sq ft · 2–4 Guests · Master Suite · Adam's Peak View",
    amenities: ["Private Cedar Hot Tub", "Keyless Digital Key", "IoT Climate", "Wood Hearth", "Forest Glass"],
    description: "Our flagship slowhouse featuring a sunken open-air cedar hot tub, wrap-around deck, and direct views of Adam's Peak."
  },
  {
    id: "forest-villa",
    name: "Forest Villa",
    price: 420,
    // TODO: replace with client photo
    image: "/images/hero-view.jpg",
    specs: "1,680 sq ft · 4 Guests · 2 En-Suite · 360° Canopy View",
    amenities: ["Cantilevered Plunge Pool", "Private Butler Service", "Stargazing Deck", "Smart Hearth", "Curated Wine Cellar"],
    description: "Two-bedroom architectural pavilion immersed in old-growth canopy with a cantilevered thermal plunge pool."
  }
];

export default function SmartHotelLandingPage() {
  // Active navigation section state
  const [activeSection, setActiveSection] = useState<string>("");

  // Mobile navigation drawer state
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  // Synchronize active section with scroll position
  useEffect(() => {
    const handleScroll = () => {
      const sectionIds = ["ridge", "residences", "wellness", "experiences", "contact"];
      const scrollPosition = window.scrollY;

      // When near bottom of page, highlight contact
      if (window.innerHeight + scrollPosition >= document.documentElement.scrollHeight - 100) {
        setActiveSection("#contact");
        return;
      }

      // If at very top (in hero), clear active section
      if (scrollPosition < 250) {
        setActiveSection("");
        return;
      }

      // Detect current visible section
      for (let i = sectionIds.length - 1; i >= 0; i--) {
        const id = sectionIds[i];
        const el = document.getElementById(id);
        if (el) {
          const rect = el.getBoundingClientRect();
          if (rect.top <= 220) {
            setActiveSection(`#${id}`);
            return;
          }
        }
      }
    };

    window.addEventListener("scroll", handleScroll, { passive: true });
    
    // Initial check on load/hash
    if (window.location.hash) {
      setActiveSection(window.location.hash);
    } else {
      handleScroll();
    }

    return () => {
      window.removeEventListener("scroll", handleScroll);
    };
  }, []);

  const handleNavClick = (e: React.MouseEvent<HTMLAnchorElement>, href: string) => {
    e.preventDefault();
    setActiveSection(href);
    const targetId = href.replace("#", "");
    const targetEl = document.getElementById(targetId);
    if (targetEl) {
      targetEl.scrollIntoView({ behavior: "smooth" });
      window.history.pushState(null, "", href);
    }
  };

  // Map view switcher: interactive MapLibre vs illustrated site plan
  const [mapViewMode, setMapViewMode] = useState<"interactive" | "illustrated">("interactive");

  // Residences view mode: "shelf" | "coverflow"
  const [residenceViewMode, setResidenceViewMode] = useState<"shelf" | "coverflow">("coverflow");

  // Coverflow slides for Residences showcase
  const coverflowSlides = [
    {
      src: "/images/slowhouse-interior.jpg",
      alt: "Master Slowhouse",
      title: "Master Slowhouse",
      subtitle: "From $340 / night · Signature Slowhouse",
      meta: [
        { label: "Dimensions", value: "1,250 sq ft" },
        { label: "Guests", value: "2–4 Guests" },
        { label: "View", value: "Direct Adam's Peak" },
        { label: "Signature", value: "Private Cedar Hot Tub" },
      ],
    },
    {
      src: "/images/pool-thumb.jpg",
      alt: "Deluxe Slowhouse",
      title: "Deluxe Slowhouse",
      subtitle: "From $290 / night · Alpine Suite",
      meta: [
        { label: "Dimensions", value: "820 sq ft" },
        { label: "Guests", value: "2 Guests" },
        { label: "Hearth", value: "Wood-Burning Stone Fire" },
        { label: "View", value: "Valley & Reservoir" },
      ],
    },
    {
      src: "/images/hero-view.jpg",
      alt: "Forest Villa",
      title: "Forest Villa",
      subtitle: "From $420 / night · Canopy Pavilion",
      meta: [
        { label: "Dimensions", value: "1,680 sq ft" },
        { label: "Guests", value: "4 Guests (2 En-Suite)" },
        { label: "Plunge", value: "Cantilevered Thermal Pool" },
        { label: "Service", value: "Private Butler Service" },
      ],
    },
    {
      src: "/images/slowhouse-interior.jpg",
      alt: "Standard Slowhouse",
      title: "Standard Slowhouse",
      subtitle: "From $240 / night · Contours Sanctuary",
      meta: [
        { label: "Dimensions", value: "580 sq ft" },
        { label: "Guests", value: "2 Guests" },
        { label: "Comfort", value: "IoT Circadian Climate" },
        { label: "View", value: "Tea Terrace Ridge" },
      ],
    },
    {
      src: "/images/keyless-entry.jpg",
      alt: "Keyless Digital Living",
      title: "Intelligent Slow Living",
      subtitle: "Quiet Ambient Technology",
      meta: [
        { label: "Access", value: "Encrypted Mobile Digital Key" },
        { label: "Heating", value: "Underfloor Geothermal" },
        { label: "Dining", value: "Heated In-Room Nook" },
      ],
    },
  ];

  // Booking Form State
  const [checkIn, setCheckIn] = useState("2026-09-15");
  const [checkOut, setCheckOut] = useState("2026-09-17");
  const [guests, setGuests] = useState("2 Adults, 0 Children");
  const [selectedSuiteId, setSelectedSuiteId] = useState("master-slowhouse");
  const [bookingFeedback, setBookingFeedback] = useState<string | null>(null);

  // Maskeliya geographic hotspots for MapLibre
  const maskeliyaHotspots = [
    {
      id: "smart-hotel-lodge",
      name: "SmartHotel Main Sanctuary",
      category: "Main Lodge & Reception",
      lng: 80.575,
      lat: 6.8336
    },
    {
      id: "residence-quarter",
      name: "Residence Quarter (Slowhouses)",
      category: "Slowhouses & Suites",
      lng: 80.571,
      lat: 6.836
    },
    {
      id: "spring-pools",
      name: "Highland Geothermal Spring Pools",
      category: "Thermal Hydrotherapy",
      lng: 80.578,
      lat: 6.8345
    },
    {
      id: "cedar-saunas",
      name: "Cedar Saunas & Cold Plunges",
      category: "Nordic Wellness",
      lng: 80.5795,
      lat: 6.8315
    },
    {
      id: "forest-villas",
      name: "Forest Villas (Canopy)",
      category: "Private Villas",
      lng: 80.569,
      lat: 6.829
    },
    {
      id: "observatory",
      name: "Adam's Peak Ridge Observatory",
      category: "Highland Viewpoint",
      lng: 80.584,
      lat: 6.838
    }
  ];

  // Suite shelf scroll reference
  const shelfRef = useRef<HTMLDivElement>(null);

  const scrollShelf = (direction: "left" | "right") => {
    if (shelfRef.current) {
      const scrollAmount = direction === "left" ? -360 : 360;
      shelfRef.current.scrollBy({ left: scrollAmount, behavior: "smooth" });
    }
  };

  // Calculate nights count
  const calculateNights = () => {
    try {
      const start = new Date(checkIn);
      const end = new Date(checkOut);
      const diffTime = end.getTime() - start.getTime();
      const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
      return diffDays > 0 ? diffDays : 2;
    } catch {
      return 2;
    }
  };

  const nights = calculateNights();
  const selectedSuite = SUITES.find((s) => s.id === selectedSuiteId) || SUITES[2];
  const estimatedTotal = selectedSuite.price * nights;

  const handleBookingSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setBookingFeedback(
      `Availability confirmed for ${selectedSuite.name} (${nights} nights, $${estimatedTotal} total). Redirecting to secure guest setup...`
    );
    setTimeout(() => {
      window.location.href = `/portal/access?suite=${selectedSuite.id}&in=${checkIn}&out=${checkOut}&nights=${nights}`;
    }, 1800);
  };

  return (
    <div className="min-h-screen bg-[#F6F1E6] text-[#3A362E] font-sans antialiased selection:bg-[#C4622D] selection:text-white">
      {/* ─────────────────────────────────────────────────────────────
          1. HEADER (Fixed/Sticky, transparent over hero)
      ───────────────────────────────────────────────────────────── */}
      <header
        className="fixed top-0 left-0 right-0 z-50 transition-all duration-300 bg-[#0F1B1A]/85 backdrop-blur-md border-b border-[#2F5C52]/30"
        role="banner"
      >
        <div className="max-w-7xl mx-auto px-6 h-20 flex items-center justify-between">
          {/* Wordmark */}
          <Link
            href="/"
            className="group flex items-baseline gap-1 text-white focus-visible:ring-2 focus-visible:ring-[#C4622D] focus-visible:outline-none rounded-sm"
          >
            <span className="font-serif font-medium text-2xl tracking-tight text-white">Smart</span>
            <span className="font-serif italic text-2xl tracking-tight text-[#E07A3E]">Hotel</span>
          </Link>

          {/* Desktop Nav Links */}
          <nav
            className="hidden md:flex items-center space-x-8 text-sm tracking-wide"
            aria-label="Main Navigation"
          >
            {NAV_ITEMS.map((item) => {
              const isActive = activeSection === item.href;
              return (
                <a
                  key={item.href}
                  href={item.href}
                  onClick={(e) => handleNavClick(e, item.href)}
                  className={`relative py-1 transition-all duration-200 focus-visible:ring-2 focus-visible:ring-[#C4622D] focus-visible:outline-none ${
                    isActive
                      ? "text-white font-semibold"
                      : "text-[#EEE7D6]/80 font-medium hover:text-white"
                  }`}
                >
                  {item.label}
                  {isActive && (
                    <span className="absolute bottom-0 left-0 right-0 h-[2px] bg-[#C4622D] transition-all duration-300" />
                  )}
                </a>
              );
            })}
          </nav>

          {/* Header Right Actions */}
          <div className="flex items-center gap-4">
            {/* Pill Button: Book a Stay */}
            <Link
              href="/login"
              className="group inline-flex items-center gap-2.5 bg-[#F6F1E6] hover:bg-white text-[#0F1B1A] font-medium text-xs tracking-wider uppercase pl-5 pr-2 py-2 rounded-full transition-all duration-200 shadow-md hover:shadow-lg focus-visible:ring-2 focus-visible:ring-[#C4622D] focus-visible:outline-none cursor-pointer"
            >
              <span>Book a Stay</span>
              <span className="flex items-center justify-center w-6 h-6 rounded-full bg-[#C4622D] text-white transition-transform duration-200 group-hover:scale-110">
                <Plus className="w-3.5 h-3.5 stroke-[2.5]" />
              </span>
            </Link>

            {/* Mobile Hamburger Button */}
            <button
              type="button"
              onClick={() => setMobileNavOpen(!mobileNavOpen)}
              className="md:hidden p-2 text-white hover:text-[#E07A3E] focus-visible:ring-2 focus-visible:ring-[#C4622D] focus-visible:outline-none rounded-md"
              aria-label="Toggle navigation menu"
              aria-expanded={mobileNavOpen}
            >
              {mobileNavOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
            </button>
          </div>
        </div>

        {/* Mobile Navigation Drawer */}
        {mobileNavOpen && (
          <div className="md:hidden bg-[#0F1B1A] border-b border-[#2F5C52]/40 px-6 py-6 space-y-4 animate-in fade-in duration-200">
            <nav className="flex flex-col space-y-3 text-base text-[#EEE7D6]">
              {NAV_ITEMS.map((item) => {
                const isActive = activeSection === item.href;
                return (
                  <a
                    key={item.href}
                    href={item.href}
                    onClick={(e) => {
                      handleNavClick(e, item.href);
                      setMobileNavOpen(false);
                    }}
                    className={`py-2 transition-colors flex items-center justify-between ${
                      isActive
                        ? "text-[#E07A3E] font-semibold"
                        : "hover:text-[#E07A3E]"
                    }`}
                  >
                    <span>{item.label}</span>
                    {isActive && <span className="w-2 h-2 rounded-full bg-[#C4622D]" />}
                  </a>
                );
              })}
              <div className="pt-3 border-t border-[#16302C] flex flex-col gap-2">
                <Link
                  href="/portal/access"
                  onClick={() => setMobileNavOpen(false)}
                  className="text-xs text-[#7C9188] hover:text-white py-1"
                >
                  Guest Access Portal →
                </Link>
                <Link
                  href="/login"
                  onClick={() => setMobileNavOpen(false)}
                  className="text-xs text-[#7C9188] hover:text-white py-1"
                >
                  Staff Sign In →
                </Link>
              </div>
            </nav>
          </div>
        )}
      </header>

      {/* ─────────────────────────────────────────────────────────────
          2. HERO SECTION WITH INTEGRATED LUXURY BOOKING BAR
      ───────────────────────────────────────────────────────────── */}
      <section
        id="book"
        className="relative min-h-[780px] lg:min-h-[860px] w-full flex flex-col justify-between overflow-hidden bg-[#0F1B1A] pt-28 pb-8 md:pb-12 scroll-mt-20"
        aria-label="Welcome to SmartHotel Maskeliya"
      >
        {/* Full-bleed background photo */}
        <div className="absolute inset-0 z-0">
          {/* TODO: replace with client photo */}
          <Image
            src="/images/hero-view.jpg"
            alt="SmartHotel Maskeliya infinity pool overlooking misty green terraced tea hills"
            fill
            priority
            className="object-cover object-center scale-[1.02] transform transition-transform duration-1000 ease-out"
          />
        </div>

        {/* Vertical dark gradient overlay */}
        <div
          className="absolute inset-0 z-10 pointer-events-none"
          style={{
            background:
              "linear-gradient(180deg, rgba(15, 27, 26, 0.45) 0%, rgba(15, 27, 26, 0.5) 40%, rgba(15, 27, 26, 0.88) 100%)"
          }}
        />

        {/* Hero Top/Middle Content: Bottom-left aligned within max-width container */}
        <div className="relative z-20 w-full max-w-7xl mx-auto px-6 md:px-12 pt-8 md:pt-14 pb-8">
          <div className="max-w-3xl space-y-5">
            {/* Small eyebrow row */}
            <div className="flex items-center gap-3">
              <span className="w-6 h-[2px] bg-[#C4622D] inline-block" />
              <span className="text-xs md:text-sm font-medium tracking-wide uppercase text-[#EEE7D6]/90">
                Maskeliya, Central Highlands, Sri Lanka
              </span>
            </div>

            {/* H1 Heading */}
            <h1 className="font-serif text-4xl sm:text-6xl md:text-7xl lg:text-[80px] font-normal leading-[1.05] tracking-tight text-[#F6F1E6]">
              Where slow living meets <span className="italic text-[#E07A3E]">intelligent</span> hospitality.
            </h1>

            {/* Subhead */}
            <p className="text-sm sm:text-base md:text-lg text-[#F6F1E6]/75 max-w-2xl font-light leading-relaxed">
              Twelve slowhouses set into the misty tea hills, built around spring-fed warmth and quiet technology.
            </p>

            {/* Quick Action Badges */}
            <div className="flex flex-wrap items-center gap-3 pt-1">
              <a
                href="#residences"
                className="group inline-flex items-center gap-2.5 bg-[#F6F1E6]/90 hover:bg-white text-[#0F1B1A] font-medium text-xs tracking-wider uppercase pl-4 pr-2 py-2 rounded-full transition-all duration-200 shadow-lg backdrop-blur-sm focus-visible:ring-2 focus-visible:ring-[#C4622D] focus-visible:outline-none"
              >
                <span>Explore Residences</span>
                <span className="flex items-center justify-center w-5 h-5 rounded-full bg-[#C4622D] text-white transition-transform duration-200 group-hover:scale-110">
                  <Plus className="w-3 h-3 stroke-[2.5]" />
                </span>
              </a>
              <span className="hidden sm:inline-block text-xs text-[#EEE7D6]/60">
                • Spring-fed thermal pools &amp; private tea ridges
              </span>
            </div>
          </div>
        </div>

        {/* Hero Bottom Docked Luxury Booking Bar (Senior UI/UX Floating Treatment) */}
        <div className="relative z-20 w-full max-w-7xl mx-auto px-6 md:px-12 mt-auto">
          <div className="bg-[#0F1B1A]/92 backdrop-blur-xl border border-[#2F5C52]/40 shadow-2xl p-5 md:p-6 text-[#F6F1E6]">
            {/* Sub-header inside bar */}
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1.5 pb-3.5 mb-4 border-b border-[#16302C]/90 text-xs">
              <div className="flex items-center gap-2">
                <span className="w-1.5 h-1.5 rounded-full bg-[#E07A3E] animate-pulse" />
                <span className="text-[11px] uppercase tracking-widest text-[#E07A3E] font-medium">
                  Direct Reservation Sanctuary
                </span>
              </div>
              <div className="text-[11px] text-[#7C9188] flex items-center gap-2">
                <span>Best Rate Guarantee</span>
                <span>•</span>
                <span>Private Airport Transfer Included</span>
              </div>
            </div>

            {/* 5-Column Booking Form */}
            <form
              onSubmit={handleBookingSubmit}
              className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4 lg:gap-0 items-center"
            >
              {/* Field 1: Check-in */}
              <div className="lg:pr-4 lg:border-r lg:border-[#16302C] space-y-1">
                <label
                  htmlFor="hero-checkin"
                  className="text-[11px] text-[#7C9188] uppercase tracking-wider font-medium flex items-center gap-1.5"
                >
                  <CalendarIcon className="w-3.5 h-3.5 text-[#C4622D]" />
                  Check-in
                </label>
                <input
                  id="hero-checkin"
                  type="date"
                  value={checkIn}
                  onChange={(e) => setCheckIn(e.target.value)}
                  className="w-full bg-[#16302C]/80 border border-[#2F5C52]/50 text-sm text-[#F6F1E6] px-3 py-2 focus:outline-none focus:border-[#C4622D] rounded-none cursor-pointer"
                  required
                />
                <p className="text-[10px] text-[#7C9188]">Min. 2-night stay</p>
              </div>

              {/* Field 2: Check-out */}
              <div className="lg:px-4 lg:border-r lg:border-[#16302C] space-y-1">
                <label
                  htmlFor="hero-checkout"
                  className="text-[11px] text-[#7C9188] uppercase tracking-wider font-medium flex items-center gap-1.5"
                >
                  <CalendarIcon className="w-3.5 h-3.5 text-[#C4622D]" />
                  Check-out
                </label>
                <input
                  id="hero-checkout"
                  type="date"
                  value={checkOut}
                  onChange={(e) => setCheckOut(e.target.value)}
                  className="w-full bg-[#16302C]/80 border border-[#2F5C52]/50 text-sm text-[#F6F1E6] px-3 py-2 focus:outline-none focus:border-[#C4622D] rounded-none cursor-pointer"
                  required
                />
                <p className="text-[10px] text-[#E07A3E] font-medium">{nights} nights selected</p>
              </div>

              {/* Field 3: Guests */}
              <div className="lg:px-4 lg:border-r lg:border-[#16302C] space-y-1">
                <label
                  htmlFor="hero-guests"
                  className="text-[11px] text-[#7C9188] uppercase tracking-wider font-medium flex items-center gap-1.5"
                >
                  <UsersIcon className="w-3.5 h-3.5 text-[#C4622D]" />
                  Guests
                </label>
                <select
                  id="hero-guests"
                  value={guests}
                  onChange={(e) => setGuests(e.target.value)}
                  className="w-full bg-[#16302C]/80 border border-[#2F5C52]/50 text-sm text-[#F6F1E6] px-3 py-2 focus:outline-none focus:border-[#C4622D] rounded-none cursor-pointer"
                >
                  <option value="1 Adult, 0 Children">1 Adult</option>
                  <option value="2 Adults, 0 Children">2 Adults</option>
                  <option value="2 Adults, 1 Child">2 Adults, 1 Child</option>
                  <option value="4 Adults, 0 Children">4 Adults (Villa)</option>
                </select>
                <p className="text-[10px] text-[#7C9188]">Adults &amp; guests</p>
              </div>

              {/* Field 4: Residence Type */}
              <div className="lg:px-4 lg:border-r lg:border-[#16302C] space-y-1">
                <label
                  htmlFor="hero-residence-type"
                  className="text-[11px] text-[#7C9188] uppercase tracking-wider font-medium flex items-center gap-1.5"
                >
                  <Sparkles className="w-3.5 h-3.5 text-[#C4622D]" />
                  Residence Type
                </label>
                <select
                  id="hero-residence-type"
                  value={selectedSuiteId}
                  onChange={(e) => setSelectedSuiteId(e.target.value)}
                  className="w-full bg-[#16302C]/80 border border-[#2F5C52]/50 text-sm text-[#F6F1E6] px-3 py-2 focus:outline-none focus:border-[#C4622D] rounded-none cursor-pointer"
                >
                  {SUITES.map((suite) => (
                    <option key={suite.id} value={suite.id}>
                      {suite.name} (${suite.price}/nt)
                    </option>
                  ))}
                </select>
                <p className="text-[10px] text-[#E07A3E] font-medium">Est. ${estimatedTotal} total</p>
              </div>

              {/* Field 5: Action Button */}
              <div className="lg:pl-4 flex flex-col justify-end pt-1 lg:pt-0">
                <button
                  type="submit"
                  className="w-full bg-[#C4622D] hover:bg-[#E07A3E] text-white font-medium text-xs tracking-wider uppercase py-3.5 px-5 rounded-full transition-all duration-200 shadow-md hover:shadow-lg focus-visible:ring-2 focus-visible:ring-white focus-visible:outline-none cursor-pointer flex items-center justify-center gap-2 group/btn"
                >
                  <span>Check Availability</span>
                  <ArrowRight className="w-4 h-4 transition-transform duration-200 group-hover/btn:translate-x-1" />
                </button>
              </div>
            </form>

            {bookingFeedback && (
              <div className="mt-3 p-2.5 bg-[#16302C] border border-[#C4622D] text-xs text-[#F6F1E6] flex items-center gap-2 animate-in fade-in">
                <Check className="w-4 h-4 text-[#E07A3E] shrink-0" />
                <span>{bookingFeedback}</span>
              </div>
            )}
          </div>
        </div>
      </section>

      {/* ─────────────────────────────────────────────────────────────
          3. TORN-EDGE TRANSITION (Signature SVG Watercolor filter)
      ───────────────────────────────────────────────────────────── */}
      <div
        className="relative w-full overflow-hidden bg-[#0F1B1A] select-none pointer-events-none -mt-1"
        aria-hidden="true"
      >
        <svg
          className="w-full h-14 md:h-24 block"
          viewBox="0 0 1440 90"
          preserveAspectRatio="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          <defs>
            <filter id="torn-edge-filter" x="0" y="0" width="100%" height="100%">
              <feTurbulence type="fractalNoise" baseFrequency="0.035 0.08" numOctaves="4" result="noise" seed="7" />
              <feDisplacementMap in="SourceGraphic" in2="noise" scale="16" xChannelSelector="R" yChannelSelector="G" />
            </filter>
          </defs>
          {/* Layer 1: Dark Cedar tone behind */}
          <path
            d="M0,15 Q360,35 720,18 T1440,25 L1440,90 L0,90 Z"
            fill="#6B4A3A"
            filter="url(#torn-edge-filter)"
            opacity="0.95"
          />
          {/* Layer 2: Paper Cream tone on top */}
          <path
            d="M0,32 Q360,52 720,28 T1440,36 L1440,90 L0,90 Z"
            fill="#F6F1E6"
            filter="url(#torn-edge-filter)"
          />
        </svg>
      </div>

      {/* ─────────────────────────────────────────────────────────────
          4. DISCOVER / MAP SECTION (Doubles as Location)
      ───────────────────────────────────────────────────────────── */}
      <section id="ridge" className="py-16 md:py-24 bg-[#F6F1E6] scroll-mt-24">
        <div className="max-w-7xl mx-auto px-6 md:px-12">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 lg:gap-16 items-center">
            {/* Left Column */}
            <div className="lg:col-span-5 space-y-6">
              {/* Eyebrow */}
              <div className="flex items-center gap-3">
                <span className="w-6 h-[2px] bg-[#C4622D] inline-block" />
                <span className="text-xs font-semibold tracking-wider text-[#6B4A3A] uppercase">
                  Find the Ridge
                </span>
              </div>

              {/* H2 */}
              <h2 className="font-serif text-3xl sm:text-4xl md:text-5xl font-normal leading-tight text-[#0F1B1A]">
                Discover <span className="italic text-[#6B4A3A]">the Reserve</span>.
              </h2>

              {/* Property Description */}
              <p className="text-[#3A362E] text-base leading-relaxed">
                Twelve slowhouses thoughtfully distributed across three distinct ecological quarters: the secluded
                Residence Quarter perched along the tea ridge, the geothermal Wellness Quarter fed by natural mountain
                springs, and the untouched Old-Growth Forest Sanctuary.
              </p>

              {/* Email Pill Link */}
              <div className="pt-2">
                <a
                  href="mailto:reservations@smarthotel.com"
                  className="inline-flex items-center gap-2 border border-[#6B4A3A]/40 hover:border-[#6B4A3A] text-[#0F1B1A] px-5 py-2.5 rounded-full text-xs font-medium tracking-wide transition-all duration-200 hover:bg-[#EEE7D6] focus-visible:ring-2 focus-visible:ring-[#C4622D] focus-visible:outline-none"
                >
                  <Mail className="w-3.5 h-3.5 text-[#C4622D]" />
                  <span>reservations@smarthotel.com</span>
                </a>
              </div>

              {/* Phone & Transfer Time */}
              <div className="pt-2 border-t border-[#6B4A3A]/20 text-xs text-[#3A362E]/80 space-y-1.5 font-light">
                <p className="flex items-center gap-2">
                  <Phone className="w-3.5 h-3.5 text-[#C4622D]" />
                  <span className="font-medium text-[#0F1B1A]">+94 51 222 3456</span>
                </p>
                <p className="text-[#3A362E]/70 pl-5.5">
                  3.5 hrs from Bandaranaike International Airport, Colombo (private transfer included)
                </p>
              </div>
            </div>

            {/* Right Column: Interactive MapLibre Map & Hand-drawn Site Map */}
            <div className="lg:col-span-7">
              <div className="relative bg-[#EEE7D6] border border-[#6B4A3A]/20 p-5 md:p-6 shadow-sm">
                {/* Header Controls: Map Toggle & Distance Badge */}
                <div className="flex items-center justify-between gap-3 mb-4 flex-wrap">
                  {/* View Mode Toggle Pill Buttons */}
                  <div className="inline-flex items-center bg-[#F6F1E6] p-1 rounded-full border border-[#6B4A3A]/20 shadow-xs">
                    <button
                      type="button"
                      onClick={() => setMapViewMode("interactive")}
                      className={`px-3.5 py-1.5 rounded-full text-xs font-medium transition-all duration-200 flex items-center gap-1.5 cursor-pointer ${
                        mapViewMode === "interactive"
                          ? "bg-[#0F1B1A] text-[#F6F1E6] shadow-sm"
                          : "text-[#3A362E]/70 hover:text-[#0F1B1A]"
                      }`}
                    >
                      <Globe className="w-3.5 h-3.5 text-[#C4622D]" />
                      <span>Interactive Highlands Map</span>
                    </button>
                    <button
                      type="button"
                      onClick={() => setMapViewMode("illustrated")}
                      className={`px-3.5 py-1.5 rounded-full text-xs font-medium transition-all duration-200 flex items-center gap-1.5 cursor-pointer ${
                        mapViewMode === "illustrated"
                          ? "bg-[#0F1B1A] text-[#F6F1E6] shadow-sm"
                          : "text-[#3A362E]/70 hover:text-[#0F1B1A]"
                      }`}
                    >
                      <Layers className="w-3.5 h-3.5 text-[#C4622D]" />
                      <span>Illustrated Reserve Plan</span>
                    </button>
                  </div>

                  {/* Distance Badge Chip */}
                  <div className="flex items-center gap-1.5 bg-[#0F1B1A] text-[#F6F1E6] px-3 py-1 rounded-full text-[11px] font-medium shadow-md">
                    <span className="w-1.5 h-1.5 rounded-full bg-[#E07A3E] animate-pulse" />
                    <span>3.5 hrs · Colombo</span>
                  </div>
                </div>

                {/* Main Map Display Area */}
                <div className="relative w-full aspect-[4/3] min-h-[380px] max-h-[460px] overflow-hidden border border-[#6B4A3A]/15 bg-[#0F1B1A]">
                  {mapViewMode === "interactive" ? (
                    /* Interactive MapLibre GL Map */
                    <div className="w-full h-full relative">
                      <Map
                        theme="dark"
                        center={[80.575, 6.8336]}
                        zoom={13.2}
                        className="w-full h-full"
                      >
                        {maskeliyaHotspots.map((spot) => (
                          <MapMarker
                            key={spot.id}
                            longitude={spot.lng}
                            latitude={spot.lat}
                          >
                            <MarkerContent>
                              <div className="group/marker relative flex items-center justify-center">
                                {/* Outer radar pulse */}
                                <span className="absolute -inset-2 rounded-full bg-[#C4622D]/20 animate-ping" />
                                {/* Main Marker Node */}
                                <div
                                  data-mapcn-marker={spot.name}
                                  className="relative size-6 rounded-full border-2 border-white bg-[#C4622D] shadow-xl flex items-center justify-center text-[10px] text-white font-bold transition-transform duration-200 group-hover/marker:scale-125 group-hover/marker:bg-[#E07A3E]"
                                >
                                  <MapPin className="w-3.5 h-3.5 fill-white stroke-[#C4622D]" />
                                </div>
                              </div>
                            </MarkerContent>
                            <MarkerTooltip offset={12}>
                              <div className="p-1 space-y-0.5 max-w-[200px]">
                                <p className="font-serif font-semibold text-xs text-[#0F1B1A]">{spot.name}</p>
                                <p className="text-[10px] text-[#C4622D] uppercase tracking-wider">{spot.category}</p>
                              </div>
                            </MarkerTooltip>
                          </MapMarker>
                        ))}
                      </Map>

                      {/* Map overlay hint */}
                      <div className="absolute bottom-3 left-3 z-10 bg-[#0F1B1A]/85 backdrop-blur-sm text-[#F6F1E6] px-3 py-1.5 rounded-md text-[11px] flex items-center gap-1.5 shadow-md border border-white/10 pointer-events-none">
                        <Navigation className="w-3 h-3 text-[#E07A3E]" />
                        <span>Maskeliya Ridge, Central Highlands, Sri Lanka</span>
                      </div>
                    </div>
                  ) : (
                    /* Hand-drawn style SVG site map */
                    <svg
                      viewBox="0 0 700 500"
                      className="w-full h-full bg-[#EEE7D6]"
                      fill="none"
                      xmlns="http://www.w3.org/2000/svg"
                      aria-label="SmartHotel Maskeliya Site Map"
                    >
                      {/* Organic Elevation Contour Lines */}
                      <path
                        d="M20,100 C150,80 280,140 450,110 C580,90 650,150 680,180"
                        stroke="#7C9188"
                        strokeWidth="1"
                        strokeDasharray="4 4"
                        opacity="0.4"
                      />
                      <path
                        d="M30,220 C180,200 320,260 480,210 C600,180 670,260 690,300"
                        stroke="#7C9188"
                        strokeWidth="1"
                        strokeDasharray="4 4"
                        opacity="0.4"
                      />
                      <path
                        d="M10,360 C160,330 300,400 460,340 C570,300 660,380 680,420"
                        stroke="#7C9188"
                        strokeWidth="1"
                        strokeDasharray="4 4"
                        opacity="0.4"
                      />

                      {/* Irregular property boundary */}
                      <path
                        d="M70,80 C190,40 460,50 600,90 C660,150 640,320 590,420 C460,460 220,440 100,400 C40,330 30,170 70,80 Z"
                        fill="#F6F1E6"
                        stroke="#6B4A3A"
                        strokeWidth="1.75"
                      />

                      {/* Sub-region: Tinted Sage-Green Wellness Quarter */}
                      <path
                        d="M280,160 C380,150 490,170 510,250 C480,310 360,330 270,290 C240,240 250,180 280,160 Z"
                        fill="#7C9188"
                        fillOpacity="0.25"
                        stroke="#2F5C52"
                        strokeWidth="1.25"
                        strokeDasharray="3 3"
                      />
                      <text x="350" y="245" fill="#2F5C52" fontSize="11" fontFamily="sans-serif" fontWeight="600" opacity="0.8">
                        WELLNESS QUARTER
                      </text>

                      {/* Maskeliya Ridge Stream / Water Path */}
                      <path
                        d="M110,95 Q200,200 240,290 T380,420"
                        stroke="#2F5C52"
                        strokeWidth="2.5"
                        strokeOpacity="0.5"
                        strokeLinecap="round"
                      />
                      <text x="140" y="160" fill="#2F5C52" fontSize="9" fontFamily="sans-serif" fontStyle="italic">
                        Spring-fed stream
                      </text>

                      {/* Pin 1: Residence Quarter */}
                      <g transform="translate(180, 130)">
                        <circle cx="0" cy="0" r="4.5" fill="#C4622D" />
                        <circle cx="0" cy="0" r="8" stroke="#C4622D" strokeWidth="1" strokeOpacity="0.5" />
                        <line x1="0" y1="0" x2="-60" y2="-40" stroke="#6B4A3A" strokeWidth="1" />
                        <circle cx="-60" cy="-40" r="2" fill="#6B4A3A" />
                        <text x="-145" y="-36" fill="#0F1B1A" fontSize="12" fontWeight="600" fontFamily="serif">
                          Residence Quarter
                        </text>
                      </g>

                      {/* Pin 2: Highland Spring Pools */}
                      <g transform="translate(340, 200)">
                        <circle cx="0" cy="0" r="4.5" fill="#C4622D" />
                        <circle cx="0" cy="0" r="8" stroke="#C4622D" strokeWidth="1" strokeOpacity="0.5" />
                        <line x1="0" y1="0" x2="50" y2="-50" stroke="#6B4A3A" strokeWidth="1" />
                        <circle cx="50" cy="-50" r="2" fill="#6B4A3A" />
                        <text x="58" y="-46" fill="#0F1B1A" fontSize="12" fontWeight="600" fontFamily="serif">
                          Highland Spring Pools
                        </text>
                      </g>

                      {/* Pin 3: Cedar Saunas */}
                      <g transform="translate(440, 270)">
                        <circle cx="0" cy="0" r="4.5" fill="#C4622D" />
                        <circle cx="0" cy="0" r="8" stroke="#C4622D" strokeWidth="1" strokeOpacity="0.5" />
                        <line x1="0" y1="0" x2="60" y2="30" stroke="#6B4A3A" strokeWidth="1" />
                        <circle cx="60" cy="30" r="2" fill="#6B4A3A" />
                        <text x="68" y="34" fill="#0F1B1A" fontSize="12" fontWeight="600" fontFamily="serif">
                          Cedar Saunas
                        </text>
                      </g>

                      {/* Pin 4: Forest Villas */}
                      <g transform="translate(210, 360)">
                        <circle cx="0" cy="0" r="4.5" fill="#C4622D" />
                        <circle cx="0" cy="0" r="8" stroke="#C4622D" strokeWidth="1" strokeOpacity="0.5" />
                        <line x1="0" y1="0" x2="-70" y2="40" stroke="#6B4A3A" strokeWidth="1" />
                        <circle cx="-70" cy="40" r="2" fill="#6B4A3A" />
                        <text x="-150" y="44" fill="#0F1B1A" fontSize="12" fontWeight="600" fontFamily="serif">
                          Forest Villas
                        </text>
                      </g>

                      {/* Pin 5: Observatory */}
                      <g transform="translate(540, 130)">
                        <circle cx="0" cy="0" r="4.5" fill="#C4622D" />
                        <circle cx="0" cy="0" r="8" stroke="#C4622D" strokeWidth="1" strokeOpacity="0.5" />
                        <line x1="0" y1="0" x2="30" y2="-30" stroke="#6B4A3A" strokeWidth="1" />
                        <circle cx="30" cy="-30" r="2" fill="#6B4A3A" />
                        <text x="38" y="-26" fill="#0F1B1A" fontSize="12" fontWeight="600" fontFamily="serif">
                          Observatory
                        </text>
                      </g>
                    </svg>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ─────────────────────────────────────────────────────────────
          6. RESIDENCES (Suites Horizontal Scroll-Snap Shelf & 3D Coverflow)
      ───────────────────────────────────────────────────────────── */}
      <section id="residences" className="py-20 md:py-28 bg-[#F6F1E6] scroll-mt-20">
        <div className="max-w-7xl mx-auto px-6 md:px-12">
          {/* Asymmetric Header */}
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-6 mb-12">
            <div className="space-y-3 max-w-xl">
              <div className="flex items-center gap-3">
                <span className="w-6 h-[2px] bg-[#C4622D] inline-block" />
                <span className="text-xs font-semibold tracking-wider text-[#6B4A3A] uppercase">
                  Residences
                </span>
              </div>
              <h2 className="font-serif text-3xl sm:text-4xl md:text-5xl font-normal leading-tight text-[#0F1B1A]">
                Four ways to <span className="italic text-[#6B4A3A]">stay slow</span>.
              </h2>
            </div>
            <div className="md:max-w-md flex flex-col justify-between items-start md:items-end gap-4">
              <p className="text-xs md:text-sm text-[#3A362E]/80 md:text-right leading-relaxed">
                Oriented toward Adam&apos;s Peak mist, engineered for acoustic silence, and attuned to your biological rhythms.
              </p>

              {/* View Switcher: 3D Coverflow vs Card Shelf */}
              <div className="flex items-center gap-3">
                <div className="inline-flex items-center bg-[#EEE7D6] p-1 rounded-full border border-[#6B4A3A]/20 shadow-xs">
                  <button
                    type="button"
                    onClick={() => setResidenceViewMode("coverflow")}
                    className={`px-3 py-1 rounded-full text-xs font-medium transition-all duration-200 cursor-pointer ${
                      residenceViewMode === "coverflow"
                        ? "bg-[#0F1B1A] text-[#F6F1E6] shadow-sm"
                        : "text-[#3A362E]/70 hover:text-[#0F1B1A]"
                    }`}
                  >
                    3D Perspective
                  </button>
                  <button
                    type="button"
                    onClick={() => setResidenceViewMode("shelf")}
                    className={`px-3 py-1 rounded-full text-xs font-medium transition-all duration-200 cursor-pointer ${
                      residenceViewMode === "shelf"
                        ? "bg-[#0F1B1A] text-[#F6F1E6] shadow-sm"
                        : "text-[#3A362E]/70 hover:text-[#0F1B1A]"
                    }`}
                  >
                    Card Shelf
                  </button>
                </div>

                {/* Shelf Controls when in shelf mode */}
                {residenceViewMode === "shelf" && (
                  <div className="hidden sm:flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => scrollShelf("left")}
                      aria-label="Previous residences"
                      className="w-8 h-8 rounded-full border border-[#6B4A3A]/30 hover:border-[#6B4A3A] hover:bg-[#EEE7D6] flex items-center justify-center text-[#0F1B1A] transition-colors focus-visible:ring-2 focus-visible:ring-[#C4622D] focus-visible:outline-none"
                    >
                      <ChevronLeft className="w-4 h-4" />
                    </button>
                    <button
                      type="button"
                      onClick={() => scrollShelf("right")}
                      aria-label="Next residences"
                      className="w-8 h-8 rounded-full border border-[#6B4A3A]/30 hover:border-[#6B4A3A] hover:bg-[#EEE7D6] flex items-center justify-center text-[#0F1B1A] transition-colors focus-visible:ring-2 focus-visible:ring-[#C4622D] focus-visible:outline-none"
                    >
                      <ChevronRight className="w-4 h-4" />
                    </button>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Conditional Display: 3D Coverflow or Horizontal Scroll-Snap Shelf */}
          {residenceViewMode === "coverflow" ? (
            /* 3D Coverflow Carousel Showcase */
            <div className="w-full py-4 bg-[#EEE7D6]/60 border border-[#6B4A3A]/20 p-6 shadow-sm overflow-hidden">
              <CoverflowCarousel
                slides={coverflowSlides}
                showCaption
                showNavigation
                showPagination
                cardWidth="clamp(220px, 28vw, 340px)"
                gap={0.06}
                rotate={42}
                depth={0.65}
                className="py-4"
              />
              <div className="text-center pt-4">
                <a
                  href="#book"
                  className="inline-flex items-center gap-2 bg-[#C4622D] hover:bg-[#E07A3E] text-white px-6 py-2.5 rounded-full text-xs font-semibold tracking-wider uppercase shadow-md transition-all duration-200"
                >
                  <span>Reserve Selected Suite</span>
                  <ArrowRight className="w-3.5 h-3.5" />
                </a>
              </div>
            </div>
          ) : (
            /* Horizontal Scroll-Snap Shelf */
            <div
              ref={shelfRef}
              className="flex overflow-x-auto snap-x snap-mandatory gap-6 pb-6 pt-2 suite-shelf focus-visible:outline-none"
              tabIndex={0}
              aria-label="Suite residences carousel"
            >
              {SUITES.map((suite) => (
                <div
                  key={suite.id}
                  className="flex-none w-[310px] sm:w-[340px] snap-start bg-[#EEE7D6] border border-[#6B4A3A]/20 flex flex-col justify-between group transition-shadow duration-300 hover:shadow-lg"
                >
                  <div>
                    {/* Photo Block (~220px tall) */}
                    <div className="relative h-[220px] w-full bg-[#0F1B1A] overflow-hidden">
                      {/* TODO: replace with client photo */}
                      <Image
                        src={suite.image}
                        alt={suite.name}
                        fill
                        className="object-cover object-center group-hover:scale-105 transition-transform duration-500"
                      />
                      {/* Price Pill: Bottom-left */}
                      <div className="absolute bottom-3 left-3 bg-[#0F1B1A]/90 backdrop-blur-sm text-[#F6F1E6] px-3 py-1 rounded-full text-xs shadow-md">
                        <span>From </span>
                        <span className="font-serif font-medium text-[#E07A3E] text-sm">${suite.price}</span>
                        <span className="text-[#7C9188] text-[11px]"> / night</span>
                      </div>
                    </div>

                    {/* Content Body */}
                    <div className="p-5 space-y-3">
                      <h3 className="font-serif text-xl text-[#0F1B1A] font-medium leading-snug">{suite.name}</h3>

                      {/* Specs Line */}
                      <p className="text-xs text-[#7C9188] font-light leading-relaxed">{suite.specs}</p>

                      <p className="text-xs text-[#3A362E]/90 line-clamp-2 leading-relaxed">{suite.description}</p>

                      {/* Amenity Chips */}
                      <div className="flex flex-wrap gap-1.5 pt-2">
                        {suite.amenities.map((amenity) => (
                          <span
                            key={amenity}
                            className="border border-[#6B4A3A]/30 text-[#3A362E] text-[10px] px-2.5 py-0.5 rounded-full bg-[#F6F1E6]/60"
                          >
                            {amenity}
                          </span>
                        ))}
                      </div>
                    </div>
                  </div>

                  {/* Card Footer Row */}
                  <div className="p-5 pt-0 border-t border-[#6B4A3A]/15 mt-4">
                    <a
                      href="#book"
                      onClick={() => setSelectedSuiteId(suite.id)}
                      className="group/link inline-flex items-center gap-1.5 text-xs font-semibold text-[#0F1B1A] hover:text-[#C4622D] transition-colors pt-3"
                    >
                      <span>Reserve</span>
                      <ArrowRight className="w-3.5 h-3.5 transition-transform duration-200 group-hover/link:translate-x-1.5 text-[#C4622D]" />
                    </a>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </section>

      {/* ─────────────────────────────────────────────────────────────
          7. INTELLIGENT HOSPITALITY (Smart Features Hairline Grid)
      ───────────────────────────────────────────────────────────── */}
      <section className="py-20 md:py-28 bg-[#EEE7D6] border-y border-[#6B4A3A]/15">
        <div className="max-w-7xl mx-auto px-6 md:px-12">
          {/* Asymmetric Header */}
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-6 mb-12">
            <div className="space-y-3 max-w-xl">
              <div className="flex items-center gap-3">
                <span className="w-6 h-[2px] bg-[#C4622D] inline-block" />
                <span className="text-xs font-semibold tracking-wider text-[#6B4A3A] uppercase">
                  Intelligent Hospitality
                </span>
              </div>
              <h2 className="font-serif text-3xl sm:text-4xl md:text-5xl font-normal leading-tight text-[#0F1B1A]">
                The house arrives <span className="italic text-[#6B4A3A]">before you do</span>.
              </h2>
            </div>
            <p className="md:max-w-xs text-xs md:text-sm text-[#3A362E]/80 md:text-right leading-relaxed">
              Frictionless, invisible intelligence that anticipates comfort without screens, noise, or intrusion.
            </p>
          </div>

          {/* 4-column Grid with Hairline Gap Lines */}
          <div className="bg-[#D8D0BE] gap-[1px] grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 border border-[#D8D0BE]">
            {/* Card 01 */}
            <div className="bg-[#EEE7D6] p-7 flex flex-col justify-between space-y-6 hover:bg-[#F6F1E6] transition-colors duration-200">
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <span className="w-8 h-8 rounded-full border border-[#C4622D] text-[#C4622D] text-xs font-medium flex items-center justify-center">
                    01
                  </span>
                  <Key className="w-4 h-4 text-[#7C9188]" />
                </div>
                <h3 className="font-serif text-lg text-[#0F1B1A] font-medium leading-snug">Keyless Digital Entry</h3>
                <p className="text-xs text-[#3A362E]/80 leading-relaxed font-light">
                  Encrypted mobile credential unlocks your slowhouse as you approach the cedar doorway. No front desk
                  queues, no physical keys.
                </p>
              </div>
              <div className="text-[11px] text-[#6B4A3A] font-medium tracking-wide">
                Seamless Touchless Access
              </div>
            </div>

            {/* Card 02 */}
            <div className="bg-[#EEE7D6] p-7 flex flex-col justify-between space-y-6 hover:bg-[#F6F1E6] transition-colors duration-200">
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <span className="w-8 h-8 rounded-full border border-[#C4622D] text-[#C4622D] text-xs font-medium flex items-center justify-center">
                    02
                  </span>
                  <Flame className="w-4 h-4 text-[#7C9188]" />
                </div>
                <h3 className="font-serif text-lg text-[#0F1B1A] font-medium leading-snug">
                  Automated Hearth &amp; Climate
                </h3>
                <p className="text-xs text-[#3A362E]/80 leading-relaxed font-light">
                  Thermal zoning learns your circadian preferences, pre-warming the wood hearth and stone bathroom floors
                  before mountain twilight drops.
                </p>
              </div>
              <div className="text-[11px] text-[#6B4A3A] font-medium tracking-wide">
                Circadian Thermal Tuning
              </div>
            </div>

            {/* Card 03 */}
            <div className="bg-[#EEE7D6] p-7 flex flex-col justify-between space-y-6 hover:bg-[#F6F1E6] transition-colors duration-200">
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <span className="w-8 h-8 rounded-full border border-[#C4622D] text-[#C4622D] text-xs font-medium flex items-center justify-center">
                    03
                  </span>
                  <Utensils className="w-4 h-4 text-[#7C9188]" />
                </div>
                <h3 className="font-serif text-lg text-[#0F1B1A] font-medium leading-snug">Curated In-Room Dining</h3>
                <p className="text-xs text-[#3A362E]/80 leading-relaxed font-light">
                  Locally sourced highland gastronomy prepared by our private chefs, discreetly delivered to your heated
                  slowhouse dining nook on your schedule.
                </p>
              </div>
              <div className="text-[11px] text-[#6B4A3A] font-medium tracking-wide">
                Zero-Disturbance Delivery
              </div>
            </div>

            {/* Card 04 */}
            <div className="bg-[#EEE7D6] p-7 flex flex-col justify-between space-y-6 hover:bg-[#F6F1E6] transition-colors duration-200">
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <span className="w-8 h-8 rounded-full border border-[#C4622D] text-[#C4622D] text-xs font-medium flex items-center justify-center">
                    04
                  </span>
                  <Bot className="w-4 h-4 text-[#7C9188]" />
                </div>
                <h3 className="font-serif text-lg text-[#0F1B1A] font-medium leading-snug">AI Concierge</h3>
                <p className="text-xs text-[#3A362E]/80 leading-relaxed font-light">
                  A quiet, conversational intelligence attuned to weather shifts, mountain trails, and personalised tea
                  tastings across the central highlands.
                </p>
              </div>
              <div className="text-[11px] text-[#6B4A3A] font-medium tracking-wide">
                Adaptive Mountain Guide
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ─────────────────────────────────────────────────────────────
          8. WELLNESS & HIGHLAND EXPERIENCES (Asymmetric Photo Mosaic)
      ───────────────────────────────────────────────────────────── */}
      <section id="wellness" className="py-20 md:py-28 bg-[#F6F1E6] scroll-mt-20">
        <div className="max-w-7xl mx-auto px-6 md:px-12">
          {/* Asymmetric Header */}
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-6 mb-12">
            <div className="space-y-3 max-w-xl">
              <div className="flex items-center gap-3">
                <span className="w-6 h-[2px] bg-[#C4622D] inline-block" />
                <span className="text-xs font-semibold tracking-wider text-[#6B4A3A] uppercase">
                  Wellness &amp; Highland Experiences
                </span>
              </div>
              <h2 className="font-serif text-3xl sm:text-4xl md:text-5xl font-normal leading-tight text-[#0F1B1A]">
                Warmth drawn <span className="italic text-[#6B4A3A]">from the earth</span>.
              </h2>
            </div>
            <p className="md:max-w-xs text-xs md:text-sm text-[#3A362E]/80 md:text-right leading-relaxed">
              Geothermal springs, crisp mountain air, and centuries-old Ceylon tea traditions designed for restoration.
            </p>
          </div>

          {/* Asymmetric Photo Mosaic: 1 large tile (spans 2 rows) + 4 smaller tiles */}
          <div className="grid grid-cols-1 md:grid-cols-3 md:grid-rows-2 gap-4 h-auto md:h-[580px]">
            {/* Tile 1: Misty Highland Spring Pools (Large: Spans 2 rows on md+) */}
            <div className="relative md:col-span-1 md:row-span-2 min-h-[320px] md:min-h-0 bg-[#0F1B1A] overflow-hidden group">
              {/* TODO: replace with client photo */}
              <Image
                src="/images/pool-thumb.jpg"
                alt="Misty Highland Spring Pools"
                fill
                className="object-cover object-center group-hover:scale-105 transition-transform duration-700"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-[#0F1B1A]/90 via-[#0F1B1A]/40 to-transparent pointer-events-none" />
              <div className="absolute bottom-6 left-6 right-6 text-white space-y-1.5">
                <span className="text-[10px] tracking-widest uppercase text-[#E07A3E] font-medium">Hydrotherapy</span>
                <h3 className="font-serif text-xl sm:text-2xl font-normal text-[#F6F1E6]">
                  Misty Highland Spring Pools
                </h3>
                <p className="text-xs text-[#EEE7D6]/75 font-light leading-relaxed">
                  Geothermal mineral warmth at 39°C overlooking cloud forests.
                </p>
              </div>
            </div>

            {/* Tile 2: Cedar Saunas & Cold Plunges */}
            <div className="relative min-h-[220px] md:min-h-0 bg-[#0F1B1A] overflow-hidden group">
              {/* TODO: replace with client photo */}
              <Image
                src="/images/slowhouse-interior.jpg"
                alt="Cedar Saunas & Cold Plunges"
                fill
                className="object-cover object-center group-hover:scale-105 transition-transform duration-700"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-[#0F1B1A]/90 via-[#0F1B1A]/30 to-transparent pointer-events-none" />
              <div className="absolute bottom-5 left-5 right-5 text-white space-y-1">
                <span className="text-[10px] tracking-widest uppercase text-[#E07A3E] font-medium">Restoration</span>
                <h3 className="font-serif text-lg font-normal text-[#F6F1E6]">Cedar Saunas &amp; Cold Plunges</h3>
              </div>
            </div>

            {/* Tile 3: Guided Forest Foraging */}
            <div className="relative min-h-[220px] md:min-h-0 bg-[#0F1B1A] overflow-hidden group">
              {/* TODO: replace with client photo */}
              <Image
                src="/images/ridge-thumb.jpg"
                alt="Guided Forest Foraging"
                fill
                className="object-cover object-center group-hover:scale-105 transition-transform duration-700"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-[#0F1B1A]/90 via-[#0F1B1A]/30 to-transparent pointer-events-none" />
              <div className="absolute bottom-5 left-5 right-5 text-white space-y-1">
                <span className="text-[10px] tracking-widest uppercase text-[#E07A3E] font-medium">Expedition</span>
                <h3 className="font-serif text-lg font-normal text-[#F6F1E6]">Guided Forest Foraging</h3>
              </div>
            </div>

            {/* Tile 4: Stargazing Observatory */}
            <div className="relative min-h-[220px] md:min-h-0 bg-[#0F1B1A] overflow-hidden group">
              {/* TODO: replace with client photo */}
              <Image
                src="/images/hero-view.jpg"
                alt="Stargazing Observatory"
                fill
                className="object-cover object-center group-hover:scale-105 transition-transform duration-700"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-[#0F1B1A]/90 via-[#0F1B1A]/30 to-transparent pointer-events-none" />
              <div className="absolute bottom-5 left-5 right-5 text-white space-y-1">
                <span className="text-[10px] tracking-widest uppercase text-[#E07A3E] font-medium">Celestial</span>
                <h3 className="font-serif text-lg font-normal text-[#F6F1E6]">Stargazing Observatory</h3>
              </div>
            </div>

            {/* Tile 5: Ceylon Tea Tasting Table */}
            <div id="experiences" className="relative min-h-[220px] md:min-h-0 bg-[#0F1B1A] overflow-hidden group scroll-mt-24">
              {/* TODO: replace with client photo */}
              <Image
                src="/images/keyless-entry.jpg"
                alt="Ceylon Tea Tasting Table"
                fill
                className="object-cover object-center group-hover:scale-105 transition-transform duration-700"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-[#0F1B1A]/90 via-[#0F1B1A]/30 to-transparent pointer-events-none" />
              <div className="absolute bottom-5 left-5 right-5 text-white space-y-1">
                <span className="text-[10px] tracking-widest uppercase text-[#E07A3E] font-medium">Highland Craft</span>
                <h3 className="font-serif text-lg font-normal text-[#F6F1E6]">Ceylon Tea Tasting Table</h3>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ─────────────────────────────────────────────────────────────
          9. TESTIMONIALS / SOCIAL PROOF (Template Review Cards)
      ───────────────────────────────────────────────────────────── */}
      <ReviewSection />

      {/* ─────────────────────────────────────────────────────────────
          10. FOOTER (Dark --ink background, 4-column layout)
      ───────────────────────────────────────────────────────────── */}
      <footer id="contact" className="bg-[#0F1B1A] text-[#F6F1E6] pt-16 pb-12 border-t border-[#16302C]">
        <div className="max-w-7xl mx-auto px-6 md:px-12">
          {/* Main 4-Column Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-10 pb-16">
            {/* Brand Column */}
            <div className="space-y-4">
              <Link href="/" className="inline-flex items-baseline gap-1 text-white">
                <span className="font-serif font-medium text-2xl tracking-tight text-white">Smart</span>
                <span className="font-serif italic text-2xl tracking-tight text-[#E07A3E]">Hotel</span>
              </Link>
              <p className="text-xs text-[#7C9188] leading-relaxed max-w-xs font-light">
                A sanctuary of slow living and quiet intelligence set within the tea ridges of Maskeliya, Sri Lanka.
              </p>
              <div className="text-xs text-[#7C9188] space-y-1 pt-2">
                <p>Maskeliya, Central Highlands, Sri Lanka</p>
                <p>reservations@smarthotel.com · +94 51 222 3456</p>
              </div>
            </div>

            {/* Column 2: Guest Access */}
            <div className="space-y-3">
              <h4 className="font-serif text-sm font-semibold tracking-wider uppercase text-[#EEE7D6] border-b border-[#16302C] pb-2">
                Guest Access
              </h4>
              <ul className="space-y-2 text-xs text-[#7C9188]">
                <li>
                  <Link href="/portal/access" className="hover:text-white transition-colors">
                    Find My Stay
                  </Link>
                </li>
                <li>
                  <Link href="/portal/access" className="hover:text-white transition-colors">
                    Set Up Credentials
                  </Link>
                </li>
                <li>
                  <Link href="/portal/access" className="hover:text-white transition-colors">
                    Open AI Concierge
                  </Link>
                </li>
              </ul>
            </div>

            {/* Column 3: Account */}
            <div className="space-y-3">
              <h4 className="font-serif text-sm font-semibold tracking-wider uppercase text-[#EEE7D6] border-b border-[#16302C] pb-2">
                Account
              </h4>
              <ul className="space-y-2 text-xs text-[#7C9188]">
                <li>
                  <Link href="/register" className="hover:text-white transition-colors">
                    Create Account
                  </Link>
                </li>
                <li>
                  <Link href="/login" className="hover:text-white transition-colors">
                    Sign In
                  </Link>
                </li>
                <li>
                  <Link href="/login" className="hover:text-[#E07A3E] transition-colors">
                    Book a Stay
                  </Link>
                </li>
              </ul>
            </div>

            {/* Column 4: Policies */}
            <div className="space-y-3">
              <h4 className="font-serif text-sm font-semibold tracking-wider uppercase text-[#EEE7D6] border-b border-[#16302C] pb-2">
                Policies
              </h4>
              <ul className="space-y-2 text-xs text-[#7C9188]">
                <li>
                  <Link href="/pages#cancellation" className="hover:text-white transition-colors">
                    Cancellation Policy
                  </Link>
                </li>
                <li>
                  <Link href="/pages#privacy" className="hover:text-white transition-colors">
                    Data Privacy &amp; GDPR
                  </Link>
                </li>
                <li>
                  <Link href="/pages#terms" className="hover:text-white transition-colors">
                    Terms of Stay
                  </Link>
                </li>
              </ul>
            </div>
          </div>

          {/* Bottom Bar below Hairline Divider */}
          <div className="pt-8 border-t border-[#16302C] flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-[#7C9188]">
            <p>© {new Date().getFullYear()} SmartHotel Maskeliya. All rights reserved.</p>
            <p className="text-[11px] tracking-wide text-[#7C9188]/80">
              Slow living · Intelligent hospitality · Maskeliya, Central Highlands
            </p>
          </div>
        </div>
      </footer>
    </div>
  );
}
