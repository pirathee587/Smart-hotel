"use client";

import { dashboardApi,type GuestSearchResultDto } from "@/services/dashboardApi";
import { useQuery } from "@tanstack/react-query";
import {
Award,
CalendarCheck,
Globe,
Loader2,
Lock,
Mail,
Phone,
Search,
ShieldCheck,
Users,
} from "lucide-react";
import Link from "next/link";
import { useState } from "react";

export default function GuestsDirectoryPage() {
  const [search, setSearch] = useState("");

  const { data: guests = [], isLoading, isFetching } = useQuery<GuestSearchResultDto[]>({
    queryKey: ["guests-search", search],
    queryFn: () => dashboardApi.searchGuests(search || undefined, 50),
    staleTime: 15_000,
  });

  return (
    <div className="p-6 lg:p-8 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1
            className="text-3xl font-bold text-[var(--text-primary)] tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            Guest Directory
          </h1>
          <p className="text-sm text-[var(--text-secondary)] mt-1">
            Authoritative guest lookup for Front Office operations with audit-logged access and sensitive field masking.
          </p>
        </div>
        <div className="flex items-center gap-2 px-3 py-1.5 rounded-xl border border-[var(--card-border)] bg-[var(--card-dark)] text-xs text-[var(--text-secondary)]">
          <ShieldCheck className="w-4 h-4 text-emerald-500 shrink-0" />
          <span>Privacy & Data Minimization Enforced</span>
        </div>
      </div>

      {/* Search Input */}
      <div className="relative max-w-md">
        <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--text-secondary)]" />
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search by name, email, phone, or national ID..."
          className="w-full pl-10 pr-10 py-2.5 text-sm rounded-xl bg-[var(--card-dark)] border border-[var(--card-border)] text-[var(--text-primary)] placeholder:text-[var(--text-secondary)] focus:outline-none focus:ring-2 focus:ring-[#C4622D]/40"
        />
        {isFetching && (
          <Loader2 className="absolute right-3.5 top-1/2 -translate-y-1/2 w-4 h-4 animate-spin text-[#C4622D]" />
        )}
      </div>

      {/* Desktop Table */}
      <div className="hidden md:block bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl overflow-hidden shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--card-border)] bg-[var(--text-primary)]/2">
                {["Guest", "Contact", "Nationality", "Loyalty Tier", "National ID / Passport", "Role", "Actions"].map(
                  (h) => (
                    <th
                      key={h}
                      className="px-4 py-3 text-left text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider"
                    >
                      {h}
                    </th>
                  )
                )}
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                [...Array(5)].map((_, i) => (
                  <tr key={i} className="border-b border-[var(--card-border)] animate-pulse">
                    {[...Array(7)].map((__, j) => (
                      <td key={j} className="px-4 py-3">
                        <div className="h-4 bg-[var(--text-primary)]/8 rounded w-3/4" />
                      </td>
                    ))}
                  </tr>
                ))
              ) : guests.length === 0 ? (
                <tr>
                  <td colSpan={7} className="text-center py-16">
                    <div className="flex flex-col items-center gap-3">
                      <Users className="w-10 h-10 text-[var(--text-secondary)]/50" />
                      <p className="text-sm text-[var(--text-secondary)]">
                        No guest records matched your search criteria.
                      </p>
                    </div>
                  </td>
                </tr>
              ) : (
                guests.map((g) => (
                  <tr
                    key={g.id}
                    className="border-b border-[var(--card-border)] hover:bg-[var(--text-primary)]/2 transition-colors"
                  >
                    <td className="px-4 py-3 font-medium text-[var(--text-primary)]">
                      {g.fullName || "—"}
                    </td>
                    <td className="px-4 py-3 text-xs text-[var(--text-secondary)] space-y-0.5">
                      <div className="flex items-center gap-1.5">
                        <Mail className="w-3 h-3 text-[var(--text-secondary)]/70" />
                        <span>{g.email || "—"}</span>
                      </div>
                      {g.phone && (
                        <div className="flex items-center gap-1.5">
                          <Phone className="w-3 h-3 text-[var(--text-secondary)]/70" />
                          <span>{g.phone}</span>
                        </div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-xs text-[var(--text-secondary)]">
                      <div className="flex items-center gap-1.5">
                        <Globe className="w-3 h-3 text-[var(--text-secondary)]/70" />
                        <span>{g.nationality || "—"}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium border border-amber-400/30 bg-amber-500/10 text-amber-500">
                        <Award className="w-3 h-3" />
                        {g.loyaltyTier || "Silver"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1.5 font-mono text-xs text-[var(--text-secondary)]">
                        <Lock className="w-3 h-3 text-emerald-500/80" />
                        <span>{g.maskedNationalId || "—"}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border border-[var(--card-border)] bg-[var(--text-primary)]/5 text-[var(--text-secondary)]">
                        {g.role || "Guest"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <Link
                        href={`/dashboard/bookings?search=${encodeURIComponent(g.fullName || g.email || "")}`}
                        className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-medium bg-[#C4622D]/10 hover:bg-[#C4622D]/20 text-[#C4622D] border border-[#C4622D]/20 rounded-lg transition-colors cursor-pointer"
                      >
                        <CalendarCheck className="w-3 h-3" />
                        <span>Bookings</span>
                      </Link>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Mobile Cards */}
      <div className="md:hidden space-y-3">
        {isLoading ? (
          [...Array(4)].map((_, i) => (
            <div
              key={i}
              className="h-28 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl animate-pulse"
            />
          ))
        ) : guests.length === 0 ? (
          <div className="p-8 text-center bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl">
            <p className="text-sm text-[var(--text-secondary)]">No guest records found.</p>
          </div>
        ) : (
          guests.map((g) => (
            <div
              key={g.id}
              className="p-4 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl space-y-3"
            >
              <div className="flex items-start justify-between">
                <div>
                  <p className="font-medium text-[var(--text-primary)] text-sm">
                    {g.fullName || "—"}
                  </p>
                  <p className="text-xs text-[var(--text-secondary)] font-mono">
                    {g.maskedNationalId || "No ID on file"}
                  </p>
                </div>
                <span className="px-2 py-0.5 rounded-full text-xs font-medium border border-amber-400/30 bg-amber-500/10 text-amber-500">
                  {g.loyaltyTier || "Silver"}
                </span>
              </div>
              <div className="text-xs text-[var(--text-secondary)] space-y-1">
                <p className="flex items-center gap-1.5">
                  <Mail className="w-3 h-3" /> {g.email}
                </p>
                {g.phone && (
                  <p className="flex items-center gap-1.5">
                    <Phone className="w-3 h-3" /> {g.phone}
                  </p>
                )}
              </div>
              <div className="pt-1">
                <Link
                  href={`/dashboard/bookings?search=${encodeURIComponent(g.fullName || g.email || "")}`}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium bg-[#C4622D]/10 text-[#C4622D] border border-[#C4622D]/20 rounded-lg cursor-pointer"
                >
                  <CalendarCheck className="w-3 h-3" />
                  <span>View Bookings</span>
                </Link>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
