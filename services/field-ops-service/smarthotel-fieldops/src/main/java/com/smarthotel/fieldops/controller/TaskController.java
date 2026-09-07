package com.smarthotel.fieldops.controller;

import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.HousekeepingTask;
import com.smarthotel.fieldops.domain.model.MaintenanceWorkOrder;
import com.smarthotel.fieldops.domain.model.StaffTask;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.CleaningType;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskPriority;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import com.smarthotel.fieldops.dto.TaskDtos.*;
import com.smarthotel.fieldops.service.TaskService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.*;

import java.math.BigDecimal;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

@RestController
@RequestMapping("/api/v1/tasks")
@RequiredArgsConstructor
@Slf4j
public class TaskController {

    private final TaskService taskService;
    private final EmployeeProfileRepository employeeProfileRepository;

    @PostMapping("/housekeeping")
    @PreAuthorize("hasAnyRole('Admin', 'Manager', 'Housekeeper')")
    public ResponseEntity<TaskResponse> createHousekeepingTask(
            @Valid @RequestBody CreateHousekeepingTaskRequest req) {

        HousekeepingTask task = HousekeepingTask.builder()
                .title(req.title())
                .description(req.description())
                .requiredRole(TaskRole.Housekeeper)
                .priority(req.priority() != null ? req.priority() : TaskPriority.Medium)
                .cleaningType(req.cleaningType() != null ? req.cleaningType() : CleaningType.Turnover)
                .linenChanged(Boolean.TRUE.equals(req.linenChanged()))
                .floorNumber(req.floorNumber() > 0 ? req.floorNumber() : 1)
                .roomId(req.roomId())
                .roomNumber(req.roomNumber())
                .hotelId(req.hotelId())
                .build();

        boolean autoDispatch = req.autoDispatch() == null || req.autoDispatch();
        HousekeepingTask created = taskService.createHousekeepingTask(task, autoDispatch);
        return ResponseEntity.status(HttpStatus.CREATED).body(mapToTaskResponse(created));
    }

    @PostMapping("/maintenance")
    @PreAuthorize("hasAnyRole('Admin', 'Manager', 'Maintenance')")
    public ResponseEntity<TaskResponse> createMaintenanceWorkOrder(
            @Valid @RequestBody CreateMaintenanceWorkOrderRequest req) {

        MaintenanceWorkOrder order = MaintenanceWorkOrder.builder()
                .title(req.title())
                .description(req.description())
                .requiredRole(TaskRole.Maintenance)
                .priority(req.priority() != null ? req.priority() : TaskPriority.Medium)
                .assetName(req.assetName())
                .location(req.location())
                .estimatedCost(req.estimatedCost())
                .safetyHazard(Boolean.TRUE.equals(req.safetyHazard()))
                .floorNumber(req.floorNumber() > 0 ? req.floorNumber() : 1)
                .hotelId(req.hotelId())
                .build();

        boolean autoDispatch = req.autoDispatch() == null || req.autoDispatch();
        MaintenanceWorkOrder created = taskService.createMaintenanceWorkOrder(order, autoDispatch);
        return ResponseEntity.status(HttpStatus.CREATED).body(mapToTaskResponse(created));
    }

    @GetMapping("/my")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<List<TaskResponse>> getMyTasks(@AuthenticationPrincipal Jwt jwt) {
        UUID employeeId = getUserId(jwt);
        List<StaffTask> tasks = taskService.getTasks(null, null, employeeId);
        return ResponseEntity.ok(tasks.stream().map(this::mapToTaskResponse).toList());
    }

    @GetMapping
    @PreAuthorize("hasAnyRole('Admin', 'Manager')")
    public ResponseEntity<List<TaskResponse>> listTasks(
            @RequestParam(required = false) TaskStatus status,
            @RequestParam(required = false) TaskRole role,
            @RequestParam(required = false) UUID employeeId) {

        List<StaffTask> tasks = taskService.getTasks(status, role, employeeId);
        return ResponseEntity.ok(tasks.stream().map(this::mapToTaskResponse).toList());
    }

    @GetMapping("/{id}")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<TaskResponse> getTaskById(@PathVariable UUID id) {
        return taskService.getTaskById(id)
                .map(this::mapToTaskResponse)
                .map(ResponseEntity::ok)
                .orElse(ResponseEntity.notFound().build());
    }

    @PostMapping("/{id}/accept")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<TaskResponse> acceptTask(
            @PathVariable UUID id,
            @AuthenticationPrincipal Jwt jwt) {

        UUID employeeId = getUserId(jwt);
        StaffTask task = taskService.acceptTask(id, employeeId);
        return ResponseEntity.ok(mapToTaskResponse(task));
    }

    @PostMapping("/{id}/reject")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<TaskResponse> rejectTask(
            @PathVariable UUID id,
            @Valid @RequestBody RejectTaskRequest req,
            @AuthenticationPrincipal Jwt jwt) {

        UUID employeeId = getUserId(jwt);
        StaffTask task = taskService.rejectTask(id, employeeId, req.reason());
        return ResponseEntity.ok(mapToTaskResponse(task));
    }

    @PostMapping("/{id}/complete")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<TaskResponse> completeTask(
            @PathVariable UUID id,
            @RequestBody(required = false) CompleteTaskRequest req,
            @AuthenticationPrincipal Jwt jwt) {

        UUID employeeId = getUserId(jwt);
        StaffTask task = taskService.completeTask(id, employeeId);
        return ResponseEntity.ok(mapToTaskResponse(task));
    }

    @PostMapping("/{id}/dispatch")
    @PreAuthorize("hasAnyRole('Admin', 'Manager')")
    public ResponseEntity<TaskResponse> manualDispatch(@PathVariable UUID id) {
        Optional<EmployeeProfile> profile = taskService.dispatchTask(id);
        return taskService.getTaskById(id)
                .map(this::mapToTaskResponse)
                .map(ResponseEntity::ok)
                .orElse(ResponseEntity.notFound().build());
    }

    @PostMapping("/profiles")
    @PreAuthorize("hasAnyRole('Admin', 'Manager')")
    public ResponseEntity<EmployeeProfile> upsertProfile(@Valid @RequestBody UpsertEmployeeProfileRequest req) {
        EmployeeProfile profile = employeeProfileRepository.findById(req.employeeId())
                .orElse(EmployeeProfile.builder().employeeId(req.employeeId()).build());

        profile.setFullName(req.fullName());
        profile.setRole(req.role());
        if (req.hourlyRate() != null) profile.setHourlyRate(req.hourlyRate());
        if (req.currentFloor() > 0) profile.setCurrentFloor(req.currentFloor());
        if (req.bankName() != null) profile.setBankName(req.bankName());
        if (req.bankAccountNumber() != null) profile.setBankAccountNumber(req.bankAccountNumber());
        if (req.bankBranch() != null) profile.setBankBranch(req.bankBranch());

        EmployeeProfile saved = employeeProfileRepository.save(profile);
        return ResponseEntity.ok(saved);
    }

    @GetMapping("/profiles")
    @PreAuthorize("hasAnyRole('Admin', 'Manager')")
    public ResponseEntity<List<EmployeeProfile>> listProfiles() {
        return ResponseEntity.ok(employeeProfileRepository.findAll());
    }

    private UUID getUserId(Jwt jwt) {
        if (jwt != null && jwt.getSubject() != null) {
            try {
                return UUID.fromString(jwt.getSubject());
            } catch (IllegalArgumentException ignored) {}
        }
        throw new IllegalStateException("Unable to resolve authenticated employee ID from token");
    }

    private TaskResponse mapToTaskResponse(StaffTask task) {
        return TaskResponse.builder()
                .id(task.getId())
                .title(task.getTitle())
                .description(task.getDescription())
                .requiredRole(task.getRequiredRole())
                .priority(task.getPriority())
                .status(task.getStatus())
                .assignedEmployeeId(task.getAssignedEmployeeId())
                .rejectionCount(task.getRejectionCount())
                .floorNumber(task.getFloorNumber())
                .hotelId(task.getHotelId())
                .roomId(task.getRoomId())
                .roomNumber(task.getRoomNumber())
                .createdAt(task.getCreatedAt())
                .assignedAt(task.getAssignedAt())
                .completedAt(task.getCompletedAt())
                .escalatedAt(task.getEscalatedAt())
                .build();
    }
}
