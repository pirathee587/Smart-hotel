"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { cn } from "@/lib/utils";
import {
  Hotel,
  LayoutDashboard,
  BedDouble,
  CalendarCheck,
  CheckSquare,
  BarChart3,
  LogOut,
  User,
} from "lucide-react";

export interface SidebarProps {
  className?: string;
}

export function Sidebar({ className }: SidebarProps) {
  const pathname = usePathname();
  const { user, logout } = useAuthStore();

  const links = [
    { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
    { href: "/dashboard/rooms", label: "Rooms", icon: BedDouble },
    { href: "/dashboard/bookings", label: "Bookings", icon: CalendarCheck },
    { href: "/dashboard/tasks", label: "Tasks & Housekeeping", icon: CheckSquare },
    { href: "/dashboard/reports", label: "Analytics & Reports", icon: BarChart3 },
  ];

  return (
    <aside
      className={cn(
        "w-64 bg-white/80 dark:bg-[#111928]/80 border-r border-card-border flex flex-col h-full backdrop-blur-lg text-text-primary",
        className
      )}
    >
      {/* Brand Header */}
      <div className="h-16 border-b border-card-border px-6 flex items-center gap-3">
        <div className="w-8 h-8 bg-primary/10 border border-primary/20 rounded-lg flex items-center justify-center text-primary">
          <Hotel className="w-4 h-4" />
        </div>
        <span className="font-bold text-lg tracking-tight">SmartHotel</span>
      </div>

      {/* Navigation Links */}
      <nav className="flex-1 px-4 py-6 space-y-1.5 overflow-y-auto">
        {links.map((link) => {
          const isActive = pathname === link.href;
          const Icon = link.icon;
          return (
            <Link
              key={link.href}
              href={link.href}
              className={cn(
                "flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-200 hover:bg-white/5 hover:text-text-primary",
                isActive
                  ? "bg-primary/15 text-primary border-l-2 border-primary hover:bg-primary/20"
                  : "text-text-secondary"
              )}
            >
              <Icon className="w-4 h-4" />
              {link.label}
            </Link>
          );
        })}
      </nav>

      {/* User Footer Profile & Logout */}
      <div className="p-4 border-t border-card-border bg-black/10">
        <div className="flex items-center gap-3 mb-4 px-2">
          <div className="w-9 h-9 rounded-full bg-white/5 border border-card-border flex items-center justify-center text-primary">
            <User className="w-4 h-4" />
          </div>
          <div className="overflow-hidden">
            <h4 className="text-xs font-semibold truncate">
              {user?.name || "Employee"}
            </h4>
            <span className="text-[10px] text-text-secondary uppercase tracking-wider">
              {user?.role || "Staff"}
            </span>
          </div>
        </div>
        <button
          onClick={logout}
          className="w-full flex items-center justify-center gap-2 px-3 py-2 text-xs font-medium text-accent bg-accent/5 hover:bg-accent/15 border border-accent/10 rounded-lg transition-all duration-200 cursor-pointer"
        >
          <LogOut className="w-3.5 h-3.5" />
          Sign Out
        </button>
      </div>
    </aside>
  );
}
export default Sidebar;
