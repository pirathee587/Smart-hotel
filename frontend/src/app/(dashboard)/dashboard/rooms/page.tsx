"use client";

import React, { useEffect, useState } from "react";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";
import api from "@/lib/axios";
import { BedDouble, Check, Sparkles, Wrench, RefreshCw } from "lucide-react";

interface Room {
  id: string;
  roomNumber: string;
  floor: number;
  status: number; // 0=Available, 1=Occupied, 2=CleaningRequired, 3=MaintenanceRequired
  roomTypeName: string;
}

export default function RoomsPage() {
  const [rooms, setRooms] = useState<Room[]>([]);
  const [filterStatus, setFilterStatus] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const fetchRooms = async () => {
    setIsLoading(true);
    try {
      const res = await api.get<Room[]>("/api/v1/rooms");
      setRooms(res.data);
    } catch {
      // Fallback demo rooms
      setRooms([
        { id: "1", roomNumber: "101", floor: 1, status: 0, roomTypeName: "Standard King" },
        { id: "2", roomNumber: "102", floor: 1, status: 1, roomTypeName: "Standard King" },
        { id: "3", roomNumber: "201", floor: 2, status: 2, roomTypeName: "Deluxe Queen" },
        { id: "4", roomNumber: "202", floor: 2, status: 3, roomTypeName: "Deluxe Queen" },
        { id: "5", roomNumber: "301", floor: 3, status: 0, roomTypeName: "Luxury Suite" },
      ]);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchRooms();
  }, []);

  const getStatusBadge = (status: number) => {
    switch (status) {
      case 0:
        return <Badge variant="success">Available</Badge>;
      case 1:
        return <Badge variant="info">Occupied</Badge>;
      case 2:
        return <Badge variant="warning">Cleaning</Badge>;
      case 3:
        return <Badge variant="danger">Maintenance</Badge>;
      default:
        return <Badge variant="ghost">Unknown</Badge>;
    }
  };

  const handleUpdateStatus = async (roomId: string, newStatus: number) => {
    try {
      await api.patch(`/api/v1/rooms/${roomId}/status`, { newStatus, reason: "Updated via dashboard" });
      fetchRooms();
    } catch {
      // Offline fallback state update
      setRooms((prev) =>
        prev.map((r) => (r.id === roomId ? { ...r, status: newStatus } : r))
      );
    }
  };

  const filteredRooms = filterStatus !== null
    ? rooms.filter((r) => r.status === filterStatus)
    : rooms;

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-text-primary to-text-secondary bg-clip-text text-transparent">
            Rooms Management
          </h1>
          <p className="text-sm text-text-secondary mt-1">
            Monitor real-time room statuses, occupancy, and assignments.
          </p>
        </div>
        <button
          onClick={fetchRooms}
          className="flex items-center gap-2 px-4 py-2 bg-white/5 border border-card-border hover:bg-white/10 rounded-lg text-sm font-medium transition-all cursor-pointer"
        >
          <RefreshCw className="w-4 h-4" /> Refresh
        </button>
      </div>

      {/* Filter Tabs */}
      <div className="flex flex-wrap items-center gap-2">
        <button
          onClick={() => setFilterStatus(null)}
          className={`px-4 py-2 rounded-lg text-xs font-semibold tracking-wide uppercase transition-all cursor-pointer ${
            filterStatus === null
              ? "bg-primary text-white"
              : "bg-white/5 text-text-secondary border border-card-border hover:bg-white/10"
          }`}
        >
          All Rooms
        </button>
        <button
          onClick={() => setFilterStatus(0)}
          className={`px-4 py-2 rounded-lg text-xs font-semibold tracking-wide uppercase transition-all cursor-pointer ${
            filterStatus === 0
              ? "bg-primary text-white"
              : "bg-white/5 text-text-secondary border border-card-border hover:bg-white/10"
          }`}
        >
          Available
        </button>
        <button
          onClick={() => setFilterStatus(1)}
          className={`px-4 py-2 rounded-lg text-xs font-semibold tracking-wide uppercase transition-all cursor-pointer ${
            filterStatus === 1
              ? "bg-primary text-white"
              : "bg-white/5 text-text-secondary border border-card-border hover:bg-white/10"
          }`}
        >
          Occupied
        </button>
        <button
          onClick={() => setFilterStatus(2)}
          className={`px-4 py-2 rounded-lg text-xs font-semibold tracking-wide uppercase transition-all cursor-pointer ${
            filterStatus === 2
              ? "bg-primary text-white"
              : "bg-white/5 text-text-secondary border border-card-border hover:bg-white/10"
          }`}
        >
          Needs Cleaning
        </button>
        <button
          onClick={() => setFilterStatus(3)}
          className={`px-4 py-2 rounded-lg text-xs font-semibold tracking-wide uppercase transition-all cursor-pointer ${
            filterStatus === 3
              ? "bg-primary text-white"
              : "bg-white/5 text-text-secondary border border-card-border hover:bg-white/10"
          }`}
        >
          Maintenance
        </button>
      </div>

      {/* Rooms Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-6">
        {isLoading ? (
          <div className="col-span-full py-12 text-center text-text-secondary text-sm">
            Loading rooms...
          </div>
        ) : filteredRooms.length === 0 ? (
          <div className="col-span-full py-12 text-center text-text-secondary text-sm">
            No rooms found matching filters.
          </div>
        ) : (
          filteredRooms.map((room) => (
            <Card key={room.id} className="relative flex flex-col justify-between h-48 border border-card-border/80">
              <div>
                <div className="flex items-start justify-between">
                  <div>
                    <span className="text-xs text-text-secondary font-medium uppercase tracking-wider">
                      Floor {room.floor}
                    </span>
                    <h3 className="text-xl font-bold mt-0.5 text-text-primary">
                      Room {room.roomNumber}
                    </h3>
                  </div>
                  {getStatusBadge(room.status)}
                </div>
                <p className="text-xs text-text-secondary mt-2">
                  {room.roomTypeName}
                </p>
              </div>

              {/* Status Action Buttons */}
              <div className="flex items-center gap-1.5 pt-4 border-t border-card-border/50">
                {room.status === 2 && (
                  <button
                    onClick={() => handleUpdateStatus(room.id, 0)}
                    className="flex-1 flex items-center justify-center gap-1.5 py-2 bg-white/5 hover:bg-white/10 text-[11px] font-semibold text-emerald-400 border border-card-border rounded-lg cursor-pointer transition-all"
                  >
                    <Check className="w-3.5 h-3.5" /> Mark Ready
                  </button>
                )}
                {room.status === 0 && (
                  <button
                    onClick={() => handleUpdateStatus(room.id, 3)}
                    className="flex-1 flex items-center justify-center gap-1.5 py-2 bg-white/5 hover:bg-white/10 text-[11px] font-semibold text-amber-400 border border-card-border rounded-lg cursor-pointer transition-all"
                  >
                    <Wrench className="w-3.5 h-3.5" /> Service Required
                  </button>
                )}
                {room.status === 3 && (
                  <button
                    onClick={() => handleUpdateStatus(room.id, 2)}
                    className="flex-1 flex items-center justify-center gap-1.5 py-2 bg-white/5 hover:bg-white/10 text-[11px] font-semibold text-sky-400 border border-card-border rounded-lg cursor-pointer transition-all"
                  >
                    <Sparkles className="w-3.5 h-3.5" /> Clean Room
                  </button>
                )}
                {room.status === 1 && (
                  <div className="text-[11px] text-text-secondary text-center w-full italic">
                    Occupied by Guest
                  </div>
                )}
              </div>
            </Card>
          ))
        )}
      </div>
    </div>
  );
}
