package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OutboxStatus;
import jakarta.persistence.*;
import lombok.*;

import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "fnb_charge_outbox", indexes = {
    @Index(name = "idx_fnb_outbox_status_retry", columnList = "status,nextRetryAt"),
    @Index(name = "idx_fnb_outbox_order_id", columnList = "orderId")
})
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class FnbChargeOutbox {

    @Id
    @Builder.Default
    private UUID id = UUID.randomUUID();

    @Column(nullable = false)
    private UUID orderId;

    @Column(nullable = false)
    private String chargeReference;

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

    private UUID financeInvoiceId;

    private String financeInvoiceNumber;

    @Builder.Default
    private Instant createdAt = Instant.now();

    private Instant confirmedAt;

    public void markConfirmed(UUID invoiceId, String invoiceNumber) {
        this.status = OutboxStatus.Confirmed;
        this.financeInvoiceId = invoiceId;
        this.financeInvoiceNumber = invoiceNumber;
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
