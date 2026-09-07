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
@Table(name = "staff_tasks")
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

    private UUID roomId;

    private String roomNumber;

    @Builder.Default
    private Instant createdAt = Instant.now();

    private Instant updatedAt;

    private Instant assignedAt;

    private Instant completedAt;

    private Instant escalatedAt;

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
        this.status = TaskStatus.InProgress;
        this.updatedAt = Instant.now();
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
