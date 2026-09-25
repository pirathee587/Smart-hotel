"use client";

import {
ArrowRight,
CalendarRange,
Check,
CheckCircle2,
Copy,
Mail,
Sparkles,
Star,
Users,
} from "lucide-react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense,useEffect,useState } from "react";

// ── Animated confetti dots ────────────────────────────────────────────────────
function ConfettiDot({ delay, x, size, color }: { delay: number; x: number; size: number; color: string }) {
  return (
    <div
      className="absolute top-0 rounded-full animate-bounce opacity-60"
      style={{
        left: `${x}%`,
        width: size,
        height: size,
        backgroundColor: color,
        animationDelay: `${delay}s`,
        animationDuration: `${1.5 + delay * 0.3}s`,
      }}
    />
  );
}

const CONFETTI = [
  { x: 10, delay: 0, size: 8, color: "#C4622D" },
  { x: 20, delay: 0.2, size: 5, color: "#E07A3E" },
  { x: 35, delay: 0.4, size: 10, color: "#2F5C52" },
  { x: 50, delay: 0.1, size: 6, color: "#7C9188" },
  { x: 65, delay: 0.3, size: 8, color: "#C4622D" },
  { x: 78, delay: 0.5, size: 5, color: "#E07A3E" },
  { x: 88, delay: 0.2, size: 7, color: "#2F5C52" },
];

// ── Main page ──────────────────────────────────────────────────────────────────
function BookingSuccessContent() {
  const searchParams = useSearchParams();

  const ref = searchParams.get("ref") ?? "SH-000000";
  const roomName = searchParams.get("roomName") ?? "Your Room";
  const checkIn = searchParams.get("checkIn") ?? "";
  const checkOut = searchParams.get("checkOut") ?? "";
  const guests = searchParams.get("guests") ?? "2";
  const total = searchParams.get("total") ?? "0";

  const [copied, setCopied] = useState(false);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    // Trigger entrance animation
    const t = setTimeout(() => setVisible(true), 100);
    return () => clearTimeout(t);
  }, []);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(ref);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const nights =
    checkIn && checkOut
      ? Math.max(
          1,
          Math.ceil(
            (new Date(checkOut).getTime() - new Date(checkIn).getTime()) /
              (1000 * 60 * 60 * 24)
          )
        )
      : 1;

  return (
    <div className="min-h-screen bg-[#0F1B1A] text-[#F6F1E6] flex items-center justify-center px-6 py-16 relative overflow-hidden">
      {/* Background glow */}
      <div className="absolute inset-0 pointer-events-none">
        <div className="absolute top-1/4 left-1/2 -translate-x-1/2 w-[600px] h-[600px] rounded-full bg-[#C4622D]/5 blur-3xl" />
        <div className="absolute bottom-0 left-0 w-80 h-80 rounded-full bg-[#2F5C52]/10 blur-3xl" />
      </div>

      {/* Confetti */}
      <div className="absolute top-12 left-0 right-0 h-16 pointer-events-none">
        {CONFETTI.map((c, i) => (
          <ConfettiDot key={i} {...c} />
        ))}
      </div>

      {/* Main card */}
      <div
        className={`relative z-10 w-full max-w-lg transition-all duration-700 ${
          visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"
        }`}
      >
        {/* Success icon */}
        <div className="flex justify-center mb-8">
          <div className="relative">
            <div className="size-24 rounded-full bg-emerald-900/30 border border-emerald-700/40 flex items-center justify-center">
              <CheckCircle2 className="size-12 text-emerald-400" />
            </div>
            {/* Pulse rings */}
            <div className="absolute inset-0 rounded-full border border-emerald-500/30 animate-ping" />
            <div
              className="absolute inset-[-8px] rounded-full border border-emerald-700/20 animate-ping"
              style={{ animationDelay: "0.3s" }}
            />
          </div>
        </div>

        {/* Heading */}
        <div className="text-center mb-8">
          <span className="text-[#C4622D] text-xs font-semibold tracking-[0.25em] uppercase block mb-2">
            Booking Confirmed
          </span>
          <h1 className="font-serif font-bold text-4xl text-[#F6F1E6] mb-3">
            Your retreat awaits!
          </h1>
          <p className="text-[#7C9188] text-sm leading-relaxed max-w-sm mx-auto">
            We&apos;ve received your reservation. A confirmation email will arrive
            shortly with everything you need for your stay.
          </p>
        </div>

        {/* Booking reference */}
        <div className="bg-[#16302C]/60 border border-[#2F5C52]/30 rounded-2xl p-5 mb-5">
          <p className="text-[#7C9188] text-[10px] font-semibold tracking-wider uppercase text-center mb-2">
            Booking Reference
          </p>
          <div className="flex items-center justify-center gap-3">
            <span className="font-serif text-3xl font-bold text-[#F6F1E6] tracking-widest">
              {ref}
            </span>
            <button
              onClick={handleCopy}
              className="size-8 rounded-lg bg-[#2F5C52]/30 hover:bg-[#2F5C52]/50 border border-[#2F5C52]/30 flex items-center justify-center transition-all duration-200 cursor-pointer"
              aria-label="Copy reference code"
            >
              {copied ? (
                <Check className="size-4 text-emerald-400" />
              ) : (
                <Copy className="size-4 text-[#7C9188]" />
              )}
            </button>
          </div>
          {copied && (
            <p className="text-emerald-400 text-[10px] text-center mt-1 font-medium">
              Copied to clipboard!
            </p>
          )}
        </div>

        {/* Stay summary */}
        <div className="bg-[#0F1B1A]/80 border border-[#2F5C52]/20 rounded-2xl p-5 mb-5 space-y-3">
          <div className="flex justify-between items-center text-sm">
            <span className="text-[#7C9188] flex items-center gap-2">
              <Star className="size-3.5 text-[#C4622D]" />
              Room
            </span>
            <span className="text-[#F6F1E6] font-medium">{roomName}</span>
          </div>
          {checkIn && (
            <div className="flex justify-between items-center text-sm">
              <span className="text-[#7C9188] flex items-center gap-2">
                <CalendarRange className="size-3.5 text-[#C4622D]" />
                Check In
              </span>
              <span className="text-[#F6F1E6]">{formatDate(checkIn)}</span>
            </div>
          )}
          {checkOut && (
            <div className="flex justify-between items-center text-sm">
              <span className="text-[#7C9188] flex items-center gap-2">
                <CalendarRange className="size-3.5 text-[#C4622D]" />
                Check Out
              </span>
              <span className="text-[#F6F1E6]">{formatDate(checkOut)}</span>
            </div>
          )}
          <div className="flex justify-between items-center text-sm">
            <span className="text-[#7C9188] flex items-center gap-2">
              <Users className="size-3.5 text-[#C4622D]" />
              Guests · Nights
            </span>
            <span className="text-[#F6F1E6]">
              {guests} guest{parseInt(guests) > 1 ? "s" : ""} · {nights} night
              {nights > 1 ? "s" : ""}
            </span>
          </div>
          <div className="border-t border-[#2F5C52]/20 pt-3 flex justify-between items-center">
            <span className="text-[#7C9188] text-sm">Total charged</span>
            <span className="text-[#C4622D] font-serif text-xl font-semibold">
              ${parseInt(total).toLocaleString()}
            </span>
          </div>
        </div>

        {/* What's next */}
        <div className="bg-[#16302C]/30 border border-[#2F5C52]/20 rounded-2xl p-5 mb-6">
          <h3 className="text-[#F6F1E6] font-semibold text-sm mb-3 flex items-center gap-2">
            <Sparkles className="size-4 text-[#C4622D]" />
            What happens next?
          </h3>
          <ul className="space-y-2.5">
            {[
              "Confirmation email sent to your inbox",
              "Payment will be processed at check-in",
              "Digital room key sent 24h before arrival",
              "AI concierge available via your guest portal",
            ].map((item, i) => (
              <li key={i} className="flex items-start gap-2.5 text-xs text-[#7C9188]">
                <span className="size-4 rounded-full bg-[#C4622D]/20 text-[#C4622D] text-[10px] font-bold flex items-center justify-center shrink-0 mt-0.5">
                  {i + 1}
                </span>
                {item}
              </li>
            ))}
          </ul>
        </div>

        {/* CTA buttons */}
        <div className="grid sm:grid-cols-2 gap-3">
          <Link
            href="/portal"
            className="flex items-center justify-center gap-2 py-3 rounded-xl bg-[#C4622D] hover:bg-[#E07A3E] text-white text-sm font-semibold transition-all duration-300 hover:shadow-lg hover:shadow-[#C4622D]/30"
          >
            <Mail className="size-4" />
            Access Guest Portal
            <ArrowRight className="size-4" />
          </Link>
          <Link
            href="/"
            className="flex items-center justify-center gap-2 py-3 rounded-xl border border-[#2F5C52]/40 hover:border-[#C4622D]/50 text-[#7C9188] hover:text-[#F6F1E6] text-sm font-semibold transition-all duration-300"
          >
            Return to Home
          </Link>
        </div>
      </div>
    </div>
  );
}

export default function BookingSuccessPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen bg-[#0F1B1A] flex items-center justify-center text-[#F6F1E6]">
          <span className="text-sm font-light">Loading reservation confirmation...</span>
        </div>
      }
    >
      <BookingSuccessContent />
    </Suspense>
  );
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString("en-US", {
    weekday: "short",
    month: "long",
    day: "numeric",
    year: "numeric",
  });
}
