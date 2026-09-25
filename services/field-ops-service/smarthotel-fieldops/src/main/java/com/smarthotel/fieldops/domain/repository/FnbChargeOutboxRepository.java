package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.FnbChargeOutbox;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OutboxStatus;
import org.springframework.data.jpa.repository.JpaRepository;

import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface FnbChargeOutboxRepository extends JpaRepository<FnbChargeOutbox, UUID> {
    List<FnbChargeOutbox> findByStatusInAndNextRetryAtLessThanEqualOrderByCreatedAtAsc(
            List<OutboxStatus> statuses, Instant now);

    Optional<FnbChargeOutbox> findByOrderId(UUID orderId);
    Optional<FnbChargeOutbox> findByChargeReference(String chargeReference);
}
