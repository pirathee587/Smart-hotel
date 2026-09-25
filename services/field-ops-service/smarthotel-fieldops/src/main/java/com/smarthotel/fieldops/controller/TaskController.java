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
import org.springframework.http.HttpHeaders;

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
    @PreAuthorize("hasRole('Owner') or (hasAnyRole('Admin', 'Manager') and #jwt.claims['departmentCode'] == 'HOUSEKEEPING')")
    public ResponseEntity<TaskResponse> createHousekeepingTask(
            @Valid @RequestBody CreateHousekeepingTaskRequest req,
            @AuthenticationPrincipal Jwt jwt) {

        UUID departmentId = resolveDepartment(jwt, req.departmentId());

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
                .departmentId(departmentId)
                .build();

        boolean autoDispatch = req.autoDispatch() == null || req.autoDispatch();
        HousekeepingTask created = taskService.createHousekeepingTask(task, autoDispatch);
        return ResponseEntity.status(HttpStatus.CREATED).body(mapToTaskResponse(created));
    }

    @PostMapping("/maintenance")
    @PreAuthorize("hasRole('Owner') or (hasAnyRole('Admin', 'Manager') and #jwt.claims['departmentCode'] == 'MAINTENANCE')")
    public ResponseEntity<TaskResponse> createMaintenanceWorkOrder(
            @Valid @RequestBody CreateMaintenanceWorkOrderRequest req,
            @AuthenticationPrincipal Jwt jwt,@RequestHeader(value=HttpHeaders.AUTHORIZATION,required=false) String authorization) {

        UUID departmentId = resolveDepartment(jwt, req.departmentId());

        MaintenanceWorkOrder order = MaintenanceWorkOrder.builder()
                .title(req.title())
                .description(req.description())
                .requiredRole(TaskRole.Maintenance)
                .priority(req.priority() != null ? req.priority() : TaskPriority.Medium)
                .assetName(req.assetName())
                .location(req.location())
                .roomId(req.roomId())
                .roomNumber(req.roomNumber())
                .issueEventId(req.issueEventId())
                .severity(req.severity()==null?"Medium":req.severity())
                .hazardDetails(req.hazardDetails())
                .estimatedCost(req.estimatedCost())
                .safetyHazard(Boolean.TRUE.equals(req.safetyHazard()))
                .floorNumber(req.floorNumber() > 0 ? req.floorNumber() : 1)
                .hotelId(req.hotelId())
                .departmentId(departmentId)
                .build();

        boolean autoDispatch = req.autoDispatch() == null || req.autoDispatch();
        MaintenanceWorkOrder created = taskService.createMaintenanceWorkOrder(order, autoDispatch,getUserId(jwt),authorization);
        return ResponseEntity.status(HttpStatus.CREATED).body(mapToTaskResponse(created));
    }

    @GetMapping("/my")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<List<TaskResponse>> getMyTasks(@AuthenticationPrincipal Jwt jwt) {
        UUID employeeId = getUserId(jwt);
        UUID departmentId = getDepartmentId(jwt);
        List<StaffTask> tasks = taskService.getTasks(departmentId, null, null, employeeId);
        return ResponseEntity.ok(tasks.stream().map(this::mapToTaskResponse).toList());
    }

    @GetMapping
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<List<TaskResponse>> listTasks(
            @RequestParam(required = false) TaskStatus status,
            @RequestParam(required = false) TaskRole role,
            @RequestParam(required = false) UUID employeeId,
            @AuthenticationPrincipal Jwt jwt) {

        List<StaffTask> tasks = isOwner(jwt)
                ? taskService.getTasks(status, role, employeeId)
                : taskService.getTasks(getDepartmentId(jwt), status, role, employeeId);
        return ResponseEntity.ok(tasks.stream().map(this::mapToTaskResponse).toList());
    }

    @GetMapping("/{id}/recommendations")
    @PreAuthorize("hasRole('Owner') or hasAnyRole('Admin','Manager')")
    public ResponseEntity<List<CandidateRecommendation>> recommendations(
            @PathVariable UUID id,
            @AuthenticationPrincipal Jwt jwt) {
        UUID departmentId = isOwner(jwt)
                ? taskService.getTaskById(id).map(StaffTask::getDepartmentId).orElseThrow(() -> new IllegalArgumentException("Task not found."))
                : getDepartmentId(jwt);
        var scores = taskService.recommendCandidates(id, departmentId);
        List<CandidateRecommendation> response = new java.util.ArrayList<>();
        for (int index = 0; index < scores.size(); index++) {
            var score = scores.get(index);
            var employee = score.employee();
            response.add(new CandidateRecommendation(
                    employee.getEmployeeId(), employee.getFullName(), employee.getRole(),
                    employee.getActiveTasksCount(), employee.getCurrentFloor(), employee.getProficiencyLevel(),
                    score.skillScore(), score.proximityScore(), score.loadScore(), score.fairnessScore(),
                    score.attendanceScore(), score.shiftAvailabilityScore(), score.qualityScore(),
                    score.completionSpeedScore(), score.guestRatingScore(), score.rejectionScore(),
                    score.totalScore(), index == 0));
        }
        return ResponseEntity.ok(response);
    }

    @GetMapping("/{id}")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<TaskResponse> getTaskById(@PathVariable UUID id, @AuthenticationPrincipal Jwt jwt) {
        Optional<StaffTask> task = isOwner(jwt) ? taskService.getTaskById(id) : taskService.getTaskById(id, getDepartmentId(jwt));
        return task
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
        StaffTask task = taskService.acceptTask(id, employeeId, getDepartmentId(jwt));
        return ResponseEntity.ok(mapToTaskResponse(task));
    }

    @PostMapping("/{id}/start-cleaning")
    @PreAuthorize("hasRole('Housekeeper') and #jwt.claims['departmentCode'] == 'HOUSEKEEPING'")
    public ResponseEntity<TaskResponse> startCleaning(@PathVariable UUID id,@AuthenticationPrincipal Jwt jwt,@RequestHeader(value = HttpHeaders.AUTHORIZATION, required = false) String authorization){return ResponseEntity.ok(mapToTaskResponse(taskService.startCleaning(id,getUserId(jwt),getDepartmentId(jwt),authorization)));}

    @PostMapping("/{id}/assign")
    @PreAuthorize("hasRole('Owner') or (hasAnyRole('Admin','Manager') and (#jwt.claims['departmentCode'] == 'HOUSEKEEPING' or #jwt.claims['departmentCode'] == 'MAINTENANCE'))")
    public ResponseEntity<TaskResponse> assign(@PathVariable UUID id,@Valid @RequestBody AssignTaskRequest req,@AuthenticationPrincipal Jwt jwt){
        UUID deptId = isOwner(jwt) ? taskService.getTaskById(id).map(StaffTask::getDepartmentId).orElse(null) : getDepartmentId(jwt);
        return ResponseEntity.ok(mapToTaskResponse(taskService.assignTask(id,req.employeeId(),getUserId(jwt),deptId)));
    }

    @PostMapping("/{id}/inspection")
    @PreAuthorize("hasRole('Owner') or (hasRole('Manager') and #jwt.claims['departmentCode'] == 'HOUSEKEEPING')")
    public ResponseEntity<TaskResponse> inspect(@PathVariable UUID id,@Valid @RequestBody InspectionRequest req,@AuthenticationPrincipal Jwt jwt,@RequestHeader(value = HttpHeaders.AUTHORIZATION, required = false) String authorization){
        UUID deptId = isOwner(jwt) ? taskService.getTaskById(id).map(StaffTask::getDepartmentId).orElse(null) : getDepartmentId(jwt);
        return ResponseEntity.ok(mapToTaskResponse(taskService.inspect(id,getUserId(jwt),deptId,req.approved(),req.notes(),authorization)));
    }

    @GetMapping("/{id}/audit")
    @PreAuthorize("hasRole('Owner') or (hasAnyRole('Admin','Manager') and (#jwt.claims['departmentCode'] == 'HOUSEKEEPING' or #jwt.claims['departmentCode'] == 'MAINTENANCE'))")
    public ResponseEntity<List<TaskAuditResponse>> audit(@PathVariable UUID id,@AuthenticationPrincipal Jwt jwt){
        UUID deptId = isOwner(jwt) ? taskService.getTaskById(id).map(StaffTask::getDepartmentId).orElse(null) : getDepartmentId(jwt);
        return ResponseEntity.ok(taskService.auditHistory(id,deptId).stream().map(a->new TaskAuditResponse(a.getId(),a.getActorId(),a.getAction(),a.getFromStatus(),a.getToStatus(),a.getDetails(),a.getCreatedAt())).toList());
    }

    @PostMapping("/{id}/reject")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<TaskResponse> rejectTask(
            @PathVariable UUID id,
            @Valid @RequestBody RejectTaskRequest req,
            @AuthenticationPrincipal Jwt jwt) {

        UUID employeeId = getUserId(jwt);
        StaffTask task = taskService.rejectTask(id, employeeId, getDepartmentId(jwt), req.reason());
        return ResponseEntity.ok(mapToTaskResponse(task));
    }

    @PostMapping("/{id}/complete")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<TaskResponse> completeTask(
            @PathVariable UUID id,
            @RequestBody(required = false) CompleteTaskRequest req,
            @AuthenticationPrincipal Jwt jwt) {

        UUID employeeId = getUserId(jwt);
        StaffTask task;
        if (req != null && "MAINTENANCE".equals(String.valueOf(jwt.getClaims().get("departmentCode"))))
            task=taskService.completeRepair(id,employeeId,getDepartmentId(jwt),req.notes(),req.actualCost(),req.partsReplaced());
        else task = taskService.completeTask(id, employeeId, getDepartmentId(jwt));
        return ResponseEntity.ok(mapToTaskResponse(task));
    }

    @PostMapping("/{id}/start-repair")
    @PreAuthorize("hasRole('Maintenance') and #jwt.claims['departmentCode'] == 'MAINTENANCE'")
    public ResponseEntity<TaskResponse> startRepair(@PathVariable UUID id,@AuthenticationPrincipal Jwt jwt){return ResponseEntity.ok(mapToTaskResponse(taskService.startRepair(id,getUserId(jwt),getDepartmentId(jwt))));}

    @PostMapping("/{id}/maintenance-verification")
    @PreAuthorize("hasRole('Owner') or (hasRole('Manager') and #jwt.claims['departmentCode'] == 'MAINTENANCE')")
    public ResponseEntity<TaskResponse> verifyRepair(@PathVariable UUID id,@Valid @RequestBody MaintenanceVerificationRequest req,@AuthenticationPrincipal Jwt jwt,@RequestHeader(value = HttpHeaders.AUTHORIZATION, required = false) String authorization){
        UUID deptId = isOwner(jwt) ? taskService.getTaskById(id).map(StaffTask::getDepartmentId).orElse(null) : getDepartmentId(jwt);
        return ResponseEntity.ok(mapToTaskResponse(taskService.verifyRepair(id,getUserId(jwt),deptId,req.approved(),req.notes(),authorization)));
    }

    @PostMapping("/{id}/maintenance-cost-approval")
    @PreAuthorize("hasRole('Owner') or (hasRole('Manager') and #jwt.claims['departmentCode'] == 'MAINTENANCE')")
    public ResponseEntity<TaskResponse> approveCost(@PathVariable UUID id,@Valid @RequestBody MaintenanceCostApprovalRequest req,@AuthenticationPrincipal Jwt jwt,@RequestHeader(value = HttpHeaders.AUTHORIZATION, required = false) String authorization){
        UUID deptId = isOwner(jwt) ? taskService.getTaskById(id).map(StaffTask::getDepartmentId).orElse(null) : getDepartmentId(jwt);
        return ResponseEntity.ok(mapToTaskResponse(taskService.approveMaintenanceCost(id,getUserId(jwt),deptId,req.currency(),authorization)));
    }

    @PostMapping("/{id}/dispatch")
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<TaskResponse> manualDispatch(@PathVariable UUID id, @AuthenticationPrincipal Jwt jwt) {
        Optional<EmployeeProfile> profile = isOwner(jwt) ? taskService.dispatchTask(id) : taskService.dispatchTask(id, getDepartmentId(jwt));
        Optional<StaffTask> task = isOwner(jwt) ? taskService.getTaskById(id) : taskService.getTaskById(id, getDepartmentId(jwt));
        return task
                .map(this::mapToTaskResponse)
                .map(ResponseEntity::ok)
                .orElse(ResponseEntity.notFound().build());
    }

    @PostMapping("/profiles")
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<EmployeeProfile> upsertProfile(@Valid @RequestBody UpsertEmployeeProfileRequest req, @AuthenticationPrincipal Jwt jwt) {
        UUID departmentId = resolveDepartment(jwt, req.departmentId());
        EmployeeProfile profile = employeeProfileRepository.findById(req.employeeId())
                .orElse(EmployeeProfile.builder().employeeId(req.employeeId()).build());

        profile.setFullName(req.fullName());
        profile.setRole(req.role());
        profile.setDepartmentId(departmentId);
        if (req.hourlyRate() != null) profile.setHourlyRate(req.hourlyRate());
        if (req.currentFloor() > 0) profile.setCurrentFloor(req.currentFloor());
        if (req.bankName() != null) profile.setBankName(req.bankName());
        if (req.bankAccountNumber() != null) profile.setBankAccountNumber(req.bankAccountNumber());
        if (req.bankBranch() != null) profile.setBankBranch(req.bankBranch());

        EmployeeProfile saved = employeeProfileRepository.save(profile);
        return ResponseEntity.ok(saved);
    }

    @GetMapping("/profiles")
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<List<EmployeeProfile>> listProfiles(@AuthenticationPrincipal Jwt jwt) {
        if (isOwner(jwt)) {
            return ResponseEntity.ok(employeeProfileRepository.findAll());
        }
        return ResponseEntity.ok(employeeProfileRepository.findByDepartmentId(getDepartmentId(jwt)));
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
                .departmentId(task.getDepartmentId())
                .roomId(task.getRoomId())
                .roomNumber(task.getRoomNumber())
                .createdAt(task.getCreatedAt())
                .assignedAt(task.getAssignedAt())
                .acceptedAt(task.getAcceptedAt())
                .startedAt(task.getStartedAt())
                .completedAt(task.getCompletedAt())
                .escalatedAt(task.getEscalatedAt())
                .checkoutEventId(task instanceof HousekeepingTask h ? h.getCheckoutEventId() : null)
                .bookingReference(task instanceof HousekeepingTask h ? h.getBookingReference() : null)
                .cleaningCompletedAt(task instanceof HousekeepingTask h ? h.getCleaningCompletedAt() : null)
                .inspectedBy(task instanceof HousekeepingTask h ? h.getInspectedBy() : null)
                .inspectedAt(task instanceof HousekeepingTask h ? h.getInspectedAt() : null)
                .inspectionPassed(task instanceof HousekeepingTask h ? h.getInspectionPassed() : null)
                .inspectionNotes(task instanceof HousekeepingTask h ? h.getInspectionNotes() : null)
                .recleanInstructions(task instanceof HousekeepingTask h ? h.getRecleanInstructions() : null)
                .assetName(task instanceof MaintenanceWorkOrder m ? m.getAssetName() : null)
                .location(task instanceof MaintenanceWorkOrder m ? m.getLocation() : null)
                .severity(task instanceof MaintenanceWorkOrder m ? m.getSeverity() : null)
                .safetyHazard(task instanceof MaintenanceWorkOrder m ? m.isSafetyHazard() : null)
                .hazardDetails(task instanceof MaintenanceWorkOrder m ? m.getHazardDetails() : null)
                .estimatedCost(task instanceof MaintenanceWorkOrder m ? m.getEstimatedCost() : null)
                .actualCost(task instanceof MaintenanceWorkOrder m ? m.getActualCost() : null)
                .partsReplaced(task instanceof MaintenanceWorkOrder m ? m.getPartsReplaced() : null)
                .repairNotes(task instanceof MaintenanceWorkOrder m ? m.getRepairNotes() : null)
                .reworkInstructions(task instanceof MaintenanceWorkOrder m ? m.getReworkInstructions() : null)
                .verifiedBy(task instanceof MaintenanceWorkOrder m ? m.getVerifiedBy() : null)
                .repairCompletedAt(task instanceof MaintenanceWorkOrder m ? m.getRepairCompletedAt() : null)
                .verifiedAt(task instanceof MaintenanceWorkOrder m ? m.getVerifiedAt() : null)
                .restrictionClearedAt(task instanceof MaintenanceWorkOrder m ? m.getRestrictionClearedAt() : null)
                .costApprovedAt(task instanceof MaintenanceWorkOrder m ? m.getCostApprovedAt() : null)
                .costApprovedBy(task instanceof MaintenanceWorkOrder m ? m.getCostApprovedBy() : null)
                .financeExpenseId(task instanceof MaintenanceWorkOrder m ? m.getFinanceExpenseId() : null)
                .financeExpenseStatus(task instanceof MaintenanceWorkOrder m ? m.getFinanceExpenseStatus() : null)
                .financeFailure(task instanceof MaintenanceWorkOrder m ? m.getFinanceFailure() : null)
                .build();
    }

    private UUID getDepartmentId(Jwt jwt) {
        Object claim = jwt != null ? jwt.getClaims().get("departmentId") : null;
        if (claim == null) throw new SecurityException("Authenticated employee has no department assignment.");
        try { return UUID.fromString(claim.toString()); }
        catch (IllegalArgumentException ex) { throw new SecurityException("Authenticated employee has an invalid department assignment."); }
    }

    private UUID resolveDepartment(Jwt jwt, UUID requestedDepartmentId) {
        if (isOwner(jwt)) {
            if (requestedDepartmentId == null) throw new IllegalArgumentException("DepartmentId is required.");
            return requestedDepartmentId;
        }
        UUID authenticatedDepartmentId = getDepartmentId(jwt);
        if (requestedDepartmentId != null && !requestedDepartmentId.equals(authenticatedDepartmentId)) {
            throw new SecurityException("Cross-department task access is forbidden.");
        }
        return authenticatedDepartmentId;
    }

    private boolean isOwner(Jwt jwt) {
        Object role = jwt != null ? jwt.getClaims().get("role") : null;
        return role != null && "Owner".equals(role.toString());
    }
}
