package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.AttendanceStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.PunchMethod;
import jakarta.persistence.*;
import lombok.*;

import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.util.UUID;

@Entity
@Table(name = "attendance_records")
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class AttendanceRecord {

    @Id
    @Builder.Default
    private UUID id = UUID.randomUUID();

    @Column(nullable = false)
    private UUID employeeId;

    @Column(nullable = false)
    private LocalDate date;

    @Column(nullable = false)
    private Instant clockIn;

    private Instant clockOut;

    private String deviceId;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private PunchMethod punchMethod = PunchMethod.Fingerprint;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private AttendanceStatus status = AttendanceStatus.Normal;

    @Builder.Default
    private double hoursWorked = 0.0;

    @Builder.Default
    private double overtimeHours = 0.0;

    @Builder.Default
    private Instant createdAt = Instant.now();

    public void clockOut(Instant clockOutTime) {
        this.clockOut = clockOutTime;
        double durationHours = Duration.between(this.clockIn, clockOutTime).toMinutes() / 60.0;
        this.hoursWorked = Math.round(durationHours * 100.0) / 100.0;

        if (this.hoursWorked > 8.0) {
            this.overtimeHours = Math.round((this.hoursWorked - 8.0) * 100.0) / 100.0;
            this.status = AttendanceStatus.OvertimePendingApproval;
        } else {
            this.status = AttendanceStatus.Normal;
        }
    }

    public void flagMissingClockOut() {
        if (this.clockOut == null && this.status != AttendanceStatus.MissingClockOut) {
            this.status = AttendanceStatus.MissingClockOut;
            this.hoursWorked = 0.0;
            this.overtimeHours = 0.0;
        }
    }
}
