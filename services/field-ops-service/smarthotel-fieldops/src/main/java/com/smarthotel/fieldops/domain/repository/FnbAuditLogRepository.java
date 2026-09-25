package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.FnbAuditLog;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.UUID;

public interface FnbAuditLogRepository extends JpaRepository<FnbAuditLog, UUID> {
    List<FnbAuditLog> findByOrderIdOrderByTimestampDesc(UUID orderId);
}
