"use client";

import React, { useEffect, useState } from "react";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";
import api from "@/lib/axios";
import {
  BedDouble,
  Users,
  CheckSquare,
  TrendingUp,
  AlertTriangle,
  ArrowUpRight,
  Plus,
} from "lucide-react";
import Link from "next/link";

interface DashboardSummary {
  rooms: {
    total: number;
    available: number;
    occupied: number;
    reserved: number;
    needsCleaning: number;
    maintenance: number;
    occupancyRate: number;
  };
}



export default function DashboardPage() {
  const [stats, setStats] = useState<DashboardSummary | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const res = await api.get<DashboardSummary>("/api/dashboard/summary");
        setStats(res.data);
      } catch (err) {
        // Fallback demo data if backend has issue
        setStats({
          rooms: {
            total: 120,
            available: 30,
            occupied: 84,
            reserved: 0,
            needsCleaning: 0,
            maintenance: 6,
            occupancyRate: 70,
          }
        });
      } finally {
        setIsLoading(false);
      }
    };
    fetchStats();
  }, []);


  return (
    <div className="space-y-8 relative">
      {/* Welcome Banner */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-text-primary to-text-secondary bg-clip-text text-transparent">
            Welcome Back, Admin
          </h1>
          <p className="text-sm text-text-secondary mt-1">
            Here is what's happening at SmartHotel today.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Link href="/bookings">
            <button className="flex items-center gap-2 px-4 py-2 bg-primary hover:bg-primary-hover text-white rounded-lg text-sm font-medium transition-all cursor-pointer">
              <Plus className="w-4 h-4" /> New Booking
            </button>
          </Link>
        </div>
      </div>

      {/* Grid Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
        <Card hoverable className="relative overflow-hidden">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-text-secondary uppercase tracking-wider">
                Occupancy Rate
              </p>
              <h3 className="text-2xl font-bold mt-2">
                {isLoading ? "..." : `${stats?.rooms?.occupancyRate}%`}
              </h3>
            </div>
            <div className="w-10 h-10 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary">
              <TrendingUp className="w-5 h-5" />
            </div>
          </div>
          <div className="mt-4 flex items-center gap-1 text-xs text-emerald-400">
            <span>+4.2%</span>
            <span className="text-text-secondary">since yesterday</span>
          </div>
        </Card>

        <Card hoverable className="relative overflow-hidden">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-text-secondary uppercase tracking-wider">
                Available Rooms
              </p>
              <h3 className="text-2xl font-bold mt-2">
                {isLoading ? "..." : stats?.rooms?.available}
              </h3>
            </div>
            <div className="w-10 h-10 rounded-lg bg-secondary/10 border border-secondary/20 flex items-center justify-center text-secondary">
              <BedDouble className="w-5 h-5" />
            </div>
          </div>
          <div className="mt-4 text-xs text-text-secondary">
            Out of <span className="text-text-primary font-medium">{stats?.rooms?.total}</span> total rooms
          </div>
        </Card>

        <Card hoverable className="relative overflow-hidden">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-text-secondary uppercase tracking-wider">
                Rooms Occupied
              </p>
              <h3 className="text-2xl font-bold mt-2">
                {isLoading ? "..." : stats?.rooms?.occupied}
              </h3>
            </div>
            <div className="w-10 h-10 rounded-lg bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400">
              <Users className="w-5 h-5" />
            </div>
          </div>
          <div className="mt-4 text-xs text-text-secondary">
            Currently checked-in guests
          </div>
        </Card>

        <Card hoverable className="relative overflow-hidden">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-text-secondary uppercase tracking-wider">
                In Maintenance
              </p>
              <h3 className="text-2xl font-bold mt-2 text-accent">
                {isLoading ? "..." : stats?.rooms?.maintenance}
              </h3>
            </div>
            <div className="w-10 h-10 rounded-lg bg-accent/10 border border-accent/20 flex items-center justify-center text-accent">
              <AlertTriangle className="w-5 h-5" />
            </div>
          </div>
          <div className="mt-4 text-xs text-text-secondary">
            Requires housekeeping/repairs
          </div>
        </Card>
      </div>

      {/* Main Dashboard Panels */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Recent Task List Summary */}
        <Card className="lg:col-span-2 space-y-6">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-lg font-bold">Housekeeping & Maintenance Tasks</h3>
              <p className="text-xs text-text-secondary mt-0.5">
                Real-time active staff operations
              </p>
            </div>
            <Link
              href="/tasks"
              className="text-xs text-primary font-medium hover:underline flex items-center gap-1"
            >
              View All <ArrowUpRight className="w-3.5 h-3.5" />
            </Link>
          </div>

          <div className="divide-y divide-card-border/50">
            {/* Task Row 1 */}
            <div className="py-4 flex items-center justify-between first:pt-0 last:pb-0">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-lg bg-white/5 border border-card-border flex items-center justify-center text-text-secondary">
                  <CheckSquare className="w-4 h-4" />
                </div>
                <div>
                  <h4 className="text-sm font-semibold">Deep Clean Room 304</h4>
                  <span className="text-xs text-text-secondary">Required Role: Housekeeper</span>
                </div>
              </div>
              <div className="flex items-center gap-3">
                <Badge variant="warning">In Progress</Badge>
                <span className="text-xs text-text-secondary">Floor 3</span>
              </div>
            </div>

            {/* Task Row 2 */}
            <div className="py-4 flex items-center justify-between last:pb-0">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-lg bg-white/5 border border-card-border flex items-center justify-center text-text-secondary">
                  <CheckSquare className="w-4 h-4" />
                </div>
                <div>
                  <h4 className="text-sm font-semibold">AC Unit Repair Room 102</h4>
                  <span className="text-xs text-text-secondary">Required Role: Maintenance</span>
                </div>
              </div>
              <div className="flex items-center gap-3">
                <Badge variant="danger">Pending</Badge>
                <span className="text-xs text-text-secondary">Floor 1</span>
              </div>
            </div>

            {/* Task Row 3 */}
            <div className="py-4 flex items-center justify-between last:pb-0">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-lg bg-white/5 border border-card-border flex items-center justify-center text-text-secondary">
                  <CheckSquare className="w-4 h-4" />
                </div>
                <div>
                  <h4 className="text-sm font-semibold">Replace Linens Room 205</h4>
                  <span className="text-xs text-text-secondary">Required Role: Housekeeper</span>
                </div>
              </div>
              <div className="flex items-center gap-3">
                <Badge variant="success">Completed</Badge>
                <span className="text-xs text-text-secondary">Floor 2</span>
              </div>
            </div>
          </div>
        </Card>

        {/* Quick Operations and Status Panel */}
        <Card className="space-y-6">
          <div>
            <h3 className="text-lg font-bold">Quick Status Operations</h3>
            <p className="text-xs text-text-secondary mt-0.5">
              Instant shortcuts
            </p>
          </div>
          <div className="space-y-3">
            <button className="w-full py-3 bg-white/5 hover:bg-white/10 text-sm font-medium border border-card-border hover:border-primary/20 rounded-lg flex items-center justify-center gap-2 cursor-pointer transition-all">
              Mark Room Maintenance
            </button>
            <button className="w-full py-3 bg-white/5 hover:bg-white/10 text-sm font-medium border border-card-border hover:border-primary/20 rounded-lg flex items-center justify-center gap-2 cursor-pointer transition-all">
              Staff Shift Check-in
            </button>
            <button className="w-full py-3 bg-white/5 hover:bg-white/10 text-sm font-medium border border-card-border hover:border-primary/20 rounded-lg flex items-center justify-center gap-2 cursor-pointer transition-all">
              Generate Today's Checkouts
            </button>
          </div>
        </Card>
      </div>
    </div>
  );
}
