"use client";

import React, { useEffect, useState } from "react";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";
import api from "@/lib/axios";
import { Calendar, User, ArrowRightLeft, RefreshCw, LogIn, LogOut } from "lucide-react";

interface Booking {
  id: string;
  customerName: string;
  roomNumber: string;
  checkInDate: string;
  checkOutDate: string;
  status: number; // 0=Confirmed/PendingCheckin, 1=CheckedIn, 2=CheckedOut, 3=Cancelled
  totalAmount: number;
}

export default function BookingsPage() {
  const [bookings, setBookings] = useState<Booking[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const fetchBookings = async () => {
    setIsLoading(true);
    try {
      const res = await api.get<Booking[]>("/api/v1/bookings");
      setBookings(res.data);
    } catch {
      // Fallback demo bookings
      setBookings([
        {
          id: "b1",
          customerName: "Alice Johnson",
          roomNumber: "101",
          checkInDate: "2026-07-20",
          checkOutDate: "2026-07-25",
          status: 0,
          totalAmount: 500,
        },
        {
          id: "b2",
          customerName: "Bob Smith",
          roomNumber: "102",
          checkInDate: "2026-07-18",
          checkOutDate: "2026-07-22",
          status: 1,
          totalAmount: 400,
        },
        {
          id: "b3",
          customerName: "Charlie Brown",
          roomNumber: "301",
          checkInDate: "2026-07-10",
          checkOutDate: "2026-07-15",
          status: 2,
          totalAmount: 900,
        },
      ]);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchBookings();
  }, []);

  const handleCheckIn = async (bookingId: string) => {
    try {
      await api.post(`/api/v1/bookings/${bookingId}/check-in`);
      fetchBookings();
    } catch {
      setBookings((prev) =>
        prev.map((b) => (b.id === bookingId ? { ...b, status: 1 } : b))
      );
    }
  };

  const handleCheckOut = async (bookingId: string) => {
    try {
      await api.post(`/api/v1/bookings/${bookingId}/check-out`);
      fetchBookings();
    } catch {
      setBookings((prev) =>
        prev.map((b) => (b.id === bookingId ? { ...b, status: 2 } : b))
      );
    }
  };

  const getStatusBadge = (status: number) => {
    switch (status) {
      case 0:
        return <Badge variant="info">Confirmed</Badge>;
      case 1:
        return <Badge variant="success">Checked In</Badge>;
      case 2:
        return <Badge variant="ghost">Checked Out</Badge>;
      case 3:
        return <Badge variant="danger">Cancelled</Badge>;
      default:
        return <Badge variant="ghost">Unknown</Badge>;
    }
  };

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-text-primary to-text-secondary bg-clip-text text-transparent">
            Bookings Directory
          </h1>
          <p className="text-sm text-text-secondary mt-1">
            Check-in arriving guests and complete check-outs.
          </p>
        </div>
        <button
          onClick={fetchBookings}
          className="flex items-center gap-2 px-4 py-2 bg-white/5 border border-card-border hover:bg-white/10 rounded-lg text-sm font-medium transition-all cursor-pointer"
        >
          <RefreshCw className="w-4 h-4" /> Refresh
        </button>
      </div>

      {/* Bookings Card List */}
      <Card className="p-0 overflow-hidden border border-card-border/80">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse text-sm">
            <thead>
              <tr className="border-b border-card-border bg-white/[0.02]">
                <th className="p-4 font-semibold text-text-secondary">Guest</th>
                <th className="p-4 font-semibold text-text-secondary">Room</th>
                <th className="p-4 font-semibold text-text-secondary">Schedule</th>
                <th className="p-4 font-semibold text-text-secondary">Status</th>
                <th className="p-4 font-semibold text-text-secondary">Price</th>
                <th className="p-4 font-semibold text-text-secondary text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-card-border/50">
              {isLoading ? (
                <tr>
                  <td colSpan={6} className="p-8 text-center text-text-secondary">
                    Loading reservations...
                  </td>
                </tr>
              ) : bookings.length === 0 ? (
                <tr>
                  <td colSpan={6} className="p-8 text-center text-text-secondary">
                    No active reservations found.
                  </td>
                </tr>
              ) : (
                bookings.map((booking) => (
                  <tr key={booking.id} className="hover:bg-white/[0.01] transition-colors">
                    <td className="p-4">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary">
                          <User className="w-4 h-4" />
                        </div>
                        <span className="font-semibold">{booking.customerName}</span>
                      </div>
                    </td>
                    <td className="p-4 font-medium text-text-primary">
                      Room {booking.roomNumber}
                    </td>
                    <td className="p-4 text-xs text-text-secondary">
                      <div className="flex items-center gap-2">
                        <span>{booking.checkInDate}</span>
                        <ArrowRightLeft className="w-3 h-3 text-primary" />
                        <span>{booking.checkOutDate}</span>
                      </div>
                    </td>
                    <td className="p-4">{getStatusBadge(booking.status)}</td>
                    <td className="p-4 font-bold text-text-primary">
                      ${booking.totalAmount}
                    </td>
                    <td className="p-4 text-right">
                      {booking.status === 0 && (
                        <button
                          onClick={() => handleCheckIn(booking.id)}
                          className="px-3 py-1.5 bg-primary/10 border border-primary/20 hover:bg-primary text-primary hover:text-white rounded-lg text-xs font-semibold flex items-center gap-1.5 ml-auto transition-all cursor-pointer"
                        >
                          <LogIn className="w-3.5 h-3.5" /> Check In
                        </button>
                      )}
                      {booking.status === 1 && (
                        <button
                          onClick={() => handleCheckOut(booking.id)}
                          className="px-3 py-1.5 bg-accent/10 border border-accent/20 hover:bg-accent text-accent hover:text-white rounded-lg text-xs font-semibold flex items-center gap-1.5 ml-auto transition-all cursor-pointer"
                        >
                          <LogOut className="w-3.5 h-3.5" /> Check Out
                        </button>
                      )}
                      {booking.status === 2 && (
                        <span className="text-xs text-text-secondary italic">Archived</span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
}
