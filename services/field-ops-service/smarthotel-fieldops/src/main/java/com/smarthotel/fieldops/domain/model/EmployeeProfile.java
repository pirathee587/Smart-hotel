package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import jakarta.persistence.*;
import lombok.*;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "employee_profiles")
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class EmployeeProfile {

    @Id
    private UUID employeeId;

    @Column(nullable = false)
    private String fullName;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private TaskRole role;

    @Column(nullable = false, precision = 12, scale = 2)
    @Builder.Default
    private BigDecimal hourlyRate = new BigDecimal("250.00"); // Standard LKR base rate

    @Builder.Default
    private int currentFloor = 1;

    @Builder.Default
    private int activeTasksCount = 0;

    @Builder.Default
    private int tasksCompletedToday = 0;

    @Builder.Default
    private int proficiencyLevel = 5; // 1-5 scale (ProficiencyLevel / 5)

    // Bank Details Stub (Event-sourced local copy, Option B)
    private String bankName;

    private String bankAccountNumber; // Masked or encrypted

    private String bankBranch;

    @Builder.Default
    private boolean active = true;

    @Builder.Default
    private Instant updatedAt = Instant.now();

    public void incrementActiveTasks() {
        this.activeTasksCount++;
        this.updatedAt = Instant.now();
    }

    public void decrementActiveTasks(boolean wasCompleted) {
        if (this.activeTasksCount > 0) {
            this.activeTasksCount--;
        }
        if (wasCompleted) {
            this.tasksCompletedToday++;
        }
        this.updatedAt = Instant.now();
    }

    public void updateLocation(int floor) {
        this.currentFloor = floor;
        this.updatedAt = Instant.now();
    }
}
