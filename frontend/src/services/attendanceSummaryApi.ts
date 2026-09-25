import api from "@/lib/axios";

export interface AttendanceSummary {
  id: string; employeeId: string; departmentId: string; employeeRole: string;
  payrollYear: number; payrollMonth: number; scheduledDays: number; workedDays: number;
  weekendDays: number; holidayDays: number; approvedPaidLeaveDays: number;
  approvedUnpaidLeaveDays: number; approvedOvertimeHours: number; missingPunches: number;
  attendanceDisputes: number; attendanceRecordIds: string[]; overtimeRecordIds: string[];
  leaveRecordIds: string[]; verificationStatus: "Draft" | "Discrepancies" | "Verified" | "Outdated";
  verifiedByManagerId?: string; verifiedAt?: string; summaryVersion: number;
}

const root = "/api/v1/attendance-summaries";
export const attendanceSummaryApi = {
  department: async (year: number, month: number) => (await api.get<AttendanceSummary[]>(root, { params: { year, month } })).data,
  verified: async () => (await api.get<AttendanceSummary[]>(`${root}/verified`)).data,
  build: async (payload: { employeeId: string; year: number; month: number; scheduledDays: number; weekendDays: number; holidayDays: number }) => (await api.post<AttendanceSummary>(root, payload)).data,
  verify: async (id: string) => (await api.post<AttendanceSummary>(`${root}/${id}/verify`)).data,
  resolve: async (id: string, payload: { attendanceRecordId: string; resolution: string; correctedClockIn: string; correctedClockOut: string }) => (await api.post<AttendanceSummary>(`${root}/${id}/resolve`, payload)).data,
};
