package com.smarthotel.fieldops.domain.model;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.*;

import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "task_allocation_logs")
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class TaskAllocationLog {

    @Id
    @Builder.Default
    private UUID id = UUID.randomUUID();

    @Column(nullable = false)
    private UUID taskId;

    @Column(nullable = false)
    private UUID employeeId;

    private double skillScore;

    private double proximityScore;

    private double loadScore;

    private double fairnessScore;

    @Column(nullable = false)
    private double totalScore;

    @Builder.Default
    private Instant allocatedAt = Instant.now();
}
