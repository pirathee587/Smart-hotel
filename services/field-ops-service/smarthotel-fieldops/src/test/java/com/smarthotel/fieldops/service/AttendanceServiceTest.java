package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.AttendanceRecord;
import com.smarthotel.fieldops.domain.model.OvertimeApproval;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.AttendanceStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OvertimeStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.PunchMethod;
import com.smarthotel.fieldops.domain.repository.AttendanceRecordRepository;
import com.smarthotel.fieldops.domain.repository.OvertimeApprovalRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class AttendanceServiceTest {

    @Mock
    private AttendanceRecordRepository attendanceRecordRepository;

    @Mock
    private OvertimeApprovalRepository overtimeApprovalRepository;

    private AttendanceService attendanceService;

    @BeforeEach
    void setUp() {
        attendanceService = new AttendanceService(attendanceRecordRepository, overtimeApprovalRepository);
    }

    @Test
    @DisplayName("Process punch: first punch of the day creates Clock-In record")
    void firstPunch_CreatesClockIn() {
        UUID empId = UUID.randomUUID();
        Instant clockInTime = Instant.parse("2026-09-06T08:00:00Z");
        LocalDate date = clockInTime.atZone(ZoneOffset.UTC).toLocalDate();

        when(attendanceRecordRepository.findByEmployeeIdAndDate(empId, date))
                .thenReturn(Optional.empty());
        when(attendanceRecordRepository.save(any(AttendanceRecord.class)))
                .thenAnswer(i -> i.getArgument(0));

        AttendanceService.PunchResult result = attendanceService.processPunch(
                empId, "FP-DEVICE-01", PunchMethod.Fingerprint, clockInTime
        );

        assertThat(result.isDuplicate()).isFalse();
        assertThat(result.record()).isNotNull();
        assertThat(result.record().getClockIn()).isEqualTo(clockInTime);
        assertThat(result.record().getClockOut()).isNull();
        assertThat(result.record().getStatus()).isEqualTo(AttendanceStatus.Normal);
    }

    @Test
    @DisplayName("60-Second Deduplication: rapid second punch within 60s is ignored as duplicate")
    void rapidPunch_Within60Seconds_IsIgnored() {
        UUID empId = UUID.randomUUID();
        Instant firstPunch = Instant.parse("2026-09-06T08:00:00Z");
        Instant rapidSecondPunch = Instant.parse("2026-09-06T08:00:25Z"); // 25 seconds later
        LocalDate date = firstPunch.atZone(ZoneOffset.UTC).toLocalDate();

        AttendanceRecord record = AttendanceRecord.builder()
                .id(UUID.randomUUID())
                .employeeId(empId)
                .date(date)
                .clockIn(firstPunch)
                .build();

        when(attendanceRecordRepository.findByEmployeeIdAndDate(empId, date))
                .thenReturn(Optional.empty());
        when(attendanceRecordRepository.save(any(AttendanceRecord.class)))
                .thenReturn(record);
        when(attendanceRecordRepository.findTopByEmployeeIdOrderByClockInDesc(empId))
                .thenReturn(Optional.of(record));

        // 1. First punch succeeds
        AttendanceService.PunchResult res1 = attendanceService.processPunch(
                empId, "FP-DEVICE-01", PunchMethod.Fingerprint, firstPunch
        );
        assertThat(res1.isDuplicate()).isFalse();

        // 2. Second punch 25s later -> flagged as duplicate
        AttendanceService.PunchResult res2 = attendanceService.processPunch(
                empId, "FP-DEVICE-01", PunchMethod.Fingerprint, rapidSecondPunch
        );
        assertThat(res2.isDuplicate()).isTrue();
        assertThat(res2.message()).contains("Duplicate punch ignored within 60s window");
        verify(attendanceRecordRepository, times(1)).save(any());
    }

    @Test
    @DisplayName("Clock-Out after 9.5 hours: calculates 1.5h overtime and triggers pending OvertimeApproval")
    void clockOut_CalculatesOvertimeAndCreatesApprovalRequest() {
        UUID empId = UUID.randomUUID();
        Instant clockIn = Instant.parse("2026-09-06T08:00:00Z");
        Instant clockOut = Instant.parse("2026-09-06T17:30:00Z"); // 9.5 hours later
        LocalDate date = clockIn.atZone(ZoneOffset.UTC).toLocalDate();

        AttendanceRecord existing = AttendanceRecord.builder()
                .id(UUID.randomUUID())
                .employeeId(empId)
                .date(date)
                .clockIn(clockIn)
                .status(AttendanceStatus.Normal)
                .build();

        // Simulate 60s elapsed
        attendanceService.processPunch(empId, "FP-01", PunchMethod.Fingerprint, clockIn);

        when(attendanceRecordRepository.findByEmployeeIdAndDate(empId, date))
                .thenReturn(Optional.of(existing));
        when(attendanceRecordRepository.save(any(AttendanceRecord.class)))
                .thenAnswer(i -> i.getArgument(0));

        AttendanceService.PunchResult result = attendanceService.processPunch(
                empId, "FP-01", PunchMethod.Fingerprint, clockOut
        );

        assertThat(result.isDuplicate()).isFalse();
        assertThat(result.record().getClockOut()).isEqualTo(clockOut);
        assertThat(result.record().getHoursWorked()).isEqualTo(9.5);
        assertThat(result.record().getOvertimeHours()).isEqualTo(1.5);

        // Verify OvertimeApproval saved with Pending status
        ArgumentCaptor<OvertimeApproval> approvalCaptor = ArgumentCaptor.forClass(OvertimeApproval.class);
        verify(overtimeApprovalRepository).save(approvalCaptor.capture());
        OvertimeApproval savedApproval = approvalCaptor.getValue();
        assertThat(savedApproval.getEmployeeId()).isEqualTo(empId);
        assertThat(savedApproval.getOvertimeHours()).isEqualTo(1.5);
        assertThat(savedApproval.getStatus()).isEqualTo(OvertimeStatus.Pending);
    }

    @Test
    @DisplayName("FlagMissingClockOuts identifies and updates stale records older than 16 hours")
    void flagMissingClockOuts_UpdatesStaleRecords() {
        UUID empId = UUID.randomUUID();
        Instant staleClockIn = Instant.now().minus(Duration.ofHours(18));

        AttendanceRecord staleRecord = AttendanceRecord.builder()
                .id(UUID.randomUUID())
                .employeeId(empId)
                .clockIn(staleClockIn)
                .clockOut(null)
                .status(AttendanceStatus.Normal)
                .build();

        when(attendanceRecordRepository.findByClockOutIsNullAndClockInBefore(any(Instant.class)))
                .thenReturn(List.of(staleRecord));

        int flaggedCount = attendanceService.flagMissingClockOuts();

        assertThat(flaggedCount).isEqualTo(1);
        assertThat(staleRecord.getStatus()).isEqualTo(AttendanceStatus.MissingClockOut);
        verify(attendanceRecordRepository).save(staleRecord);
    }

    @Test
    @DisplayName("Manager approves overtime: status updates to Approved and AttendanceRecord updated")
    void managerApproveOvertime_UpdatesStatus() {
        UUID approvalId = UUID.randomUUID();
        UUID managerId = UUID.randomUUID();
        UUID recordId = UUID.randomUUID();

        OvertimeApproval approval = OvertimeApproval.builder()
                .id(approvalId)
                .attendanceRecordId(recordId)
                .employeeId(UUID.randomUUID())
                .overtimeHours(2.0)
                .status(OvertimeStatus.Pending)
                .build();

        AttendanceRecord record = AttendanceRecord.builder()
                .id(recordId)
                .status(AttendanceStatus.Normal)
                .build();

        when(overtimeApprovalRepository.findById(approvalId)).thenReturn(Optional.of(approval));
        when(overtimeApprovalRepository.save(any(OvertimeApproval.class))).thenAnswer(i -> i.getArgument(0));
        when(attendanceRecordRepository.findById(recordId)).thenReturn(Optional.of(record));

        OvertimeApproval approved = attendanceService.approveOvertime(approvalId, managerId, "High occupancy shift");

        assertThat(approved.getStatus()).isEqualTo(OvertimeStatus.Approved);
        assertThat(approved.getManagerId()).isEqualTo(managerId);
        assertThat(approved.getManagerNotes()).isEqualTo("High occupancy shift");
        assertThat(record.getStatus()).isEqualTo(AttendanceStatus.OvertimeApproved);
    }
}
