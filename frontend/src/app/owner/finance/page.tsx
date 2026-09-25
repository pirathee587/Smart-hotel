"use client";

import { financeApi } from "@/services/financeApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

export default function OwnerFinancePage() {
  const queryClient = useQueryClient();
  const { data, isLoading, error } = useQuery({
    queryKey: ["owner-finance-overview"],
    queryFn: financeApi.overview,
  });
  const expenses = useQuery({
    queryKey: ["owner-finance-expenses"],
    queryFn: financeApi.expenses,
  });
  const refunds = useQuery({
    queryKey: ["owner-finance-refunds"],
    queryFn: financeApi.refunds,
  });
  const releases = useQuery({
    queryKey: ["owner-finance-releases"],
    queryFn: financeApi.releases,
  });
  const payroll = useQuery({
    queryKey: ["owner-finance-payroll"],
    queryFn: financeApi.payroll,
  });
  const settings = useQuery({
    queryKey: ["finance-settings"],
    queryFn: financeApi.settings,
  });
  const [limits, setLimits] = useState({
    expense: "",
    refund: "",
    release: "",
  });
  const saveSettings = useMutation({
    mutationFn: () =>
      financeApi.saveSettings({
        managerExpenseApprovalLimit: Number(limits.expense),
        managerRefundApprovalLimit: Number(limits.refund),
        managerReleaseApprovalLimit: Number(limits.release),
        currency: "LKR",
      }),
    onSuccess: async () =>
      queryClient.invalidateQueries({ queryKey: ["finance-settings"] }),
  });
  const decide = useMutation({
    mutationFn: ({
      type,
      id,
      approve,
    }: {
      type: "expense" | "refund" | "release" | "payroll";
      id: string;
      approve: boolean;
    }) =>
      type === "expense"
        ? financeApi.decideExpense(id, approve)
        : type === "refund"
          ? financeApi.decideRefund(id, approve)
          : type === "release"
            ? financeApi.decideRelease(id, approve)
            : financeApi.decidePayroll(id, approve),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["owner-finance-expenses"],
      });
      await queryClient.invalidateQueries({
        queryKey: ["owner-finance-refunds"],
      });
      await queryClient.invalidateQueries({
        queryKey: ["owner-finance-releases"],
      });
      await queryClient.invalidateQueries({
        queryKey: ["owner-finance-payroll"],
      });
      await queryClient.invalidateQueries({
        queryKey: ["owner-finance-overview"],
      });
    },
  });
  const money = (value: number) =>
    new Intl.NumberFormat("en-LK", {
      style: "currency",
      currency: data?.currency || "LKR",
    }).format(value || 0);
  const escalated = [
    ...(expenses.data || []).map((r) => ({ ...r, type: "expense" as const })),
    ...(refunds.data || []).map((r) => ({ ...r, type: "refund" as const })),
    ...(releases.data || []).map((r) => ({ ...r, type: "release" as const })),
    ...(payroll.data || [])
      .filter(
        (r) =>
          String(r.status).toLowerCase().includes("ownerapproval") ||
          r.status === 2,
      )
      .map((r) => ({
        ...r,
        amount: r.netSalary,
        description: `Payroll for employee ${r.employeeId}`,
        approvalStage: "Owner",
        type: "payroll" as const,
      })),
  ].filter(
    (r) =>
      (r.type === "payroll" ||
        r.status === 0 ||
        String(r.status).toLowerCase().includes("pending")) &&
      (r.approvalStage === 1 ||
        String(r.approvalStage).toLowerCase().includes("owner")),
  );
  return (
    <main className="mx-auto max-w-7xl space-y-6 p-6 lg:p-8">
      <header>
        <p className="text-xs uppercase tracking-wider text-[var(--text-secondary)]">
          Hotel-wide oversight
        </p>
        <h1 className="text-3xl font-bold">Finance</h1>
      </header>
      {error && (
        <p className="text-red-300">
          Could not load authorized financial records.
        </p>
      )}
      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {[
          ["Collected revenue", data?.collectedRevenue],
          ["Recorded expenses", data?.recordedExpenses],
          ["Outstanding payments", data?.outstandingPayments],
          ["Refunded", data?.refundedAmount],
          ["Escrow held", data?.heldFunds],
          ["Released (not settled)", data?.releasedFunds],
          ["Bank settled", data?.settledFunds],
        ].map(([label, value]) => (
          <div
            key={String(label)}
            className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5"
          >
            <p className="text-xs uppercase text-[var(--text-secondary)]">
              {String(label)}
            </p>
            <p className="mt-2 text-2xl font-bold">
              {isLoading ? "…" : money(Number(value || 0))}
            </p>
          </div>
        ))}
      </section>
      {data && (
        <p className="text-xs text-[var(--text-secondary)]">
          {data.metricDefinition}
        </p>
      )}
      <section className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5">
        <h2 className="text-lg font-semibold">Approval thresholds</h2>
        <p className="mt-1 text-xs text-[var(--text-secondary)]">
          Current: expenses{" "}
          {money(settings.data?.managerExpenseApprovalLimit || 0)}, refunds{" "}
          {money(settings.data?.managerRefundApprovalLimit || 0)}, escrow
          releases {money(settings.data?.managerReleaseApprovalLimit || 0)}
        </p>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            saveSettings.mutate();
          }}
          className="mt-4 flex flex-wrap gap-3"
        >
          <input
            required
            type="number"
            min="0"
            step="0.01"
            placeholder="Expense manager limit"
            value={limits.expense}
            onChange={(e) => setLimits({ ...limits, expense: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent px-3 py-2"
          />
          <input
            required
            type="number"
            min="0"
            step="0.01"
            placeholder="Refund manager limit"
            value={limits.refund}
            onChange={(e) => setLimits({ ...limits, refund: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent px-3 py-2"
          />
          <input
            required
            type="number"
            min="0"
            step="0.01"
            placeholder="Release manager limit"
            value={limits.release}
            onChange={(e) => setLimits({ ...limits, release: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent px-3 py-2"
          />
          <button className="rounded-lg bg-[#C4622D] px-4 py-2 text-white">
            Save thresholds
          </button>
        </form>
      </section>
      <section className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5">
        <h2 className="text-lg font-semibold">High-value approvals</h2>
        <div className="mt-3 space-y-2">
          {escalated.length === 0 ? (
            <p className="text-sm text-[var(--text-secondary)]">
              No requests awaiting Owner decision.
            </p>
          ) : (
            escalated.map((request) => (
              <div
                key={`${request.type}-${request.id}`}
                className="flex items-center justify-between rounded-xl border border-[var(--card-border)] p-3"
              >
                <span>
                  {request.type} · {money(request.amount)} ·{" "}
                  {request.description}
                </span>
                <div className="flex gap-2">
                  <button
                    onClick={() =>
                      decide.mutate({
                        type: request.type,
                        id: request.id,
                        approve: false,
                      })
                    }
                    className="rounded-lg border border-red-500/30 px-3 py-1 text-xs text-red-300"
                  >
                    Reject
                  </button>
                  <button
                    onClick={() =>
                      decide.mutate({
                        type: request.type,
                        id: request.id,
                        approve: true,
                      })
                    }
                    className="rounded-lg bg-[#2F5C52] px-3 py-1 text-xs text-white"
                  >
                    Approve
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      </section>
    </main>
  );
}
