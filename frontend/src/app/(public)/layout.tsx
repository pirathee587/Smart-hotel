"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Hotel, Mail, Phone, MapPin, Menu, X, Sun, Moon } from "lucide-react";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { useTheme } from "@/components/common/ThemeProvider";

const links = [
  { href: "/", label: "Home" },
  { href: "/rooms", label: "Rooms" },
  { href: "/about", label: "About" },
  { href: "/blog", label: "Blog" },
  { href: "/pages", label: "Pages" },
  { href: "/contact", label: "Contact" },
];

export default function PublicLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const pathname = usePathname();
  const [mobileMenuOpen, setMobileMenuOpen] = React.useState(false);
  const { isAuthenticated, initialize } = useAuthStore();
  const { theme, toggleTheme } = useTheme();

  React.useEffect(() => {
    initialize();
  }, [initialize]);

  const isHomePage = pathname === "/";

  return (
    <div className="min-h-screen bg-bg-dark text-text-primary flex flex-col font-sans transition-colors duration-300">
      {/* Standard Header (rendered on subpages) */}
      {!isHomePage && (
        <header className="sticky top-0 z-50 w-full border-b-[0.5px] border-card-border bg-[#FAFAF8]/92 dark:bg-[#0b0f19]/85 backdrop-blur-[12px] shadow-lg shadow-black/5 dark:shadow-black/20 transition-colors duration-300">
          <div className="max-w-7xl mx-auto px-6 h-20 flex items-center justify-between">
            {/* Brand Logo */}
            <Link href="/" className="flex items-center gap-2.5 group">
              <div className="bg-primary/20 ring-primary/45 flex size-9 items-center justify-center rounded-lg ring-1 transition-all duration-300 group-hover:scale-105">
                <Hotel className="size-5 text-primary" />
              </div>
              <span className="font-serif font-bold text-xl tracking-wide text-text-primary group-hover:text-primary transition-colors">
                SmartHotel
              </span>
            </Link>

            {/* Desktop Navigation Links */}
            <nav className="hidden md:flex items-center gap-8">
              {links.map((link) => {
                const isActive = pathname === link.href;
                return (
                  <Link
                    key={link.href}
                    href={link.href}
                    className={cn(
                      "text-sm font-medium tracking-wide transition-all duration-200 hover:text-primary relative py-1",
                      isActive ? "text-primary font-semibold" : "text-text-secondary hover:text-text-primary"
                    )}
                  >
                    {link.label}
                    {isActive && (
                      <span className="absolute bottom-0 left-0 w-full h-[2px] bg-primary rounded-full animate-pulse" />
                    )}
                  </Link>
                );
              })}
            </nav>

            {/* Book Button & Login / Mobile Menu button & Theme toggle */}
            <div className="flex items-center gap-4">
              {/* Theme Toggle Button */}
              <button
                onClick={toggleTheme}
                aria-label="Toggle theme"
                className="relative size-9 rounded-lg hover:bg-white/5 dark:hover:bg-white/10 flex items-center justify-center border border-transparent hover:border-card-border transition-all duration-300 text-text-secondary hover:text-text-primary cursor-pointer overflow-hidden"
              >
                <div className="relative w-4 h-4 flex items-center justify-center">
                  <Sun className="absolute w-4 h-4 text-amber-400 transition-all duration-500 transform dark:scale-100 dark:rotate-0 scale-0 rotate-90" />
                  <Moon className="absolute w-4 h-4 text-indigo-500 transition-all duration-500 transform dark:scale-0 dark:rotate-90 scale-100 rotate-0" />
                </div>
              </button>

              {isAuthenticated ? (
                <Link href="/dashboard">
                  <button className="hidden sm:inline-flex items-center justify-center px-5 py-2.5 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold transition-all duration-300 hover:-translate-y-0.5 shadow-lg shadow-indigo-500/20 cursor-pointer">
                    Dashboard
                  </button>
                </Link>
              ) : (
                <Link href="/login">
                  <button className="hidden sm:inline-flex items-center justify-center px-5 py-2.5 rounded-lg bg-primary hover:bg-primary/90 text-primary-foreground text-xs font-semibold transition-all duration-300 hover:-translate-y-0.5 shadow-lg shadow-primary/20 cursor-pointer">
                    Book a room
                  </button>
                </Link>
              )}

              {/* Mobile Menu Button */}
              <button
                onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
                className="md:hidden p-2 rounded-lg text-text-secondary hover:text-text-primary hover:bg-white/5 transition-colors cursor-pointer"
              >
                {mobileMenuOpen ? <X className="size-6" /> : <Menu className="size-6" />}
              </button>
            </div>
          </div>

          {/* Mobile Navigation Dropdown */}
          {mobileMenuOpen && (
            <div className="md:hidden border-b border-card-border bg-card-dark px-6 py-4 space-y-3 animate-in fade-in slide-in-from-top-2 duration-200">
              {links.map((link) => {
                const isActive = pathname === link.href;
                return (
                  <Link
                    key={link.href}
                    href={link.href}
                    onClick={() => setMobileMenuOpen(false)}
                    className={cn(
                      "block px-3 py-2 rounded-lg text-base font-medium transition-colors",
                      isActive ? "bg-primary/10 text-primary" : "text-text-secondary hover:bg-white/5 dark:hover:bg-white/10"
                    )}
                  >
                    {link.label}
                  </Link>
                );
              })}
              <div className="pt-2">
                {isAuthenticated ? (
                  <Link href="/dashboard" onClick={() => setMobileMenuOpen(false)}>
                    <button className="w-full py-2.5 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white font-semibold text-sm transition-colors cursor-pointer">
                      Dashboard
                    </button>
                  </Link>
                ) : (
                  <Link href="/login" onClick={() => setMobileMenuOpen(false)}>
                    <button className="w-full py-2.5 rounded-lg bg-primary hover:bg-primary/90 text-primary-foreground font-semibold text-sm transition-colors cursor-pointer">
                      Book a room
                    </button>
                  </Link>
                )}
              </div>
            </div>
          )}
        </header>
      )}

      {/* Main Content Area */}
      <main className="flex-1 flex flex-col relative overflow-hidden">
        {children}
      </main>

      {/* Standard Footer (rendered on subpages) */}
      {!isHomePage && (
        <footer className="bg-[#1B2A4A] text-slate-300 border-t border-white/10">
          <div className="max-w-7xl mx-auto px-6 py-16 grid gap-10 md:grid-cols-2 lg:grid-cols-4">
            <div className="flex flex-col gap-4">
              <Link href="/" className="flex items-center gap-2 group w-fit">
                <div className="bg-primary/20 ring-primary/45 flex size-8 items-center justify-center rounded-lg ring-1">
                  <Hotel className="size-4.5 text-primary" />
                </div>
                <span className="font-serif font-bold text-lg tracking-wide text-white">
                  SmartHotel
                </span>
              </Link>
              <p className="text-xs leading-relaxed text-slate-400 max-w-[280px]">
                A luxury slow-living sanctuary offering bespoke hospitality, geothermal thermal pools, and intelligent room automation.
              </p>
              <div className="flex items-start gap-2.5 mt-2">
                <MapPin className="size-4.5 text-primary shrink-0 mt-0.5" />
                <div className="text-xs text-slate-400">
                  <p>Vinterfjellet Ridge, 61°12&apos;N 8°24&apos;E</p>
                  <p>Norvik Reserve, Norway</p>
                </div>
              </div>
            </div>

            <div className="flex flex-col gap-4">
              <h3 className="text-white font-serif font-semibold text-sm tracking-wider uppercase border-b border-white/10 pb-2">
                Reservations
              </h3>
              <p className="text-xs text-slate-400">
                Contact our concierge 24/7 for booking inquiries, packages, and custom requests.
              </p>
              <div className="space-y-3.5 mt-2">
                <div className="flex items-center gap-2.5 text-xs text-slate-400">
                  <Phone className="size-4 text-primary" />
                  <span>+38(050)-247-08-12</span>
                </div>
                <div className="flex items-center gap-2.5 text-xs text-slate-400">
                  <Mail className="size-4 text-primary" />
                  <span>concierge@smarthotel.com</span>
                </div>
              </div>
            </div>

            <div className="flex flex-col gap-4">
              <h3 className="text-white font-serif font-semibold text-sm tracking-wider uppercase border-b border-white/10 pb-2">
                Navigation
              </h3>
              <nav className="grid grid-cols-2 gap-2 text-xs">
                {links.map((link) => (
                  <Link
                    key={link.href}
                    href={link.href}
                    className="hover:text-primary transition-colors text-slate-400"
                  >
                    {link.label}
                  </Link>
                ))}
              </nav>
            </div>

            <div className="flex flex-col gap-4">
              <h3 className="text-white font-serif font-semibold text-sm tracking-wider uppercase border-b border-white/10 pb-2">
                Quiet Letters
              </h3>
              <p className="text-xs text-slate-400">
                Subscribe to seasonal retreat openings and culinary gatherings.
              </p>
              <form onSubmit={(e) => e.preventDefault()} className="flex items-center gap-2 mt-2">
                <input
                  type="email"
                  placeholder="Enter your email"
                  className="flex-1 bg-white/10 border border-white/15 rounded-lg px-3 py-2 text-xs text-white placeholder-slate-400 focus:outline-none focus:border-primary transition-colors"
                  required
                />
                <button
                  type="submit"
                  className="bg-primary hover:bg-primary/90 text-white px-4 py-2 rounded-lg text-xs font-semibold uppercase tracking-wider transition-colors cursor-pointer"
                >
                  Join
                </button>
              </form>
            </div>
          </div>

          <div className="border-t border-white/5 py-6 px-6 text-center text-xs text-slate-500">
            <p>© {new Date().getFullYear()} Norvik Slowhouse &bull; SmartHotel Resort &amp; Spa. All rights reserved.</p>
          </div>
        </footer>
      )}
    </div>
  );
}
