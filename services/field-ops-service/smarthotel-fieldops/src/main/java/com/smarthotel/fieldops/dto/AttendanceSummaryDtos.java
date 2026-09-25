package com.smarthotel.fieldops.dto;

import com.smarthotel.fieldops.domain.model.MonthlyAttendanceSummary.VerificationStatus;
import jakarta.validation.constraints.*;
import java.time.Instant;
import java.util.*;

public class AttendanceSummaryDtos {
    public record BuildSummaryRequest(@NotNull UUID employeeId, @Min(2000) int year, @Min(1) @Max(12) int month, @Min(0) int scheduledDays, @Min(0) int weekendDays, @Min(0) int holidayDays) {}
    public record ResolveDiscrepancyRequest(@NotNull UUID attendanceRecordId, @NotBlank String resolution, @NotNull Instant correctedClockIn, @NotNull Instant correctedClockOut) {}
    public record SummaryResponse(UUID id, UUID employeeId, UUID departmentId, String employeeRole, int payrollYear, int payrollMonth, int scheduledDays, int workedDays, int weekendDays, int holidayDays, int approvedPaidLeaveDays, int approvedUnpaidLeaveDays, double approvedOvertimeHours, int missingPunches, int attendanceDisputes, List<UUID> attendanceRecordIds, List<UUID> overtimeRecordIds, List<UUID> leaveRecordIds, VerificationStatus verificationStatus, UUID verifiedByManagerId, Instant verifiedAt, int summaryVersion) {}
}
