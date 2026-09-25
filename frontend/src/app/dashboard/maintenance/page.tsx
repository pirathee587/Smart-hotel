"use client";
import { useAuthStore } from "@/features/auth/store/useAuthStore";
import {
  maintenanceApi,
  type MaintenanceStatus,
  type MaintenanceTask,
} from "@/services/maintenanceApi";
import {
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  RefreshCw,
  Wrench,
} from "lucide-react";
import { useMemo, useState } from "react";
const lanes: { label: string; statuses: MaintenanceStatus[] }[] = [
  { label: "New / unassigned", statuses: ["Pending"] },
  {
    label: "Assigned / repair",
    statuses: ["Assigned", "Accepted", "InProgress", "InspectionRejected"],
  },
  { label: "Awaiting verification", statuses: ["AwaitingInspection"] },
  { label: "Verified / completed", statuses: ["InspectionApproved"] },
];
export default function MaintenancePage() {
  const user = useAuthStore((s) => s.user);
  const manager = ["Manager", "Admin"].includes(String(user?.role));
  const qc = useQueryClient();
  const [error, setError] = useState("");
  const [assign, setAssign] = useState<Record<string, string>>({});
  const [dialog, setDialog] = useState<{
    task: MaintenanceTask;
    kind: "complete" | "approve" | "reject";
    notes: string;
    cost: string;
    parts: string;
  } | null>(null);
  const [issue, setIssue] = useState({
    title: "",
    description: "",
    roomId: "",
    roomNumber: "",
    assetName: "",
    severity: "Medium",
    safetyHazard: false,
    hazardDetails: "",
    estimatedCost: "0",
  });
  const tasks = useQuery({
    queryKey: ["maintenance", manager],
    queryFn: () => maintenanceApi.list(manager),
    refetchInterval: 20_000,
  });
  const profiles = useQuery({
    queryKey: ["maintenance-profiles"],
    queryFn: maintenanceApi.profiles,
    enabled: manager,
  });
  const pendingTasks = tasks.data?.filter((task) => task.status === "Pending") ?? [];
  const candidateQueries = useQueries({
    queries: pendingTasks.map((task) => ({
      queryKey: ["task-recommendations", task.id],
      queryFn: () => maintenanceApi.recommendations(task.id),
      enabled: manager,
      staleTime: 15_000,
    })),
  });
  const candidatesByTask = Object.fromEntries(
    pendingTasks.map((task, index) => [task.id, candidateQueries[index]?.data ?? []]),
  );
  const refresh = () => qc.invalidateQueries({ queryKey: ["maintenance"] });
  const action = useMutation({
    mutationFn: async ({
      task,
      kind,
      employeeId,
    }: {
      task?: MaintenanceTask;
      kind: string;
      employeeId?: string;
    }) => {
      setError("");
      if (kind === "create")
        await maintenanceApi.create({
          ...issue,
          estimatedCost: Number(issue.estimatedCost),
        });
      if (!task) return;
      if (kind === "assign" && employeeId)
        await maintenanceApi.assign(task.id, employeeId);
      if (kind === "accept") await maintenanceApi.accept(task.id);
      if (kind === "start") await maintenanceApi.start(task.id);
      if (kind === "complete" && dialog)
        await maintenanceApi.complete(
          task.id,
          dialog.notes,
          Number(dialog.cost),
          dialog.parts,
        );
      if ((kind === "approve" || kind === "reject") && dialog)
        await maintenanceApi.verify(task.id, kind === "approve", dialog.notes);
      if (kind === "cost") await maintenanceApi.approveCost(task.id);
    },
    onSuccess: () => {
      setDialog(null);
      refresh();
    },
    onError: (e) =>
      setError(e instanceof Error ? e.message : "Maintenance action failed"),
  });
  const stats = useMemo(
    () => ({
      new: tasks.data?.filter((x) => x.status === "Pending").length || 0,
      active:
        tasks.data?.filter((x) =>
          ["Assigned", "Accepted", "InProgress", "InspectionRejected"].includes(
            x.status,
          ),
        ).length || 0,
      verify:
        tasks.data?.filter((x) => x.status === "AwaitingInspection").length ||
        0,
      blocked: tasks.data?.filter((x) => !x.restrictionClearedAt).length || 0,
    }),
    [tasks.data],
  );
  return (
    <div className="mx-auto max-w-7xl space-y-6 p-6 lg:p-8">
      <div className="flex items-end justify-between">
        <div>
          <h1
            className="text-3xl font-bold tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            Maintenance Operations
          </h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Room restrictions, verified repairs and Finance expense handoff
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
        {[
          ["New issues", stats.new, AlertTriangle],
          ["Active repairs", stats.active, Wrench],
          ["Awaiting verification", stats.verify, Clock3],
          ["Out of service", stats.blocked, CheckCircle2],
        ].map(([l, v, I]) => {
          const Icon = I as typeof Wrench;
          return (
            <div
              key={String(l)}
              className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4"
            >
              <Icon className="h-5 w-5 text-[#E87332]" />
              <p className="mt-3 text-2xl font-semibold">{String(v)}</p>
              <p className="text-xs text-[var(--text-secondary)]">
                {String(l)}
              </p>
            </div>
          );
        })}
      </div>
      {manager && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            action.mutate({ kind: "create" });
          }}
          className="grid gap-3 rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4 md:grid-cols-4"
        >
          <input
            required
            placeholder="Issue title"
            value={issue.title}
            onChange={(e) => setIssue({ ...issue, title: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent p-2"
          />
          <input
            required
            placeholder="Room UUID"
            value={issue.roomId}
            onChange={(e) => setIssue({ ...issue, roomId: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent p-2"
          />
          <input
            required
            placeholder="Room number"
            value={issue.roomNumber}
            onChange={(e) => setIssue({ ...issue, roomNumber: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent p-2"
          />
          <input
            required
            placeholder="Asset"
            value={issue.assetName}
            onChange={(e) => setIssue({ ...issue, assetName: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent p-2"
          />
          <textarea
            required
            placeholder="Issue description"
            value={issue.description}
            onChange={(e) =>
              setIssue({ ...issue, description: e.target.value })
            }
            className="rounded-lg border border-[var(--card-border)] bg-transparent p-2 md:col-span-2"
          />
          <select
            value={issue.severity}
            onChange={(e) => setIssue({ ...issue, severity: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent p-2"
          >
            <option>Low</option>
            <option>Medium</option>
            <option>High</option>
            <option>Critical</option>
          </select>
          <input
            type="number"
            min="0"
            placeholder="Estimated cost"
            value={issue.estimatedCost}
            onChange={(e) =>
              setIssue({ ...issue, estimatedCost: e.target.value })
            }
            className="rounded-lg border border-[var(--card-border)] bg-transparent p-2"
          />
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={issue.safetyHazard}
              onChange={(e) =>
                setIssue({ ...issue, safetyHazard: e.target.checked })
              }
            />
            Safety hazard
          </label>
          <input
            placeholder="Hazard details"
            value={issue.hazardDetails}
            onChange={(e) =>
              setIssue({ ...issue, hazardDetails: e.target.value })
            }
            className="rounded-lg border border-[var(--card-border)] bg-transparent p-2 md:col-span-2"
          />
          <button className="rounded-lg bg-[#2F5C52] p-2 text-white">
            Report issue
          </button>
        </form>
      )}
      {error && (
        <p className="rounded-xl bg-red-500/10 p-3 text-sm text-red-300">
          {error}
        </p>
      )}
      <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-4">
        {lanes.map((l) => (
          <section
            key={l.label}
            className="min-h-80 rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)]/60"
          >
            <h2 className="border-b border-[var(--card-border)] px-4 py-3 text-sm font-semibold">
              {l.label}
            </h2>
            <div className="space-y-3 p-3">
              {tasks.data
                ?.filter((t) => l.statuses.includes(t.status))
                .map((t) => (
                  <article
                    key={t.id}
                    className="rounded-xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4"
                  >
                    <p className="font-medium">{t.title}</p>
                    <p className="mt-1 text-xs text-[var(--text-secondary)]">
                      Room {t.roomNumber} · {t.assetName} · {t.severity}
                    </p>
                    {t.safetyHazard && (
                      <p className="mt-2 text-xs text-red-300">
                        Safety hazard: {t.hazardDetails}
                      </p>
                    )}
                    {t.reworkInstructions && (
                      <p className="mt-2 text-xs text-amber-200">
                        Rework: {t.reworkInstructions}
                      </p>
                    )}
                    {manager && t.status === "Pending" && (
                      <div className="mt-3 flex gap-2">
                        <select
                          value={assign[t.id] || ""}
                          onChange={(e) =>
                            setAssign({ ...assign, [t.id]: e.target.value })
                          }
                          className="min-w-0 flex-1 rounded-lg border border-[var(--card-border)] bg-transparent p-1 text-xs"
                        >
                          <option value="">
                            {candidatesByTask[t.id]?.[0]
                              ? `Recommended: ${candidatesByTask[t.id][0].fullName}`
                              : "Select technician"}
                          </option>
                          {(candidatesByTask[t.id]?.length
                            ? candidatesByTask[t.id]
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
                              task: t,
                              kind: "assign",
                              employeeId:
                                assign[t.id] ||
                                candidatesByTask[t.id]?.[0]?.employeeId,
                            })
                          }
                          className="rounded-lg bg-[#2F5C52] px-2 text-xs"
                        >
                          Assign
                        </button>
                      </div>
                    )}
                    <div className="mt-3 flex flex-wrap gap-2">
                      {!manager && t.status === "Assigned" && (
                        <button
                          onClick={() =>
                            action.mutate({ task: t, kind: "accept" })
                          }
                        >
                          Accept
                        </button>
                      )}
                      {!manager &&
                        ["Accepted", "InspectionRejected"].includes(
                          t.status,
                        ) && (
                          <button
                            onClick={() =>
                              action.mutate({ task: t, kind: "start" })
                            }
                          >
                            Start repair
                          </button>
                        )}
                      {!manager && t.status === "InProgress" && (
                        <button
                          onClick={() =>
                            setDialog({
                              task: t,
                              kind: "complete",
                              notes: "",
                              cost: "0",
                              parts: "",
                            })
                          }
                        >
                          Complete
                        </button>
                      )}
                      {manager && t.status === "AwaitingInspection" && (
                        <>
                          <button
                            onClick={() =>
                              setDialog({
                                task: t,
                                kind: "approve",
                                notes: "",
                                cost: "0",
                                parts: "",
                              })
                            }
                          >
                            Verify
                          </button>
                          <button
                            onClick={() =>
                              setDialog({
                                task: t,
                                kind: "reject",
                                notes: "",
                                cost: "0",
                                parts: "",
                              })
                            }
                          >
                            Reject
                          </button>
                        </>
                      )}
                      {manager &&
                        t.status === "InspectionApproved" &&
                        !t.financeExpenseId &&
                        Number(t.actualCost) > 0 && (
                          <button
                            onClick={() =>
                              action.mutate({ task: t, kind: "cost" })
                            }
                          >
                            Submit cost
                          </button>
                        )}
                    </div>
                  </article>
                ))}
            </div>
          </section>
        ))}
      </div>
      {dialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
          <form
            onSubmit={(e) => {
              e.preventDefault();
              action.mutate({ task: dialog.task, kind: dialog.kind });
            }}
            className="w-full max-w-md space-y-3 rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5"
          >
            <h2 className="text-lg font-semibold">
              {dialog.kind === "complete"
                ? "Repair completion"
                : dialog.kind === "approve"
                  ? "Verify repair"
                  : "Reject for rework"}
            </h2>
            <textarea
              required
              value={dialog.notes}
              onChange={(e) => setDialog({ ...dialog, notes: e.target.value })}
              placeholder="Notes / instructions"
              className="min-h-24 w-full rounded-lg border border-[var(--card-border)] bg-transparent p-2"
            />
            {dialog.kind === "complete" && (
              <>
                <input
                  required
                  type="number"
                  min="0"
                  value={dialog.cost}
                  onChange={(e) =>
                    setDialog({ ...dialog, cost: e.target.value })
                  }
                  className="w-full rounded-lg border border-[var(--card-border)] bg-transparent p-2"
                  placeholder="Actual cost"
                />
                <input
                  value={dialog.parts}
                  onChange={(e) =>
                    setDialog({ ...dialog, parts: e.target.value })
                  }
                  className="w-full rounded-lg border border-[var(--card-border)] bg-transparent p-2"
                  placeholder="Parts used"
                />
              </>
            )}
            <div className="flex justify-end gap-2">
              <button type="button" onClick={() => setDialog(null)}>
                Cancel
              </button>
              <button className="rounded-lg bg-[#2F5C52] px-4 py-2 text-white">
                Confirm
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
