package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.ChargeStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.KdsStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OrderType;
import jakarta.persistence.*;
import lombok.*;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "kds_orders", indexes = {
    @Index(name = "idx_kds_orders_status", columnList = "status"),
    @Index(name = "idx_kds_orders_order_num", columnList = "orderNumber")
})
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class KdsOrder {

    @Id
    @Builder.Default
    private UUID id = UUID.randomUUID();

    @Column(nullable = false, unique = true)
    private String orderNumber;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private OrderType orderType = OrderType.Restaurant;

    private String tableOrRoomNumber;

    private String roomNumber;

    private UUID bookingId;

    private UUID customerId;

    private String customerName;

    @Column(columnDefinition = "TEXT")
    private String itemsJson;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private KdsStatus status = KdsStatus.Received;

    private UUID cookEmployeeId;

    private String chefName;

    private UUID waiterEmployeeId;

    private String waiterName;

    @Column(precision = 12, scale = 2)
    @Builder.Default
    private BigDecimal subtotal = BigDecimal.ZERO;

    @Column(precision = 12, scale = 2)
    @Builder.Default
    private BigDecimal taxAmount = BigDecimal.ZERO;

    @Column(precision = 12, scale = 2)
    @Builder.Default
    private BigDecimal totalAmount = BigDecimal.ZERO;

    @Column(length = 3)
    @Builder.Default
    private String currency = "LKR";

    private UUID financeInvoiceId;

    private String financeInvoiceNumber;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private ChargeStatus chargeStatus = ChargeStatus.PendingSubmission;

    @Column(columnDefinition = "TEXT")
    private String chargeError;

    @Column(columnDefinition = "TEXT")
    private String notes;

    @Column(columnDefinition = "TEXT")
    private String cancellationReason;

    @Builder.Default
    private Instant receivedAt = Instant.now();

    private Instant acceptedAt;

    private Instant preparingAt;

    private Instant readyAt;

    private Instant collectedAt;

    private Instant deliveredAt;

    private Instant cancelledAt;

    // Chef alias for cookEmployeeId
    public UUID getChefEmployeeId() {
        return cookEmployeeId;
    }

    public void setChefEmployeeId(UUID chefId) {
        this.cookEmployeeId = chefId;
    }

    public void accept(UUID chefId, String chefName) {
        if (this.status != KdsStatus.Received) {
            throw new IllegalStateException("Order can only be accepted when in Received status. Current: " + this.status);
        }
        this.status = KdsStatus.Accepted;
        this.cookEmployeeId = chefId;
        this.chefName = chefName;
        this.acceptedAt = Instant.now();
    }

    public void startPreparing(UUID cookId) {
        startPreparing(cookId, null);
    }

    public void startPreparing(UUID cookId, String chefName) {
        if (this.status != KdsStatus.Received && this.status != KdsStatus.Accepted) {
            throw new IllegalStateException("Order can only start preparing when in Received or Accepted status. Current: " + this.status);
        }
        this.status = KdsStatus.Preparing;
        if (cookId != null) {
            this.cookEmployeeId = cookId;
        }
        if (chefName != null) {
            this.chefName = chefName;
        }
        this.preparingAt = Instant.now();
    }

    public void markReady() {
        if (this.status != KdsStatus.Preparing) {
            throw new IllegalStateException("Order can only be marked ready when in Preparing status. Current: " + this.status);
        }
        this.status = KdsStatus.Ready;
        this.readyAt = Instant.now();
    }

    public void markCollected(UUID waiterId, String waiterName) {
        if (this.status != KdsStatus.Ready) {
            throw new IllegalStateException("Order can only be collected when in Ready status. Current: " + this.status);
        }
        this.status = KdsStatus.Collected;
        if (waiterId != null) {
            this.waiterEmployeeId = waiterId;
        }
        if (waiterName != null) {
            this.waiterName = waiterName;
        }
        this.collectedAt = Instant.now();
    }

    public void markDelivered() {
        markDelivered(null, null);
    }

    public void markDelivered(UUID waiterId, String waiterName) {
        if (this.status != KdsStatus.Collected && this.status != KdsStatus.Ready) {
            throw new IllegalStateException("Order can only be delivered when in Ready or Collected status. Current: " + this.status);
        }
        this.status = KdsStatus.Delivered;
        if (waiterId != null) {
            this.waiterEmployeeId = waiterId;
        }
        if (waiterName != null) {
            this.waiterName = waiterName;
        }
        this.deliveredAt = Instant.now();
        if (this.chargeStatus == ChargeStatus.PendingSubmission) {
            this.chargeStatus = ChargeStatus.PendingFinanceConfirmation;
        }
    }

    public void cancel() {
        cancel("Cancelled without specified reason", null, null);
    }

    public void cancel(String reason, UUID actorId, String actorRole) {
        if (this.status == KdsStatus.Delivered) {
            throw new IllegalStateException("Delivered orders cannot be cancelled directly; financial credit note/reversal required.");
        }
        if (this.status == KdsStatus.Cancelled) {
            return; // idempotent
        }
        this.status = KdsStatus.Cancelled;
        this.chargeStatus = ChargeStatus.Cancelled;
        this.cancellationReason = reason;
        this.cancelledAt = Instant.now();
    }
}
