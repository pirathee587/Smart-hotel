package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.*;
import com.smarthotel.fieldops.domain.model.MonthlyAttendanceSummary.VerificationStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.*;
import com.smarthotel.fieldops.domain.repository.*;
import com.smarthotel.fieldops.dto.AttendanceSummaryDtos.BuildSummaryRequest;
import org.junit.jupiter.api.*;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.*;
import java.time.*;
import java.util.*;
import static org.assertj.core.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(org.mockito.junit.jupiter.MockitoExtension.class)
class AttendanceSummaryServiceTest {
    @Mock MonthlyAttendanceSummaryRepository summaries; @Mock AttendanceRecordRepository attendance; @Mock OvertimeApprovalRepository overtime; @Mock EmployeeProfileRepository employees; @Mock LeaveRequestRepository leaveRequests;
    AttendanceSummaryService service;
    @BeforeEach void setup() { service = new AttendanceSummaryService(summaries, attendance, overtime, employees, leaveRequests); lenient().when(summaries.save(any())).thenAnswer(i -> i.getArgument(0)); }

    @Test void managerCannotBuildForAnotherDepartment() {
        UUID employeeId = UUID.randomUUID(); when(employees.findById(employeeId)).thenReturn(Optional.of(profile(employeeId, UUID.randomUUID())));
        assertThatThrownBy(() -> service.build(UUID.randomUUID(), request(employeeId))).isInstanceOf(SecurityException.class);
    }

    @Test void buildsMonthlyTotalsFromAttendanceAndApprovedOvertimeOnly() {
        UUID department = UUID.randomUUID(), employeeId = UUID.randomUUID(), recordId = UUID.randomUUID();
        when(employees.findById(employeeId)).thenReturn(Optional.of(profile(employeeId, department)));
        AttendanceRecord record = AttendanceRecord.builder().id(recordId).employeeId(employeeId).date(LocalDate.of(2026, 8, 4)).clockIn(Instant.parse("2026-08-04T08:00:00Z")).clockOut(Instant.parse("2026-08-04T17:00:00Z")).hoursWorked(9).overtimeHours(1).status(AttendanceStatus.OvertimeApproved).build();
        when(attendance.findByEmployeeIdAndDateBetweenOrderByDateAsc(eq(employeeId), any(), any())).thenReturn(List.of(record));
        when(overtime.findByAttendanceRecordId(recordId)).thenReturn(Optional.of(OvertimeApproval.builder().id(UUID.randomUUID()).attendanceRecordId(recordId).employeeId(employeeId).overtimeHours(1).status(OvertimeStatus.Approved).build()));
        when(summaries.findTopByEmployeeIdAndPayrollYearAndPayrollMonthOrderBySummaryVersionDesc(employeeId, 2026, 8)).thenReturn(Optional.empty());
        when(leaveRequests.approvedForPeriod(eq(employeeId), any(), any())).thenReturn(List.of());
        MonthlyAttendanceSummary result = service.build(department, request(employeeId));
        assertThat(result.getWorkedDays()).isEqualTo(1); assertThat(result.getApprovedOvertimeHours()).isEqualTo(1); assertThat(result.getSummaryVersion()).isEqualTo(1); assertThat(result.getVerificationStatus()).isEqualTo(VerificationStatus.Draft);
    }

    @Test void verificationRejectsDiscrepanciesAndOutdatedVersions() {
        UUID id = UUID.randomUUID(), department = UUID.randomUUID();
        MonthlyAttendanceSummary summary = MonthlyAttendanceSummary.builder().id(id).employeeId(UUID.randomUUID()).departmentId(department).employeeRole("Employee").payrollYear(2026).payrollMonth(8).missingPunches(1).verificationStatus(VerificationStatus.Discrepancies).summaryVersion(1).build();
        when(summaries.findByIdAndDepartmentId(id, department)).thenReturn(Optional.of(summary));
        assertThatThrownBy(() -> service.verify(id, UUID.randomUUID(), department)).isInstanceOf(IllegalStateException.class);
    }

    @Test void approvedLeaveIsClippedToMonthAndAttendanceOverlapBecomesDispute() {
        UUID department=UUID.randomUUID(),employeeId=UUID.randomUUID(); when(employees.findById(employeeId)).thenReturn(Optional.of(profile(employeeId,department)));
        AttendanceRecord worked=AttendanceRecord.builder().id(UUID.randomUUID()).employeeId(employeeId).date(LocalDate.of(2026,8,1)).clockIn(Instant.parse("2026-08-01T08:00:00Z")).clockOut(Instant.parse("2026-08-01T16:00:00Z")).status(AttendanceStatus.Normal).build();
        when(attendance.findByEmployeeIdAndDateBetweenOrderByDateAsc(eq(employeeId),any(),any())).thenReturn(List.of(worked)); when(overtime.findByAttendanceRecordId(any())).thenReturn(Optional.empty());
        LeaveRequest paid=LeaveRequest.builder().id(UUID.randomUUID()).employeeId(employeeId).departmentId(department).paid(true).status(LeaveRequest.Status.Approved).startDate(LocalDate.of(2026,7,31)).endDate(LocalDate.of(2026,8,2)).build();
        LeaveRequest unpaid=LeaveRequest.builder().id(UUID.randomUUID()).employeeId(employeeId).departmentId(department).paid(false).status(LeaveRequest.Status.Approved).startDate(LocalDate.of(2026,8,3)).endDate(LocalDate.of(2026,8,3)).build();
        when(leaveRequests.approvedForPeriod(eq(employeeId),any(),any())).thenReturn(List.of(paid,unpaid)); when(summaries.findTopByEmployeeIdAndPayrollYearAndPayrollMonthOrderBySummaryVersionDesc(employeeId,2026,8)).thenReturn(Optional.empty());
        MonthlyAttendanceSummary result=service.build(department,request(employeeId)); assertThat(result.getApprovedPaidLeaveDays()).isEqualTo(2); assertThat(result.getApprovedUnpaidLeaveDays()).isEqualTo(1); assertThat(result.getAttendanceDisputes()).isEqualTo(1); assertThat(result.getVerificationStatus()).isEqualTo(VerificationStatus.Discrepancies);
    }

    @Test void verificationRejectsLeaveChangedAfterSummaryWasBuilt() {
        UUID id=UUID.randomUUID(),department=UUID.randomUUID(),employeeId=UUID.randomUUID(),leaveId=UUID.randomUUID();
        MonthlyAttendanceSummary summary=MonthlyAttendanceSummary.builder().id(id).employeeId(employeeId).departmentId(department).employeeRole("Staff").payrollYear(2026).payrollMonth(8).approvedPaidLeaveDays(0).approvedUnpaidLeaveDays(0).attendanceDisputes(0).leaveRecordIds("").verificationStatus(VerificationStatus.Draft).summaryVersion(1).build();
        LeaveRequest newlyApproved=LeaveRequest.builder().id(leaveId).employeeId(employeeId).departmentId(department).paid(true).status(LeaveRequest.Status.Approved).startDate(LocalDate.of(2026,8,10)).endDate(LocalDate.of(2026,8,10)).build();
        when(summaries.findByIdAndDepartmentId(id,department)).thenReturn(Optional.of(summary));
        when(attendance.findByEmployeeIdAndDateBetweenOrderByDateAsc(eq(employeeId),any(),any())).thenReturn(List.of());
        when(leaveRequests.approvedForPeriod(eq(employeeId),any(),any())).thenReturn(List.of(newlyApproved));
        assertThatThrownBy(()->service.verify(id,UUID.randomUUID(),department)).isInstanceOf(IllegalStateException.class).hasMessageContaining("Approved leave changed");
        verify(summaries,never()).save(argThat(s->s.getVerificationStatus()==VerificationStatus.Verified));
    }

    private static EmployeeProfile profile(UUID id, UUID department) { return EmployeeProfile.builder().employeeId(id).departmentId(department).fullName("Employee").role(TaskRole.Staff).build(); }
    private static BuildSummaryRequest request(UUID id) { return new BuildSummaryRequest(id, 2026, 8, 22, 8, 1); }
}
