package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.TaskOutbox;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OutboxStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.time.Instant;
import java.util.List;
import java.util.UUID;

@Repository
public interface TaskOutboxRepository extends JpaRepository<TaskOutbox, UUID> {
    List<TaskOutbox> findByStatusInAndNextRetryAtLessThanEqualOrderByCreatedAtAsc(
            List<OutboxStatus> statuses, Instant cutoff);
    List<TaskOutbox> findByAggregateId(UUID aggregateId);
}
