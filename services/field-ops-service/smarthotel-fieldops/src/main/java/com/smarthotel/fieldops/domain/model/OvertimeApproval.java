package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OvertimeStatus;
import jakarta.persistence.*;
import lombok.*;

import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "overtime_approvals")
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class OvertimeApproval {

    @Id
    @Builder.Default
    private UUID id = UUID.randomUUID();

    @Column(nullable = false)
    private UUID attendanceRecordId;

    @Column(nullable = false)
    private UUID employeeId;

    private UUID managerId;

    @Column(nullable = false)
    private double overtimeHours;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private OvertimeStatus status = OvertimeStatus.Pending;

    private String managerNotes;

    @Builder.Default
    private Instant requestedAt = Instant.now();

    private Instant actionedAt;

    public void approve(UUID managerId, String notes) {
        if (managerId != null && managerId.equals(this.employeeId)) {
            throw new SecurityException("Employees cannot approve their own overtime.");
        }
        this.status = OvertimeStatus.Approved;
        this.managerId = managerId;
        this.managerNotes = notes;
        this.actionedAt = Instant.now();
    }

    public void reject(UUID managerId, String notes) {
        if (managerId != null && managerId.equals(this.employeeId)) {
            throw new SecurityException("Employees cannot reject their own overtime.");
        }
        this.status = OvertimeStatus.Rejected;
        this.managerId = managerId;
        this.managerNotes = notes;
        this.actionedAt = Instant.now();
    }
}
