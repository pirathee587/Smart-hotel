package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.AttendanceRecord;
import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.OvertimeApproval;
import com.smarthotel.fieldops.domain.model.PayrollRecord;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.AttendanceStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OvertimeStatus;
import com.smarthotel.fieldops.domain.repository.AttendanceRecordRepository;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import com.smarthotel.fieldops.domain.repository.OvertimeApprovalRepository;
import com.smarthotel.fieldops.domain.repository.PayrollRecordRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.math.BigDecimal;
import java.time.Instant;
import java.time.LocalDate;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class PayrollServiceTest {

    @Mock
    private AttendanceRecordRepository attendanceRecordRepository;

    @Mock
    private OvertimeApprovalRepository overtimeApprovalRepository;

    @Mock
    private EmployeeProfileRepository employeeProfileRepository;

    @Mock
    private PayrollRecordRepository payrollRecordRepository;

    private PayrollService payrollService;

    @BeforeEach
    void setUp() {
        payrollService = new PayrollService(
                attendanceRecordRepository,
                overtimeApprovalRepository,
                employeeProfileRepository,
                payrollRecordRepository
        );
    }

    @Test
    @DisplayName("Generate payroll: regular shift with 8 hours earns base pay without overtime")
    void generatePayroll_StandardShift() {
        UUID empId = UUID.randomUUID();
        LocalDate start = LocalDate.of(2026, 9, 1);
        LocalDate end = LocalDate.of(2026, 9, 7);

        EmployeeProfile profile = EmployeeProfile.builder()
                .employeeId(empId)
                .fullName("Saman Perera")
                .hourlyRate(new BigDecimal("250.00"))
                .build();

        AttendanceRecord record = AttendanceRecord.builder()
                .id(UUID.randomUUID())
                .employeeId(empId)
                .date(start)
                .clockIn(Instant.now())
                .clockOut(Instant.now().plusSeconds(8 * 3600))
                .hoursWorked(8.0)
                .overtimeHours(0.0)
                .status(AttendanceStatus.Normal)
                .build();

        when(employeeProfileRepository.findById(empId)).thenReturn(Optional.of(profile));
        when(attendanceRecordRepository.findByEmployeeIdAndDateBetweenOrderByDateAsc(empId, start, end))
                .thenReturn(List.of(record));
        when(payrollRecordRepository.save(any(PayrollRecord.class))).thenAnswer(i -> i.getArgument(0));

        PayrollRecord payroll = payrollService.generatePayroll(empId, start, end);

        assertThat(payroll.getRegularHours()).isEqualTo(8.0);
        assertThat(payroll.getOvertimeHours()).isEqualTo(0.0);
        assertThat(payroll.getRegularPay()).isEqualByComparingTo("2000.00"); // 8 * 250
        assertThat(payroll.getOvertimePay()).isEqualByComparingTo("0.00");
        assertThat(payroll.getGrossPay()).isEqualByComparingTo("2000.00");
    }

    @Test
    @DisplayName("Generate payroll: unapproved overtime (status Pending) earns ZERO overtime pay")
    void generatePayroll_UnapprovedOvertime_YieldsZeroOvertimePay() {
        UUID empId = UUID.randomUUID();
        LocalDate date = LocalDate.of(2026, 9, 1);
        UUID recId = UUID.randomUUID();

        EmployeeProfile profile = EmployeeProfile.builder()
                .employeeId(empId)
                .hourlyRate(new BigDecimal("250.00"))
                .build();

        AttendanceRecord record = AttendanceRecord.builder()
                .id(recId)
                .employeeId(empId)
                .date(date)
                .clockIn(Instant.now())
                .clockOut(Instant.now().plusSeconds(10 * 3600))
                .hoursWorked(10.0)
                .overtimeHours(2.0)
                .status(AttendanceStatus.Normal)
                .build();

        OvertimeApproval pendingApproval = OvertimeApproval.builder()
                .id(UUID.randomUUID())
                .attendanceRecordId(recId)
                .employeeId(empId)
                .overtimeHours(2.0)
                .status(OvertimeStatus.Pending)
                .build();

        when(employeeProfileRepository.findById(empId)).thenReturn(Optional.of(profile));
        when(attendanceRecordRepository.findByEmployeeIdAndDateBetweenOrderByDateAsc(empId, date, date))
                .thenReturn(List.of(record));
        when(overtimeApprovalRepository.findByAttendanceRecordId(recId)).thenReturn(Optional.of(pendingApproval));
        when(payrollRecordRepository.save(any(PayrollRecord.class))).thenAnswer(i -> i.getArgument(0));

        PayrollRecord payroll = payrollService.generatePayroll(empId, date, date);

        assertThat(payroll.getRegularHours()).isEqualTo(8.0);
        assertThat(payroll.getOvertimeHours()).isEqualTo(0.0); // Not counted because not approved
        assertThat(payroll.getRegularPay()).isEqualByComparingTo("2000.00");
        assertThat(payroll.getOvertimePay()).isEqualByComparingTo("0.00");
        assertThat(payroll.getGrossPay()).isEqualByComparingTo("2000.00");
    }

    @Test
    @DisplayName("Generate payroll: approved overtime applies exact 1.5x multiplier")
    void generatePayroll_ApprovedOvertime_AppliesOnePointFiveMultiplier() {
        UUID empId = UUID.randomUUID();
        LocalDate date = LocalDate.of(2026, 9, 1);
        UUID recId = UUID.randomUUID();

        EmployeeProfile profile = EmployeeProfile.builder()
                .employeeId(empId)
                .hourlyRate(new BigDecimal("300.00"))
                .build();

        AttendanceRecord record = AttendanceRecord.builder()
                .id(recId)
                .employeeId(empId)
                .date(date)
                .clockIn(Instant.now())
                .clockOut(Instant.now().plusSeconds(10 * 3600))
                .hoursWorked(10.0)
                .overtimeHours(2.0)
                .status(AttendanceStatus.OvertimeApproved)
                .build();

        OvertimeApproval approvedApproval = OvertimeApproval.builder()
                .id(UUID.randomUUID())
                .attendanceRecordId(recId)
                .employeeId(empId)
                .overtimeHours(2.0)
                .status(OvertimeStatus.Approved)
                .build();

        when(employeeProfileRepository.findById(empId)).thenReturn(Optional.of(profile));
        when(attendanceRecordRepository.findByEmployeeIdAndDateBetweenOrderByDateAsc(empId, date, date))
                .thenReturn(List.of(record));
        when(overtimeApprovalRepository.findByAttendanceRecordId(recId)).thenReturn(Optional.of(approvedApproval));
        when(payrollRecordRepository.save(any(PayrollRecord.class))).thenAnswer(i -> i.getArgument(0));

        PayrollRecord payroll = payrollService.generatePayroll(empId, date, date);

        assertThat(payroll.getRegularHours()).isEqualTo(8.0);
        assertThat(payroll.getOvertimeHours()).isEqualTo(2.0);

        // Regular: 8 * 300 = 2400.00
        assertThat(payroll.getRegularPay()).isEqualByComparingTo("2400.00");

        // Overtime: 2 hours * 300 rate * 1.5 multiplier = 900.00
        assertThat(payroll.getOvertimePay()).isEqualByComparingTo("900.00");

        // Gross: 2400 + 900 = 3300.00
        assertThat(payroll.getGrossPay()).isEqualByComparingTo("3300.00");
    }
}
