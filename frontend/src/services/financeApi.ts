import api from "@/lib/axios";

export interface FinanceOverview {
  currency: string;
  collectedRevenue: number;
  recordedExpenses: number;
  outstandingPayments: number;
  refundedAmount: number;
  pendingExpenses: number;
  pendingRefunds: number;
  heldFunds: number;
  releasedFunds: number;
  settledFunds: number;
  metricDefinition: string;
}

export interface FinanceRequestRecord {
  id: string;
  amount: number;
  currency: string;
  description: string;
  status: string | number;
  approvalStage: string | number;
  submittedByUserId: string;
  createdAtUtc: string;
  category?: string;
}

export interface FinanceSettings {
  managerExpenseApprovalLimit: number;
  managerRefundApprovalLimit: number;
  managerReleaseApprovalLimit: number;
  currency: string;
}

export interface PaymentTransaction {
  id: string;
  bookingId: string;
  bookingReference: string;
  amount: number;
  currency: string;
  provider: string | number;
  status: string | number;
  createdAtUtc: string;
}

export interface FinanceInvoice {
  id: string;
  invoiceNumber: string;
  partyName: string;
  totalAmount: number;
  paidAmount: number;
  currency: string;
  status: string | number;
  dueDate: string;
}

export interface FinanceReportSummary {
  verifiedCollectedRevenue: number;
  refunds: number;
  heldFunds: number;
  settledFunds: number;
  approvedExpenses: number;
  approvedPayroll: number;
  definition: string;
}

export interface FinancePayrollRecord {
  id: string;
  sourcePayrollId: string;
  employeeId: string;
  netSalary: number;
  currency: string;
  status: string | number;
  periodEnd: string;
  periodStart: string;
  baseSalary: number;
  allowances: number;
  overtimePay: number;
  grossSalary: number;
  deductions: number;
  calculationSnapshotJson: string;
  attendanceSnapshotJson: string;
}

export const financeApi = {
  overview: async () =>
    (await api.get<FinanceOverview>("/api/v1/finance/overview")).data,
  expenses: async () =>
    (await api.get<FinanceRequestRecord[]>("/api/v1/finance/expenses")).data,
  refunds: async () =>
    (await api.get<FinanceRequestRecord[]>("/api/v1/finance/refunds")).data,
  submitExpense: async (payload: {
    amount: number;
    currency: string;
    description: string;
    category: string;
    vendorReference?: string;
    idempotencyKey: string;
    departmentId: string;
  }) => (await api.post("/api/v1/finance/expenses", payload)).data,
  decideExpense: async (id: string, approve: boolean, reason?: string) =>
    (
      await api.post(`/api/v1/finance/expenses/${id}/decision`, {
        approve,
        reason,
      })
    ).data,
  settings: async () =>
    (await api.get<FinanceSettings | null>("/api/v1/finance/settings")).data,
  saveSettings: async (payload: FinanceSettings) =>
    (await api.put<FinanceSettings>("/api/v1/finance/settings", payload)).data,
  decideRefund: async (id: string, approve: boolean, reason?: string) =>
    (
      await api.post(`/api/v1/finance/refunds/${id}/decision`, {
        approve,
        reason,
      })
    ).data,
  transactions: async () =>
    (
      await api.get<PaymentTransaction[]>(
        "/api/v1/finance/operations/transactions",
      )
    ).data,
  invoices: async () =>
    (await api.get<FinanceInvoice[]>("/api/v1/finance/operations/invoices"))
      .data,
  reportSummary: async () =>
    (
      await api.get<FinanceReportSummary>(
        "/api/v1/finance/operations/reports/summary",
      )
    ).data,
  releases: async () =>
    (await api.get<FinanceRequestRecord[]>("/api/v1/escrow/releases")).data,
  payroll: async () =>
    (
      await api.get<FinancePayrollRecord[]>(
        "/api/v1/finance/operations/payroll",
      )
    ).data,
  decideRelease: async (id: string, approve: boolean) =>
    (await api.post(`/api/v1/escrow/releases/${id}/decision`, { approve }))
      .data,
  decidePayroll: async (id: string, approve: boolean) =>
    (
      await api.post(`/api/v1/finance/operations/payroll/${id}/decision`, {
        approve,
      })
    ).data,
  exportTransactions: async (format: "csv" | "xls") =>
    (
      await api.get(`/api/v1/finance/operations/reports/export.${format}`, {
        responseType: "blob",
      })
    ).data as Blob,
};
