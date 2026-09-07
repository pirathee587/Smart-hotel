package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.PrePersist;
import jakarta.persistence.Table;
import lombok.*;
import lombok.experimental.SuperBuilder;

import java.math.BigDecimal;

@Entity
@Table(name = "maintenance_work_orders")
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

    @PrePersist
    public void ensureMaintenanceRole() {
        if (getRequiredRole() == null) {
            setRequiredRole(TaskRole.Maintenance);
        }
    }
}
