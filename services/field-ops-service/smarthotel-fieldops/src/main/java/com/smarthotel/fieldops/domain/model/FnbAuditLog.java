package com.smarthotel.fieldops.domain.model;

import jakarta.persistence.*;
import lombok.*;

import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "fnb_audit_logs", indexes = {
    @Index(name = "idx_fnb_audit_order_id", columnList = "orderId"),
    @Index(name = "idx_fnb_audit_timestamp", columnList = "timestamp")
})
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class FnbAuditLog {

    @Id
    @Builder.Default
    private UUID id = UUID.randomUUID();

    @Column(nullable = false)
    private UUID orderId;

    @Column(nullable = false)
    private String orderNumber;

    @Column(nullable = false)
    private String action;

    private UUID actorUserId;

    private String actorRole;

    @Column(columnDefinition = "TEXT")
    private String details;

    private String reason;

    private String previousState;

    private String newState;

    @Column(nullable = false)
    @Builder.Default
    private Instant timestamp = Instant.now();
}
