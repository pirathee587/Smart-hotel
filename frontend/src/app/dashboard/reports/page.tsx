"use client";

import {
dashboardApi,
type ComplaintDto,
type ReviewDto,
} from "@/services/dashboardApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import {
AlertCircle,
Check,
Clock,
Eye,
EyeOff,
Loader2,
Star,
X
} from "lucide-react";
import Image from "next/image";
import React,{ useState } from "react";

// ── Helpers ──────────────────────────────────────────────────────────────────

const COMPLAINT_STATUS_LABELS: Record<number, string> = {
  1: "Open",
  2: "In Progress",
  3: "Escalated",
  4: "Resolved",
  5: "Closed",
  6: "Reopened",
};

const COMPLAINT_STATUS_COLORS: Record<number, string> = {
  1: "bg-amber-500/15 text-amber-600 border-amber-400/30",
  2: "bg-[#C4622D]/15 text-[#C4622D] border-[#C4622D]/30",
  3: "bg-red-500/15 text-red-600 border-red-400/30",
  4: "bg-[#2F5C52]/15 text-[#2F5C52] border-[#2F5C52]/30",
  5: "bg-gray-500/15 text-gray-500 border-gray-400/30",
  6: "bg-amber-500/15 text-amber-600 border-amber-400/30",
};

const SEVERITY_LABELS: Record<number, { label: string; color: string }> = {
  1: { label: "Low", color: "text-[#2F5C52]" },
  2: { label: "Medium", color: "text-amber-600" },
  3: { label: "High", color: "text-[#C4622D]" },
  4: { label: "Critical", color: "text-red-600 font-bold" },
};

function statusNum(s: number | string): number {
  if (typeof s === "number") return s;
  return parseInt(s as string, 10) || 1;
}

function severityNum(s: number | string): number {
  if (typeof s === "number") return s;
  return parseInt(s as string, 10) || 1;
}

// ── Complaint Update Modal ────────────────────────────────────────────────────

function ComplaintUpdateModal({
  complaint,
  onClose,
  onUpdated,
}: {
  complaint: ComplaintDto;
  onClose: () => void;
  onUpdated: () => void;
}) {
  const s = statusNum(complaint.status);
  const [newStatus, setNewStatus] = useState(s);
  const [note, setNote] = useState("");
  const [resolutionNotes, setResolutionNotes] = useState(complaint.resolutionNotes ?? "");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await dashboardApi.updateComplaintStatus(complaint.id, {
        status: newStatus,
        note: note || undefined,
        resolutionNotes: resolutionNotes || undefined,
      });
      onUpdated();
      onClose();
    } catch {
      setError("Failed to update complaint status.");
    } finally {
      setLoading(false);
    }
  };

  const inp = "w-full px-3 py-2 text-sm rounded-lg bg-[var(--bg-dark)] border border-[var(--card-border)] text-[var(--text-primary)] focus:outline-none focus:ring-2 focus:ring-[#C4622D]/40";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="bg-[var(--card-dark)] rounded-2xl border border-[var(--card-border)] shadow-2xl w-full max-w-md">
        <div className="flex items-center justify-between p-6 border-b border-[var(--card-border)]">
          <h2 className="font-bold text-[var(--text-primary)]" style={{ fontFamily: "var(--font-fraunces), serif" }}>
            Update Complaint
          </h2>
          <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-[var(--text-primary)]/8 text-[var(--text-secondary)] cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="text-sm text-[var(--text-secondary)] space-y-1 p-3 bg-[var(--bg-dark)] rounded-xl border border-[var(--card-border)]">
            <p className="font-medium text-[var(--text-primary)]">{complaint.title}</p>
            <p className="text-xs">{complaint.description}</p>
          </div>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">New Status</label>
            <select
              value={newStatus}
              onChange={(e) => setNewStatus(Number(e.target.value))}
              className={inp}
            >
              {Object.entries(COMPLAINT_STATUS_LABELS).map(([v, label]) => (
                <option key={v} value={v}>{label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Timeline Note (optional)</label>
            <textarea rows={2} className={inp} value={note} onChange={(e) => setNote(e.target.value)} placeholder="What action was taken?" />
          </div>
          {(newStatus === 4 || newStatus === 5) && (
            <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Resolution Notes</label>
              <textarea rows={3} className={inp} value={resolutionNotes} onChange={(e) => setResolutionNotes(e.target.value)} placeholder="How was this resolved?" />
            </div>
          )}
          {error && (
            <div className="flex items-center gap-2 text-xs text-red-500 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 px-3 py-2 rounded-lg">
              <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {error}
            </div>
          )}
          <div className="flex items-center gap-3 justify-end pt-1">
            <button type="button" onClick={onClose} className="px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 rounded-lg cursor-pointer">
              Cancel
            </button>
            <button type="submit" disabled={loading} className="flex items-center gap-2 px-5 py-2 text-sm font-medium bg-[#C4622D] hover:bg-[#E07A3E] text-white rounded-lg transition-colors disabled:opacity-60 cursor-pointer">
              {loading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Check className="w-3.5 h-3.5" />}
              Update
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ── Complaints Tab ────────────────────────────────────────────────────────────

function ComplaintsTab() {
  const qc = useQueryClient();
  const [selectedComplaint, setSelectedComplaint] = useState<ComplaintDto | null>(null);

  const { data: complaints = [], isLoading } = useQuery({
    queryKey: ["complaints"],
    queryFn: () => dashboardApi.getComplaints(),
    staleTime: 30_000,
  });

  return (
    <>
      {isLoading ? (
        <div className="space-y-3">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="h-24 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl animate-pulse" />
          ))}
        </div>
      ) : complaints.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 gap-4">
          <div className="relative w-56 h-36 rounded-xl overflow-hidden opacity-60">
            <Image src="/images/pool-thumb.jpg" alt="No complaints" fill className="object-cover" />
          </div>
          <p className="text-sm text-[var(--text-secondary)]">No complaints on record. Great job!</p>
        </div>
      ) : (
        <div className="space-y-3">
          {complaints.map((c) => {
            const s = statusNum(c.status);
            const sev = severityNum(c.severity);
            const sevInfo = SEVERITY_LABELS[sev] ?? SEVERITY_LABELS[1];
            return (
              <div key={c.id} className="p-4 sm:p-5 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl space-y-3 hover:shadow-md transition-shadow">
                <div className="flex items-start justify-between gap-3">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap mb-1">
                      <h4 className="text-sm font-semibold text-[var(--text-primary)]">{c.title}</h4>
                      {c.isOverdue && (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-red-600/20 text-red-600 border border-red-500/30">
                          <Clock className="w-3 h-3" /> SLA Overdue
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-[var(--text-secondary)] line-clamp-2">{c.description}</p>
                  </div>
                  <div className="flex flex-col items-end gap-1.5 shrink-0">
                    <span className={`px-2.5 py-0.5 rounded-full text-xs font-medium border ${COMPLAINT_STATUS_COLORS[s] ?? ""}`}>
                      {COMPLAINT_STATUS_LABELS[s] ?? "Unknown"}
                    </span>
                    <span className={`text-xs font-medium ${sevInfo.color}`}>
                      {sevInfo.label} severity
                    </span>
                  </div>
                </div>
                <div className="flex items-center justify-between">
                  <p className="text-xs text-[var(--text-secondary)]">
                    Filed {new Date(c.createdAtUtc).toLocaleDateString()} · SLA: {new Date(c.slaDeadlineUtc).toLocaleDateString()}
                  </p>
                  <button
                    onClick={() => setSelectedComplaint(c)}
                    className="text-xs font-medium text-[#C4622D] hover:underline cursor-pointer"
                  >
                    Update status →
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {selectedComplaint && (
        <ComplaintUpdateModal
          complaint={selectedComplaint}
          onClose={() => setSelectedComplaint(null)}
          onUpdated={() => qc.invalidateQueries({ queryKey: ["complaints"] })}
        />
      )}
    </>
  );
}

// ── Reviews Tab ───────────────────────────────────────────────────────────────

function ReviewsTab() {
  const qc = useQueryClient();
  const [hiding, setHiding] = useState<string | null>(null);

  const { data: reviews = [], isLoading } = useQuery({
    queryKey: ["reviews"],
    queryFn: () => dashboardApi.getAllReviews(),
    staleTime: 30_000,
  });

  const toggleMutation = useMutation({
    mutationFn: async ({ id, isPublished }: { id: string; isPublished: boolean }) => {
      if (isPublished) {
        await dashboardApi.hideReview(id);
      } else {
        await dashboardApi.unhideReview(id);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["reviews"] }),
  });

  const handleToggle = async (review: ReviewDto) => {
    setHiding(review.id);
    try {
      await toggleMutation.mutateAsync({ id: review.id, isPublished: review.isPublished });
    } finally {
      setHiding(null);
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-3">
        {[...Array(4)].map((_, i) => (
          <div key={i} className="h-28 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl animate-pulse" />
        ))}
      </div>
    );
  }

  if (reviews.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4">
        <div className="relative w-56 h-36 rounded-xl overflow-hidden opacity-60">
          <Image src="/images/slowhouse-interior.jpg" alt="No reviews" fill className="object-cover" />
        </div>
        <p className="text-sm text-[var(--text-secondary)]">No reviews to moderate.</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {reviews.map((r) => (
        <div key={r.id} className={`p-4 sm:p-5 bg-[var(--card-dark)] border rounded-2xl space-y-3 transition-all ${r.isPublished ? "border-[var(--card-border)]" : "border-gray-300/20 opacity-60"}`}>
          <div className="flex items-start justify-between gap-3">
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-2">
                <div className="flex">
                  {[...Array(5)].map((_, i) => (
                    <Star
                      key={i}
                      className={`w-3.5 h-3.5 ${i < r.rating ? "text-[#E07A3E] fill-[#E07A3E]" : "text-gray-300"}`}
                    />
                  ))}
                </div>
                <span className="text-xs text-[var(--text-secondary)]">{new Date(r.createdAtUtc).toLocaleDateString()}</span>
                {!r.isPublished && (
                  <span className="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-gray-500/15 text-gray-500 border border-gray-400/30">
                    Hidden
                  </span>
                )}
              </div>
              <p className="text-sm text-[var(--text-primary)]">{r.comment}</p>
              <p className="text-xs text-[var(--text-secondary)] mt-1 font-mono">{r.customerId.slice(0, 8)}…</p>
            </div>
            <button
              onClick={() => handleToggle(r)}
              disabled={hiding === r.id}
              className={`flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-xl border transition-colors cursor-pointer shrink-0 ${
                r.isPublished
                  ? "bg-[#C4622D]/10 text-[#C4622D] border-[#C4622D]/20 hover:bg-[#C4622D]/20"
                  : "bg-[#2F5C52]/10 text-[#2F5C52] border-[#2F5C52]/20 hover:bg-[#2F5C52]/20"
              }`}
            >
              {hiding === r.id ? (
                <Loader2 className="w-3.5 h-3.5 animate-spin" />
              ) : r.isPublished ? (
                <><EyeOff className="w-3.5 h-3.5" /> Hide</>
              ) : (
                <><Eye className="w-3.5 h-3.5" /> Unhide</>
              )}
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export default function ReportsPage() {
  const [tab, setTab] = useState<"complaints" | "reviews">("complaints");

  return (
    <div className="p-6 lg:p-8 max-w-5xl mx-auto space-y-6">
      <div>
        <h1
          className="text-3xl font-bold text-[var(--text-primary)] tracking-tight"
          style={{ fontFamily: "var(--font-fraunces), serif" }}
        >
          Analytics & Reports
        </h1>
        <p className="text-sm text-[var(--text-secondary)] mt-1">
          Manage guest complaints and moderate reviews
        </p>
      </div>

      {/* Tab switcher */}
      <div className="flex gap-1 p-1 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-xl w-fit">
        {(["complaints", "reviews"] as const).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-5 py-2 rounded-lg text-sm font-medium transition-all duration-200 cursor-pointer ${
              tab === t ? "bg-[#C4622D] text-white shadow-sm" : "text-[var(--text-secondary)] hover:text-[var(--text-primary)]"
            }`}
          >
            {t === "complaints" ? "Complaints" : "Reviews"}
          </button>
        ))}
      </div>

      {tab === "complaints" ? <ComplaintsTab /> : <ReviewsTab />}
    </div>
  );
}
