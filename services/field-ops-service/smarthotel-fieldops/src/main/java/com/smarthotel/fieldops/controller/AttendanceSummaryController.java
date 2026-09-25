package com.smarthotel.fieldops.controller;

import com.smarthotel.fieldops.domain.model.MonthlyAttendanceSummary;
import com.smarthotel.fieldops.dto.AttendanceSummaryDtos.*;
import com.smarthotel.fieldops.service.AttendanceSummaryService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.*;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.*;
import java.util.*;

@RestController @RequestMapping("/api/v1/attendance-summaries") @RequiredArgsConstructor
public class AttendanceSummaryController {
    private final AttendanceSummaryService service;
    @GetMapping @PreAuthorize("hasAnyRole('Owner', 'Manager')")
    public List<SummaryResponse> department(@RequestParam int year, @RequestParam int month, @RequestParam(required = false) UUID departmentId, @AuthenticationPrincipal Jwt jwt) {
        if (isOwner(jwt)) {
            if (departmentId != null) return service.department(departmentId, year, month).stream().map(this::map).toList();
            return service.verified().stream().filter(s -> s.getPayrollYear() == year && s.getPayrollMonth() == month).map(this::map).toList();
        }
        return service.department(department(jwt), year, month).stream().map(this::map).toList();
    }
    @PostMapping @PreAuthorize("hasRole('Manager')")
    public ResponseEntity<SummaryResponse> build(@Valid @RequestBody BuildSummaryRequest request, @AuthenticationPrincipal Jwt jwt) { return ResponseEntity.status(HttpStatus.CREATED).body(map(service.build(department(jwt), request))); }
    @PostMapping("/{id}/verify") @PreAuthorize("hasRole('Manager')")
    public SummaryResponse verify(@PathVariable UUID id, @AuthenticationPrincipal Jwt jwt) { return map(service.verify(id, user(jwt), department(jwt))); }
    @PostMapping("/{id}/resolve") @PreAuthorize("hasRole('Manager')")
    public SummaryResponse resolve(@PathVariable UUID id, @Valid @RequestBody ResolveDiscrepancyRequest request, @AuthenticationPrincipal Jwt jwt) { return map(service.resolve(id, department(jwt), request)); }
    @GetMapping("/verified") @PreAuthorize("hasRole('Owner') or (hasAnyRole('Employee','Admin','Manager') and #jwt.claims['departmentCode'] == 'FINANCE')")
    public List<SummaryResponse> verified(@AuthenticationPrincipal Jwt jwt) { return service.verified().stream().map(this::map).toList(); }
    @GetMapping("/{id}/verified") @PreAuthorize("hasRole('Owner') or (hasAnyRole('Employee','Admin','Manager') and #jwt.claims['departmentCode'] == 'FINANCE')")
    public SummaryResponse verifiedById(@PathVariable UUID id, @AuthenticationPrincipal Jwt jwt) { return map(service.verifiedById(id)); }
    private UUID user(Jwt jwt) { return UUID.fromString(jwt.getSubject()); }
    private UUID department(Jwt jwt) { Object value = jwt.getClaims().get("departmentId"); if (value == null) throw new SecurityException("Department assignment is required."); return UUID.fromString(value.toString()); }
    private boolean isOwner(Jwt jwt) { Object r = jwt != null ? jwt.getClaims().get("role") : null; return r != null && "Owner".equals(r.toString()); }
    private SummaryResponse map(MonthlyAttendanceSummary s) { return new SummaryResponse(s.getId(), s.getEmployeeId(), s.getDepartmentId(), s.getEmployeeRole(), s.getPayrollYear(), s.getPayrollMonth(), s.getScheduledDays(), s.getWorkedDays(), s.getWeekendDays(), s.getHolidayDays(), s.getApprovedPaidLeaveDays(), s.getApprovedUnpaidLeaveDays(), s.getApprovedOvertimeHours(), s.getMissingPunches(), s.getAttendanceDisputes(), AttendanceSummaryService.split(s.getAttendanceRecordIds()), AttendanceSummaryService.split(s.getOvertimeRecordIds()), AttendanceSummaryService.split(s.getLeaveRecordIds()), s.getVerificationStatus(), s.getVerifiedByManagerId(), s.getVerifiedAt(), s.getSummaryVersion()); }
}
