package com.smarthotel.fieldops.domain.model;
import jakarta.persistence.*; import lombok.*; import java.time.Instant; import java.util.UUID;
@Entity @Table(name="leave_audit_logs", indexes=@Index(name="ix_leave_audit_request_created", columnList="leave_request_id,created_at"))
@Getter @Setter @NoArgsConstructor @AllArgsConstructor @Builder
public class LeaveAuditLog { @Id @Builder.Default private UUID id=UUID.randomUUID(); @Column(name="leave_request_id",nullable=false) private UUID leaveRequestId; @Column(name="actor_id",nullable=false) private UUID actorId; @Column(name="action",nullable=false,length=40) private String action; @Column(name="details",nullable=false,length=1000) private String details; @Column(name="created_at",nullable=false) @Builder.Default private Instant createdAt=Instant.now(); }
