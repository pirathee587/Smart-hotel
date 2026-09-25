package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.*;
import com.smarthotel.fieldops.domain.model.MonthlyAttendanceSummary.VerificationStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.AttendanceStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OvertimeStatus;
import com.smarthotel.fieldops.domain.repository.*;
import com.smarthotel.fieldops.dto.AttendanceSummaryDtos.*;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import java.time.*;
import java.util.*;

@Service @RequiredArgsConstructor
public class AttendanceSummaryService {
    private final MonthlyAttendanceSummaryRepository summaries;
    private final AttendanceRecordRepository attendance;
    private final OvertimeApprovalRepository overtime;
    private final EmployeeProfileRepository employees;
    private final LeaveRequestRepository leaveRequests;

    @Transactional
    public MonthlyAttendanceSummary build(UUID managerDepartmentId, BuildSummaryRequest request) {
        EmployeeProfile employee = employees.findById(request.employeeId()).orElseThrow(() -> new IllegalArgumentException("Employee not found."));
        if (!managerDepartmentId.equals(employee.getDepartmentId())) throw new SecurityException("Cross-department attendance access is forbidden.");
        LocalDate start = LocalDate.of(request.year(), request.month(), 1), end = start.withDayOfMonth(start.lengthOfMonth());
        List<AttendanceRecord> records = attendance.findByEmployeeIdAndDateBetweenOrderByDateAsc(employee.getEmployeeId(), start, end);
        int missing = (int) records.stream().filter(r -> r.getClockOut() == null || r.getStatus() == AttendanceStatus.MissingClockOut).count();
        int worked = (int) records.stream().filter(r -> r.getClockOut() != null && r.getStatus() != AttendanceStatus.MissingClockOut).map(AttendanceRecord::getDate).distinct().count();
        List<OvertimeApproval> approved = records.stream().map(r -> overtime.findByAttendanceRecordId(r.getId()).orElse(null)).filter(Objects::nonNull).filter(a -> a.getStatus() == OvertimeStatus.Approved).toList();
        Set<LocalDate> workedDates = records.stream().filter(r -> r.getClockOut() != null && r.getStatus() != AttendanceStatus.MissingClockOut).map(AttendanceRecord::getDate).collect(java.util.stream.Collectors.toSet());
        LeaveSnapshot leave = authoritativeLeave(employee.getEmployeeId(), start, end, workedDates);
        Optional<MonthlyAttendanceSummary> previous = summaries.findTopByEmployeeIdAndPayrollYearAndPayrollMonthOrderBySummaryVersionDesc(employee.getEmployeeId(), request.year(), request.month());
        previous.filter(p -> p.getVerificationStatus() == VerificationStatus.Verified).ifPresent(p -> { p.setVerificationStatus(VerificationStatus.Outdated); p.setUpdatedAt(Instant.now()); });
        MonthlyAttendanceSummary summary = MonthlyAttendanceSummary.builder().employeeId(employee.getEmployeeId()).departmentId(managerDepartmentId).employeeRole(employee.getRole().name()).payrollYear(request.year()).payrollMonth(request.month()).scheduledDays(request.scheduledDays()).workedDays(worked).weekendDays(request.weekendDays()).holidayDays(request.holidayDays()).approvedPaidLeaveDays(leave.paidDays()).approvedUnpaidLeaveDays(leave.unpaidDays()).approvedOvertimeHours(approved.stream().mapToDouble(OvertimeApproval::getOvertimeHours).sum()).missingPunches(missing).attendanceDisputes(leave.workedDayOverlaps()).attendanceRecordIds(join(records.stream().map(AttendanceRecord::getId).toList())).overtimeRecordIds(join(approved.stream().map(OvertimeApproval::getId).toList())).leaveRecordIds(join(leave.recordIds())).verificationStatus(missing > 0 || leave.workedDayOverlaps() > 0 ? VerificationStatus.Discrepancies : VerificationStatus.Draft).summaryVersion(previous.map(p -> p.getSummaryVersion() + 1).orElse(1)).build();
        return summaries.save(summary);
    }

    @Transactional
    public MonthlyAttendanceSummary verify(UUID id, UUID managerId, UUID departmentId) {
        MonthlyAttendanceSummary summary = summaries.findByIdAndDepartmentId(id, departmentId).orElseThrow(() -> new SecurityException("Summary is outside the Manager's department."));
        if (summary.getVerificationStatus() == VerificationStatus.Outdated) throw new IllegalStateException("Outdated summaries cannot be verified; build a corrected version.");
        LocalDate start = LocalDate.of(summary.getPayrollYear(), summary.getPayrollMonth(), 1), end = start.withDayOfMonth(start.lengthOfMonth());
        Set<LocalDate> workedDates = attendance.findByEmployeeIdAndDateBetweenOrderByDateAsc(summary.getEmployeeId(), start, end).stream().filter(r -> r.getClockOut() != null && r.getStatus() != AttendanceStatus.MissingClockOut).map(AttendanceRecord::getDate).collect(java.util.stream.Collectors.toSet());
        LeaveSnapshot leave = authoritativeLeave(summary.getEmployeeId(), start, end, workedDates);
        if (summary.getApprovedPaidLeaveDays() != leave.paidDays() || summary.getApprovedUnpaidLeaveDays() != leave.unpaidDays() || summary.getAttendanceDisputes() != leave.workedDayOverlaps() || !new HashSet<>(split(summary.getLeaveRecordIds())).equals(new HashSet<>(leave.recordIds()))) {
            throw new IllegalStateException("Approved leave changed after this summary was built; rebuild and resolve discrepancies before verification.");
        }
        if (summary.getMissingPunches() > 0 || summary.getAttendanceDisputes() > 0) throw new IllegalStateException("Resolve all missing punches and disputes first.");
        summaries.findTopByEmployeeIdAndPayrollYearAndPayrollMonthOrderBySummaryVersionDesc(summary.getEmployeeId(), summary.getPayrollYear(), summary.getPayrollMonth()).filter(latest -> !latest.getId().equals(id)).ifPresent(latest -> { throw new IllegalStateException("A newer summary version exists."); });
        summary.setVerificationStatus(VerificationStatus.Verified); summary.setVerifiedByManagerId(managerId); summary.setVerifiedAt(Instant.now()); summary.setUpdatedAt(Instant.now()); return summaries.save(summary);
    }

    @Transactional
    public MonthlyAttendanceSummary resolve(UUID summaryId, UUID departmentId, ResolveDiscrepancyRequest request) {
        MonthlyAttendanceSummary summary = summaries.findByIdAndDepartmentId(summaryId, departmentId).orElseThrow(() -> new SecurityException("Summary is outside the Manager's department."));
        AttendanceRecord record = attendance.findById(request.attendanceRecordId()).orElseThrow(() -> new IllegalArgumentException("Attendance record not found."));
        EmployeeProfile employee = employees.findById(record.getEmployeeId()).orElseThrow(() -> new IllegalArgumentException("Employee not found."));
        if (!departmentId.equals(employee.getDepartmentId()) || !record.getEmployeeId().equals(summary.getEmployeeId())) throw new SecurityException("Cross-department or cross-employee correction is forbidden.");
        if (record.getDate().getYear() != summary.getPayrollYear() || record.getDate().getMonthValue() != summary.getPayrollMonth()) throw new IllegalArgumentException("Attendance record is outside the payroll period.");
        if (!request.correctedClockOut().isAfter(request.correctedClockIn())) throw new IllegalArgumentException("Corrected clock-out must be after clock-in.");
        record.setClockIn(request.correctedClockIn()); record.clockOut(request.correctedClockOut()); attendance.save(record);
        summary.setVerificationStatus(VerificationStatus.Outdated); summary.setUpdatedAt(Instant.now()); return summaries.save(summary);
    }

    @Transactional
    public void markOutdated(UUID employeeId, LocalDate date) {
        summaries.findTopByEmployeeIdAndPayrollYearAndPayrollMonthOrderBySummaryVersionDesc(employeeId, date.getYear(), date.getMonthValue()).filter(s -> s.getVerificationStatus() == VerificationStatus.Verified).ifPresent(s -> { s.setVerificationStatus(VerificationStatus.Outdated); s.setUpdatedAt(Instant.now()); });
    }

    @Transactional(readOnly=true) public List<MonthlyAttendanceSummary> department(UUID departmentId, int year, int month) { return summaries.findByDepartmentIdAndPayrollYearAndPayrollMonthOrderByEmployeeIdAscSummaryVersionDesc(departmentId, year, month); }
    @Transactional(readOnly=true) public List<MonthlyAttendanceSummary> verified() { return summaries.findByVerificationStatusOrderByVerifiedAtDesc(VerificationStatus.Verified); }
    @Transactional(readOnly=true) public MonthlyAttendanceSummary verifiedById(UUID id) { MonthlyAttendanceSummary s = summaries.findById(id).orElseThrow(() -> new IllegalArgumentException("Summary not found.")); if (s.getVerificationStatus() != VerificationStatus.Verified) throw new IllegalStateException("Summary is not verified or is outdated."); return s; }
    private static String join(List<UUID> ids) { return ids.stream().map(UUID::toString).reduce((a,b) -> a + "," + b).orElse(""); }
    public static List<UUID> split(String value) { return value == null || value.isBlank() ? List.of() : Arrays.stream(value.split(",")).map(UUID::fromString).toList(); }
    private LeaveSnapshot authoritativeLeave(UUID employeeId, LocalDate start, LocalDate end, Set<LocalDate> workedDates) {
        List<LeaveRequest> approved = leaveRequests.approvedForPeriod(employeeId, start, end);
        Set<LocalDate> paid = new HashSet<>(), unpaid = new HashSet<>();
        for (LeaveRequest leave : approved) {
            LocalDate cursor = leave.getStartDate().isBefore(start) ? start : leave.getStartDate();
            LocalDate last = leave.getEndDate().isAfter(end) ? end : leave.getEndDate();
            while (!cursor.isAfter(last)) { (leave.isPaid() ? paid : unpaid).add(cursor); cursor = cursor.plusDays(1); }
        }
        Set<LocalDate> allLeaveDates = new HashSet<>(paid); allLeaveDates.addAll(unpaid);
        int overlaps = (int) allLeaveDates.stream().filter(workedDates::contains).count();
        return new LeaveSnapshot(paid.size(), unpaid.size(), overlaps, approved.stream().map(LeaveRequest::getId).distinct().sorted().toList());
    }
    private record LeaveSnapshot(int paidDays, int unpaidDays, int workedDayOverlaps, List<UUID> recordIds) {}
}
