"use client";

import { dashboardApi,type StaffTaskDto } from "@/services/dashboardApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import Image from "next/image";
import { useMemo } from "react";

// ── Helpers ──────────────────────────────────────────────────────────────────

const PRIORITY_LABELS: Record<number, { label: string; color: string }> = {
  0: { label: "Low", color: "bg-[#2F5C52]/15 text-[#2F5C52] border-[#2F5C52]/20" },
  1: { label: "Medium", color: "bg-amber-500/15 text-amber-600 border-amber-400/20" },
  2: { label: "High", color: "bg-[#C4622D]/15 text-[#C4622D] border-[#C4622D]/20" },
};

const COLUMNS: { status: number; label: string; color: string; headerColor: string }[] = [
  { status: 0, label: "Pending", color: "border-amber-400/30", headerColor: "bg-amber-500/10 text-amber-700" },
  { status: 3, label: "In Progress", color: "border-[#C4622D]/30", headerColor: "bg-[#C4622D]/10 text-[#C4622D]" },
  { status: 4, label: "Completed", color: "border-[#2F5C52]/30", headerColor: "bg-[#2F5C52]/10 text-[#2F5C52]" },
  { status: 5, label: "Cancelled", color: "border-gray-300/30", headerColor: "bg-gray-500/10 text-gray-500" },
];

const ACTION_MAP: Record<number, { action: string; label: string; color: string }[]> = {
  0: [{ action: "assign", label: "Assign", color: "bg-amber-500/10 text-amber-600 border-amber-400/20 hover:bg-amber-500/20" }],
  1: [{ action: "accept", label: "Accept", color: "bg-[#2F5C52]/10 text-[#2F5C52] border-[#2F5C52]/20 hover:bg-[#2F5C52]/20" }],
  2: [{ action: "start", label: "Start", color: "bg-[#C4622D]/10 text-[#C4622D] border-[#C4622D]/20 hover:bg-[#C4622D]/20" }],
  3: [
    { action: "complete", label: "Complete", color: "bg-[#2F5C52]/10 text-[#2F5C52] border-[#2F5C52]/20 hover:bg-[#2F5C52]/20" },
    { action: "cancel", label: "Cancel", color: "bg-red-500/10 text-red-600 border-red-400/20 hover:bg-red-500/20" },
  ],
};

// ── Task Card ─────────────────────────────────────────────────────────────────

function TaskCard({ task, onAction }: { task: StaffTaskDto; onAction: (id: string, action: string) => void }) {
  const priority = PRIORITY_LABELS[task.priority] ?? PRIORITY_LABELS[0];
  const actions = ACTION_MAP[task.status] ?? [];

  return (
    <div className="p-4 rounded-xl bg-[var(--card-dark)] border border-[var(--card-border)] space-y-3 hover:shadow-md transition-shadow duration-200">
      <div className="flex items-start justify-between gap-2">
        <p className="text-sm font-medium text-[var(--text-primary)] leading-snug">{task.title}</p>
        <span className={`shrink-0 px-2 py-0.5 rounded-full text-[10px] font-semibold border ${priority.color}`}>
          {priority.label}
        </span>
      </div>
      <div className="text-xs text-[var(--text-secondary)] space-y-0.5">
        <p>Floor {task.targetFloor} · {task.requiredRole}</p>
        {task.assignedEmployeeName && <p>👤 {task.assignedEmployeeName}</p>}
      </div>
      {actions.length > 0 && (
        <div className="flex flex-wrap gap-2 pt-1 border-t border-[var(--card-border)]">
          {actions.map((a) => (
            <button
              key={a.action}
              onClick={() => onAction(task.id, a.action)}
              className={`px-2.5 py-1 text-xs font-medium rounded-lg border transition-colors cursor-pointer ${a.color}`}
            >
              {a.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Kanban Column ─────────────────────────────────────────────────────────────

function KanbanColumn({
  column,
  tasks,
  loading,
  onAction,
}: {
  column: typeof COLUMNS[0];
  tasks: StaffTaskDto[];
  loading: boolean;
  onAction: (id: string, action: string) => void;
}) {
  return (
    <div className={`flex flex-col rounded-2xl border ${column.color} bg-[var(--card-dark)]/60 min-h-[400px]`}>
      <div className={`flex items-center justify-between px-4 py-3 rounded-t-2xl ${column.headerColor} border-b border-inherit`}>
        <span className="text-xs font-bold uppercase tracking-wider">{column.label}</span>
        <span className="text-xs font-bold opacity-70">{tasks.length}</span>
      </div>
      <div className="flex-1 p-3 space-y-3 overflow-y-auto">
        {loading ? (
          [...Array(2)].map((_, i) => (
            <div key={i} className="h-24 rounded-xl bg-[var(--text-primary)]/5 animate-pulse" />
          ))
        ) : tasks.length === 0 ? (
          <div className="flex items-center justify-center h-full py-8">
            <p className="text-xs text-[var(--text-secondary)] text-center">No tasks</p>
          </div>
        ) : (
          tasks.map((t) => <TaskCard key={t.id} task={t} onAction={onAction} />)
        )}
      </div>
    </div>
  );
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export default function TasksPage() {
  const qc = useQueryClient();

  const { data: tasks = [], isLoading } = useQuery({
    queryKey: ["dashboard-tasks"],
    queryFn: () => dashboardApi.getTasks(),
    staleTime: 20_000,
    refetchInterval: 30_000,
  });

  const actionMutation = useMutation({
    mutationFn: ({ id, action }: { id: string; action: string }) =>
      dashboardApi.updateTaskStatus(id, action),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["dashboard-tasks"] }),
  });

  const grouped = useMemo(() => {
    const map: Record<number, StaffTaskDto[]> = { 0: [], 1: [], 2: [], 3: [], 4: [], 5: [] };
    tasks.forEach((t) => {
      const g = map[t.status];
      if (g) g.push(t);
    });
    // Merge 0+1+2 into "Pending" column for display
    map[0] = [...(map[0] ?? []), ...(map[1] ?? []), ...(map[2] ?? [])];
    return map;
  }, [tasks]);

  const totalPending = (grouped[0] ?? []).length;
  const totalInProgress = (grouped[3] ?? []).length;
  const totalCompleted = (grouped[4] ?? []).length;

  return (
    <div className="p-6 lg:p-8 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-4">
        <div>
          <h1
            className="text-3xl font-bold text-[var(--text-primary)] tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            Tasks & Housekeeping
          </h1>
          <p className="text-sm text-[var(--text-secondary)] mt-1">
            {totalPending} pending · {totalInProgress} in progress · {totalCompleted} completed today
          </p>
        </div>
        <div className="flex items-center gap-2 text-xs text-[var(--text-secondary)] bg-[var(--card-dark)] border border-[var(--card-border)] px-3 py-2 rounded-xl">
          <div className="w-2 h-2 rounded-full bg-[#2F5C52] animate-pulse" />
          Auto-refreshes every 30s
        </div>
      </div>

      {/* Empty state */}
      {!isLoading && tasks.length === 0 && (
        <div className="flex flex-col items-center justify-center py-20 gap-4">
          <div className="relative w-64 h-40 rounded-xl overflow-hidden opacity-60">
            <Image src="/images/slowhouse-hero.jpg" alt="No tasks" fill className="object-cover" />
          </div>
          <p className="text-sm text-[var(--text-secondary)]">No tasks found. All quiet on the floor!</p>
        </div>
      )}

      {/* Kanban Board */}
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
        {COLUMNS.map((col) => (
          <KanbanColumn
            key={col.status}
            column={col}
            tasks={grouped[col.status] ?? []}
            loading={isLoading}
            onAction={(id, action) => actionMutation.mutate({ id, action })}
          />
        ))}
      </div>
    </div>
  );
}
