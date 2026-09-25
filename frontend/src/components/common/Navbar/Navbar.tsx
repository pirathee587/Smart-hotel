"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { cn } from "@/lib/utils";
import { Bell,ChevronDown,LogOut,Settings } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect,useRef,useState } from "react";

export interface NavbarProps {
  className?: string;
}

export function Navbar({ className }: NavbarProps) {
  const router = useRouter();
  const menuRef = useRef<HTMLDivElement>(null);
  const [isProfileOpen, setIsProfileOpen] = useState(false);
  const { user, logout } = useAuthStore();
  const firstName = user?.name?.trim().split(/\s+/)[0] || "Staff";
  const initials = (user?.name || "Staff User")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();

  useEffect(() => {
    const closeMenu = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setIsProfileOpen(false);
      }
    };
    document.addEventListener("mousedown", closeMenu);
    return () => document.removeEventListener("mousedown", closeMenu);
  }, []);

  const handleLogout = () => {
    logout();
    router.replace("/staff/login");
  };

  return (
    <header
      className={cn(
        "h-20 border-b border-[var(--card-border)] bg-[#16302C]/80 backdrop-blur-md flex items-center justify-between px-5 sm:px-8 text-white transition-colors duration-300",
        className
      )}
    >
      <h2 className="text-xl sm:text-2xl font-semibold tracking-wide">
        Welcome back, {firstName}! <span aria-hidden="true">👋</span>
      </h2>

      {/* Utilities */}
      <div className="flex items-center gap-3 sm:gap-5">
        <button
          aria-label="Notifications"
          className="relative w-9 h-9 rounded-lg bg-white/5 hover:bg-white/10 flex items-center justify-center border border-white/10 hover:border-white/30 transition-all duration-200 text-white cursor-pointer"
        >
          <Bell className="w-5 h-5 text-white" strokeWidth={2.25} />
          <span className="absolute top-1.5 right-1.5 w-2.5 h-2.5 rounded-full bg-[#E87332] border-2 border-[#16302C] animate-pulse" />
        </button>

        <div className="h-9 w-px bg-white/15" />

        <div ref={menuRef} className="relative">
          <button
            type="button"
            aria-haspopup="menu"
            aria-expanded={isProfileOpen}
            onClick={() => setIsProfileOpen((open) => !open)}
            className="flex items-center gap-3 rounded-xl px-2 py-1.5 hover:bg-white/5 transition-colors cursor-pointer"
          >
            <span className="flex h-9 w-9 items-center justify-center rounded-full bg-[#C4622D] text-xs font-bold text-white ring-2 ring-white/15">
              {initials}
            </span>
            <span className="hidden sm:block min-w-24 text-left leading-tight">
              <span className="block text-sm font-semibold text-white truncate max-w-36">{firstName}</span>
              <span className="block mt-1 text-[10px] uppercase tracking-[0.14em] text-white/60">{String(user?.role || "Staff")}</span>
            </span>
            <ChevronDown className={`hidden sm:block h-4 w-4 text-white/60 transition-transform ${isProfileOpen ? "rotate-180" : ""}`} />
          </button>

          {isProfileOpen && (
            <div role="menu" className="absolute right-0 top-[calc(100%+10px)] z-50 w-56 overflow-hidden rounded-2xl border border-white/10 bg-[#16302C] p-2 shadow-2xl shadow-black/30">
              <Link
                href="/dashboard/settings"
                role="menuitem"
                onClick={() => setIsProfileOpen(false)}
                className="flex items-center gap-3 rounded-xl px-3 py-3 text-sm text-white hover:bg-white/8 transition-colors"
              >
                <Settings className="h-4 w-4 text-[#F6F1E6]" />
                Settings
              </Link>
              <div className="my-1 h-px bg-white/10" />
              <button
                type="button"
                role="menuitem"
                onClick={handleLogout}
                className="flex w-full items-center gap-3 rounded-xl px-3 py-3 text-sm text-[#F28A4B] hover:bg-[#C4622D]/15 transition-colors cursor-pointer"
              >
                <LogOut className="h-4 w-4" />
                Log Out
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
export default Navbar;
