package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.OvertimeApproval;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OvertimeStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

@Repository
public interface OvertimeApprovalRepository extends JpaRepository<OvertimeApproval, UUID> {
    List<OvertimeApproval> findByStatus(OvertimeStatus status);
    List<OvertimeApproval> findByEmployeeIdAndStatus(UUID employeeId, OvertimeStatus status);
    Optional<OvertimeApproval> findByAttendanceRecordId(UUID attendanceRecordId);
}
