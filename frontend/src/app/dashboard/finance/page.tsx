"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { financeApi,type FinanceRequestRecord } from "@/services/financeApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import {
Building2,
FileText,
Landmark,
Receipt,
RefreshCcw,
Scale,
ScrollText,
ShieldCheck,
Users,
WalletCards,
} from "lucide-react";
import Link from "next/link";
import type { ElementType } from "react";
import { useState } from "react";

const money = (value: number, currency = "LKR") =>
  new Intl.NumberFormat("en-LK", { style: "currency", currency }).format(value);

export default function FinanceDashboardPage() {
  const { user } = useAuthStore();
  const queryClient = useQueryClient();
  const role = String(user?.role || "");
  const isManager = role === "Manager";
  const canSubmit =
    role === "Manager" ||
    user?.designation === "Accountant" ||
    user?.designation === "Finance Assistant";
  const [form, setForm] = useState({
    amount: "",
    category: "",
    description: "",
  });
  const overview = useQuery({
    queryKey: ["finance-overview"],
    queryFn: financeApi.overview,
    retry: false,
  });
  const expenses = useQuery({
    queryKey: ["finance-expenses"],
    queryFn: financeApi.expenses,
    retry: false,
  });
  const refunds = useQuery({
    queryKey: ["finance-refunds"],
    queryFn: financeApi.refunds,
    retry: false,
  });
  const transactions = useQuery({
    queryKey: ["finance-transactions"],
    queryFn: financeApi.transactions,
    retry: false,
  });
  const invoices = useQuery({
    queryKey: ["finance-invoices"],
    queryFn: financeApi.invoices,
    retry: false,
  });
  const report = useQuery({
    queryKey: ["finance-report-summary"],
    queryFn: financeApi.reportSummary,
    retry: false,
  });
  const submit = useMutation({
    mutationFn: () =>
      financeApi.submitExpense({
        amount: Number(form.amount),
        currency: "LKR",
        category: form.category,
        description: form.description,
        idempotencyKey: crypto.randomUUID(),
        departmentId: user!.departmentId!,
      }),
    onSuccess: async () => {
      setForm({ amount: "", category: "", description: "" });
      await queryClient.invalidateQueries({ queryKey: ["finance-expenses"] });
    },
  });
  const decide = useMutation({
    mutationFn: ({
      request,
      approve,
    }: {
      request: FinanceRequestRecord;
      approve: boolean;
    }) => financeApi.decideExpense(request.id, approve),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["finance-expenses"] });
      await queryClient.invalidateQueries({ queryKey: ["finance-overview"] });
    },
  });
  const downloadReport = async (format: "csv" | "xls") => {
    const blob = await financeApi.exportTransactions(format);
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `finance-transactions.${format}`;
    link.click();
    URL.revokeObjectURL(url);
  };
  const data = overview.data;
  const cards: Array<{ label: string; value?: number; Icon: ElementType }> = [
    { label: "Funds Held", value: data?.heldFunds, Icon: ShieldCheck },
    { label: "Released", value: data?.releasedFunds, Icon: WalletCards },
    { label: "Bank Settled", value: data?.settledFunds, Icon: Building2 },
    {
      label: "Collected Revenue",
      value: data?.collectedRevenue,
      Icon: Landmark,
    },
    {
      label: "Recorded Expenses",
      value: data?.recordedExpenses,
      Icon: Receipt,
    },
    {
      label: "Outstanding",
      value: data?.outstandingPayments,
      Icon: WalletCards,
    },
    { label: "Refunded", value: data?.refundedAmount, Icon: RefreshCcw },
  ];

  return (
    <main className="mx-auto max-w-7xl space-y-6 p-6 lg:p-8">
      <header>
        <p className="text-xs uppercase tracking-wider text-[var(--text-secondary)]">
          Finance Department · {user?.designation || role}
        </p>
        <h1 className="text-3xl font-bold text-[var(--text-primary)]">
          Finance Overview
        </h1>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          Authorized revenue, expense and refund records from the live database.
        </p>
      </header>
      {overview.isError && (
        <div className="rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-200">
          Financial reporting access has not been granted to this account.
        </div>
      )}
      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {cards.map(({ label, value, Icon }) => (
          <div
            key={label}
            className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5"
          >
            <Icon className="mb-3 h-5 w-5 text-[#E87332]" />
            <p className="text-xs uppercase text-[var(--text-secondary)]">
              {label}
            </p>
            <p className="mt-1 text-2xl font-bold">
              {money(Number(value || 0), data?.currency)}
            </p>
          </div>
        ))}
      </section>
      {data && (
        <p className="text-xs text-[var(--text-secondary)]">
          {data.metricDefinition}
        </p>
      )}
      <section className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        {(role === "Admin"
          ? [
              [
                "Finance Staff",
                "Department staff management",
                Users,
                "/dashboard/employees",
              ],
              [
                "Permissions",
                "Explicit financial access",
                ShieldCheck,
                "/dashboard/settings",
              ],
              [
                "Audit Log",
                "Authorized activity history",
                ScrollText,
                "#audit",
              ],
            ]
          : [
              [
                "Payments",
                `${transactions.data?.length || 0} tracked`,
                WalletCards,
                "#transactions",
              ],
              [
                "Invoices",
                `${invoices.data?.length || 0} records`,
                FileText,
                "#invoices",
              ],
              [
                "Reconciliation",
                "Settlement and bank matching",
                Scale,
                "#reconciliation",
              ],
              ["Payroll", "Restricted salary workflow", Users, "#payroll"],
            ]
        ).map(([title, description, Icon, href]) => (
          <Link
            key={String(title)}
            href={String(href)}
            className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4 hover:border-[#C4622D]/60"
          >
            <Icon className="mb-2 h-5 w-5 text-[#E87332]" />
            <p className="font-semibold">{String(title)}</p>
            <p className="text-xs text-[var(--text-secondary)]">
              {String(description)}
            </p>
          </Link>
        ))}
      </section>
      {canSubmit && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            submit.mutate();
          }}
          className="grid gap-3 rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5 md:grid-cols-4"
        >
          <input
            required
            type="number"
            min="0.01"
            step="0.01"
            placeholder="Amount"
            value={form.amount}
            onChange={(e) => setForm({ ...form, amount: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent px-3 py-2"
          />
          <input
            required
            placeholder="Category"
            value={form.category}
            onChange={(e) => setForm({ ...form, category: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent px-3 py-2"
          />
          <input
            required
            placeholder="Description"
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
            className="rounded-lg border border-[var(--card-border)] bg-transparent px-3 py-2"
          />
          <button
            disabled={submit.isPending}
            className="rounded-lg bg-[#C4622D] px-4 py-2 text-white"
          >
            Submit Expense
          </button>
        </form>
      )}
      <section className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5">
        <h2 className="mb-4 text-lg font-semibold">Expense Requests</h2>
        <div className="space-y-2">
          {(expenses.data || []).map((request) => (
            <div
              key={request.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--card-border)] p-3"
            >
              <div>
                <p className="font-medium">
                  {request.category || "Expense"} ·{" "}
                  {money(request.amount, request.currency)}
                </p>
                <p className="text-xs text-[var(--text-secondary)]">
                  {request.description} · {String(request.status)}
                </p>
              </div>
              {isManager &&
                (request.status === 0 ||
                  String(request.status).toLowerCase().includes("pending")) && (
                  <div className="flex gap-2">
                    <button
                      onClick={() => decide.mutate({ request, approve: false })}
                      className="rounded-lg border border-red-500/30 px-3 py-1 text-xs text-red-300"
                    >
                      Reject
                    </button>
                    <button
                      onClick={() => decide.mutate({ request, approve: true })}
                      className="rounded-lg bg-[#2F5C52] px-3 py-1 text-xs text-white"
                    >
                      Approve
                    </button>
                  </div>
                )}
            </div>
          ))}
        </div>
      </section>
      <section
        id="transactions"
        className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5"
      >
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-lg font-semibold">Payment Transactions</h2>
          <div className="flex gap-2">
            <button
              onClick={() => downloadReport("csv")}
              className="rounded-lg border border-[var(--card-border)] px-3 py-1 text-xs"
            >
              Export CSV
            </button>
            <button
              onClick={() => downloadReport("xls")}
              className="rounded-lg border border-[var(--card-border)] px-3 py-1 text-xs"
            >
              Export Excel
            </button>
          </div>
        </div>
        <div className="mt-3 space-y-2">
          {(transactions.data || []).slice(0, 8).map((payment) => (
            <div
              key={payment.id}
              className="flex flex-wrap justify-between gap-2 rounded-xl border border-[var(--card-border)] p-3 text-sm"
            >
              <span>{payment.bookingReference || payment.id}</span>
              <span>
                {money(payment.amount, payment.currency)} ·{" "}
                {String(payment.status)}
              </span>
            </div>
          ))}
          {transactions.isError && (
            <p className="text-sm text-[var(--text-secondary)]">
              Payment tracking requires reporting permission.
            </p>
          )}
        </div>
      </section>
      <section
        id="invoices"
        className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5"
      >
        <h2 className="text-lg font-semibold">
          Invoices & Outstanding Charges
        </h2>
        <div className="mt-3 space-y-2">
          {(invoices.data || []).slice(0, 8).map((invoice) => (
            <div
              key={invoice.id}
              className="flex flex-wrap justify-between gap-2 rounded-xl border border-[var(--card-border)] p-3 text-sm"
            >
              <span>
                {invoice.invoiceNumber} · {invoice.partyName}
              </span>
              <span>
                {money(
                  invoice.totalAmount - invoice.paidAmount,
                  invoice.currency,
                )}{" "}
                outstanding · {String(invoice.status)}
              </span>
            </div>
          ))}
          {invoices.isError && (
            <p className="text-sm text-[var(--text-secondary)]">
              Invoice preparation requires Accountant, Finance Assistant, or
              explicit access.
            </p>
          )}
        </div>
      </section>
      {report.data && (
        <section className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5">
          <h2 className="text-lg font-semibold">30-day Financial Summary</h2>
          <div className="mt-3 grid gap-3 sm:grid-cols-3">
            <p>
              Verified revenue
              <br />
              <strong>{money(report.data.verifiedCollectedRevenue)}</strong>
            </p>
            <p>
              Approved expenses
              <br />
              <strong>{money(report.data.approvedExpenses)}</strong>
            </p>
            <p>
              Approved payroll
              <br />
              <strong>{money(report.data.approvedPayroll)}</strong>
            </p>
          </div>
          <p className="mt-3 text-xs text-[var(--text-secondary)]">
            {report.data.definition}
          </p>
        </section>
      )}
      <section className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5">
        <h2 className="text-lg font-semibold">Refund Requests</h2>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          {refunds.data?.length || 0} authorized records. Approval does not
          execute an external refund.
        </p>
      </section>
    </main>
  );
}
