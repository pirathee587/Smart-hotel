package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.KdsStatus;
import jakarta.persistence.*;
import lombok.*;

import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "kds_orders")
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

    private String tableOrRoomNumber;

    @Column(columnDefinition = "TEXT")
    private String itemsJson;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private KdsStatus status = KdsStatus.Received;

    private UUID cookEmployeeId;

    @Builder.Default
    private Instant receivedAt = Instant.now();

    private Instant preparingAt;

    private Instant readyAt;

    private Instant deliveredAt;

    public void startPreparing(UUID cookId) {
        this.status = KdsStatus.Preparing;
        this.cookEmployeeId = cookId;
        this.preparingAt = Instant.now();
    }

    public void markReady() {
        this.status = KdsStatus.Ready;
        this.readyAt = Instant.now();
    }

    public void markDelivered() {
        this.status = KdsStatus.Delivered;
        this.deliveredAt = Instant.now();
    }

    public void cancel() {
        this.status = KdsStatus.Cancelled;
    }
}
