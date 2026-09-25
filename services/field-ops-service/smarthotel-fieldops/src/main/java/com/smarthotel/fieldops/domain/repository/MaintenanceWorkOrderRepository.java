package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.MaintenanceWorkOrder;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;
import java.util.Optional;

@Repository
public interface MaintenanceWorkOrderRepository extends JpaRepository<MaintenanceWorkOrder, UUID> {
    List<MaintenanceWorkOrder> findByStatus(TaskStatus status);
    Optional<MaintenanceWorkOrder> findByIssueEventId(UUID issueEventId);
}
