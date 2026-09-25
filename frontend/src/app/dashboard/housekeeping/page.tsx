"use client";
import { useAuthStore } from "@/features/auth/store/useAuthStore";
import {
  housekeepingApi,
  type HousekeepingStatus,
  type HousekeepingTask,
} from "@/services/housekeepingApi";
import {
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  BedDouble,
  CheckCircle2,
  ClipboardCheck,
  Clock3,
  RefreshCw,
  Sparkles,
  type LucideIcon,
} from "lucide-react";
import { useMemo, useState } from "react";

const columns: { status: HousekeepingStatus[]; label: string }[] = [
  {
    status: ["Pending", "Assigned", "Accepted"],
    label: "Unassigned / assigned",
  },
  {
    status: ["InProgress", "InspectionRejected"],
    label: "Cleaning / re-clean",
  },
  { status: ["AwaitingInspection"], label: "Awaiting inspection" },
  { status: ["InspectionApproved"], label: "Room ready" },
];
export default function HousekeepingPage() {
  const user = useAuthStore((s) => s.user);
  const manager = ["Manager", "Admin"].includes(String(user?.role));
  const qc = useQueryClient();
  const [error, setError] = useState("");
  const [assignments, setAssignments] = useState<Record<string, string>>({});
  const [inspection, setInspection] = useState<{
    task: HousekeepingTask;
    approved: boolean;
    notes: string;
  } | null>(null);
  const tasks = useQuery({
    queryKey: ["housekeeping", manager],
    queryFn: () => housekeepingApi.list(manager),
    refetchInterval: 20_000,
  });
  const profiles = useQuery({
    queryKey: ["housekeeping-profiles"],
    queryFn: housekeepingApi.profiles,
    enabled: manager,
  });
  const pendingTasks =
    tasks.data?.filter((task) => task.status === "Pending") ?? [];
  const candidateQueries = useQueries({
    queries: pendingTasks.map((task) => ({
      queryKey: ["task-recommendations", task.id],
      queryFn: () => housekeepingApi.recommendations(task.id),
      enabled: manager,
      staleTime: 15_000,
    })),
  });
  const candidatesByTask = Object.fromEntries(
    pendingTasks.map((task, index) => [
      task.id,
      candidateQueries[index]?.data ?? [],
    ]),
  );
  const refresh = () => qc.invalidateQueries({ queryKey: ["housekeeping"] });
  const action = useMutation({
    mutationFn: async ({
      task,
      type,
      employeeId,
      notes,
    }: {
      task: HousekeepingTask;
      type: string;
      employeeId?: string;
      notes?: string;
    }) => {
      setError("");
      if (type === "assign") {
        if (!employeeId) throw new Error("Select a Housekeeper");
        await housekeepingApi.assign(task.id, employeeId);
      }
      if (type === "accept") await housekeepingApi.accept(task.id);
      if (type === "start") await housekeepingApi.start(task.id);
      if (type === "complete") await housekeepingApi.complete(task.id);
      if (type === "approve" || type === "reject") {
        if (!notes?.trim()) throw new Error("Inspection notes are required");
        await housekeepingApi.inspect(task.id, type === "approve", notes);
      }
    },
    onSuccess: () => {
      setInspection(null);
      refresh();
    },
    onError: (e) =>
      setError(e instanceof Error ? e.message : "Housekeeping action failed"),
  });
  const stats = useMemo(
    () => ({
      unassigned: tasks.data?.filter((t) => t.status === "Pending").length || 0,
      active:
        tasks.data?.filter((t) =>
          ["Assigned", "Accepted", "InProgress", "InspectionRejected"].includes(
            t.status,
          ),
        ).length || 0,
      inspection:
        tasks.data?.filter((t) => t.status === "AwaitingInspection").length ||
        0,
      ready:
        tasks.data?.filter((t) => t.status === "InspectionApproved").length ||
        0,
    }),
    [tasks.data],
  );
  const statCards: { label: string; value: number; Icon: LucideIcon }[] = [
    { label: "Unassigned", value: stats.unassigned, Icon: Clock3 },
    { label: "Active cleaning", value: stats.active, Icon: Sparkles },
    {
      label: "Awaiting inspection",
      value: stats.inspection,
      Icon: ClipboardCheck,
    },
    { label: "Rooms ready", value: stats.ready, Icon: CheckCircle2 },
  ];
  const actions = (task: HousekeepingTask) =>
    manager
      ? task.status === "AwaitingInspection"
        ? ["approve", "reject"]
        : []
      : task.status === "Assigned"
        ? ["accept"]
        : ["Accepted", "InspectionRejected"].includes(task.status)
          ? ["start"]
          : task.status === "InProgress"
            ? ["complete"]
            : [];
  return (
    <div className="mx-auto max-w-7xl space-y-6 p-6 lg:p-8">
      <div className="flex items-end justify-between">
        <div>
          <h1
            className="text-3xl font-bold tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            Housekeeping Operations
          </h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Task-linked cleaning, Manager inspection and authoritative room
            readiness
          </p>
        </div>
        <button
          onClick={() => tasks.refetch()}
          className="rounded-xl border border-[var(--card-border)] p-2"
        >
          <RefreshCw className="h-4 w-4" />
        </button>
      </div>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {statCards.map(({ label, value, Icon }) => (
          <div
            key={label}
            className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4"
          >
            <Icon className="h-5 w-5 text-[#E87332]" />
            <p className="mt-3 text-2xl font-semibold">{value}</p>
            <p className="text-xs text-[var(--text-secondary)]">{label}</p>
          </div>
        ))}
      </div>
      {error && (
        <div className="rounded-xl border border-red-400/30 bg-red-500/10 p-3 text-sm text-red-300">
          {error}
        </div>
      )}
      <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-4">
        {columns.map((col) => (
          <section
            key={col.label}
            className="min-h-80 rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)]/60"
          >
            <h2 className="border-b border-[var(--card-border)] px-4 py-3 text-sm font-semibold">
              {col.label}
            </h2>
            <div className="space-y-3 p-3">
              {tasks.isLoading ? (
                <p className="text-sm text-[var(--text-secondary)]">Loading…</p>
              ) : (
                tasks.data
                  ?.filter((t) => col.status.includes(t.status))
                  .map((task) => (
                    <article
                      key={task.id}
                      className="rounded-xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4"
                    >
                      <div className="flex justify-between gap-2">
                        <p className="font-medium">{task.title}</p>
                        <BedDouble className="h-4 w-4 text-[#E87332]" />
                      </div>
                      <p className="mt-2 text-xs text-[var(--text-secondary)]">
                        Room {task.roomNumber || task.roomId || "—"} · Floor{" "}
                        {task.floorNumber}
                      </p>
                      <p className="mt-1 text-xs">{task.status}</p>
                      {task.recleanInstructions && (
                        <p className="mt-2 rounded-lg bg-amber-500/10 p-2 text-xs text-amber-200">
                          Re-clean: {task.recleanInstructions}
                        </p>
                      )}
                      {manager && task.status === "Pending" && (
                        <div className="mt-3 flex gap-2">
                          <select
                            value={assignments[task.id] || ""}
                            onChange={(e) =>
                              setAssignments((v) => ({
                                ...v,
                                [task.id]: e.target.value,
                              }))
                            }
                            className="min-w-0 flex-1 rounded-lg border border-[var(--card-border)] bg-transparent px-2 py-1 text-xs"
                          >
                            <option value="">
                              {candidatesByTask[task.id]?.[0]
                                ? `Recommended: ${candidatesByTask[task.id][0].fullName}`
                                : "Select Housekeeper"}
                            </option>
                            {(candidatesByTask[task.id]?.length
                              ? candidatesByTask[task.id]
                              : (profiles.data ?? [])
                            ).map((p) => (
                              <option key={p.employeeId} value={p.employeeId}>
                                {p.fullName}
                                {"recommended" in p && p.recommended
                                  ? ` — Recommended (${Math.round(p.totalScore * 100)}%)`
                                  : ""}
                              </option>
                            ))}
                          </select>
                          <button
                            onClick={() =>
                              action.mutate({
                                task,
                                type: "assign",
                                employeeId:
                                  assignments[task.id] ||
                                  candidatesByTask[task.id]?.[0]?.employeeId,
                              })
                            }
                            className="rounded-lg bg-[#2F5C52] px-3 py-1 text-xs text-white"
                          >
                            Assign
                          </button>
                        </div>
                      )}
                      <div className="mt-3 flex flex-wrap gap-2">
                        {actions(task).map((type) => (
                          <button
                            key={type}
                            disabled={action.isPending}
                            onClick={() =>
                              type === "approve" || type === "reject"
                                ? setInspection({
                                    task,
                                    approved: type === "approve",
                                    notes: "",
                                  })
                                : action.mutate({ task, type })
                            }
                            className="rounded-lg bg-[#2F5C52] px-3 py-1.5 text-xs font-medium text-white capitalize disabled:opacity-50"
                          >
                            {type}
                          </button>
                        ))}
                      </div>
                    </article>
                  ))
              )}
            </div>
          </section>
        ))}
      </div>
      {inspection && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
          <div className="w-full max-w-md rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5">
            <h2 className="text-lg font-semibold">
              {inspection.approved
                ? "Approve inspection"
                : "Reject for re-cleaning"}
            </h2>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              {inspection.task.title}
            </p>
            <textarea
              value={inspection.notes}
              onChange={(e) =>
                setInspection((v) => (v ? { ...v, notes: e.target.value } : v))
              }
              placeholder={
                inspection.approved
                  ? "Inspection notes"
                  : "Required re-cleaning instructions"
              }
              className="mt-4 min-h-28 w-full rounded-xl border border-[var(--card-border)] bg-transparent p-3 text-sm"
            />
            <div className="mt-4 flex justify-end gap-2">
              <button
                onClick={() => setInspection(null)}
                className="rounded-lg border border-[var(--card-border)] px-4 py-2 text-sm"
              >
                Cancel
              </button>
              <button
                disabled={action.isPending || !inspection.notes.trim()}
                onClick={() =>
                  action.mutate({
                    task: inspection.task,
                    type: inspection.approved ? "approve" : "reject",
                    notes: inspection.notes,
                  })
                }
                className="rounded-lg bg-[#2F5C52] px-4 py-2 text-sm text-white disabled:opacity-50"
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
