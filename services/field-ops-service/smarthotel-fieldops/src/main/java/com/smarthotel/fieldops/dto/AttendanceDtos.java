package com.smarthotel.fieldops.dto;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.AttendanceStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OvertimeStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.PunchMethod;
import jakarta.validation.constraints.NotNull;
import lombok.Builder;

import java.time.Instant;
import java.time.LocalDate;
import java.util.UUID;

public class AttendanceDtos {

    public record BiometricPunchRequest(
            @NotNull UUID employeeId,
            String deviceId,
            PunchMethod method,
            Instant timestamp
    ) {}

    @Builder
    public record PunchResponse(
            UUID recordId,
            UUID employeeId,
            LocalDate date,
            Instant clockIn,
            Instant clockOut,
            double hoursWorked,
            double overtimeHours,
            AttendanceStatus status,
            boolean duplicate,
            String message
    ) {}

    public record OvertimeDecisionRequest(
            String notes
    ) {}

    @Builder
    public record OvertimeApprovalResponse(
            UUID id,
            UUID attendanceRecordId,
            UUID employeeId,
            double overtimeHours,
            OvertimeStatus status,
            UUID managerId,
            String managerNotes,
            Instant requestedAt,
            Instant actionedAt
    ) {}

    @Builder
    public record AttendanceRecordResponse(
            UUID id,
            UUID employeeId,
            LocalDate date,
            Instant clockIn,
            Instant clockOut,
            double hoursWorked,
            double overtimeHours,
            PunchMethod punchMethod,
            String deviceId,
            AttendanceStatus status
    ) {}
}
