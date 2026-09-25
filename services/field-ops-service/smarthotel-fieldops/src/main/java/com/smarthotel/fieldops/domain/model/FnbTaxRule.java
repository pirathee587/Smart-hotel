package com.smarthotel.fieldops.domain.model;

import jakarta.persistence.*;
import lombok.*;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "fnb_tax_rules")
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class FnbTaxRule {

    @Id
    @Builder.Default
    private UUID id = UUID.randomUUID();

    @Column(nullable = false)
    private String category;

    @Column(nullable = false, precision = 6, scale = 4)
    @Builder.Default
    private BigDecimal taxRate = BigDecimal.ZERO;

    @Column(nullable = false)
    @Builder.Default
    private Instant effectiveFrom = Instant.now();

    private Instant effectiveTo;

    private String description;

    @Column(nullable = false)
    @Builder.Default
    private boolean active = true;
}
