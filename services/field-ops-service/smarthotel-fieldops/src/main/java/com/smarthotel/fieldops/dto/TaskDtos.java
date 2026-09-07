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
            Boolean autoDispatch
    ) {}

    public record CreateMaintenanceWorkOrderRequest(
            @NotBlank String title,
            String description,
            TaskPriority priority,
            @NotBlank String assetName,
            String location,
            BigDecimal estimatedCost,
            Boolean safetyHazard,
            int floorNumber,
            UUID hotelId,
            Boolean autoDispatch
    ) {}

    public record RejectTaskRequest(
            @NotBlank String reason
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
            UUID roomId,
            String roomNumber,
            Instant createdAt,
            Instant assignedAt,
            Instant completedAt,
            Instant escalatedAt
    ) {}
}
