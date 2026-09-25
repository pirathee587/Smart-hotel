"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { dashboardApi } from "@/services/dashboardApi";
import { useQuery } from "@tanstack/react-query";
import {
AlertCircle,
BedDouble,
CalendarCheck,
CheckSquare,
ChevronRight,
Clock,
Hotel,
TrendingUp,
Users,
} from "lucide-react";
import Link from "next/link";
import React from "react";

// ── Stat card component ──────────────────────────────────────────────────────

interface StatCardProps {
  label: string;
  value: string | number;
  subtext?: string;
  icon: React.ElementType;
  color: "teal" | "pine" | "ember" | "cedar";
  href?: string;
  loading?: boolean;
}

const colorMap = {
  teal: {
    bg: "bg-[#2F5C52]/10",
    border: "border-[#2F5C52]/20",
    icon: "text-[#2F5C52]",
    badge: "bg-[#2F5C52]/15 text-[#2F5C52]",
  },
  pine: {
    bg: "bg-[#2F5C52]/10",
    border: "border-[#2F5C52]/20",
    icon: "text-[#2F5C52]",
    badge: "bg-[#2F5C52]/15 text-[#2F5C52]",
  },
  ember: {
    bg: "bg-[#C4622D]/10",
    border: "border-[#C4622D]/20",
    icon: "text-[#C4622D]",
    badge: "bg-[#C4622D]/15 text-[#C4622D]",
  },
  cedar: {
    bg: "bg-[#6B4A3A]/10",
    border: "border-[#6B4A3A]/20",
    icon: "text-[#6B4A3A]",
    badge: "bg-[#6B4A3A]/15 text-[#6B4A3A]",
  },
};

function StatCard({ label, value, subtext, icon: Icon, color, href, loading }: StatCardProps) {
  const c = colorMap[color];
  const inner = (
    <div
      className={`group relative p-5 rounded-2xl bg-[var(--card-dark)] border ${c.border} hover:shadow-lg transition-all duration-300 hover:-translate-y-0.5 cursor-default overflow-hidden`}
    >
      {/* Background gradient */}
      <div className={`absolute inset-0 ${c.bg} opacity-0 group-hover:opacity-100 transition-opacity duration-300 rounded-2xl`} />

      <div className="relative flex items-start justify-between gap-4">
        <div className="flex-1">
          <p className="text-xs text-[var(--text-secondary)] font-medium uppercase tracking-wider mb-2">{label}</p>
          {loading ? (
            <div className="space-y-2 animate-pulse">
              <div className="h-8 w-24 bg-[var(--text-primary)]/10 rounded-lg" />
              <div className="h-3 w-32 bg-[var(--text-primary)]/6 rounded" />
            </div>
          ) : (
            <>
              <p className="text-3xl font-bold text-[var(--text-primary)] tracking-tight" style={{ fontFamily: "var(--font-fraunces), serif" }}>
                {value}
              </p>
              {subtext && <p className="text-xs text-[var(--text-secondary)] mt-1">{subtext}</p>}
            </>
          )}
        </div>
        <div className={`w-11 h-11 rounded-xl ${c.bg} border ${c.border} flex items-center justify-center shrink-0`}>
          <Icon className={`w-5 h-5 ${c.icon}`} />
        </div>
      </div>

      {href && (
        <div className="relative mt-4 pt-3 border-t border-[var(--card-border)] flex items-center justify-between">
          <span className="text-xs text-[var(--text-secondary)]">View details</span>
          <ChevronRight className="w-3.5 h-3.5 text-[var(--text-secondary)] group-hover:translate-x-0.5 transition-transform" />
        </div>
      )}
    </div>
  );

  if (href) {
    return <Link href={href} className="block">{inner}</Link>;
  }
  return inner;
}

// ── Recent activity item ─────────────────────────────────────────────────────

function ActivityItem({ text, time, type }: { text: string; time: string; type: "booking" | "task" | "alert" }) {
  const colors = {
    booking: "bg-[#2F5C52]/20 text-[#2F5C52]",
    task: "bg-[#C4622D]/20 text-[#C4622D]",
    alert: "bg-[#6B4A3A]/20 text-[#6B4A3A]",
  };
  return (
    <div className="flex items-start gap-3 py-3 border-b border-[var(--card-border)] last:border-0">
      <div className={`w-2 h-2 rounded-full mt-1.5 shrink-0 ${colors[type].split(" ")[0]}`} />
      <div className="flex-1 min-w-0">
        <p className="text-sm text-[var(--text-primary)] leading-snug">{text}</p>
        <p className="text-xs text-[var(--text-secondary)] mt-0.5">{time}</p>
      </div>
    </div>
  );
}

// ── Main Page ────────────────────────────────────────────────────────────────

export default function DashboardOverviewPage() {
  const { user } = useAuthStore();

  const { data: rooms = [], isLoading: roomsLoading } = useQuery({
    queryKey: ["dashboard-rooms"],
    queryFn: () => dashboardApi.getRooms({ pageSize: 200 }),
    staleTime: 60_000,
  });

  const { data: bookings = [], isLoading: bookingsLoading } = useQuery({
    queryKey: ["dashboard-bookings"],
    queryFn: () => dashboardApi.getBookings({ pageSize: 200 }),
    staleTime: 60_000,
  });

  const { data: tasks = [], isLoading: tasksLoading } = useQuery({
    queryKey: ["dashboard-tasks"],
    queryFn: () => dashboardApi.getTasks(),
    staleTime: 60_000,
  });

  // Derived stats
  const availableRooms = rooms.filter((r) => r.status === 0).length;
  const occupiedRooms = rooms.filter((r) => r.status === 1).length;
  const dirtyRooms = rooms.filter((r) => r.status === 2 || r.status === 3).length;

  const checkedInToday = bookings.filter((b) => b.status === 2).length;
  const pendingPayment = bookings.filter((b) => b.status === 0).length;
  const confirmedBookings = bookings.filter((b) => b.status === 1).length;

  const pendingTasks = tasks.filter((t) => t.status === 0).length;
  const inProgressTasks = tasks.filter((t) => t.status === 3).length;

  const isLoading = roomsLoading || bookingsLoading || tasksLoading;

  const greeting = () => {
    const h = new Date().getHours();
    if (h < 12) return "Good morning";
    if (h < 17) return "Good afternoon";
    return "Good evening";
  };

  return (
    <div className="p-6 lg:p-8 space-y-8 max-w-7xl mx-auto">
      {/* ── Header ─────────────────────────────────────────────────── */}
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-4">
        <div>
          <p className="text-xs text-[var(--text-secondary)] font-medium uppercase tracking-wider mb-1">
            {new Date().toLocaleDateString("en-US", { weekday: "long", year: "numeric", month: "long", day: "numeric" })}
          </p>
          <h1
            className="text-3xl sm:text-4xl font-bold text-[var(--text-primary)] tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            {greeting()}, {user?.name?.split(" ")[0] || "Admin"}
          </h1>
          <p className="text-sm text-[var(--text-secondary)] mt-1">
            Here&apos;s what&apos;s happening at SmartHotel Maskeliya today.
          </p>
        </div>

        <div className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-[#2F5C52]/10 border border-[#2F5C52]/20">
          <Hotel className="w-4 h-4 text-[#2F5C52]" />
          <span className="text-sm font-medium text-[#2F5C52]">Maskeliya, Sri Lanka</span>
        </div>
      </div>

      {/* ── Primary Stats ────────────────────────────────────────────── */}
      <div>
        <h2 className="text-xs font-semibold uppercase tracking-wider text-[var(--text-secondary)] mb-4">
          Room Status
        </h2>
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <StatCard
            label="Available"
            value={availableRooms}
            subtext={`of ${rooms.length} total rooms`}
            icon={BedDouble}
            color="pine"
            href="/dashboard/rooms"
            loading={isLoading}
          />
          <StatCard
            label="Occupied"
            value={occupiedRooms}
            subtext="guests currently staying"
            icon={Users}
            color="teal"
            href="/dashboard/rooms"
            loading={isLoading}
          />
          <StatCard
            label="Needs Cleaning"
            value={dirtyRooms}
            subtext="pending housekeeping"
            icon={AlertCircle}
            color="ember"
            href="/dashboard/rooms"
            loading={isLoading}
          />
          <StatCard
            label="Check-ins Active"
            value={checkedInToday}
            subtext="currently checked in"
            icon={TrendingUp}
            color="cedar"
            href="/dashboard/bookings"
            loading={isLoading}
          />
        </div>
      </div>

      {/* ── Booking Stats ─────────────────────────────────────────────── */}
      <div>
        <h2 className="text-xs font-semibold uppercase tracking-wider text-[var(--text-secondary)] mb-4">
          Bookings
        </h2>
        <div className="grid grid-cols-2 lg:grid-cols-3 gap-4">
          <StatCard
            label="Awaiting Payment"
            value={pendingPayment}
            subtext="require confirmation"
            icon={Clock}
            color="ember"
            href="/dashboard/bookings"
            loading={isLoading}
          />
          <StatCard
            label="Confirmed"
            value={confirmedBookings}
            subtext="upcoming stays"
            icon={CalendarCheck}
            color="pine"
            href="/dashboard/bookings"
            loading={isLoading}
          />
          <StatCard
            label="Tasks Pending"
            value={pendingTasks}
            subtext={`${inProgressTasks} in progress`}
            icon={CheckSquare}
            color="teal"
            href="/dashboard/tasks"
            loading={isLoading}
          />
        </div>
      </div>

      {/* ── Bottom panels ─────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Recent Bookings */}
        <div className="p-6 rounded-2xl bg-[var(--card-dark)] border border-[var(--card-border)]">
          <div className="flex items-center justify-between mb-4">
            <h3 className="font-semibold text-[var(--text-primary)]" style={{ fontFamily: "var(--font-fraunces), serif" }}>
              Recent Bookings
            </h3>
            <Link href="/dashboard/bookings" className="text-xs text-[#C4622D] hover:underline flex items-center gap-1">
              View all <ChevronRight className="w-3 h-3" />
            </Link>
          </div>

          {bookingsLoading ? (
            <div className="space-y-3 animate-pulse">
              {[...Array(4)].map((_, i) => (
                <div key={i} className="flex gap-3 py-3 border-b border-[var(--card-border)]">
                  <div className="w-2 h-2 rounded-full bg-[var(--text-primary)]/10 mt-1.5" />
                  <div className="flex-1 space-y-1.5">
                    <div className="h-4 bg-[var(--text-primary)]/10 rounded w-3/4" />
                    <div className="h-3 bg-[var(--text-primary)]/6 rounded w-1/3" />
                  </div>
                </div>
              ))}
            </div>
          ) : bookings.length === 0 ? (
            <p className="text-sm text-[var(--text-secondary)] text-center py-8">No bookings found.</p>
          ) : (
            <div>
              {bookings.slice(0, 5).map((b) => (
                <ActivityItem
                  key={b.id}
                  text={`Room ${b.roomNumber} — ${b.customerLastName || "Guest"} · ${b.bookingReference || b.id.slice(0, 8)}`}
                  time={`Check-in: ${new Date(b.checkInDate).toLocaleDateString()}`}
                  type="booking"
                />
              ))}
            </div>
          )}
        </div>

        {/* Recent Tasks */}
        <div className="p-6 rounded-2xl bg-[var(--card-dark)] border border-[var(--card-border)]">
          <div className="flex items-center justify-between mb-4">
            <h3 className="font-semibold text-[var(--text-primary)]" style={{ fontFamily: "var(--font-fraunces), serif" }}>
              Active Tasks
            </h3>
            <Link href="/dashboard/tasks" className="text-xs text-[#C4622D] hover:underline flex items-center gap-1">
              View all <ChevronRight className="w-3 h-3" />
            </Link>
          </div>

          {tasksLoading ? (
            <div className="space-y-3 animate-pulse">
              {[...Array(4)].map((_, i) => (
                <div key={i} className="flex gap-3 py-3 border-b border-[var(--card-border)]">
                  <div className="w-2 h-2 rounded-full bg-[var(--text-primary)]/10 mt-1.5" />
                  <div className="flex-1 space-y-1.5">
                    <div className="h-4 bg-[var(--text-primary)]/10 rounded w-3/4" />
                    <div className="h-3 bg-[var(--text-primary)]/6 rounded w-1/3" />
                  </div>
                </div>
              ))}
            </div>
          ) : tasks.filter((t) => t.status < 4).length === 0 ? (
            <p className="text-sm text-[var(--text-secondary)] text-center py-8">No active tasks.</p>
          ) : (
            <div>
              {tasks.filter((t) => t.status < 4).slice(0, 5).map((t) => (
                <ActivityItem
                  key={t.id}
                  text={`${t.title} — Floor ${t.targetFloor} · ${t.requiredRole}`}
                  time={t.assignedEmployeeName ? `Assigned to ${t.assignedEmployeeName}` : "Unassigned"}
                  type={t.status === 3 ? "task" : "alert"}
                />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
