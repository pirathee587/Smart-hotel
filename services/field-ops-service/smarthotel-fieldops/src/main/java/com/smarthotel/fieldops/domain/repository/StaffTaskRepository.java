package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.StaffTask;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface StaffTaskRepository extends JpaRepository<StaffTask, UUID> {
    List<StaffTask> findByStatus(TaskStatus status);
    List<StaffTask> findByAssignedEmployeeId(UUID employeeId);
    List<StaffTask> findByHotelId(UUID hotelId);
    List<StaffTask> findByRequiredRoleAndStatus(TaskRole role, TaskStatus status);
}
