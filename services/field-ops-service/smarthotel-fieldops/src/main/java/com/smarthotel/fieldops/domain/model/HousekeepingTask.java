package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.CleaningType;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import jakarta.persistence.*;
import lombok.*;
import lombok.experimental.SuperBuilder;

import java.util.UUID;
import java.time.Instant;

@Entity
@Table(name = "housekeeping_tasks", uniqueConstraints = @UniqueConstraint(name = "uq_housekeeping_checkout_event", columnNames = "checkoutEventId"))
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@SuperBuilder
public class HousekeepingTask extends StaffTask {

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private CleaningType cleaningType = CleaningType.Turnover;

    @Builder.Default
    private boolean linenChanged = false;

    private UUID inspectedBy;

    private Boolean inspectionPassed;

    private UUID checkoutEventId;
    @Column(name = "concierge_event_id")
    private UUID conciergeEventId;
    private String bookingReference;
    private Instant cleaningCompletedAt;
    private Instant inspectedAt;
    @Column(length = 1000) private String inspectionNotes;
    @Column(length = 1000) private String recleanInstructions;
    private UUID readinessEventId;

    public void cleaningCompleted() {
        if (getStatus() != com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.InProgress) throw new IllegalStateException("Cleaning must be in progress before completion.");
        setStatus(com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.AwaitingInspection);
        cleaningCompletedAt = Instant.now(); setCompletedAt(cleaningCompletedAt); setUpdatedAt(cleaningCompletedAt);
    }

    public void rejectInspection(UUID managerId, String instructions) {
        if (getStatus() != com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.AwaitingInspection) throw new IllegalStateException("Only completed cleaning can be inspected.");
        if (managerId.equals(getAssignedEmployeeId())) throw new SecurityException("Housekeepers cannot inspect their own cleaning task.");
        inspectedBy = managerId; inspectionPassed = false; inspectedAt = Instant.now(); recleanInstructions = instructions; inspectionNotes = instructions;
        setStatus(com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.InspectionRejected); setUpdatedAt(inspectedAt);
    }

    public void approveInspection(UUID managerId, String notes, UUID eventId) {
        if (getStatus() != com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.AwaitingInspection) throw new IllegalStateException("Only completed cleaning can be inspected.");
        if (managerId.equals(getAssignedEmployeeId())) throw new SecurityException("Housekeepers cannot inspect their own cleaning task.");
        inspectedBy = managerId; inspectionPassed = true; inspectedAt = Instant.now(); inspectionNotes = notes; readinessEventId = eventId;
        setStatus(com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus.InspectionApproved); setUpdatedAt(inspectedAt);
    }

    @PrePersist
    public void ensureHousekeeperRole() {
        if (getRequiredRole() == null) {
            setRequiredRole(TaskRole.Housekeeper);
        }
    }
}
