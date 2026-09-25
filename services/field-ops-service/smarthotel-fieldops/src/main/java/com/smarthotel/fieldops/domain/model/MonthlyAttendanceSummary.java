package com.smarthotel.fieldops.domain.model;

import jakarta.persistence.*;
import lombok.*;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "monthly_attendance_summaries",
    uniqueConstraints = @UniqueConstraint(name = "uq_attendance_summary_employee_period_version", columnNames = {"employee_id", "payroll_year", "payroll_month", "summary_version"}),
    indexes = {@Index(name = "ix_attendance_summary_department_period", columnList = "department_id,payroll_year,payroll_month"), @Index(name = "ix_attendance_summary_status", columnList = "verification_status,verified_at")})
@Getter @Setter @NoArgsConstructor @AllArgsConstructor @Builder
public class MonthlyAttendanceSummary {
    public enum VerificationStatus { Draft, Discrepancies, Verified, Outdated }
    @Id @Builder.Default private UUID id = UUID.randomUUID();
    @Column(name="employee_id", nullable=false) private UUID employeeId;
    @Column(name="department_id", nullable=false) private UUID departmentId;
    @Column(name="employee_role", nullable=false, length=40) private String employeeRole;
    @Column(name="payroll_year", nullable=false) private int payrollYear;
    @Column(name="payroll_month", nullable=false) private int payrollMonth;
    @Column(name="scheduled_days", nullable=false) private int scheduledDays;
    @Column(name="worked_days", nullable=false) private int workedDays;
    @Column(name="weekend_days", nullable=false) private int weekendDays;
    @Column(name="holiday_days", nullable=false) private int holidayDays;
    @Column(name="approved_paid_leave_days", nullable=false) private int approvedPaidLeaveDays;
    @Column(name="approved_unpaid_leave_days", nullable=false) private int approvedUnpaidLeaveDays;
    @Column(name="approved_overtime_hours", nullable=false) private double approvedOvertimeHours;
    @Column(name="missing_punches", nullable=false) private int missingPunches;
    @Column(name="attendance_disputes", nullable=false) private int attendanceDisputes;
    @Column(name="attendance_record_ids", nullable=false, columnDefinition="text") private String attendanceRecordIds = "";
    @Column(name="overtime_record_ids", nullable=false, columnDefinition="text") private String overtimeRecordIds = "";
    @Column(name="leave_record_ids", nullable=false, columnDefinition="text") private String leaveRecordIds = "";
    @Enumerated(EnumType.STRING) @Column(name="verification_status", nullable=false, length=30) private VerificationStatus verificationStatus;
    @Column(name="verified_by_manager_id") private UUID verifiedByManagerId;
    @Column(name="verified_at") private Instant verifiedAt;
    @Column(name="summary_version", nullable=false) private int summaryVersion;
    @Column(name="created_at", nullable=false) @Builder.Default private Instant createdAt = Instant.now();
    @Column(name="updated_at", nullable=false) @Builder.Default private Instant updatedAt = Instant.now();
}
