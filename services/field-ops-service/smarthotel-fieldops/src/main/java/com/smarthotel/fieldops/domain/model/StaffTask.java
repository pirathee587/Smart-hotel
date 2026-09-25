package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskPriority;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import jakarta.persistence.*;
import lombok.*;
import lombok.experimental.SuperBuilder;

import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "staff_tasks", indexes = {
        @Index(name = "ix_staff_tasks_department_status", columnList = "departmentId,status"),
        @Index(name = "ix_staff_tasks_assignee_status", columnList = "assignedEmployeeId,status")
})
@Inheritance(strategy = InheritanceType.JOINED)
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@SuperBuilder
public class StaffTask {

    @Id
    @Builder.Default
    private UUID id = UUID.randomUUID();

    @Column(nullable = false)
    private String title;

    @Column(length = 2000)
    private String description;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private TaskRole requiredRole;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private TaskPriority priority = TaskPriority.Medium;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private TaskStatus status = TaskStatus.Pending;

    private UUID assignedEmployeeId;

    @Builder.Default
    private int rejectionCount = 0;

    @Builder.Default
    private int floorNumber = 1;

    private UUID hotelId;

    private UUID departmentId;

    private UUID roomId;

    private String roomNumber;

    @Builder.Default
    private Instant createdAt = Instant.now();

    private Instant updatedAt;

    private Instant assignedAt;

    private Instant acceptedAt;

    private Instant startedAt;

    private Instant completedAt;

    private Instant escalatedAt;

    @Enumerated(EnumType.STRING)
    @Column(length = 20)
    private TaskPriority aiSuggestedPriority;

    private Integer aiSuggestedSlaMinutes;

    private Integer slaMinutes;

    private Instant aiSuggestionGeneratedAt;

    public void applyAiSuggestion(TaskPriority suggestedPriority, int suggestedSlaMinutes) {
        if (suggestedPriority == null || suggestedSlaMinutes <= 0) {
            throw new IllegalArgumentException("AI priority and SLA suggestion must be valid.");
        }
        this.aiSuggestedPriority = suggestedPriority;
        this.aiSuggestedSlaMinutes = suggestedSlaMinutes;
        this.aiSuggestionGeneratedAt = Instant.now();
        this.priority = suggestedPriority;
        this.slaMinutes = suggestedSlaMinutes;
        this.updatedAt = Instant.now();
    }

    public void overridePlanning(TaskPriority effectivePriority, int effectiveSlaMinutes) {
        if (effectivePriority == null || effectiveSlaMinutes <= 0) {
            throw new IllegalArgumentException("Effective priority and SLA must be valid.");
        }
        this.priority = effectivePriority;
        this.slaMinutes = effectiveSlaMinutes;
        this.updatedAt = Instant.now();
    }

    public void assignTo(UUID employeeId) {
        this.assignedEmployeeId = employeeId;
        this.status = TaskStatus.Assigned;
        this.assignedAt = Instant.now();
        this.updatedAt = Instant.now();
    }

    public void accept() {
        if (this.status != TaskStatus.Assigned) {
            throw new IllegalStateException("Task must be in Assigned state to be accepted.");
        }
        this.status = TaskStatus.Accepted;
        this.acceptedAt = Instant.now();
        this.updatedAt = this.acceptedAt;
    }

    public void start() {
        if (this.status != TaskStatus.Accepted && this.status != TaskStatus.InspectionRejected) {
            throw new IllegalStateException("Task must be accepted or returned for re-cleaning before work starts.");
        }
        this.status = TaskStatus.InProgress;
        this.startedAt = Instant.now();
        this.updatedAt = this.startedAt;
    }

    public boolean reject(String reason) {
        this.rejectionCount++;
        this.assignedEmployeeId = null;
        this.updatedAt = Instant.now();

        if (this.rejectionCount >= 3) {
            this.status = TaskStatus.Escalated;
            this.escalatedAt = Instant.now();
            return true; // Escalated
        } else {
            this.status = TaskStatus.Pending;
            return false; // Ready for re-allocation
        }
    }

    public void complete() {
        this.status = TaskStatus.Completed;
        this.completedAt = Instant.now();
        this.updatedAt = Instant.now();
    }

    public void cancel() {
        this.status = TaskStatus.Cancelled;
        this.updatedAt = Instant.now();
    }
}
