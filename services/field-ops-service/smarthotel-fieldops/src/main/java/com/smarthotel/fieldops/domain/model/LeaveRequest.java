package com.smarthotel.fieldops.domain.model;

import jakarta.persistence.*;
import lombok.*;
import java.time.*;
import java.util.UUID;

@Entity @Table(name="leave_requests", indexes={@Index(name="ix_leave_employee_dates", columnList="employee_id,start_date,end_date"),@Index(name="ix_leave_department_status", columnList="department_id,status,created_at")})
@Getter @Setter @NoArgsConstructor @AllArgsConstructor @Builder
public class LeaveRequest {
    public enum Status { Pending, Approved, Rejected, Cancelled }
    @Id @Builder.Default private UUID id = UUID.randomUUID();
    @Column(name="employee_id", nullable=false) private UUID employeeId;
    @Column(name="department_id", nullable=false) private UUID departmentId;
    @Column(name="policy_id", nullable=false) private UUID policyId;
    @Column(name="leave_type", nullable=false, length=60) private String leaveType;
    @Column(name="paid", nullable=false) private boolean paid;
    @Column(name="start_date", nullable=false) private LocalDate startDate;
    @Column(name="end_date", nullable=false) private LocalDate endDate;
    @Column(name="reason", nullable=false, length=1000) private String reason;
    @Enumerated(EnumType.STRING) @Column(name="status", nullable=false, length=20) @Builder.Default private Status status = Status.Pending;
    @Column(name="decision_manager_id") private UUID decisionManagerId;
    @Column(name="decision_reason", length=1000) private String decisionReason;
    @Column(name="decided_at") private Instant decidedAt;
    @Column(name="created_at", nullable=false) @Builder.Default private Instant createdAt = Instant.now();
    @Column(name="updated_at", nullable=false) @Builder.Default private Instant updatedAt = Instant.now();
    @Version @Column(name="row_version", nullable=false) private long rowVersion;
}
