package com.smarthotel.fieldops.domain.model;
import jakarta.persistence.*; import lombok.*; import java.time.Instant; import java.util.UUID;
@Entity @Table(name="task_audit_logs", indexes={@Index(name="ix_task_audit_task_created",columnList="taskId,createdAt"),@Index(name="ix_task_audit_actor",columnList="actorId")})
@Getter @Setter @NoArgsConstructor @AllArgsConstructor @Builder
public class TaskAuditLog {
 @Id @Builder.Default private UUID id=UUID.randomUUID(); @Column(nullable=false) private UUID taskId; @Column(nullable=false) private UUID actorId;
 @Column(nullable=false,length=80) private String action; @Column(nullable=false,length=80) private String fromStatus; @Column(nullable=false,length=80) private String toStatus;
 @Column(length=2000) private String details; @Builder.Default @Column(nullable=false) private Instant createdAt=Instant.now();
}
