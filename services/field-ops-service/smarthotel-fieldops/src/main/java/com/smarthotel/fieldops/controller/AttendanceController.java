package com.smarthotel.fieldops.controller;

import com.smarthotel.fieldops.domain.model.AttendanceRecord;
import com.smarthotel.fieldops.domain.model.OvertimeApproval;
import com.smarthotel.fieldops.domain.repository.AttendanceRecordRepository;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import com.smarthotel.fieldops.dto.AttendanceDtos.*;
import com.smarthotel.fieldops.service.AttendanceService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.format.annotation.DateTimeFormat;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.*;

import java.time.LocalDate;
import java.util.List;
import java.util.Map;
import java.util.UUID;

@RestController
@RequestMapping("/api/v1/attendance")
@RequiredArgsConstructor
@Slf4j
public class AttendanceController {

    private final AttendanceService attendanceService;
    private final AttendanceRecordRepository attendanceRecordRepository;
    private final EmployeeProfileRepository employeeProfileRepository;

    /**
     * Biometric punch ingestion endpoint.
     * Authenticated via X-Device-Api-Key header (DeviceApiKeyFilter).
     * Bypasses user JWT requirement.
     */
    @PostMapping("/punch")
    public ResponseEntity<PunchResponse> recordPunch(@Valid @RequestBody BiometricPunchRequest req) {
        log.info("Received biometric punch request for employee {} via device {}", req.employeeId(), req.deviceId());

        AttendanceService.PunchResult result = attendanceService.processPunch(
                req.employeeId(),
                req.deviceId(),
                req.method(),
                req.timestamp()
        );

        AttendanceRecord rec = result.record();
        PunchResponse response = PunchResponse.builder()
                .recordId(rec != null ? rec.getId() : null)
                .employeeId(req.employeeId())
                .date(rec != null ? rec.getDate() : null)
                .clockIn(rec != null ? rec.getClockIn() : null)
                .clockOut(rec != null ? rec.getClockOut() : null)
                .hoursWorked(rec != null ? rec.getHoursWorked() : 0.0)
                .overtimeHours(rec != null ? rec.getOvertimeHours() : 0.0)
                .status(rec != null ? rec.getStatus() : null)
                .duplicate(result.isDuplicate())
                .message(result.message())
                .build();

        return ResponseEntity.status(result.isDuplicate() ? HttpStatus.OK : HttpStatus.CREATED).body(response);
    }

    @GetMapping("/my")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<List<AttendanceRecordResponse>> getMyAttendance(
            @RequestParam(required = false) @DateTimeFormat(iso = DateTimeFormat.ISO.DATE) LocalDate startDate,
            @RequestParam(required = false) @DateTimeFormat(iso = DateTimeFormat.ISO.DATE) LocalDate endDate,
            @AuthenticationPrincipal Jwt jwt) {

        UUID employeeId = getUserId(jwt);
        LocalDate start = startDate != null ? startDate : LocalDate.now().minusMonths(1);
        LocalDate end = endDate != null ? endDate : LocalDate.now();

        List<AttendanceRecord> records = attendanceService.getEmployeeAttendance(employeeId, start, end);
        return ResponseEntity.ok(records.stream().map(this::mapToRecordResponse).toList());
    }

    @GetMapping("/records")
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<List<AttendanceRecordResponse>> getAllRecords(
            @RequestParam(required = false) UUID employeeId,
            @RequestParam(required = false) @DateTimeFormat(iso = DateTimeFormat.ISO.DATE) LocalDate startDate,
            @RequestParam(required = false) @DateTimeFormat(iso = DateTimeFormat.ISO.DATE) LocalDate endDate,
            @AuthenticationPrincipal Jwt jwt) {

        LocalDate start = startDate != null ? startDate : LocalDate.now().minusMonths(1);
        LocalDate end = endDate != null ? endDate : LocalDate.now();

        List<AttendanceRecord> records;
        if (isOwner(jwt)) {
            if (employeeId != null) {
                records = attendanceService.getEmployeeAttendance(employeeId, start, end);
            } else {
                records = attendanceRecordRepository.findByDateBetweenOrderByDateAsc(start, end);
            }
        } else {
            UUID departmentId = getDepartmentId(jwt);
            var departmentEmployees = employeeProfileRepository.findByDepartmentId(departmentId).stream().map(e -> e.getEmployeeId()).collect(java.util.stream.Collectors.toSet());
            if (employeeId != null) {
                if (!departmentEmployees.contains(employeeId)) throw new SecurityException("Cross-department attendance access is forbidden.");
                records = attendanceService.getEmployeeAttendance(employeeId, start, end);
            } else {
                records = attendanceRecordRepository.findByDateBetweenOrderByDateAsc(start, end).stream().filter(r -> departmentEmployees.contains(r.getEmployeeId())).toList();
            }
        }

        return ResponseEntity.ok(records.stream().map(this::mapToRecordResponse).toList());
    }

    @GetMapping("/overtime/pending")
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<List<OvertimeApprovalResponse>> getPendingOvertimeApprovals(@AuthenticationPrincipal Jwt jwt) {
        List<OvertimeApproval> approvals = attendanceService.getPendingOvertimeApprovals();
        if (isOwner(jwt)) {
            return ResponseEntity.ok(approvals.stream().map(this::mapToApprovalResponse).toList());
        }
        UUID departmentId = getDepartmentId(jwt);
        var employeeIds = employeeProfileRepository.findByDepartmentId(departmentId).stream().map(e -> e.getEmployeeId()).collect(java.util.stream.Collectors.toSet());
        return ResponseEntity.ok(approvals.stream().filter(a -> employeeIds.contains(a.getEmployeeId())).map(this::mapToApprovalResponse).toList());
    }

    @PostMapping("/overtime/{id}/approve")
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<OvertimeApprovalResponse> approveOvertime(
            @PathVariable UUID id,
            @RequestBody(required = false) OvertimeDecisionRequest req,
            @AuthenticationPrincipal Jwt jwt) {

        UUID managerId = getUserId(jwt);
        assertSameDepartment(id, jwt);
        String notes = req != null ? req.notes() : null;
        OvertimeApproval approved = attendanceService.approveOvertime(id, managerId, notes);
        return ResponseEntity.ok(mapToApprovalResponse(approved));
    }

    @PostMapping("/overtime/{id}/reject")
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<OvertimeApprovalResponse> rejectOvertime(
            @PathVariable UUID id,
            @RequestBody(required = false) OvertimeDecisionRequest req,
            @AuthenticationPrincipal Jwt jwt) {

        UUID managerId = getUserId(jwt);
        assertSameDepartment(id, jwt);
        String notes = req != null ? req.notes() : null;
        OvertimeApproval rejected = attendanceService.rejectOvertime(id, managerId, notes);
        return ResponseEntity.ok(mapToApprovalResponse(rejected));
    }

    @PostMapping("/flag-missing")
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<Map<String, Object>> flagMissingClockOuts() {
        int count = attendanceService.flagMissingClockOuts();
        return ResponseEntity.ok(Map.of("flaggedCount", count, "message", "Evaluated and flagged missing clock outs older than 16 hours."));
    }

    private UUID getUserId(Jwt jwt) {
        if (jwt != null && jwt.getSubject() != null) {
            try {
                return UUID.fromString(jwt.getSubject());
            } catch (IllegalArgumentException ignored) {}
        }
        throw new IllegalStateException("Unable to resolve employee ID from token");
    }

    private UUID getDepartmentId(Jwt jwt) {
        Object claim = jwt != null ? jwt.getClaims().get("departmentId") : null;
        if (claim == null) throw new SecurityException("Department assignment is required.");
        return UUID.fromString(claim.toString());
    }

    private boolean isOwner(Jwt jwt) {
        Object role = jwt != null ? jwt.getClaims().get("role") : null;
        return role != null && "Owner".equals(role.toString());
    }

    private void assertSameDepartment(UUID approvalId, Jwt jwt) {
        if (isOwner(jwt)) return;
        OvertimeApproval approval = attendanceService.getPendingOvertimeApprovals().stream().filter(a -> a.getId().equals(approvalId)).findFirst().orElseThrow(() -> new IllegalArgumentException("Pending overtime approval not found."));
        var employee = employeeProfileRepository.findById(approval.getEmployeeId()).orElseThrow(() -> new IllegalArgumentException("Employee not found."));
        if (!getDepartmentId(jwt).equals(employee.getDepartmentId())) throw new SecurityException("Cross-department overtime approval is forbidden.");
    }

    private AttendanceRecordResponse mapToRecordResponse(AttendanceRecord rec) {
        return AttendanceRecordResponse.builder()
                .id(rec.getId())
                .employeeId(rec.getEmployeeId())
                .date(rec.getDate())
                .clockIn(rec.getClockIn())
                .clockOut(rec.getClockOut())
                .hoursWorked(rec.getHoursWorked())
                .overtimeHours(rec.getOvertimeHours())
                .punchMethod(rec.getPunchMethod())
                .deviceId(rec.getDeviceId())
                .status(rec.getStatus())
                .build();
    }

    private OvertimeApprovalResponse mapToApprovalResponse(OvertimeApproval app) {
        return OvertimeApprovalResponse.builder()
                .id(app.getId())
                .attendanceRecordId(app.getAttendanceRecordId())
                .employeeId(app.getEmployeeId())
                .overtimeHours(app.getOvertimeHours())
                .status(app.getStatus())
                .managerId(app.getManagerId())
                .managerNotes(app.getManagerNotes())
                .requestedAt(app.getRequestedAt())
                .actionedAt(app.getActionedAt())
                .build();
    }
}
