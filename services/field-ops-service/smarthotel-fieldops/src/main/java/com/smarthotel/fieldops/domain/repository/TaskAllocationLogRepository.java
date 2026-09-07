package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.TaskAllocationLog;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface TaskAllocationLogRepository extends JpaRepository<TaskAllocationLog, UUID> {
    List<TaskAllocationLog> findByTaskIdOrderByAllocatedAtDesc(UUID taskId);
}
