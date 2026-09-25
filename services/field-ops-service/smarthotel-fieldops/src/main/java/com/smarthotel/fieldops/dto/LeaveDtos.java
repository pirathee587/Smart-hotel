package com.smarthotel.fieldops.dto;
import com.smarthotel.fieldops.domain.model.LeaveRequest.Status; import jakarta.validation.constraints.*; import java.time.*; import java.util.UUID;
public class LeaveDtos {
 public record PolicyRequest(@NotBlank String leaveType, boolean paid, @NotNull LocalDate effectiveFrom, LocalDate effectiveTo) {}
 public record PolicyResponse(UUID id,String leaveType,boolean paid,LocalDate effectiveFrom,LocalDate effectiveTo,boolean active) {}
 public record SubmitRequest(@NotNull UUID policyId,@NotNull LocalDate startDate,@NotNull LocalDate endDate,@NotBlank @Size(max=1000) String reason) {}
 public record DecisionRequest(boolean approve,@NotBlank @Size(max=1000) String reason) {}
 public record LeaveResponse(UUID id,UUID employeeId,UUID departmentId,UUID policyId,String leaveType,boolean paid,LocalDate startDate,LocalDate endDate,String reason,Status status,UUID decisionManagerId,String decisionReason,Instant decidedAt,Instant createdAt,long rowVersion) {}
}
