"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { Mail,ShieldCheck,User } from "lucide-react";

export default function StaffSettingsPage() {
  const user = useAuthStore((state) => state.user);

  return (
    <div className="mx-auto max-w-3xl space-y-6 p-6 lg:p-8">
      <div>
        <h1 className="text-3xl font-bold text-[var(--text-primary)]" style={{ fontFamily: "var(--font-fraunces), serif" }}>
          Profile Settings
        </h1>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">Your SmartHotel staff account information.</p>
      </div>

      <section className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-6">
        <div className="mb-6 flex items-center gap-4">
          <div className="flex h-14 w-14 items-center justify-center rounded-full bg-[#C4622D] text-lg font-bold text-white">
            {(user?.name || "Staff User").split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join("").toUpperCase()}
          </div>
          <div>
            <h2 className="font-semibold text-[var(--text-primary)]">{user?.name || "Staff User"}</h2>
            <p className="text-sm text-[var(--text-secondary)]">{String(user?.role || "Staff")}</p>
          </div>
        </div>

        <div className="divide-y divide-[var(--card-border)]">
          <div className="flex items-center gap-3 py-4"><User className="h-4 w-4 text-[#E87332]" /><div><p className="text-xs text-[var(--text-secondary)]">Full name</p><p className="text-sm text-[var(--text-primary)]">{user?.name || "—"}</p></div></div>
          <div className="flex items-center gap-3 py-4"><Mail className="h-4 w-4 text-[#E87332]" /><div><p className="text-xs text-[var(--text-secondary)]">Work email</p><p className="text-sm text-[var(--text-primary)]">{user?.email || "—"}</p></div></div>
          <div className="flex items-center gap-3 py-4"><ShieldCheck className="h-4 w-4 text-[#E87332]" /><div><p className="text-xs text-[var(--text-secondary)]">Portal role</p><p className="text-sm text-[var(--text-primary)]">{String(user?.role || "Staff")}</p></div></div>
        </div>
      </section>
    </div>
  );
}
