"use client";

import { dashboardApi } from "@/services/dashboardApi";
import { useQuery } from "@tanstack/react-query";
import { BadgeDollarSign,BedDouble,CirclePercent,Star,TrendingUp } from "lucide-react";

const cards = [
  { key: "revenue", label: "Revenue", icon: BadgeDollarSign, color: "pine" },
  { key: "occupancy", label: "Occupancy", icon: BedDouble, color: "teal" },
  { key: "adr", label: "ADR", icon: TrendingUp, color: "cedar" },
  { key: "profitMargin", label: "Profit Margin", icon: CirclePercent, color: "ember" },
  { key: "guestSatisfaction", label: "Guest Satisfaction", icon: Star, color: "pine" },
] as const;

const colorMap = {
  teal: { bg: "bg-[#2F5C52]/10", border: "border-[#2F5C52]/20", icon: "text-[#2F5C52]" },
  pine: { bg: "bg-[#2F5C52]/10", border: "border-[#2F5C52]/20", icon: "text-[#2F5C52]" },
  ember: { bg: "bg-[#C4622D]/10", border: "border-[#C4622D]/20", icon: "text-[#C4622D]" },
  cedar: { bg: "bg-[#6B4A3A]/10", border: "border-[#6B4A3A]/20", icon: "text-[#6B4A3A]" },
};

export default function OwnerDashboardPage() {
  const { data, isLoading } = useQuery({ queryKey: ["owner-metrics"], queryFn: () => dashboardApi.getOwnerMetrics() });
  const values = {
    revenue: data ? `LKR ${data.revenue.toLocaleString()}` : "—",
    occupancy: data ? `${data.occupancy.toFixed(1)}%` : "—",
    adr: data ? `LKR ${data.adr.toLocaleString(undefined, { maximumFractionDigits: 0 })}` : "—",
    profitMargin: data?.profitMargin == null ? "N/A" : `${data.profitMargin.toFixed(1)}%`,
    guestSatisfaction: data?.guestSatisfaction == null ? "N/A" : `${data.guestSatisfaction.toFixed(1)} / 5`,
  };

  return <div className="p-6 lg:p-8 max-w-7xl mx-auto space-y-6">
    <div><h1 className="text-3xl font-bold text-[var(--text-primary)] tracking-tight" style={{ fontFamily: "var(--font-fraunces), serif" }}>Executive Dashboard</h1><p className="text-sm text-[var(--text-secondary)] mt-1">Single-property performance overview</p></div>
    <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-5 gap-4">{cards.map(({ key, label, icon: Icon, color }) => { const c = colorMap[color]; return <div key={key} className={`group relative p-5 rounded-2xl bg-[var(--card-dark)] border ${c.border} hover:shadow-lg transition-all duration-300 hover:-translate-y-0.5 overflow-hidden`}><div className={`absolute inset-0 ${c.bg} opacity-0 group-hover:opacity-100 transition-opacity duration-300 rounded-2xl`} /><div className="relative"><div className={`w-10 h-10 rounded-xl ${c.bg} flex items-center justify-center mb-4`}><Icon className={`w-5 h-5 ${c.icon}`} /></div><p className="text-xs text-[var(--text-secondary)] font-medium uppercase tracking-wider mb-2">{label}</p>{isLoading ? <div className="h-8 w-24 bg-[var(--text-primary)]/8 rounded animate-pulse" /> : <p className="text-2xl font-bold text-[var(--text-primary)]">{values[key]}</p>}<p className="text-xs text-[var(--text-secondary)] mt-2">{key === "revenue" ? (data?.revenueTrend == null ? "No prior-month baseline" : `${data.revenueTrend >= 0 ? "+" : ""}${data.revenueTrend.toFixed(1)}% vs prior month`) : key === "profitMargin" ? "Cost data unavailable" : "Current property aggregate"}</p></div></div>; })}</div>
  </div>;
}
