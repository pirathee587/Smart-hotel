package com.smarthotel.fieldops.domain.repository;
import com.smarthotel.fieldops.domain.model.LeaveAuditLog; import org.springframework.data.jpa.repository.JpaRepository; import java.util.UUID;
public interface LeaveAuditLogRepository extends JpaRepository<LeaveAuditLog,UUID> {}
