"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { getStaffNavigation } from "@/lib/staffNavigation";
import { cn } from "@/lib/utils";
import {
Banknote,
CalendarDays,
ClipboardCheck,
Landmark,
LayoutDashboard,
LogOut,
ShieldCheck,
User,
Users,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

export interface SidebarProps {
  className?: string;
}

const OWNER_LINKS = [
  { href: "/owner", label: "Executive Dashboard", icon: LayoutDashboard },
  { href: "/owner/admins", label: "Administrators", icon: ShieldCheck },
  { href: "/owner/approvals", label: "Approvals", icon: ClipboardCheck },
  { href: "/owner/staff-oversight", label: "Staff Oversight", icon: Users },
  { href: "/owner/finance", label: "Finance Overview", icon: Landmark },
  { href: "/owner/salaries", label: "Salary Management", icon: Banknote },
  { href: "/owner/leave-policies", label: "Leave Policies", icon: CalendarDays },
  { href: "/dashboard", label: "Operations Oversight", icon: LayoutDashboard },
];

function getNavLinks(user: ReturnType<typeof useAuthStore.getState>["user"], pathname: string) {
  const normalizedRole = String(user?.role || "");
  if (normalizedRole === "Owner") {
    if (pathname.startsWith("/dashboard")) {
      return getStaffNavigation(user);
    }
    return OWNER_LINKS;
  }
  return getStaffNavigation(user);
}

export function Sidebar({ className }: SidebarProps) {
  const pathname = usePathname();
  const { user, logout } = useAuthStore();

  const links = getNavLinks(user, pathname);

  return (
    <aside
      className={cn(
        "w-64 bg-[#0F1B1A]/95 border-r border-[rgba(246,241,230,0.10)] flex flex-col h-full backdrop-blur-lg text-[#F6F1E6]",
        className
      )}
    >
      {/* Brand Header */}
      <div className="h-20 border-b border-card-border bg-[#0F1B1A] px-6 flex items-center justify-center">
        <span
          className="text-[29px] leading-none font-semibold tracking-[-0.04em] text-[#F4F2ED]"
          style={{ fontFamily: "var(--font-fraunces), Georgia, serif" }}
          aria-label="Smart Hotel"
        >
          Smart <span className="italic font-medium text-[#E87332]">Hotel</span>
        </span>
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
                "flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-200",
                isActive
                  ? "bg-[#9F4724] text-white border-l-2 border-[#E87332] shadow-sm"
                  : "text-white hover:bg-[#C4622D]/20 hover:text-[#F28A4B]"
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
            <h4 className="text-xs font-semibold text-white truncate">
              {user?.name || "Employee"}
            </h4>
            <span className="text-[10px] text-white/70 uppercase tracking-wider">
              {user?.role || "Staff"}
            </span>
          </div>
        </div>
        <button
          onClick={logout}
          className="w-full flex items-center justify-center gap-2 px-3 py-2 text-xs font-medium text-white bg-[#9F4724] hover:bg-[#C4622D] border border-[#E87332]/40 rounded-lg transition-all duration-200 cursor-pointer"
        >
          <LogOut className="w-3.5 h-3.5" />
          Sign Out
        </button>
      </div>
    </aside>
  );
}
export default Sidebar;
