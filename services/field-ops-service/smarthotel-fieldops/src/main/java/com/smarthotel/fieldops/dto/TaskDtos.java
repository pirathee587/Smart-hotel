package com.smarthotel.fieldops.dto;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.CleaningType;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskPriority;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.Builder;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;
import java.util.List;

public class TaskDtos {

    public record CreateHousekeepingTaskRequest(
            @NotBlank String title,
            String description,
            TaskPriority priority,
            CleaningType cleaningType,
            Boolean linenChanged,
            int floorNumber,
            UUID roomId,
            String roomNumber,
            UUID hotelId,
            UUID departmentId,
            Boolean autoDispatch
    ) {}

    public record CreateMaintenanceWorkOrderRequest(
            @NotBlank String title,
            @NotBlank String description,
            TaskPriority priority,
            @NotBlank String assetName,
            String location,
            UUID roomId,
            String roomNumber,
            UUID issueEventId,
            String severity,
            String hazardDetails,
            BigDecimal estimatedCost,
            Boolean safetyHazard,
            int floorNumber,
            UUID hotelId,
            UUID departmentId,
            Boolean autoDispatch
    ) {}

    public record RejectTaskRequest(
            @NotBlank String reason
    ) {}

    public record AssignTaskRequest(@NotNull UUID employeeId) {}
    public record InspectionRequest(boolean approved, @NotBlank String notes) {}
    public record MaintenanceVerificationRequest(boolean approved, @NotBlank String notes) {}
    public record MaintenanceCostApprovalRequest(@NotBlank String currency) {}
    public record TaskAuditResponse(UUID id, UUID actorId, String action, String fromStatus, String toStatus, String details, Instant createdAt) {}
    public record CandidateRecommendation(
            UUID employeeId,
            String fullName,
            TaskRole role,
            int activeTasksCount,
            int currentFloor,
            int proficiencyLevel,
            double skillScore,
            double proximityScore,
            double loadScore,
            double fairnessScore,
            double attendanceScore,
            double shiftAvailabilityScore,
            double qualityScore,
            double completionSpeedScore,
            double guestRatingScore,
            double rejectionScore,
            double totalScore,
            boolean recommended
    ) {}

    public record CompleteTaskRequest(
            String notes,
            Boolean inspectionPassed,
            BigDecimal actualCost,
            String partsReplaced
    ) {}

    public record UpsertEmployeeProfileRequest(
            @NotNull UUID employeeId,
            @NotBlank String fullName,
            @NotNull TaskRole role,
            UUID departmentId,
            BigDecimal hourlyRate,
            int currentFloor,
            String bankName,
            String bankAccountNumber,
            String bankBranch
    ) {}

    @Builder
    public record TaskResponse(
            UUID id,
            String title,
            String description,
            TaskRole requiredRole,
            TaskPriority priority,
            TaskStatus status,
            UUID assignedEmployeeId,
            int rejectionCount,
            int floorNumber,
            UUID hotelId,
            UUID departmentId,
            UUID roomId,
            String roomNumber,
            Instant createdAt,
            Instant assignedAt,
            Instant acceptedAt,
            Instant startedAt,
            Instant completedAt,
            Instant escalatedAt,
            UUID checkoutEventId,
            String bookingReference,
            Instant cleaningCompletedAt,
            UUID inspectedBy,
            Instant inspectedAt,
            Boolean inspectionPassed,
            String inspectionNotes,
            String recleanInstructions
            ,String assetName,String location,String severity,Boolean safetyHazard,String hazardDetails,
            BigDecimal estimatedCost,BigDecimal actualCost,String partsReplaced,String repairNotes,String reworkInstructions,
            UUID verifiedBy,Instant repairCompletedAt,Instant verifiedAt,Instant restrictionClearedAt,
            Instant costApprovedAt,UUID costApprovedBy,UUID financeExpenseId,String financeExpenseStatus,String financeFailure
    ) {}
}
