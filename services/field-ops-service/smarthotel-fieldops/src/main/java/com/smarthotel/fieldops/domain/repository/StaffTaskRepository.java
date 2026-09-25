package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.StaffTask;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface StaffTaskRepository extends JpaRepository<StaffTask, UUID> {
    List<StaffTask> findByStatus(TaskStatus status);
    List<StaffTask> findByAssignedEmployeeId(UUID employeeId);
    List<StaffTask> findByHotelId(UUID hotelId);
    List<StaffTask> findByRequiredRoleAndStatus(TaskRole role, TaskStatus status);
    List<StaffTask> findByDepartmentId(UUID departmentId);
    List<StaffTask> findByDepartmentIdAndAssignedEmployeeId(UUID departmentId, UUID employeeId);
    List<StaffTask> findByDepartmentIdAndStatus(UUID departmentId, TaskStatus status);
    List<StaffTask> findByDepartmentIdAndRequiredRoleAndStatus(UUID departmentId, TaskRole role, TaskStatus status);
    long countByDepartmentIdIsNull();
    @Lock(jakarta.persistence.LockModeType.PESSIMISTIC_WRITE)
    @org.springframework.data.jpa.repository.Query("select t from StaffTask t where t.id=:id")
    java.util.Optional<StaffTask> findLockedById(@org.springframework.data.repository.query.Param("id") UUID id);
}
