package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OutboxStatus;
import jakarta.persistence.*;
import lombok.*;

import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "task_outbox", indexes = {
    @Index(name = "idx_task_outbox_status_retry", columnList = "status,nextRetryAt"),
    @Index(name = "idx_task_outbox_aggregate", columnList = "aggregateId")
})
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class TaskOutbox {

    @Id
    @Builder.Default
    private UUID id = UUID.randomUUID();

    @Column(nullable = false)
    private UUID aggregateId;

    @Column(nullable = false)
    private String eventType;

    @Column(columnDefinition = "TEXT", nullable = false)
    private String payloadJson;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private OutboxStatus status = OutboxStatus.Pending;

    @Builder.Default
    private int retryCount = 0;

    @Builder.Default
    private Instant nextRetryAt = Instant.now();

    @Column(columnDefinition = "TEXT")
    private String lastError;

    @Builder.Default
    private Instant createdAt = Instant.now();

    private Instant confirmedAt;

    public void markConfirmed() {
        this.status = OutboxStatus.Confirmed;
        this.confirmedAt = Instant.now();
        this.lastError = null;
    }

    public void recordFailure(String error, Instant nextRetry) {
        this.retryCount++;
        this.lastError = error;
        this.nextRetryAt = nextRetry;
        if (this.retryCount >= 10) {
            this.status = OutboxStatus.Failed;
        } else {
            this.status = OutboxStatus.Pending;
        }
    }
}
