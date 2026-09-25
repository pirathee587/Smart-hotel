package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;
import org.springframework.data.jpa.repository.Lock;
import jakarta.persistence.LockModeType;
import java.util.Optional;

import java.util.List;
import java.util.UUID;

@Repository
public interface EmployeeProfileRepository extends JpaRepository<EmployeeProfile, UUID> {
    List<EmployeeProfile> findByRoleAndActiveTrue(TaskRole role);
    List<EmployeeProfile> findByActiveTrue();
    List<EmployeeProfile> findByRoleAndDepartmentIdAndActiveTrue(TaskRole role, UUID departmentId);
    List<EmployeeProfile> findByDepartmentId(UUID departmentId);
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<EmployeeProfile> findLockedByEmployeeId(UUID employeeId);
}
