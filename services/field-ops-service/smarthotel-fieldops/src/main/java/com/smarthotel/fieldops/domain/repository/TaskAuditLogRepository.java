package com.smarthotel.fieldops.domain.repository;
import com.smarthotel.fieldops.domain.model.TaskAuditLog; import org.springframework.data.jpa.repository.JpaRepository; import java.util.*;
public interface TaskAuditLogRepository extends JpaRepository<TaskAuditLog,UUID>{ List<TaskAuditLog> findByTaskIdOrderByCreatedAtAsc(UUID taskId); }
