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
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.*;

@Service
@RequiredArgsConstructor
@Slf4j
public class PayrollService {

    private final AttendanceRecordRepository attendanceRecordRepository;
    private final OvertimeApprovalRepository overtimeApprovalRepository;
    private final EmployeeProfileRepository employeeProfileRepository;
    private final PayrollRecordRepository payrollRecordRepository;

    @Transactional
    public PayrollRecord generatePayroll(UUID employeeId, LocalDate start, LocalDate end) {
        log.info("Generating payroll for employee {} between {} and {}", employeeId, start, end);

        EmployeeProfile profile = employeeProfileRepository.findById(employeeId)
                .orElseThrow(() -> new IllegalArgumentException("Employee profile not found: " + employeeId));

        List<AttendanceRecord> records = attendanceRecordRepository
                .findByEmployeeIdAndDateBetweenOrderByDateAsc(employeeId, start, end);

        double regularHours = 0.0;
        double approvedOvertimeHours = 0.0;

        for (AttendanceRecord record : records) {
            // Missing clock-outs or incomplete records don't accrue regular hours automatically
            if (record.getStatus() == AttendanceStatus.MissingClockOut || record.getClockOut() == null) {
                continue;
            }

            double worked = record.getHoursWorked();
            double shiftRegular = Math.min(worked, 8.0);
            regularHours += shiftRegular;

            // Check if overtime occurred on this record
            if (record.getOvertimeHours() > 0) {
                Optional<OvertimeApproval> approval = overtimeApprovalRepository.findByAttendanceRecordId(record.getId());
                // Gated on Manager approval!
                if (approval.isPresent() && approval.get().getStatus() == OvertimeStatus.Approved) {
                    approvedOvertimeHours += record.getOvertimeHours();
                } else {
                    log.info("Overtime of {}h for record {} not counted in payroll (Status: {})",
                            record.getOvertimeHours(), record.getId(),
                            approval.map(a -> a.getStatus().name()).orElse("No Approval Record"));
                }
            }
        }

        BigDecimal hourlyRate = profile.getHourlyRate();

        PayrollRecord payrollRecord = PayrollRecord.calculate(
                employeeId,
                start,
                end,
                regularHours,
                approvedOvertimeHours,
                hourlyRate
        );

        return payrollRecordRepository.save(payrollRecord);
    }

    @Transactional(readOnly = true)
    public List<PayrollRecord> getEmployeePayrollHistory(UUID employeeId) {
        return payrollRecordRepository.findByEmployeeIdOrderByPayPeriodEndDesc(employeeId);
    }

    @Transactional(readOnly = true)
    public Optional<PayrollRecord> getPayrollById(UUID id) {
        return payrollRecordRepository.findById(id);
    }
}
