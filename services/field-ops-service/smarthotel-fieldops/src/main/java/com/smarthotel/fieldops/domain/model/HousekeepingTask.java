package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.CleaningType;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import jakarta.persistence.*;
import lombok.*;
import lombok.experimental.SuperBuilder;

import java.util.UUID;

@Entity
@Table(name = "housekeeping_tasks")
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

    @PrePersist
    public void ensureHousekeeperRole() {
        if (getRequiredRole() == null) {
            setRequiredRole(TaskRole.Housekeeper);
        }
    }
}
