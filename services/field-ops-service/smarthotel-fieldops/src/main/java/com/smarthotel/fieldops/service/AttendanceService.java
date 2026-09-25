package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.AttendanceRecord;
import com.smarthotel.fieldops.domain.model.OvertimeApproval;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.AttendanceStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OvertimeStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.PunchMethod;
import com.smarthotel.fieldops.domain.repository.AttendanceRecordRepository;
import com.smarthotel.fieldops.domain.repository.OvertimeApprovalRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.util.*;

@Service
@RequiredArgsConstructor
@Slf4j
public class AttendanceService {

    private final AttendanceRecordRepository attendanceRecordRepository;
    private final OvertimeApprovalRepository overtimeApprovalRepository;
    private final AttendanceSummaryService attendanceSummaryService;

    // In-memory cache to support strict 60s deduplication across rapid punches
    private final Map<UUID, Instant> lastPunchTimestamps = new HashMap<>();

    public record PunchResult(AttendanceRecord record, boolean isDuplicate, String message) {}

    @Transactional
    public PunchResult processPunch(UUID employeeId, String deviceId, PunchMethod method, Instant punchTime) {
        Instant now = punchTime != null ? punchTime : Instant.now();

        // 1. 60-Second Deduplication Rule
        Instant lastPunch = lastPunchTimestamps.get(employeeId);
        if (lastPunch != null) {
            long secondsBetween = Math.abs(Duration.between(lastPunch, now).toSeconds());
            if (secondsBetween < 60) {
                log.info("Ignored rapid punch for employee {} (occurred {}s ago)", employeeId, secondsBetween);
                Optional<AttendanceRecord> latest = attendanceRecordRepository.findTopByEmployeeIdOrderByClockInDesc(employeeId);
                return new PunchResult(latest.orElse(null), true, "Duplicate punch ignored within 60s window.");
            }
        }

        lastPunchTimestamps.put(employeeId, now);

        LocalDate today = now.atZone(ZoneOffset.UTC).toLocalDate();
        Optional<AttendanceRecord> existingRecord = attendanceRecordRepository.findByEmployeeIdAndDate(employeeId, today);

        if (existingRecord.isEmpty()) {
            // Clock-In
            AttendanceRecord record = AttendanceRecord.builder()
                    .employeeId(employeeId)
                    .date(today)
                    .clockIn(now)
                    .deviceId(deviceId)
                    .punchMethod(method != null ? method : PunchMethod.Fingerprint)
                    .status(AttendanceStatus.Normal)
                    .build();

            AttendanceRecord saved = attendanceRecordRepository.save(record);
            attendanceSummaryService.markOutdated(employeeId, today);
            log.info("Clocked IN employee {} at {}", employeeId, now);
            return new PunchResult(saved, false, "Clock-in recorded successfully.");
        }

        AttendanceRecord record = existingRecord.get();
        if (record.getClockOut() == null) {
            // Clock-Out
            record.clockOut(now);
            AttendanceRecord saved = attendanceRecordRepository.save(record);
            attendanceSummaryService.markOutdated(employeeId, today);
            log.info("Clocked OUT employee {} at {}. Worked: {}h, Overtime: {}h",
                    employeeId, now, saved.getHoursWorked(), saved.getOvertimeHours());

            // If overtime detected, create OvertimeApproval requiring Manager authorization
            if (saved.getOvertimeHours() > 0) {
                OvertimeApproval approval = OvertimeApproval.builder()
                        .attendanceRecordId(saved.getId())
                        .employeeId(employeeId)
                        .overtimeHours(saved.getOvertimeHours())
                        .status(OvertimeStatus.Pending)
                        .build();
                overtimeApprovalRepository.save(approval);
                log.info("Created pending OvertimeApproval for employee {} ({}h)", employeeId, saved.getOvertimeHours());
            }

            return new PunchResult(saved, false, "Clock-out recorded successfully.");
        }

        return new PunchResult(record, false, "Employee has already completed shift for today.");
    }

    @Transactional
    public OvertimeApproval approveOvertime(UUID approvalId, UUID managerId, String notes) {
        OvertimeApproval approval = overtimeApprovalRepository.findById(approvalId)
                .orElseThrow(() -> new IllegalArgumentException("Overtime approval not found: " + approvalId));

        if (managerId != null && managerId.equals(approval.getEmployeeId())) {
            throw new SecurityException("Employees cannot approve their own overtime.");
        }

        approval.approve(managerId, notes);
        OvertimeApproval saved = overtimeApprovalRepository.save(approval);
        attendanceSummaryService.markOutdated(approval.getEmployeeId(), attendanceRecordRepository.findById(approval.getAttendanceRecordId()).orElseThrow().getDate());

        // Update attendance record status
        attendanceRecordRepository.findById(approval.getAttendanceRecordId()).ifPresent(rec -> {
            rec.setStatus(AttendanceStatus.OvertimeApproved);
            attendanceRecordRepository.save(rec);
        });

        log.info("Manager {} approved {}h overtime for employee {}", managerId, approval.getOvertimeHours(), approval.getEmployeeId());
        return saved;
    }

    @Transactional
    public OvertimeApproval rejectOvertime(UUID approvalId, UUID managerId, String notes) {
        OvertimeApproval approval = overtimeApprovalRepository.findById(approvalId)
                .orElseThrow(() -> new IllegalArgumentException("Overtime approval not found: " + approvalId));

        if (managerId != null && managerId.equals(approval.getEmployeeId())) {
            throw new SecurityException("Employees cannot reject their own overtime.");
        }

        approval.reject(managerId, notes);
        OvertimeApproval saved = overtimeApprovalRepository.save(approval);
        attendanceSummaryService.markOutdated(approval.getEmployeeId(), attendanceRecordRepository.findById(approval.getAttendanceRecordId()).orElseThrow().getDate());

        // Update attendance record status
        attendanceRecordRepository.findById(approval.getAttendanceRecordId()).ifPresent(rec -> {
            rec.setStatus(AttendanceStatus.Normal);
            attendanceRecordRepository.save(rec);
            attendanceSummaryService.markOutdated(rec.getEmployeeId(), rec.getDate());
        });

        log.info("Manager {} rejected overtime for employee {}", managerId, approval.getEmployeeId());
        return saved;
    }

    /**
     * Automatic scheduled background task:
     * Evaluates open shifts older than 16 hours without clock-out and flags them as MissingClockOut.
     */
    @Scheduled(cron = "0 0 4 * * ?") // 4:00 AM daily
    @Transactional
    public int flagMissingClockOuts() {
        Instant cutoff = Instant.now().minus(Duration.ofHours(16));
        List<AttendanceRecord> staleRecords = attendanceRecordRepository.findByClockOutIsNullAndClockInBefore(cutoff);

        for (AttendanceRecord rec : staleRecords) {
            rec.flagMissingClockOut();
            attendanceRecordRepository.save(rec);
            log.warn("Flagged attendance record {} as MissingClockOut for employee {}", rec.getId(), rec.getEmployeeId());
        }

        return staleRecords.size();
    }

    @Transactional(readOnly = true)
    public List<AttendanceRecord> getEmployeeAttendance(UUID employeeId, LocalDate start, LocalDate end) {
        return attendanceRecordRepository.findByEmployeeIdAndDateBetweenOrderByDateAsc(employeeId, start, end);
    }

    @Transactional(readOnly = true)
    public List<OvertimeApproval> getPendingOvertimeApprovals() {
        return overtimeApprovalRepository.findByStatus(OvertimeStatus.Pending);
    }
}
