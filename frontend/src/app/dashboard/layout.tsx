"use client";

import { Navbar } from "@/components/common/Navbar/Navbar";
import { Sidebar } from "@/components/common/Sidebar/Sidebar";
import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { canAccessDashboardPath,getStaffHome } from "@/lib/staffNavigation";
import { Menu,X } from "lucide-react";
import { usePathname,useRouter } from "next/navigation";
import React,{ useEffect,useState } from "react";

const ALLOWED_ROLES = ["Owner", "Admin", "Manager", "Employee", "FrontDesk", "Receptionist", "Housekeeping", "Housekeeper", "Maintenance", "Kitchen", "Chef", "Waiter", "Security"];

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const { user, isAuthenticated, mustResetPassword, initialize, logout } = useAuthStore();
  const router = useRouter();
  const pathname = usePathname();
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    let active = true;
    void initialize().finally(() => {
      if (active) setHydrated(true);
    });
    return () => { active = false; };
  }, [initialize]);

  useEffect(() => {
    if (!hydrated) return;
    if (!isAuthenticated || !user) {
      router.replace("/staff/login");
      return;
    }
    if (mustResetPassword) {
      router.replace("/portal/force-reset-password");
      return;
    }
    if (user.profileCompletionRequired) {
      router.replace("/portal/complete-profile");
      return;
    }
    const role = String(user.role ?? "");
    if (!ALLOWED_ROLES.includes(role)) {
      router.replace("/staff/login");
      return;
    }
    if (role !== "Owner" && !user.departmentId) {
      logout();
      router.replace("/staff/login?error=missing-department");
      return;
    }
    if (!canAccessDashboardPath(user, pathname)) {
      router.replace(getStaffHome(user));
    }
  }, [hydrated, isAuthenticated, user, mustResetPassword, router, pathname, logout]);

  if (!hydrated || !isAuthenticated || !user) {
    return (
      <div className="staff-portal-theme min-h-screen bg-[#0F1B1A] flex items-center justify-center">
        <div className="flex flex-col items-center gap-4">
          <div className="w-10 h-10 rounded-full border-2 border-[#C4622D] border-t-transparent animate-spin" />
          <p className="text-xs text-[var(--text-secondary)] font-sans">Loading dashboard…</p>
        </div>
      </div>
    );
  }

  return (
    <div className="staff-portal-theme min-h-screen bg-[#0F1B1A] text-[var(--text-primary)] flex font-sans">
      {/* ── Desktop Sidebar ──────────────────────────────────────── */}
      <div className="hidden lg:block h-screen sticky top-0 shrink-0">
        <Sidebar className="h-full" />
      </div>

      {/* ── Mobile Drawer Overlay ─────────────────────────────────── */}
      {isDrawerOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/60 backdrop-blur-sm lg:hidden"
          onClick={() => setIsDrawerOpen(false)}
        />
      )}

      {/* ── Mobile Sidebar Drawer ─────────────────────────────────── */}
      <div
        className={`fixed left-0 top-0 h-full z-50 transition-transform duration-300 lg:hidden ${
          isDrawerOpen ? "translate-x-0" : "-translate-x-full"
        }`}
      >
        <Sidebar className="h-full shadow-2xl" />
        <button
          onClick={() => setIsDrawerOpen(false)}
          className="absolute top-4 right-[-40px] p-2 bg-[var(--card-dark)] rounded-r-lg border border-l-0 border-[var(--card-border)] text-[var(--text-secondary)]"
          aria-label="Close sidebar"
        >
          <X className="w-4 h-4" />
        </button>
      </div>

      {/* ── Main Content ─────────────────────────────────────────── */}
      <div className="flex-1 flex flex-col min-h-screen overflow-hidden">
        <div className="hidden lg:flex items-center border-b border-[var(--card-border)] bg-[#16302C]/80 backdrop-blur-lg sticky top-0 z-30">
          <Navbar className="flex-1" />
        </div>
        {/* Mobile topbar */}
        <header className="lg:hidden h-14 flex items-center gap-3 px-4 border-b border-[var(--card-border)] bg-[var(--card-dark)]/80 backdrop-blur-lg sticky top-0 z-30">
          <button
            onClick={() => setIsDrawerOpen(true)}
            className="p-2 rounded-lg hover:bg-white/5 text-[var(--text-secondary)] transition-colors"
            aria-label="Open sidebar"
          >
            <Menu className="w-5 h-5" />
          </button>
          <span
            className="font-bold text-base text-[var(--text-primary)] tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            SmartHotel
          </span>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-y-auto">
          {children}
        </main>
      </div>
    </div>
  );
}
