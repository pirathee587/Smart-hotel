package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface EmployeeProfileRepository extends JpaRepository<EmployeeProfile, UUID> {
    List<EmployeeProfile> findByRoleAndActiveTrue(TaskRole role);
    List<EmployeeProfile> findByActiveTrue();
}
