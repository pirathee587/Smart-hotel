package com.smarthotel.fieldops.domain.model;

import jakarta.persistence.*;
import lombok.*;
import java.time.*;
import java.util.UUID;

@Entity @Table(name="leave_policies", uniqueConstraints=@UniqueConstraint(name="uq_leave_policy_type_effective", columnNames={"leave_type","effective_from"}), indexes=@Index(name="ix_leave_policy_active", columnList="leave_type,active,effective_from"))
@Getter @Setter @NoArgsConstructor @AllArgsConstructor @Builder
public class LeavePolicy {
    @Id @Builder.Default private UUID id = UUID.randomUUID();
    @Column(name="leave_type", nullable=false, length=60) private String leaveType;
    @Column(name="paid", nullable=false) private boolean paid;
    @Column(name="effective_from", nullable=false) private LocalDate effectiveFrom;
    @Column(name="effective_to") private LocalDate effectiveTo;
    @Column(name="active", nullable=false) @Builder.Default private boolean active = true;
    @Column(name="configured_by_owner_id", nullable=false) private UUID configuredByOwnerId;
    @Column(name="created_at", nullable=false) @Builder.Default private Instant createdAt = Instant.now();
}
