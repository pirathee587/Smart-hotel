package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.HousekeepingTask;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface HousekeepingTaskRepository extends JpaRepository<HousekeepingTask, UUID> {
    List<HousekeepingTask> findByRoomIdAndStatus(UUID roomId, TaskStatus status);
    List<HousekeepingTask> findByStatus(TaskStatus status);
}
