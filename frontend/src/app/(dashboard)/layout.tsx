"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { Sidebar } from "@/components/common/Sidebar";
import { Navbar } from "@/components/common/Navbar";


export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const { isAuthenticated, initialize } = useAuthStore();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    initialize();
    setMounted(true);
  }, [initialize]);

  useEffect(() => {
    if (mounted && !isAuthenticated) {
      router.push("/login");
    }

  }, [mounted, isAuthenticated, router]);

  if (!mounted || !isAuthenticated) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-bg-dark text-text-secondary text-sm">
        Authenticating...
      </div>
    );
  }

  return (
    <div className="flex h-screen bg-bg-dark text-text-primary overflow-hidden">
      {/* Sidebar Navigation */}
      <Sidebar />

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Top Navbar */}
        <Navbar />

        {/* Dynamic Page Views */}
        <main className="flex-1 overflow-x-hidden overflow-y-auto bg-bg-dark p-8 relative">
          {/* Subtle background glows */}
          <div className="absolute top-0 right-1/4 w-96 h-96 bg-primary/5 rounded-full blur-[100px] pointer-events-none" />
          {children}
        </main>
      </div>
    </div>
  );
}
