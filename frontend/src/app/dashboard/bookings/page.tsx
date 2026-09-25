"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import {
dashboardApi,
type DashboardBookingDto,
type FrontOfficeAuditLogDto,
type RoomDto,
type RoomReadinessDto,
} from "@/services/dashboardApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import {
AlertCircle,
AlertTriangle,
Ban,
BedDouble,
CalendarCheck,
CalendarClock,
Check,
CreditCard,
DoorClosed,
History,
Loader2,
LogIn,
LogOut,
Search,
User,
X,
} from "lucide-react";
import Image from "next/image";
import React,{ useState } from "react";

// ── Status helpers ─────────────────────────────────────────────────────────────

const BOOKING_STATUS_LABELS: Record<number, string> = {
  0: "Pending Payment",
  1: "Confirmed",
  2: "Checked In",
  3: "Checked Out",
  4: "Cancelled",
};

const BOOKING_STATUS_COLORS: Record<number, string> = {
  0: "bg-amber-500/15 text-amber-600 border-amber-400/30",
  1: "bg-[#2F5C52]/15 text-[#2F5C52] border-[#2F5C52]/30",
  2: "bg-[#C4622D]/15 text-[#C4622D] border-[#C4622D]/30",
  3: "bg-gray-500/15 text-gray-500 border-gray-400/30",
  4: "bg-red-500/15 text-red-600 border-red-400/30",
};

function statusNum(s: number | string): number {
  return typeof s === "number" ? s : parseInt(s as string, 10) || 0;
}

// ── Payment Confirm Modal ──────────────────────────────────────────────────────

function PaymentModal({
  booking,
  onClose,
  onConfirmed,
}: {
  booking: DashboardBookingDto;
  onClose: () => void;
  onConfirmed: () => void;
}) {
  const [reference, setReference] = useState("");
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reference.trim()) {
      setError("Payment reference is required.");
      return;
    }
    setLoading(true);
    setError(null);
    try {
      await dashboardApi.confirmPayment(booking.id, reference.trim(), notes.trim() || undefined);
      onConfirmed();
      onClose();
    } catch {
      setError("Failed to confirm payment. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  const inp =
    "w-full px-3 py-2 text-sm rounded-lg bg-[var(--bg-dark)] border border-[var(--card-border)] text-[var(--text-primary)] focus:outline-none focus:ring-2 focus:ring-[#C4622D]/40";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="bg-[var(--card-dark)] rounded-2xl border border-[var(--card-border)] shadow-2xl w-full max-w-md">
        <div className="flex items-center justify-between p-6 border-b border-[var(--card-border)]">
          <h2
            className="font-bold text-[var(--text-primary)]"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            Confirm Payment
          </h2>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-[var(--text-primary)]/8 text-[var(--text-secondary)] cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="p-3 rounded-xl bg-[var(--bg-dark)] border border-[var(--card-border)] text-sm text-[var(--text-secondary)] space-y-1">
            <p>
              <span className="font-medium text-[var(--text-primary)]">Booking:</span>{" "}
              {booking.bookingReference || booking.id.slice(0, 8)}
            </p>
            <p>
              <span className="font-medium text-[var(--text-primary)]">Guest:</span>{" "}
              {booking.customerLastName || "—"}
            </p>
            <p>
              <span className="font-medium text-[var(--text-primary)]">Amount:</span> LKR{" "}
              {booking.totalAmount.toLocaleString()}
            </p>
          </div>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
              Payment Reference *
            </label>
            <input
              className={inp}
              value={reference}
              onChange={(e) => setReference(e.target.value)}
              placeholder="Bank transfer ref, receipt no…"
              required
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
              Notes (optional)
            </label>
            <textarea
              rows={2}
              className={inp}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Any additional notes…"
            />
          </div>
          {error && (
            <div className="flex items-center gap-2 text-xs text-red-500 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 px-3 py-2 rounded-lg">
              <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {error}
            </div>
          )}
          <div className="flex items-center gap-3 justify-end pt-1">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 rounded-lg cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading}
              className="flex items-center gap-2 px-5 py-2 text-sm font-medium bg-[#2F5C52] hover:bg-[#2F5C52]/90 text-white rounded-lg transition-colors disabled:opacity-60 cursor-pointer"
            >
              {loading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Check className="w-3.5 h-3.5" />}
              Confirm Payment
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ── Room Assignment Modal ──────────────────────────────────────────────────────

function AssignRoomModal({
  booking,
  onClose,
  onAssigned,
}: {
  booking: DashboardBookingDto;
  onClose: () => void;
  onAssigned: () => void;
}) {
  const [selectedRoomId, setSelectedRoomId] = useState("");
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const { data: rooms = [], isLoading: loadingRooms } = useQuery<RoomDto[]>({
    queryKey: ["rooms-for-assignment"],
    queryFn: () => dashboardApi.getRooms(),
  });

  const availableRooms = rooms.filter(
    (r) => r.id !== booking.roomId && r.status !== 5 // Not OutOfOrder
  );

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedRoomId) {
      setError("Please select a room to assign.");
      return;
    }
    setLoading(true);
    setError(null);
    try {
      await dashboardApi.assignRoom(booking.id, selectedRoomId, reason.trim() || undefined);
      onAssigned();
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to assign room.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const inp =
    "w-full px-3 py-2 text-sm rounded-lg bg-[var(--bg-dark)] border border-[var(--card-border)] text-[var(--text-primary)] focus:outline-none focus:ring-2 focus:ring-[#C4622D]/40";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="bg-[var(--card-dark)] rounded-2xl border border-[var(--card-border)] shadow-2xl w-full max-w-md">
        <div className="flex items-center justify-between p-6 border-b border-[var(--card-border)]">
          <div className="flex items-center gap-2">
            <DoorClosed className="w-5 h-5 text-[#C4622D]" />
            <h2
              className="font-bold text-[var(--text-primary)]"
              style={{ fontFamily: "var(--font-fraunces), serif" }}
            >
              Reassign Room
            </h2>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-[var(--text-primary)]/8 text-[var(--text-secondary)] cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="p-3 rounded-xl bg-[var(--bg-dark)] border border-[var(--card-border)] text-sm text-[var(--text-secondary)] space-y-1">
            <p>
              <span className="font-medium text-[var(--text-primary)]">Booking:</span>{" "}
              {booking.bookingReference || booking.id.slice(0, 8)}
            </p>
            <p>
              <span className="font-medium text-[var(--text-primary)]">Current Room:</span>{" "}
              {booking.roomNumber || "Unassigned"}
            </p>
            <p>
              <span className="font-medium text-[var(--text-primary)]">Dates:</span>{" "}
              {new Date(booking.checkInDate).toLocaleDateString()} →{" "}
              {new Date(booking.checkOutDate).toLocaleDateString()}
            </p>
          </div>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
              Select New Room *
            </label>
            {loadingRooms ? (
              <div className="flex items-center gap-2 py-2 text-xs text-[var(--text-secondary)]">
                <Loader2 className="w-4 h-4 animate-spin text-[#C4622D]" /> Loading rooms…
              </div>
            ) : (
              <select
                className={inp}
                value={selectedRoomId}
                onChange={(e) => setSelectedRoomId(e.target.value)}
                required
              >
                <option value="">-- Choose eligible room --</option>
                {availableRooms.map((r) => (
                  <option key={r.id} value={r.id}>
                    Room {r.roomNumber} (Floor {r.floor} · {r.roomTypeName})
                  </option>
                ))}
              </select>
            )}
          </div>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
              Reason for Reassignment
            </label>
            <input
              className={inp}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g., Guest upgrade, maintenance relocation"
            />
          </div>
          {error && (
            <div className="flex items-center gap-2 text-xs text-red-500 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 px-3 py-2 rounded-lg">
              <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {error}
            </div>
          )}
          <div className="flex items-center gap-3 justify-end pt-1">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 rounded-lg cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading || !selectedRoomId}
              className="flex items-center gap-2 px-5 py-2 text-sm font-medium bg-[#C4622D] hover:bg-[#C4622D]/90 text-white rounded-lg transition-colors disabled:opacity-60 cursor-pointer"
            >
              {loading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Check className="w-3.5 h-3.5" />}
              Assign Room
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ── Staff Cancellation Override Modal ──────────────────────────────────────────

function StaffCancelModal({
  booking,
  onClose,
  onCancelled,
}: {
  booking: DashboardBookingDto;
  onClose: () => void;
  onCancelled: () => void;
}) {
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setError("A cancellation reason is required for staff override.");
      return;
    }
    setLoading(true);
    setError(null);
    try {
      await dashboardApi.cancelBookingStaff(booking.id, reason.trim());
      onCancelled();
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to cancel booking.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const inp =
    "w-full px-3 py-2 text-sm rounded-lg bg-[var(--bg-dark)] border border-[var(--card-border)] text-[var(--text-primary)] focus:outline-none focus:ring-2 focus:ring-red-500/40";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="bg-[var(--card-dark)] rounded-2xl border border-[var(--card-border)] shadow-2xl w-full max-w-md">
        <div className="flex items-center justify-between p-6 border-b border-[var(--card-border)]">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-5 h-5 text-red-500" />
            <h2
              className="font-bold text-[var(--text-primary)]"
              style={{ fontFamily: "var(--font-fraunces), serif" }}
            >
              Staff Cancellation Override
            </h2>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-[var(--text-primary)]/8 text-[var(--text-secondary)] cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="p-3 rounded-xl bg-red-500/10 border border-red-500/20 text-xs text-red-400 space-y-1">
            <p className="font-semibold">Manager Authorization Required</p>
            <p>This override cancels the reservation and logs an immutable Front Office audit record.</p>
          </div>
          <div className="p-3 rounded-xl bg-[var(--bg-dark)] border border-[var(--card-border)] text-sm text-[var(--text-secondary)] space-y-1">
            <p>
              <span className="font-medium text-[var(--text-primary)]">Booking:</span>{" "}
              {booking.bookingReference || booking.id.slice(0, 8)}
            </p>
            <p>
              <span className="font-medium text-[var(--text-primary)]">Guest:</span>{" "}
              {booking.customerLastName || "—"}
            </p>
          </div>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
              Cancellation Reason *
            </label>
            <textarea
              rows={3}
              className={inp}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Provide justification for cancelling this reservation…"
              required
            />
          </div>
          {error && (
            <div className="flex items-center gap-2 text-xs text-red-500 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 px-3 py-2 rounded-lg">
              <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {error}
            </div>
          )}
          <div className="flex items-center gap-3 justify-end pt-1">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 rounded-lg cursor-pointer"
            >
              Close
            </button>
            <button
              type="submit"
              disabled={loading || !reason.trim()}
              className="flex items-center gap-2 px-5 py-2 text-sm font-medium bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors disabled:opacity-60 cursor-pointer"
            >
              {loading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Ban className="w-3.5 h-3.5" />}
              Confirm Cancellation
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ── Audit History Modal ────────────────────────────────────────────────────────

function AuditHistoryModal({
  booking,
  onClose,
}: {
  booking: DashboardBookingDto;
  onClose: () => void;
}) {
  const { data: logs = [], isLoading } = useQuery<FrontOfficeAuditLogDto[]>({
    queryKey: ["booking-audit-logs", booking.id],
    queryFn: () => dashboardApi.getBookingAuditLogs(booking.id),
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="bg-[var(--card-dark)] rounded-2xl border border-[var(--card-border)] shadow-2xl w-full max-w-lg max-h-[85vh] flex flex-col">
        <div className="flex items-center justify-between p-6 border-b border-[var(--card-border)]">
          <div className="flex items-center gap-2">
            <History className="w-5 h-5 text-[#C4622D]" />
            <h2
              className="font-bold text-[var(--text-primary)]"
              style={{ fontFamily: "var(--font-fraunces), serif" }}
            >
              Front Office Audit History
            </h2>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-[var(--text-primary)]/8 text-[var(--text-secondary)] cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="p-6 overflow-y-auto space-y-4">
          <div className="p-3 rounded-xl bg-[var(--bg-dark)] border border-[var(--card-border)] text-sm text-[var(--text-secondary)]">
            <p>
              <span className="font-medium text-[var(--text-primary)]">Booking Ref:</span>{" "}
              {booking.bookingReference || booking.id.slice(0, 8)}
            </p>
            <p>
              <span className="font-medium text-[var(--text-primary)]">Guest:</span>{" "}
              {booking.customerLastName || "—"}
            </p>
          </div>

          {isLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="w-6 h-6 animate-spin text-[#C4622D]" />
            </div>
          ) : logs.length === 0 ? (
            <div className="text-center py-8 text-sm text-[var(--text-secondary)]">
              No audit logs recorded for this reservation yet.
            </div>
          ) : (
            <div className="relative pl-6 border-l border-[var(--card-border)] space-y-6">
              {logs.map((log) => (
                <div key={log.id} className="relative">
                  <div className="absolute -left-[31px] top-1 w-3 h-3 rounded-full bg-[#C4622D] border-2 border-[var(--card-dark)]" />
                  <div className="flex items-baseline justify-between">
                    <span className="text-sm font-semibold text-[var(--text-primary)]">
                      {log.action}
                    </span>
                    <span className="text-xs text-[var(--text-secondary)] font-mono">
                      {new Date(log.timestampUtc).toLocaleString()}
                    </span>
                  </div>
                  <div className="mt-1 flex items-center gap-2 text-xs text-[var(--text-secondary)]">
                    <User className="w-3 h-3" />
                    <span>
                      {log.actorRole || "System"} ({log.departmentCode || "FrontOffice"})
                    </span>
                  </div>
                  {log.details && (
                    <p className="mt-1.5 p-2 rounded-lg bg-[var(--bg-dark)] text-xs text-[var(--text-secondary)] border border-[var(--card-border)]">
                      {log.details}
                    </p>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="p-4 border-t border-[var(--card-border)] flex justify-end">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium bg-[var(--text-primary)]/10 hover:bg-[var(--text-primary)]/15 text-[var(--text-primary)] rounded-lg cursor-pointer"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

// ── Booking Row ────────────────────────────────────────────────────────────────

function BookingRow({
  booking,
  onCheckIn,
  onCheckOut,
  onConfirmPayment,
  onAssignRoom,
  onCancelStaff,
  onViewAudit,
  readiness,
  canOverrideCancel,
}: {
  booking: DashboardBookingDto;
  onCheckIn: (id: string) => void;
  onCheckOut: (id: string) => void;
  onConfirmPayment: (b: DashboardBookingDto) => void;
  onAssignRoom: (b: DashboardBookingDto) => void;
  onCancelStaff: (b: DashboardBookingDto) => void;
  onViewAudit: (b: DashboardBookingDto) => void;
  readiness?: RoomReadinessDto;
  canOverrideCancel: boolean;
}) {
  const [actioning, setActioning] = useState(false);
  const s = statusNum(booking.status);

  const handleCheckIn = async () => {
    setActioning(true);
    try {
      await onCheckIn(booking.id);
    } finally {
      setActioning(false);
    }
  };
  const handleCheckOut = async () => {
    setActioning(true);
    try {
      await onCheckOut(booking.id);
    } finally {
      setActioning(false);
    }
  };

  return (
    <tr className="border-b border-[var(--card-border)] hover:bg-[var(--text-primary)]/2 transition-colors">
      <td className="px-4 py-3 font-mono text-xs text-[var(--text-secondary)]">
        {booking.bookingReference || booking.id.slice(0, 8)}
      </td>
      <td className="px-4 py-3 text-sm text-[var(--text-primary)]">
        {booking.customerLastName || "—"}
        {booking.customerEmail && (
          <div className="text-xs text-[var(--text-secondary)] truncate max-w-[160px]">
            {booking.customerEmail}
          </div>
        )}
      </td>
      <td className="px-4 py-3 text-sm text-[var(--text-secondary)]">
        <div className="font-medium text-[var(--text-primary)]">Room {booking.roomNumber || "—"}</div>
      </td>
      <td className="px-4 py-3 text-xs text-[var(--text-secondary)]">
        <div>{new Date(booking.checkInDate).toLocaleDateString()}</div>
        <div>{new Date(booking.checkOutDate).toLocaleDateString()}</div>
      </td>
      <td className="px-4 py-3 text-sm font-medium text-[var(--text-primary)]">
        LKR {booking.totalAmount.toLocaleString()}
      </td>
      <td className="px-4 py-3">
        {s === 1 && (
          <div className="mb-1.5 text-xs">
            {readiness?.readyForCheckIn ? (
              <span className="text-emerald-500 font-medium">Ready for check-in</span>
            ) : (
              <span
                className="text-amber-500 font-medium flex items-center gap-1"
                title={readiness?.blocker || "Blocked by readiness contract"}
              >
                <AlertTriangle className="w-3 h-3 shrink-0" />
                <span className="truncate max-w-[140px]">{readiness?.blocker || "Not ready"}</span>
              </span>
            )}
          </div>
        )}
        <span
          className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${BOOKING_STATUS_COLORS[s] ?? ""}`}
        >
          {BOOKING_STATUS_LABELS[s] ?? "Unknown"}
        </span>
      </td>
      <td className="px-4 py-3">
        <div className="flex items-center gap-1.5 flex-wrap">
          {actioning ? (
            <Loader2 className="w-4 h-4 animate-spin text-[var(--text-secondary)]" />
          ) : (
            <>
              {s === 0 && (
                <button
                  onClick={() => onConfirmPayment(booking)}
                  className="flex items-center gap-1 px-2.5 py-1 text-xs font-medium bg-[#2F5C52]/10 hover:bg-[#2F5C52]/20 text-[#2F5C52] border border-[#2F5C52]/20 rounded-lg transition-colors cursor-pointer"
                >
                  <CreditCard className="w-3 h-3" /> Pay
                </button>
              )}
              {s === 1 && (
                <>
                  <button
                    onClick={handleCheckIn}
                    disabled={!readiness?.readyForCheckIn}
                    title={
                      !readiness?.readyForCheckIn
                        ? readiness?.blocker || "Room not ready for check-in"
                        : "Authoritative check-in with atomic room claim"
                    }
                    className="flex items-center gap-1 px-2.5 py-1 text-xs font-medium bg-[#C4622D]/10 hover:bg-[#C4622D]/20 text-[#C4622D] border border-[#C4622D]/20 rounded-lg transition-colors disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
                  >
                    <LogIn className="w-3 h-3" /> Check In
                  </button>
                  <button
                    onClick={() => onAssignRoom(booking)}
                    title="Reassign room"
                    className="flex items-center gap-1 px-2 py-1 text-xs font-medium bg-[var(--text-primary)]/5 hover:bg-[var(--text-primary)]/10 text-[var(--text-secondary)] border border-[var(--card-border)] rounded-lg transition-colors cursor-pointer"
                  >
                    <BedDouble className="w-3 h-3" /> Room
                  </button>
                </>
              )}
              {s === 2 && (
                <button
                  onClick={handleCheckOut}
                  className="flex items-center gap-1 px-2.5 py-1 text-xs font-medium bg-[var(--text-secondary)]/10 hover:bg-[var(--text-secondary)]/20 text-[var(--text-secondary)] border border-[var(--card-border)] rounded-lg transition-colors cursor-pointer"
                >
                  <LogOut className="w-3 h-3" /> Check Out
                </button>
              )}
              {(s === 0 || s === 1) && canOverrideCancel && (
                <button
                  onClick={() => onCancelStaff(booking)}
                  title="Staff cancellation override"
                  className="flex items-center gap-1 px-2 py-1 text-xs font-medium bg-red-500/10 hover:bg-red-500/20 text-red-400 border border-red-500/20 rounded-lg transition-colors cursor-pointer"
                >
                  <Ban className="w-3 h-3" />
                </button>
              )}
              <button
                onClick={() => onViewAudit(booking)}
                title="View Front Office audit history"
                className="p-1 text-[var(--text-secondary)] hover:text-[var(--text-primary)] rounded hover:bg-[var(--text-primary)]/5 cursor-pointer"
              >
                <History className="w-3.5 h-3.5" />
              </button>
            </>
          )}
        </div>
      </td>
    </tr>
  );
}

// ── Mobile Booking Card ────────────────────────────────────────────────────────

function BookingCard({
  booking,
  onCheckIn,
  onCheckOut,
  onConfirmPayment,
  onAssignRoom,
  onCancelStaff,
  onViewAudit,
  readiness,
  canOverrideCancel,
}: {
  booking: DashboardBookingDto;
  onCheckIn: (id: string) => void;
  onCheckOut: (id: string) => void;
  onConfirmPayment: (b: DashboardBookingDto) => void;
  onAssignRoom: (b: DashboardBookingDto) => void;
  onCancelStaff: (b: DashboardBookingDto) => void;
  onViewAudit: (b: DashboardBookingDto) => void;
  readiness?: RoomReadinessDto;
  canOverrideCancel: boolean;
}) {
  const s = statusNum(booking.status);
  return (
    <div className="p-4 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl space-y-3">
      <div className="flex items-start justify-between">
        <div>
          <p className="font-medium text-[var(--text-primary)] text-sm">
            {booking.customerLastName || "Guest"}
          </p>
          <p className="text-xs text-[var(--text-secondary)] font-mono">
            {booking.bookingReference || booking.id.slice(0, 8)}
          </p>
        </div>
        <span
          className={`px-2.5 py-0.5 rounded-full text-xs font-medium border ${BOOKING_STATUS_COLORS[s] ?? ""}`}
        >
          {BOOKING_STATUS_LABELS[s] ?? "Unknown"}
        </span>
      </div>

      <div className="text-xs text-[var(--text-secondary)] space-y-0.5">
        <p>
          Room {booking.roomNumber} · LKR {booking.totalAmount.toLocaleString()}
        </p>
        <p>
          {new Date(booking.checkInDate).toLocaleDateString()} →{" "}
          {new Date(booking.checkOutDate).toLocaleDateString()}
        </p>
      </div>

      {s === 1 && (
        <div className="text-xs">
          {readiness?.readyForCheckIn ? (
            <span className="text-emerald-500 font-medium">Ready for check-in</span>
          ) : (
            <span className="text-amber-500 font-medium flex items-center gap-1">
              <AlertTriangle className="w-3 h-3 shrink-0" />
              <span>{readiness?.blocker || "Room not ready"}</span>
            </span>
          )}
        </div>
      )}

      <div className="flex items-center gap-2 pt-1 flex-wrap">
        {s === 0 && (
          <button
            onClick={() => onConfirmPayment(booking)}
            className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium bg-[#2F5C52]/10 text-[#2F5C52] border border-[#2F5C52]/20 rounded-lg cursor-pointer"
          >
            <CreditCard className="w-3 h-3" /> Confirm Payment
          </button>
        )}
        {s === 1 && (
          <>
            <button
              onClick={() => onCheckIn(booking.id)}
              disabled={!readiness?.readyForCheckIn}
              className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium bg-[#C4622D]/10 text-[#C4622D] border border-[#C4622D]/20 rounded-lg disabled:opacity-40 cursor-pointer"
            >
              <LogIn className="w-3 h-3" /> Check In
            </button>
            <button
              onClick={() => onAssignRoom(booking)}
              className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium bg-[var(--text-primary)]/5 text-[var(--text-secondary)] border border-[var(--card-border)] rounded-lg cursor-pointer"
            >
              <BedDouble className="w-3 h-3" /> Room
            </button>
          </>
        )}
        {s === 2 && (
          <button
            onClick={() => onCheckOut(booking.id)}
            className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium bg-[var(--text-secondary)]/10 text-[var(--text-secondary)] border border-[var(--card-border)] rounded-lg cursor-pointer"
          >
            <LogOut className="w-3 h-3" /> Check Out
          </button>
        )}
        {(s === 0 || s === 1) && canOverrideCancel && (
          <button
            onClick={() => onCancelStaff(booking)}
            className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium bg-red-500/10 text-red-400 border border-red-500/20 rounded-lg cursor-pointer"
          >
            <Ban className="w-3 h-3" /> Cancel
          </button>
        )}
        <button
          onClick={() => onViewAudit(booking)}
          className="p-1.5 text-[var(--text-secondary)] hover:text-[var(--text-primary)] rounded hover:bg-[var(--text-primary)]/5 cursor-pointer ml-auto"
        >
          <History className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export default function BookingsPage() {
  const qc = useQueryClient();
  const { user } = useAuthStore();
  const [statusFilter, setStatusFilter] = useState<number | undefined>(undefined);
  const [search, setSearch] = useState("");
  const [paymentBooking, setPaymentBooking] = useState<DashboardBookingDto | null>(null);
  const [assignBooking, setAssignBooking] = useState<DashboardBookingDto | null>(null);
  const [cancelBooking, setCancelBooking] = useState<DashboardBookingDto | null>(null);
  const [auditBooking, setAuditBooking] = useState<DashboardBookingDto | null>(null);
  const [actionError, setActionError] = useState("");

  const canOverrideCancel =
    user?.role === "Manager" ||
    user?.role === "Admin" ||
    user?.role === 1 ||
    user?.role === 2;

  const { data: bookings = [], isLoading } = useQuery({
    queryKey: ["dashboard-bookings", statusFilter, search],
    queryFn: () =>
      dashboardApi.getBookings({
        status: statusFilter,
        search: search || undefined,
        pageSize: 200,
      }),
    staleTime: 30_000,
  });

  const readiness = useQuery({
    queryKey: ["front-office-readiness", bookings.map((b) => b.id).join(",")],
    enabled: bookings.length > 0,
    queryFn: async () => {
      const entries = await Promise.all(
        bookings
          .filter((b) => statusNum(b.status) === 1 && b.roomId)
          .map(async (b) => [b.roomId, await dashboardApi.getRoomReadiness(b.roomId)] as const)
      );
      return Object.fromEntries(entries) as Record<string, RoomReadinessDto>;
    },
    refetchInterval: 15_000,
  });

  const { data: summary } = useQuery({
    queryKey: ["front-office-summary"],
    queryFn: () => dashboardApi.getFrontOfficeSummary(),
    staleTime: 30_000,
  });

  const checkInMutation = useMutation({
    mutationFn: (id: string) => dashboardApi.checkInGuest(id),
    onSuccess: () => {
      setActionError("");
      qc.invalidateQueries({ queryKey: ["dashboard-bookings"] });
      qc.invalidateQueries({ queryKey: ["front-office-readiness"] });
      qc.invalidateQueries({ queryKey: ["front-office-summary"] });
    },
    onError: (e) => setActionError(e instanceof Error ? e.message : "Check-in failed"),
  });

  const checkOutMutation = useMutation({
    mutationFn: (id: string) => dashboardApi.checkOutGuest(id),
    onSuccess: () => {
      setActionError("");
      qc.invalidateQueries({ queryKey: ["dashboard-bookings"] });
      qc.invalidateQueries({ queryKey: ["front-office-summary"] });
    },
    onError: (e) => setActionError(e instanceof Error ? e.message : "Check-out failed"),
  });

  const arrivals =
    summary?.todayArrivals ??
    bookings.filter(
      (b) =>
        b.checkInDate.slice(0, 10) === new Date().toISOString().slice(0, 10) &&
        statusNum(b.status) === 1
    ).length;

  const departures =
    summary?.todayDepartures ??
    bookings.filter(
      (b) =>
        b.checkOutDate.slice(0, 10) === new Date().toISOString().slice(0, 10) &&
        statusNum(b.status) === 2
    ).length;

  const pending =
    summary?.pendingCheckIns ?? bookings.filter((b) => statusNum(b.status) === 1).length;

  const activeOccupancy =
    summary?.activeOccupancy ?? bookings.filter((b) => statusNum(b.status) === 2).length;

  return (
    <div className="p-6 lg:p-8 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div>
        <h1
          className="text-3xl font-bold text-[var(--text-primary)] tracking-tight"
          style={{ fontFamily: "var(--font-fraunces), serif" }}
        >
          Bookings & Front Desk
        </h1>
        <p className="text-sm text-[var(--text-secondary)] mt-1">
          {bookings.length} reservations loaded · Authoritative readiness gating active
        </p>
      </div>

      {/* Metrics Bar */}
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {[
          ["Today's arrivals", arrivals, CalendarCheck],
          ["Today's departures", departures, CalendarClock],
          ["Pending check-ins", pending, LogIn],
          ["Active occupancy", activeOccupancy, BedDouble],
        ].map(([label, value, Icon]) => {
          const I = Icon as typeof BedDouble;
          return (
            <div
              key={String(label)}
              className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4 shadow-sm"
            >
              <I className="h-5 w-5 text-[#C4622D]" />
              <p className="mt-3 text-2xl font-semibold">{String(value)}</p>
              <p className="text-xs text-[var(--text-secondary)]">{String(label)}</p>
            </div>
          );
        })}
      </div>

      {actionError && (
        <div className="rounded-xl border border-red-400/30 bg-red-500/10 p-3 text-sm text-red-400">
          {actionError}
        </div>
      )}

      {/* Filters */}
      <div className="flex flex-col sm:flex-row gap-3">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--text-secondary)]" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search guest name, email, ref…"
            className="w-full pl-9 pr-4 py-2.5 text-sm rounded-xl bg-[var(--card-dark)] border border-[var(--card-border)] text-[var(--text-primary)] placeholder:text-[var(--text-secondary)] focus:outline-none focus:ring-2 focus:ring-[#C4622D]/30"
          />
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            onClick={() => setStatusFilter(undefined)}
            className={`px-3 py-1 rounded-full text-xs font-medium border transition-colors cursor-pointer ${statusFilter === undefined ? "bg-[#C4622D] text-white border-[#C4622D]" : "border-[var(--card-border)] text-[var(--text-secondary)] hover:border-[#C4622D]/40"}`}
          >
            All
          </button>
          {Object.entries(BOOKING_STATUS_LABELS).map(([v, label]) => (
            <button
              key={v}
              onClick={() => setStatusFilter(Number(v))}
              className={`px-3 py-1 rounded-full text-xs font-medium border transition-colors cursor-pointer ${statusFilter === Number(v) ? "bg-[#C4622D] text-white border-[#C4622D]" : "border-[var(--card-border)] text-[var(--text-secondary)] hover:border-[#C4622D]/40"}`}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {/* Desktop Table */}
      <div className="hidden md:block bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl overflow-hidden shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--card-border)] bg-[var(--text-primary)]/2">
                {["Reference", "Guest", "Room", "Dates", "Amount", "Readiness & Status", "Actions"].map(
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
                [...Array(6)].map((_, i) => (
                  <tr key={i} className="border-b border-[var(--card-border)] animate-pulse">
                    {[...Array(7)].map((__, j) => (
                      <td key={j} className="px-4 py-3">
                        <div className="h-4 bg-[var(--text-primary)]/8 rounded w-3/4" />
                      </td>
                    ))}
                  </tr>
                ))
              ) : bookings.length === 0 ? (
                <tr>
                  <td colSpan={7} className="text-center py-16">
                    <div className="flex flex-col items-center gap-3">
                      <div className="relative w-48 h-28 rounded-xl overflow-hidden opacity-60">
                        <Image src="/images/hero-view.jpg" alt="No bookings" fill className="object-cover" />
                      </div>
                      <p className="text-sm text-[var(--text-secondary)]">No bookings match your filters.</p>
                    </div>
                  </td>
                </tr>
              ) : (
                bookings.map((b) => (
                  <BookingRow
                    key={b.id}
                    booking={b}
                    onCheckIn={(id) => checkInMutation.mutate(id)}
                    onCheckOut={(id) => checkOutMutation.mutate(id)}
                    onConfirmPayment={setPaymentBooking}
                    onAssignRoom={setAssignBooking}
                    onCancelStaff={setCancelBooking}
                    onViewAudit={setAuditBooking}
                    readiness={readiness.data?.[b.roomId]}
                    canOverrideCancel={canOverrideCancel}
                  />
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
              className="h-32 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl animate-pulse"
            />
          ))
        ) : (
          bookings.map((b) => (
            <BookingCard
              key={b.id}
              booking={b}
              onCheckIn={(id) => checkInMutation.mutate(id)}
              onCheckOut={(id) => checkOutMutation.mutate(id)}
              onConfirmPayment={setPaymentBooking}
              onAssignRoom={setAssignBooking}
              onCancelStaff={setCancelBooking}
              onViewAudit={setAuditBooking}
              readiness={readiness.data?.[b.roomId]}
              canOverrideCancel={canOverrideCancel}
            />
          ))
        )}
      </div>

      {/* Payment Modal */}
      {paymentBooking && (
        <PaymentModal
          booking={paymentBooking}
          onClose={() => setPaymentBooking(null)}
          onConfirmed={() => qc.invalidateQueries({ queryKey: ["dashboard-bookings"] })}
        />
      )}

      {/* Room Assignment Modal */}
      {assignBooking && (
        <AssignRoomModal
          booking={assignBooking}
          onClose={() => setAssignBooking(null)}
          onAssigned={() => {
            qc.invalidateQueries({ queryKey: ["dashboard-bookings"] });
            qc.invalidateQueries({ queryKey: ["front-office-readiness"] });
          }}
        />
      )}

      {/* Staff Cancel Modal */}
      {cancelBooking && (
        <StaffCancelModal
          booking={cancelBooking}
          onClose={() => setCancelBooking(null)}
          onCancelled={() => {
            qc.invalidateQueries({ queryKey: ["dashboard-bookings"] });
            qc.invalidateQueries({ queryKey: ["front-office-summary"] });
          }}
        />
      )}

      {/* Audit History Modal */}
      {auditBooking && (
        <AuditHistoryModal
          booking={auditBooking}
          onClose={() => setAuditBooking(null)}
        />
      )}
    </div>
  );
}
