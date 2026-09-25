package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.MonthlyAttendanceSummary;
import com.smarthotel.fieldops.domain.model.MonthlyAttendanceSummary.VerificationStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import java.util.*;

public interface MonthlyAttendanceSummaryRepository extends JpaRepository<MonthlyAttendanceSummary, UUID> {
    List<MonthlyAttendanceSummary> findByDepartmentIdAndPayrollYearAndPayrollMonthOrderByEmployeeIdAscSummaryVersionDesc(UUID departmentId, int year, int month);
    List<MonthlyAttendanceSummary> findByVerificationStatusOrderByVerifiedAtDesc(VerificationStatus status);
    Optional<MonthlyAttendanceSummary> findTopByEmployeeIdAndPayrollYearAndPayrollMonthOrderBySummaryVersionDesc(UUID employeeId, int year, int month);
    Optional<MonthlyAttendanceSummary> findByIdAndDepartmentId(UUID id, UUID departmentId);
}
