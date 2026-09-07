package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.KdsOrder;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.KdsStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

@Repository
public interface KdsOrderRepository extends JpaRepository<KdsOrder, UUID> {
    List<KdsOrder> findByStatusInOrderByReceivedAtAsc(List<KdsStatus> statuses);
    Optional<KdsOrder> findByOrderNumber(String orderNumber);
}
