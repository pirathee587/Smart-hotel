"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { dashboardApi,type RoomDto,type RoomTypeDto } from "@/services/dashboardApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import {
AlertCircle,
BedDouble,
Edit2,
Eye,
Images,
Loader2,
Plus,
Trash2,
Upload,
X
} from "lucide-react";
import Image from "next/image";
import React,{ useState } from "react";

// ── Status helpers ────────────────────────────────────────────────────────────

const ROOM_STATUS_LABELS: Record<number, string> = {
  0: "Available",
  1: "Occupied",
  2: "Dirty",
  3: "In Cleaning",
  4: "Inspected",
  5: "Out of Order",
};

const ROOM_STATUS_COLORS: Record<number, string> = {
  0: "bg-[#2F5C52]/20 text-[#2F5C52] border-[#2F5C52]/30",
  1: "bg-[#C4622D]/20 text-[#C4622D] border-[#C4622D]/30",
  2: "bg-amber-500/20 text-amber-600 border-amber-500/30",
  3: "bg-blue-500/20 text-blue-600 border-blue-500/30",
  4: "bg-emerald-500/20 text-emerald-600 border-emerald-500/30",
  5: "bg-red-500/20 text-red-600 border-red-500/30",
};

// ── Skeleton ──────────────────────────────────────────────────────────────────

function TableRowSkeleton() {
  return (
    <tr className="border-b border-[var(--card-border)] animate-pulse">
      {[...Array(5)].map((_, i) => (
        <td key={i} className="px-4 py-3">
          <div className="h-4 bg-[var(--text-primary)]/8 rounded w-3/4" />
        </td>
      ))}
    </tr>
  );
}

// ── Room Table ────────────────────────────────────────────────────────────────

function RoomsTable({
  rooms,
  loading,
  isAdmin,
  onStatusChange,
}: {
  rooms: RoomDto[];
  loading: boolean;
  isAdmin: boolean;
  onStatusChange: (roomId: string, status: number) => void;
}) {
  const [changing, setChanging] = useState<string | null>(null);

  const handleChange = async (roomId: string, status: number) => {
    setChanging(roomId);
    await onStatusChange(roomId, status);
    setChanging(null);
  };

  if (!loading && rooms.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4">
        <div className="relative w-48 h-32 rounded-xl overflow-hidden opacity-60">
          <Image src="/images/slowhouse-interior.jpg" alt="Empty rooms" fill className="object-cover" />
        </div>
        <p className="text-sm text-[var(--text-secondary)]">No rooms found matching your filters.</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-[var(--card-border)]">
            <th className="px-4 py-3 text-left text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider">Room</th>
            <th className="px-4 py-3 text-left text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider">Floor</th>
            <th className="px-4 py-3 text-left text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider">Type</th>
            <th className="px-4 py-3 text-left text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider">Status</th>
            {isAdmin && <th className="px-4 py-3 text-left text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider">Actions</th>}
          </tr>
        </thead>
        <tbody>
          {loading
            ? [...Array(8)].map((_, i) => <TableRowSkeleton key={i} />)
            : rooms.map((room) => (
                <tr key={room.id} className="border-b border-[var(--card-border)] hover:bg-[var(--text-primary)]/2 transition-colors">
                  <td className="px-4 py-3 font-medium text-[var(--text-primary)]">{room.roomNumber}</td>
                  <td className="px-4 py-3 text-[var(--text-secondary)]">{room.floor}</td>
                  <td className="px-4 py-3 text-[var(--text-secondary)]">{room.roomTypeName}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${ROOM_STATUS_COLORS[room.status] ?? "bg-gray-100 text-gray-600 border-gray-200"}`}>
                      {ROOM_STATUS_LABELS[room.status] ?? "Unknown"}
                    </span>
                  </td>
                  {isAdmin && (
                    <td className="px-4 py-3">
                      {changing === room.id ? (
                        <Loader2 className="w-4 h-4 animate-spin text-[var(--text-secondary)]" />
                      ) : (
                        <select
                          value={room.status}
                          onChange={(e) => handleChange(room.id, Number(e.target.value))}
                          className="text-xs px-2 py-1 rounded-lg bg-[var(--bg-dark)] border border-[var(--card-border)] text-[var(--text-primary)] cursor-pointer focus:outline-none focus:ring-2 focus:ring-[#C4622D]/40"
                        >
                          {Object.entries(ROOM_STATUS_LABELS).map(([v, label]) => (
                            <option key={v} value={v}>{label}</option>
                          ))}
                        </select>
                      )}
                    </td>
                  )}
                </tr>
              ))}
        </tbody>
      </table>
    </div>
  );
}

// ── Room Type Card ────────────────────────────────────────────────────────────

function RoomTypeCard({
  rt,
  onEdit,
  onPublish,
  onManageImages,
}: {
  rt: RoomTypeDto;
  onEdit: (rt: RoomTypeDto) => void;
  onPublish: (id: string) => void;
  onManageImages: (rt: RoomTypeDto) => void;
}) {
  const primaryImg = rt.images.find((i) => i.isPrimary) ?? rt.images[0];
  return (
    <div className="rounded-2xl bg-[var(--card-dark)] border border-[var(--card-border)] overflow-hidden hover:shadow-lg transition-all duration-300 hover:-translate-y-0.5">
      <div className="relative h-36">
        {primaryImg ? (
          <Image src={primaryImg.imageUrl} alt={rt.name} fill className="object-cover" />
        ) : (
          <div className="w-full h-full bg-[#2F5C52]/10 flex items-center justify-center">
            <BedDouble className="w-10 h-10 text-[#2F5C52]/40" />
          </div>
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent" />
        <div className="absolute bottom-3 left-3 right-3 flex items-center justify-between">
          <span
            className="text-white text-sm font-semibold truncate"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            {rt.name}
          </span>
          <span
            className={`px-2 py-0.5 rounded-full text-[10px] font-semibold border ${
              rt.isPublished
                ? "bg-[#2F5C52]/80 text-white border-[#2F5C52]"
                : "bg-amber-500/80 text-white border-amber-500"
            }`}
          >
            {rt.isPublished ? "Published" : "Draft"}
          </span>
        </div>
      </div>

      <div className="p-4">
        <div className="flex items-center justify-between mb-3">
          <div>
            <p className="text-xs text-[var(--text-secondary)]">{rt.bedType} · {rt.capacity} guests · {rt.roomSizeSqFt} sqft</p>
            <p className="text-base font-bold text-[#C4622D] mt-0.5">
              LKR {rt.pricePerNight.toLocaleString()}<span className="text-xs font-normal text-[var(--text-secondary)]">/night</span>
            </p>
          </div>
          <span className="text-xs text-[var(--text-secondary)]">{rt.images.length} photos</span>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => onEdit(rt)}
            className="flex-1 flex items-center justify-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border border-[var(--card-border)] text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 transition-colors cursor-pointer"
          >
            <Edit2 className="w-3.5 h-3.5" /> Edit
          </button>
          <button
            onClick={() => onManageImages(rt)}
            className="flex-1 flex items-center justify-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border border-[var(--card-border)] text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 transition-colors cursor-pointer"
          >
            <Images className="w-3.5 h-3.5" /> Photos
          </button>
          {!rt.isPublished && (
            <button
              onClick={() => onPublish(rt.id)}
              className="flex-1 flex items-center justify-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-[#2F5C52] text-white hover:bg-[#2F5C52]/90 transition-colors cursor-pointer"
            >
              <Eye className="w-3.5 h-3.5" /> Publish
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Room Type Form Modal ──────────────────────────────────────────────────────

function RoomTypeFormModal({
  initial,
  hotelId,
  onClose,
  onSaved,
}: {
  initial?: RoomTypeDto;
  hotelId: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [form, setForm] = useState({
    name: initial?.name ?? "",
    title: initial?.title ?? "",
    bedType: initial?.bedType ?? "",
    capacity: initial?.capacity ?? 2,
    roomSizeSqFt: initial?.roomSizeSqFt ?? 400,
    pricePerNight: initial?.pricePerNight ?? 0,
    cleaningFee: initial?.cleaningFee ?? 0,
    amenitiesFee: initial?.amenitiesFee ?? 0,
    longDescription: initial?.longDescription ?? "",
    cancellationPolicyText: initial?.cancellationPolicyText ?? "",
    highlights: initial?.highlights.join("\n") ?? "",
    amenities: initial?.amenities.join("\n") ?? "",
    isActive: initial?.isActive ?? true,
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const payload = {
        ...form,
        capacity: Number(form.capacity),
        roomSizeSqFt: Number(form.roomSizeSqFt),
        pricePerNight: Number(form.pricePerNight),
        cleaningFee: Number(form.cleaningFee),
        amenitiesFee: Number(form.amenitiesFee),
        highlights: form.highlights.split("\n").map((s) => s.trim()).filter(Boolean),
        amenities: form.amenities.split("\n").map((s) => s.trim()).filter(Boolean),
      };
      if (initial) {
        await dashboardApi.updateRoomType(initial.id, { ...payload });
      } else {
        await dashboardApi.createRoomType({ hotelId, ...payload });
      }
      onSaved();
      onClose();
    } catch {
      setError("Failed to save room type. Please try again.");
    } finally {
      setSaving(false);
    }
  };

  const inp = "w-full px-3 py-2 text-sm rounded-lg bg-[var(--bg-dark)] border border-[var(--card-border)] text-[var(--text-primary)] focus:outline-none focus:ring-2 focus:ring-[#C4622D]/40";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="bg-[var(--card-dark)] rounded-2xl border border-[var(--card-border)] shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b border-[var(--card-border)]">
          <h2 className="text-lg font-bold text-[var(--text-primary)]" style={{ fontFamily: "var(--font-fraunces), serif" }}>
            {initial ? "Edit Room Type" : "New Room Type"}
          </h2>
          <button onClick={onClose} className="p-2 rounded-lg hover:bg-[var(--text-primary)]/8 text-[var(--text-secondary)] cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Name</label>
              <input className={inp} value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} required />
            </div>
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Title (display)</label>
              <input className={inp} value={form.title} onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))} />
            </div>
          </div>
          <div className="grid grid-cols-3 gap-4">
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Bed Type</label>
              <input className={inp} value={form.bedType} onChange={(e) => setForm((f) => ({ ...f, bedType: e.target.value }))} placeholder="King, Twin…" />
            </div>
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Capacity</label>
              <input type="number" min={1} className={inp} value={form.capacity} onChange={(e) => setForm((f) => ({ ...f, capacity: Number(e.target.value) }))} />
            </div>
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Size (sqft)</label>
              <input type="number" min={1} className={inp} value={form.roomSizeSqFt} onChange={(e) => setForm((f) => ({ ...f, roomSizeSqFt: Number(e.target.value) }))} />
            </div>
          </div>
          <div className="grid grid-cols-3 gap-4">
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Price/Night (LKR)</label>
              <input type="number" min={0} className={inp} value={form.pricePerNight} onChange={(e) => setForm((f) => ({ ...f, pricePerNight: Number(e.target.value) }))} />
            </div>
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Cleaning Fee</label>
              <input type="number" min={0} className={inp} value={form.cleaningFee} onChange={(e) => setForm((f) => ({ ...f, cleaningFee: Number(e.target.value) }))} />
            </div>
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Amenities Fee</label>
              <input type="number" min={0} className={inp} value={form.amenitiesFee} onChange={(e) => setForm((f) => ({ ...f, amenitiesFee: Number(e.target.value) }))} />
            </div>
          </div>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Long Description</label>
            <textarea rows={3} className={inp} value={form.longDescription} onChange={(e) => setForm((f) => ({ ...f, longDescription: e.target.value }))} />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Highlights (one per line)</label>
              <textarea rows={3} className={inp} value={form.highlights} onChange={(e) => setForm((f) => ({ ...f, highlights: e.target.value }))} placeholder="Tea plantation views&#10;Private plunge pool…" />
            </div>
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Amenities (one per line)</label>
              <textarea rows={3} className={inp} value={form.amenities} onChange={(e) => setForm((f) => ({ ...f, amenities: e.target.value }))} placeholder="WiFi&#10;Air conditioning…" />
            </div>
          </div>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Cancellation Policy</label>
            <textarea rows={2} className={inp} value={form.cancellationPolicyText} onChange={(e) => setForm((f) => ({ ...f, cancellationPolicyText: e.target.value }))} />
          </div>

          {error && (
            <div className="flex items-center gap-2 text-xs text-red-500 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 px-3 py-2 rounded-lg">
              <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {error}
            </div>
          )}

          <div className="flex items-center justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 rounded-lg transition-colors cursor-pointer">
              Cancel
            </button>
            <button
              type="submit"
              disabled={saving}
              className="flex items-center gap-2 px-5 py-2 text-sm font-medium bg-[#C4622D] hover:bg-[#E07A3E] text-white rounded-lg transition-colors disabled:opacity-60 cursor-pointer"
            >
              {saving && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
              {initial ? "Save Changes" : "Create Room Type"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ── Image Manager Modal ───────────────────────────────────────────────────────

function ImageManagerModal({ rt, onClose, onUpdated }: { rt: RoomTypeDto; onClose: () => void; onUpdated: () => void }) {
  const [uploading, setUploading] = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploading(true);
    try {
      await dashboardApi.uploadRoomTypeImage(rt.id, file, rt.images.length === 0);
      onUpdated();
    } catch {
      // swallow
    } finally {
      setUploading(false);
    }
  };

  const handleDelete = async (imageId: string) => {
    setDeleting(imageId);
    try {
      await dashboardApi.deleteRoomTypeImage(rt.id, imageId);
      onUpdated();
    } finally {
      setDeleting(null);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="bg-[var(--card-dark)] rounded-2xl border border-[var(--card-border)] shadow-2xl w-full max-w-xl max-h-[85vh] flex flex-col">
        <div className="flex items-center justify-between p-6 border-b border-[var(--card-border)]">
          <h2 className="text-lg font-bold text-[var(--text-primary)]" style={{ fontFamily: "var(--font-fraunces), serif" }}>
            Photos — {rt.name}
          </h2>
          <button onClick={onClose} className="p-2 rounded-lg hover:bg-[var(--text-primary)]/8 text-[var(--text-secondary)] cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="p-6 overflow-y-auto flex-1 space-y-4">
          <label className="flex items-center justify-center gap-2 w-full py-3 rounded-xl border-2 border-dashed border-[var(--card-border)] hover:border-[#C4622D]/40 transition-colors text-sm text-[var(--text-secondary)] cursor-pointer hover:text-[#C4622D]">
            {uploading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
            {uploading ? "Uploading…" : "Upload new photo (JPEG, PNG, WebP, max 5MB)"}
            <input type="file" className="sr-only" accept="image/jpeg,image/png,image/webp" onChange={handleUpload} disabled={uploading} />
          </label>

          <div className="grid grid-cols-2 gap-3">
            {rt.images.length === 0 && (
              <p className="col-span-2 text-center text-sm text-[var(--text-secondary)] py-8">No photos yet. Upload one above.</p>
            )}
            {rt.images.map((img) => (
              <div key={img.id} className="relative group rounded-xl overflow-hidden aspect-video bg-black/20">
                <Image src={img.imageUrl} alt="Room type photo" fill className="object-cover" />
                {img.isPrimary && (
                  <span className="absolute top-2 left-2 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-[#2F5C52]/80 text-white border border-[#2F5C52]">
                    Primary
                  </span>
                )}
                <div className="absolute inset-0 bg-black/0 group-hover:bg-black/40 transition-colors flex items-center justify-center opacity-0 group-hover:opacity-100">
                  <button
                    onClick={() => handleDelete(img.id)}
                    disabled={deleting === img.id}
                    className="p-2 bg-red-600/90 hover:bg-red-600 rounded-full text-white transition-colors cursor-pointer"
                  >
                    {deleting === img.id ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export default function RoomsPage() {
  const { user } = useAuthStore();
  const qc = useQueryClient();
  const isAdmin = String(user?.role) === "Admin";

  const [tab, setTab] = useState<"rooms" | "types">("rooms");
  const [statusFilter, setStatusFilter] = useState<number | undefined>(undefined);
  const [editingRt, setEditingRt] = useState<RoomTypeDto | undefined>(undefined);
  const [showNewRtForm, setShowNewRtForm] = useState(false);
  const [managingImagesFor, setManagingImagesFor] = useState<RoomTypeDto | undefined>(undefined);

  const { data: rooms = [], isLoading: roomsLoading } = useQuery({
    queryKey: ["rooms", statusFilter],
    queryFn: () => dashboardApi.getRooms({ status: statusFilter, pageSize: 500 }),
    staleTime: 30_000,
  });

  const { data: roomTypes = [], isLoading: rtLoading } = useQuery({
    queryKey: ["room-types"],
    queryFn: () => dashboardApi.getRoomTypes(),
    staleTime: 60_000,
    enabled: tab === "types",
  });

  const updateStatusMutation = useMutation({
    mutationFn: ({ roomId, status }: { roomId: string; status: number }) =>
      dashboardApi.updateRoomStatus(roomId, status),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["rooms"] }),
  });

  const publishMutation = useMutation({
    mutationFn: (id: string) => dashboardApi.publishRoomType(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["room-types"] }),
  });

  const filteredRooms = statusFilter !== undefined
    ? rooms.filter((r) => r.status === statusFilter)
    : rooms;

  return (
    <div className="p-6 lg:p-8 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1
            className="text-3xl font-bold text-[var(--text-primary)] tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            Room Management
          </h1>
          <p className="text-sm text-[var(--text-secondary)] mt-1">
            {rooms.length} rooms · {roomTypes.length} room types
          </p>
        </div>

        {isAdmin && tab === "types" && (
          <button
            onClick={() => setShowNewRtForm(true)}
            className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-[#C4622D] hover:bg-[#E07A3E] text-white text-sm font-medium transition-colors cursor-pointer shadow-sm"
          >
            <Plus className="w-4 h-4" /> New Room Type
          </button>
        )}
      </div>

      {/* Tab switcher */}
      <div className="flex gap-1 p-1 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-xl w-fit">
        {(["rooms", "types"] as const).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-5 py-2 rounded-lg text-sm font-medium transition-all duration-200 cursor-pointer ${
              tab === t
                ? "bg-[#C4622D] text-white shadow-sm"
                : "text-[var(--text-secondary)] hover:text-[var(--text-primary)]"
            }`}
          >
            {t === "rooms" ? "Rooms" : "Room Types"}
          </button>
        ))}
      </div>

      {/* Rooms tab */}
      {tab === "rooms" && (
        <div className="bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl overflow-hidden">
          {/* Filters */}
          <div className="flex flex-wrap items-center gap-3 p-4 border-b border-[var(--card-border)]">
            <span className="text-xs font-semibold text-[var(--text-secondary)] uppercase tracking-wider">Filter:</span>
            <button
              onClick={() => setStatusFilter(undefined)}
              className={`px-3 py-1 rounded-full text-xs font-medium border transition-colors cursor-pointer ${statusFilter === undefined ? "bg-[#C4622D] text-white border-[#C4622D]" : "border-[var(--card-border)] text-[var(--text-secondary)] hover:border-[#C4622D]/40"}`}
            >
              All
            </button>
            {Object.entries(ROOM_STATUS_LABELS).map(([v, label]) => (
              <button
                key={v}
                onClick={() => setStatusFilter(Number(v))}
                className={`px-3 py-1 rounded-full text-xs font-medium border transition-colors cursor-pointer ${statusFilter === Number(v) ? "bg-[#C4622D] text-white border-[#C4622D]" : "border-[var(--card-border)] text-[var(--text-secondary)] hover:border-[#C4622D]/40"}`}
              >
                {label}
              </button>
            ))}
          </div>

          <RoomsTable
            rooms={filteredRooms}
            loading={roomsLoading}
            isAdmin={isAdmin}
            onStatusChange={(id, status) => updateStatusMutation.mutate({ roomId: id, status })}
          />
        </div>
      )}

      {/* Room Types tab */}
      {tab === "types" && (
        <>
          {rtLoading ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
              {[...Array(6)].map((_, i) => (
                <div key={i} className="rounded-2xl bg-[var(--card-dark)] border border-[var(--card-border)] h-72 animate-pulse" />
              ))}
            </div>
          ) : roomTypes.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-20 gap-4">
              <div className="relative w-64 h-40 rounded-xl overflow-hidden opacity-60">
                <Image src="/images/ridge-thumb.jpg" alt="Empty state" fill className="object-cover" />
              </div>
              <p className="text-sm text-[var(--text-secondary)]">No room types yet. Create your first one.</p>
              {isAdmin && (
                <button
                  onClick={() => setShowNewRtForm(true)}
                  className="flex items-center gap-2 px-4 py-2 text-sm font-medium bg-[#C4622D] text-white rounded-xl hover:bg-[#E07A3E] transition-colors cursor-pointer"
                >
                  <Plus className="w-4 h-4" /> New Room Type
                </button>
              )}
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
              {roomTypes.map((rt) => (
                <RoomTypeCard
                  key={rt.id}
                  rt={rt}
                  onEdit={setEditingRt}
                  onPublish={(id) => publishMutation.mutate(id)}
                  onManageImages={setManagingImagesFor}
                />
              ))}
            </div>
          )}
        </>
      )}

      {/* Modals */}
      {(showNewRtForm || editingRt) && (
        <RoomTypeFormModal
          initial={editingRt}
          hotelId="00000000-0000-0000-0000-000000000001"
          onClose={() => { setShowNewRtForm(false); setEditingRt(undefined); }}
          onSaved={() => qc.invalidateQueries({ queryKey: ["room-types"] })}
        />
      )}
      {managingImagesFor && (
        <ImageManagerModal
          rt={managingImagesFor}
          onClose={() => setManagingImagesFor(undefined)}
          onUpdated={() => {
            qc.invalidateQueries({ queryKey: ["room-types"] });
            // update the local ref
            dashboardApi.getRoomTypes().then((types) => {
              const updated = types.find((t) => t.id === managingImagesFor.id);
              if (updated) setManagingImagesFor(updated);
            });
          }}
        />
      )}
    </div>
  );
}
