"use client";

import { Navbar } from "@/components/common/Navbar/Navbar";
import { Sidebar } from "@/components/common/Sidebar/Sidebar";
import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { Menu,X } from "lucide-react";
import { useRouter } from "next/navigation";
import React,{ useEffect,useState } from "react";

export default function OwnerLayout({ children }: { children: React.ReactNode }) {
  const { user, isAuthenticated, mustResetPassword, initialize } = useAuthStore();
  const router = useRouter();
  const [hydrated, setHydrated] = useState(false);
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);

  useEffect(() => {
    let active = true;
    void initialize().finally(() => {
      if (active) setHydrated(true);
    });
    return () => { active = false; };
  }, [initialize]);
  useEffect(() => {
    if (!hydrated) return;
    if (!isAuthenticated || !user) return void router.replace("/staff/login");
    if (mustResetPassword) return void router.replace("/portal/force-reset-password");
    if (user.profileCompletionRequired) return void router.replace("/portal/complete-profile");
    if (String(user.role) !== "Owner") router.replace("/dashboard");
  }, [hydrated, isAuthenticated, user, mustResetPassword, router]);

  if (!hydrated || !isAuthenticated || !user || String(user.role) !== "Owner") {
    return <div className="staff-portal-theme min-h-screen bg-[#0F1B1A] flex items-center justify-center"><div className="flex flex-col items-center gap-4"><div className="w-10 h-10 rounded-full border-2 border-[#C4622D] border-t-transparent animate-spin" /><p className="text-xs text-[var(--text-secondary)] font-sans">Loading dashboard…</p></div></div>;
  }

  return (
    <div className="staff-portal-theme min-h-screen bg-[#0F1B1A] text-[var(--text-primary)] flex font-sans">
      <div className="hidden lg:block h-screen sticky top-0 shrink-0"><Sidebar className="h-full" /></div>
      {isDrawerOpen && <div className="fixed inset-0 z-40 bg-black/60 backdrop-blur-sm lg:hidden" onClick={() => setIsDrawerOpen(false)} />}
      <div className={`fixed left-0 top-0 h-full z-50 transition-transform duration-300 lg:hidden ${isDrawerOpen ? "translate-x-0" : "-translate-x-full"}`}>
        <Sidebar className="h-full shadow-2xl" />
        <button onClick={() => setIsDrawerOpen(false)} className="absolute top-4 right-[-40px] p-2 bg-[var(--card-dark)] rounded-r-lg border border-l-0 border-[var(--card-border)] text-[var(--text-secondary)]"><X className="w-4 h-4" /></button>
      </div>
      <div className="flex-1 flex flex-col min-h-screen overflow-hidden">
        <div className="flex items-center border-b border-[var(--card-border)] bg-[var(--card-dark)]/80 backdrop-blur-lg sticky top-0 z-30">
          <button onClick={() => setIsDrawerOpen(true)} className="lg:hidden ml-3 p-2 rounded-lg hover:bg-white/5 text-[var(--text-secondary)] transition-colors"><Menu className="w-5 h-5" /></button>
          <Navbar className="flex-1" />
        </div>
        <main className="flex-1 overflow-y-auto">{children}</main>
      </div>
    </div>
  );
}
