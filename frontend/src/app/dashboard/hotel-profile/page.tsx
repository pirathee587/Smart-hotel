"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { dashboardApi,type HotelDto } from "@/services/dashboardApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import {
AlertCircle,
CheckCircle2,
Edit2,
Hotel,
Loader2,
Save,
} from "lucide-react";
import Image from "next/image";
import React,{ useState } from "react";

// ── Main Page ─────────────────────────────────────────────────────────────────

export default function HotelProfilePage() {
  const { user } = useAuthStore();
  const qc = useQueryClient();
  const isAdmin = String(user?.role) === "Admin";

  const { data: hotels = [], isLoading } = useQuery({
    queryKey: ["hotels"],
    queryFn: () => dashboardApi.getHotels(),
    staleTime: 120_000,
  });

  const hotel = hotels[0] as HotelDto | undefined;

  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({
    name: "",
    address: "",
    phone: "",
    email: "",
    totalFloors: 0,
  });
  const [success, setSuccess] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const startEditing = () => {
    if (!hotel) return;
    setForm({ name: hotel.name, address: hotel.address, phone: hotel.phone, email: hotel.email, totalFloors: hotel.totalFloors });
    setEditing(true);
  };

  const updateMutation = useMutation({
    mutationFn: (payload: typeof form) => dashboardApi.updateHotel(hotel!.id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["hotels"] });
      setSuccess("Hotel profile updated successfully.");
      setEditing(false);
      setTimeout(() => setSuccess(null), 4000);
    },
    onError: () => {
      setError("Failed to update hotel profile.");
      setTimeout(() => setError(null), 4000);
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    updateMutation.mutate({ ...form, totalFloors: Number(form.totalFloors) });
  };

  const inp = (disabled?: boolean) =>
    `w-full px-4 py-3 text-sm rounded-xl border text-[var(--text-primary)] focus:outline-none focus:ring-2 focus:ring-[#C4622D]/40 transition-colors ${
      disabled
        ? "bg-[var(--bg-dark)] border-transparent cursor-default opacity-70"
        : "bg-[var(--card-dark)] border-[var(--card-border)] focus:border-[#C4622D]/50"
    }`;

  return (
    <div className="p-6 lg:p-8 max-w-4xl mx-auto space-y-8">
      {/* Header with hero image */}
      <div className="relative rounded-2xl overflow-hidden h-44">
        <Image
          src="/images/hero-view.jpg"
          alt="Hotel Maskeliya"
          fill
          className="object-cover"
        />
        <div className="absolute inset-0 bg-gradient-to-r from-[#0F1B1A]/80 via-[#0F1B1A]/50 to-transparent" />
        <div className="absolute inset-0 flex flex-col justify-end p-6">
          <div className="flex items-center gap-2 mb-1">
            <Hotel className="w-4 h-4 text-[#E07A3E]" />
            <span className="text-xs text-white/70 font-medium uppercase tracking-wider">Hotel Profile</span>
          </div>
          <h1
            className="text-3xl font-bold text-white tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            {isLoading ? "Loading…" : (hotel?.name ?? "SmartHotel Maskeliya")}
          </h1>
        </div>
      </div>

      {/* Status messages */}
      {success && (
        <div className="flex items-center gap-2 text-sm text-[#2F5C52] bg-[#2F5C52]/10 border border-[#2F5C52]/20 px-4 py-3 rounded-xl animate-in fade-in slide-in-from-top-2">
          <CheckCircle2 className="w-4 h-4 shrink-0" /> {success}
        </div>
      )}
      {error && (
        <div className="flex items-center gap-2 text-sm text-[#C4622D] bg-[#C4622D]/10 border border-[#C4622D]/20 px-4 py-3 rounded-xl">
          <AlertCircle className="w-4 h-4 shrink-0" /> {error}
        </div>
      )}

      {/* Hotel details card */}
      <div className="bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl overflow-hidden">
        <div className="flex items-center justify-between px-6 py-4 border-b border-[var(--card-border)]">
          <h2 className="font-semibold text-[var(--text-primary)]" style={{ fontFamily: "var(--font-fraunces), serif" }}>
            Hotel Details
          </h2>
          {isAdmin && !editing && hotel && (
            <button
              onClick={startEditing}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-[#C4622D] bg-[#C4622D]/10 border border-[#C4622D]/20 rounded-xl hover:bg-[#C4622D]/20 transition-colors cursor-pointer"
            >
              <Edit2 className="w-3.5 h-3.5" /> Edit
            </button>
          )}
        </div>

        {isLoading ? (
          <div className="p-6 space-y-4 animate-pulse">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="space-y-1">
                <div className="h-3 w-20 bg-[var(--text-primary)]/10 rounded" />
                <div className="h-10 bg-[var(--text-primary)]/6 rounded-xl" />
              </div>
            ))}
          </div>
        ) : !hotel ? (
          <div className="flex flex-col items-center justify-center py-16 gap-3">
            <Hotel className="w-10 h-10 text-[var(--text-secondary)]" />
            <p className="text-sm text-[var(--text-secondary)]">No hotel data found.</p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="p-6 space-y-5">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
              <div>
                <label className="block text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider mb-1.5">Hotel Name</label>
                <input
                  className={inp(!editing)}
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  readOnly={!editing}
                  required
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider mb-1.5">Email</label>
                <input
                  type="email"
                  className={inp(!editing)}
                  value={form.email}
                  onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                  readOnly={!editing}
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider mb-1.5">Phone</label>
                <input
                  className={inp(!editing)}
                  value={form.phone}
                  onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
                  readOnly={!editing}
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider mb-1.5">Total Floors</label>
                <input
                  type="number"
                  min={1}
                  className={inp(!editing)}
                  value={form.totalFloors}
                  onChange={(e) => setForm((f) => ({ ...f, totalFloors: Number(e.target.value) }))}
                  readOnly={!editing}
                />
              </div>
            </div>
            <div>
              <label className="block text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider mb-1.5">Address</label>
              <textarea
                rows={2}
                className={inp(!editing)}
                value={form.address}
                onChange={(e) => setForm((f) => ({ ...f, address: e.target.value }))}
                readOnly={!editing}
              />
            </div>

            {/* Read-only stats */}
            <div className="pt-4 border-t border-[var(--card-border)] grid grid-cols-2 sm:grid-cols-3 gap-4">
              <div className="p-4 rounded-xl bg-[var(--bg-dark)] border border-[var(--card-border)] text-center">
                <p className="text-2xl font-bold text-[var(--text-primary)]" style={{ fontFamily: "var(--font-fraunces), serif" }}>{hotel.totalRooms}</p>
                <p className="text-xs text-[var(--text-secondary)] mt-1">Total Rooms</p>
              </div>
              <div className="p-4 rounded-xl bg-[var(--bg-dark)] border border-[var(--card-border)] text-center">
                <p className="text-2xl font-bold text-[var(--text-primary)]" style={{ fontFamily: "var(--font-fraunces), serif" }}>{hotel.totalFloors}</p>
                <p className="text-xs text-[var(--text-secondary)] mt-1">Floors</p>
              </div>
            </div>

            {editing && (
              <div className="flex items-center gap-3 justify-end pt-2">
                <button
                  type="button"
                  onClick={() => { setEditing(false); if (hotel) setForm({ name: hotel.name, address: hotel.address, phone: hotel.phone, email: hotel.email, totalFloors: hotel.totalFloors }); }}
                  className="px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 rounded-xl cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={updateMutation.isPending}
                  className="flex items-center gap-2 px-6 py-2.5 text-sm font-medium bg-[#C4622D] hover:bg-[#E07A3E] text-white rounded-xl transition-colors disabled:opacity-60 cursor-pointer shadow-sm"
                >
                  {updateMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                  Save Changes
                </button>
              </div>
            )}
          </form>
        )}
      </div>
    </div>
  );
}
