import api from "@/lib/axios";

export interface SalaryStructure { id: string; departmentId: string; employeeRole: string; monthlyBasicSalary: number; overtimeHourlyRate: number; currency: string; effectiveFrom: string; effectiveTo?: string; revision: number }
export interface SalaryAllowance { id: string; departmentId?: string; employeeRole?: string; employeeId?: string; kind: string | number; calculationType: string | number; value: number; currency: string; effectiveFrom: string; effectiveTo?: string }
export interface PayrollRecord { id: string; employeeId: string; departmentId: string; employeeRole: string; periodStart: string; periodEnd: string; baseSalary: number; allowances: number; overtimeHours: number; overtimePay: number; grossSalary: number; deductions: number; netSalary: number; currency: string; status: string | number; calculationSnapshotJson: string; attendanceSnapshotJson: string }
export interface MockPayrollPayment { id: string; payrollId: string; instructionReference: string; scenario: string | number; status: string | number; amount: number; currency: string; attemptCount: number; mockProviderReference?: string; failureReason?: string; confirmedAtUtc?: string }
export interface MySalaryResponse { current: { baseSalary: number; currency: string; departmentId: string; employeeRole: string } | null; allowances: SalaryAllowance[]; payrolls: PayrollRecord[]; payments: MockPayrollPayment[] }

const root = "/api/v1/salary-payroll";
export const salaryPayrollApi = {
  structures: async () => (await api.get<SalaryStructure[]>(`${root}/structures`)).data,
  saveStructure: async (payload: Omit<SalaryStructure, "id" | "effectiveTo" | "revision">) => (await api.post<SalaryStructure>(`${root}/structures`, payload)).data,
  allowances: async () => (await api.get<SalaryAllowance[]>(`${root}/allowances`)).data,
  saveAllowance: async (payload: Omit<SalaryAllowance, "id" | "effectiveTo">) => (await api.post<SalaryAllowance>(`${root}/allowances`, payload)).data,
  saveOverride: async (payload: { employeeId: string; monthlyBasicSalary: number; overtimeHourlyRate?: number; currency: string; effectiveFrom: string; reason: string }) => (await api.post(`${root}/overrides`, payload)).data,
  payments: async () => (await api.get<MockPayrollPayment[]>(`${root}/mock-payments`)).data,
  processPayment: async (payrollId: string, scenario: "Success" | "Failure" | "Timeout" | "DelayedSuccess") => (await api.post<MockPayrollPayment>(`${root}/payroll/${payrollId}/mock-payment`, { idempotencyKey: crypto.randomUUID(), scenario })).data,
  confirmDelayed: async (id: string) => (await api.post<MockPayrollPayment>(`${root}/mock-payments/${id}/confirm-delayed`)).data,
  calculate: async (summary: import("@/services/attendanceSummaryApi").AttendanceSummary) => {
    const periodStart = `${summary.payrollYear}-${String(summary.payrollMonth).padStart(2, "0")}-01`;
    const periodEnd = new Date(Date.UTC(summary.payrollYear, summary.payrollMonth, 0)).toISOString().slice(0, 10);
    const role = ["Admin", "Manager", "Employee"].includes(summary.employeeRole) ? summary.employeeRole : "Employee";
    return (await api.post<PayrollRecord>(`${root}/calculate`, {
      sourcePayrollId: summary.id, employeeId: summary.employeeId, departmentId: summary.departmentId,
      employeeRole: role, periodStart, periodEnd, approvedOvertimeHours: summary.approvedOvertimeHours,
      authorizedDeductions: 0, attendanceVerified: true, deductionItems: [],
      attendance: { summaryId: summary.id, summaryVersion: summary.summaryVersion, scheduledDays: summary.scheduledDays, workedDays: summary.workedDays, weekendDays: summary.weekendDays,
        holidayDays: summary.holidayDays, approvedPaidLeaveDays: summary.approvedPaidLeaveDays,
        approvedUnpaidLeaveDays: summary.approvedUnpaidLeaveDays, approvedOvertimeHours: summary.approvedOvertimeHours,
        missingPunches: summary.missingPunches, disputedRecords: summary.attendanceDisputes,
        verifiedByManagerId: summary.verifiedByManagerId, verifiedAtUtc: summary.verifiedAt,
        attendanceRecordIds: summary.attendanceRecordIds, approvedLeaveRecordIds: summary.leaveRecordIds }
    })).data;
  },
  mine: async () => (await api.get<MySalaryResponse>(`${root}/me`)).data,
  payslip: async (payrollId: string) => (await api.get(`/api/v1/finance/operations/payroll/${payrollId}/payslip`, { responseType: "blob" })).data as Blob,
};
