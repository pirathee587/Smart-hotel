package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.PrePersist;
import jakarta.persistence.Table;
import lombok.*;
import lombok.experimental.SuperBuilder;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "maintenance_work_orders", uniqueConstraints = {
        @jakarta.persistence.UniqueConstraint(name = "uq_maintenance_issue_event", columnNames = "issueEventId")
}, indexes = {
        @jakarta.persistence.Index(name = "ix_maintenance_finance_status", columnList = "financeExpenseStatus,costApprovedAt")
})
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@SuperBuilder
public class MaintenanceWorkOrder extends StaffTask {

    @Column(nullable = false)
    private String assetName;

    private String location;

    private BigDecimal estimatedCost;

    private BigDecimal actualCost;

    @Column(length = 2000)
    private String partsReplaced;

    @Builder.Default
    private boolean safetyHazard = false;

    private UUID issueEventId;
    private String severity;
    @Column(length = 2000) private String hazardDetails;
    @Column(length = 4000) private String repairNotes;
    @Column(length = 2000) private String reworkInstructions;
    private UUID verifiedBy;
    private Instant repairCompletedAt;
    private Instant verifiedAt;
    private Instant restrictionClearedAt;
    private UUID restrictionEventId;
    private UUID clearanceEventId;
    private Instant costApprovedAt;
    private UUID costApprovedBy;
    private UUID financeExpenseId;
    private String financeExpenseStatus;
    @Column(length = 1000) private String financeFailure;

    @PrePersist
    public void ensureMaintenanceRole() {
        if (getRequiredRole() == null) {
            setRequiredRole(TaskRole.Maintenance);
        }
    }

    public void repairCompleted(String notes, BigDecimal cost, String parts) {
        if (getStatus() != com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.InProgress)
            throw new IllegalStateException("Repair must be in progress before completion.");
        repairNotes = notes; actualCost = cost; partsReplaced = parts;
        repairCompletedAt = Instant.now(); setStatus(com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.AwaitingInspection);
        setUpdatedAt(repairCompletedAt);
    }

    public void verifyRepair(UUID managerId, boolean approved, String notes) {
        if (getStatus() != com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.AwaitingInspection)
            throw new IllegalStateException("Repair is not awaiting Manager verification.");
        if (managerId.equals(getAssignedEmployeeId())) throw new SecurityException("Technicians cannot verify their own repairs.");
        verifiedBy = managerId; verifiedAt = Instant.now();
        if (approved) setStatus(com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.InspectionApproved);
        else { setStatus(com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.InspectionRejected); reworkInstructions = notes; }
        setUpdatedAt(verifiedAt);
    }
}
