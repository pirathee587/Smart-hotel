package com.smarthotel.fieldops.domain.repository;
import com.smarthotel.fieldops.domain.model.LeaveRequest; import com.smarthotel.fieldops.domain.model.LeaveRequest.Status; import org.springframework.data.jpa.repository.*; import org.springframework.data.repository.query.Param; import java.time.LocalDate; import java.util.*;
public interface LeaveRequestRepository extends JpaRepository<LeaveRequest,UUID> {
 List<LeaveRequest> findByEmployeeIdOrderByCreatedAtDesc(UUID employeeId); List<LeaveRequest> findByDepartmentIdOrderByCreatedAtDesc(UUID departmentId);
 @Query("select l from LeaveRequest l where l.employeeId=:employeeId and l.status in :statuses and l.startDate<=:end and l.endDate>=:start") List<LeaveRequest> overlapping(@Param("employeeId") UUID employeeId,@Param("start") LocalDate start,@Param("end") LocalDate end,@Param("statuses") Collection<Status> statuses);
 @Query("select l from LeaveRequest l where l.employeeId=:employeeId and l.status='Approved' and l.startDate<=:end and l.endDate>=:start") List<LeaveRequest> approvedForPeriod(@Param("employeeId") UUID employeeId,@Param("start") LocalDate start,@Param("end") LocalDate end);
}
