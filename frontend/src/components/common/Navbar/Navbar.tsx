"use client";

import React, { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { Bell, Calendar, Sun, Moon } from "lucide-react";
import { cn } from "@/lib/utils";
import { useTheme } from "@/components/common/ThemeProvider";

export interface NavbarProps {
  className?: string;
}

export function Navbar({ className }: NavbarProps) {
  const pathname = usePathname();
  const [time, setTime] = useState("");
  const { theme, toggleTheme } = useTheme();

  useEffect(() => {
    const updateTime = () => {
      const now = new Date();
      setTime(
        now.toLocaleTimeString("en-US", {
          hour: "2-digit",
          minute: "2-digit",
          second: "2-digit",
        })
      );
    };
    updateTime();
    const interval = setInterval(updateTime, 1000);
    return () => clearInterval(interval);
  }, []);

  // Compute page titles from pathname
  const getPageTitle = () => {
    switch (pathname) {
      case "/dashboard":
        return "Dashboard Overview";
      case "/dashboard/rooms":
        return "Room Directory";
      case "/dashboard/bookings":
        return "Reservations & Bookings";
      case "/dashboard/tasks":
        return "Smart Task Allocator";
      case "/dashboard/reports":
        return "Analytics & Insights";
      default:
        return "SmartHotel Portal";
    }
  };

  return (
    <header
      className={cn(
        "h-16 border-b border-card-border bg-white/40 dark:bg-[#111928]/40 backdrop-blur-md flex items-center justify-between px-8 text-text-primary transition-colors duration-300",
        className
      )}
    >
      {/* Dynamic Title */}
      <h2 className="font-semibold text-lg tracking-wide">{getPageTitle()}</h2>

      {/* Utilities */}
      <div className="flex items-center gap-6">
        {/* Live Clock */}
        <div className="hidden sm:flex items-center gap-2 text-xs text-text-secondary font-medium bg-white/5 border border-card-border px-3 py-1.5 rounded-lg transition-colors duration-300">
          <Calendar className="w-3.5 h-3.5 text-primary" />
          <span>{new Date().toLocaleDateString("en-US", { dateStyle: "medium" })}</span>
          <span className="border-l border-card-border pl-2 text-primary">{time}</span>
        </div>

        {/* Theme Toggle Button */}
        <button
          onClick={toggleTheme}
          aria-label="Toggle theme"
          className="relative w-8 h-8 rounded-lg hover:bg-white/5 dark:hover:bg-white/10 flex items-center justify-center border border-transparent hover:border-card-border transition-all duration-300 text-text-secondary hover:text-text-primary cursor-pointer overflow-hidden"
        >
          <div className="relative w-4 h-4 flex items-center justify-center">
            {/* Sun Icon (visible in dark mode) */}
            <Sun className="absolute w-4 h-4 text-amber-400 transition-all duration-500 transform dark:scale-100 dark:rotate-0 scale-0 rotate-90" />
            {/* Moon Icon (visible in light mode) */}
            <Moon className="absolute w-4 h-4 text-indigo-500 transition-all duration-500 transform dark:scale-0 dark:rotate-90 scale-100 rotate-0" />
          </div>
        </button>

        {/* Mock Notification Bell */}
        <button className="relative w-8 h-8 rounded-lg hover:bg-white/5 flex items-center justify-center border border-transparent hover:border-card-border transition-all duration-200 text-text-secondary hover:text-text-primary cursor-pointer">
          <Bell className="w-4 h-4" />
          <span className="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-accent animate-pulse" />
        </button>
      </div>
    </header>
  );
}
export default Navbar;
