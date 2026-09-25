package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.HousekeepingTask;
import com.smarthotel.fieldops.domain.model.MaintenanceWorkOrder;
import com.smarthotel.fieldops.domain.model.StaffTask;
import com.smarthotel.fieldops.domain.model.TaskAuditLog;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import com.smarthotel.fieldops.domain.repository.HousekeepingTaskRepository;
import com.smarthotel.fieldops.domain.repository.MaintenanceWorkOrderRepository;
import com.smarthotel.fieldops.domain.repository.StaffTaskRepository;
import com.smarthotel.fieldops.domain.repository.TaskAuditLogRepository;
import com.smarthotel.fieldops.messaging.TaskEventPublisher;
import com.smarthotel.fieldops.service.allocation.AllocationEngine;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;
import java.time.Instant;
import java.nio.charset.StandardCharsets;

@Service
@RequiredArgsConstructor
@Slf4j
public class TaskService {

    private final StaffTaskRepository staffTaskRepository;
    private final HousekeepingTaskRepository housekeepingTaskRepository;
    private final MaintenanceWorkOrderRepository maintenanceWorkOrderRepository;
    private final EmployeeProfileRepository employeeProfileRepository;
    private final AllocationEngine allocationEngine;
    private final TaskEventPublisher taskEventPublisher;
    private final TaskAuditLogRepository taskAuditLogRepository;
    private final RoomReadinessClient roomReadinessClient;
    private final MaintenanceIntegrationClient maintenanceIntegrationClient;

    // Track employee IDs who have rejected each specific task to prevent repeat allocation
    private final Map<UUID, Set<UUID>> taskRejectionRegistry = new HashMap<>();

    @Transactional
    public StaffTask createTask(StaffTask task, boolean autoDispatch) {
        requireDepartment(task);
        StaffTask savedTask = staffTaskRepository.save(task);

        if (autoDispatch) {
            dispatchTask(savedTask.getId());
        }

        return savedTask;
    }

    @Transactional
    public HousekeepingTask createHousekeepingTask(HousekeepingTask task, boolean autoDispatch) {
        requireDepartment(task);
        HousekeepingTask saved = housekeepingTaskRepository.save(task);
        if (autoDispatch) {
            dispatchTask(saved.getId());
        }
        return saved;
    }

    @Transactional
    public HousekeepingTask createTurnoverTask(HousekeepingTask task, UUID checkoutEventId, boolean autoDispatch) {
        return housekeepingTaskRepository.findByCheckoutEventId(checkoutEventId).orElseGet(() -> {
            task.setCheckoutEventId(checkoutEventId); HousekeepingTask saved=createHousekeepingTask(task,autoDispatch);
            audit(saved,checkoutEventId,"CheckoutTaskCreated",TaskStatus.Pending, saved.getStatus(), saved.getBookingReference()); return saved;
        });
    }

    @Transactional
    public HousekeepingTask createConciergeHousekeepingTask(HousekeepingTask task, UUID conciergeEventId) {
        requireDepartment(task);
        if (conciergeEventId != null) {
            var existing = housekeepingTaskRepository.findByConciergeEventId(conciergeEventId);
            if (existing.isPresent()) {
                log.info("Concierge housekeeping task with eventId {} already exists: {}", conciergeEventId, existing.get().getId());
                return existing.get();
            }
            task.setConciergeEventId(conciergeEventId);
        }
        // Force unassigned Pending status to preserve Housekeeping Manager assignment workflow
        task.setStatus(TaskStatus.Pending);
        task.setAssignedEmployeeId(null);
        HousekeepingTask saved = housekeepingTaskRepository.save(task);
        audit(saved, conciergeEventId != null ? conciergeEventId : saved.getId(), "ConciergeTaskCreated", TaskStatus.Pending, saved.getStatus(), "AI Concierge requested");
        return saved;
    }

    @Transactional
    public MaintenanceWorkOrder createConciergeMaintenanceOrder(MaintenanceWorkOrder order, UUID eventId) {
        requireDepartment(order);
        if (eventId != null) {
            var existing = maintenanceWorkOrderRepository.findByIssueEventId(eventId);
            if (existing.isPresent()) {
                log.info("Concierge maintenance work order with eventId {} already exists: {}", eventId, existing.get().getId());
                return existing.get();
            }
            order.setIssueEventId(eventId);
        }
        // Force unassigned Pending status to preserve Maintenance Manager assignment workflow
        order.setStatus(TaskStatus.Pending);
        order.setAssignedEmployeeId(null);

        // If safety hazard and room ID present, trigger restriction via integration client
        if (order.isSafetyHazard() && order.getRoomId() != null) {
            UUID restrictionEvent = stableEvent(order.getId(), "maintenance:restrict");
            order.setRestrictionEventId(restrictionEvent);
            try {
                maintenanceIntegrationClient.restrict(order.getRoomId(), order.getId(), restrictionEvent,
                        order.getDepartmentId(), eventId != null ? eventId : order.getId(),
                        order.getDescription(), order.getSeverity() != null ? order.getSeverity() : "Medium",
                        true, Instant.now(), null);
            } catch (Exception ex) {
                log.warn("Could not apply safety restriction to Hotel Ops for room {}: {}", order.getRoomId(), ex.getMessage());
            }
        }

        MaintenanceWorkOrder saved = maintenanceWorkOrderRepository.save(order);
        audit(saved, eventId != null ? eventId : saved.getId(), "ConciergeMaintenanceReported", TaskStatus.Pending, saved.getStatus(), "AI Concierge requested");
        return saved;
    }

    @Transactional public StaffTask assignTask(UUID taskId,UUID employeeId,UUID managerId,UUID departmentId){
        StaffTask task=getLockedAuthorizedTask(taskId,departmentId); if(task.getStatus()!=TaskStatus.Pending&&task.getStatus()!=TaskStatus.Escalated)throw new IllegalStateException("Only unassigned or escalated tasks can be assigned.");
        EmployeeProfile employee=employeeProfileRepository.findById(employeeId).orElseThrow(()->new IllegalArgumentException("Employee not found."));
        if(!departmentId.equals(employee.getDepartmentId())||employee.getRole()!=task.getRequiredRole())throw new SecurityException("Employee role and department must match the task.");
        TaskStatus from=task.getStatus(); task.assignTo(employeeId); employee.incrementActiveTasks(); employeeProfileRepository.save(employee); audit(task,managerId,"Assigned",from,task.getStatus(),"Assigned to "+employeeId);
        StaffTask assigned = staffTaskRepository.save(task);
        taskEventPublisher.publishTaskDispatched(assigned, employee);
        return assigned;
    }

    @Transactional
    public MaintenanceWorkOrder createMaintenanceWorkOrder(MaintenanceWorkOrder order, boolean autoDispatch, UUID actorId, String bearerToken) {
        requireDepartment(order);
        if(order.getRoomId()==null) throw new IllegalArgumentException("RoomId is required for a room-blocking Maintenance work order.");
        if(order.getIssueEventId()!=null){var existing=maintenanceWorkOrderRepository.findByIssueEventId(order.getIssueEventId());if(existing.isPresent())return existing.get();}
        UUID restrictionEvent=stableEvent(order.getId(),"maintenance:restrict"); order.setRestrictionEventId(restrictionEvent);
        maintenanceIntegrationClient.restrict(order.getRoomId(),order.getId(),restrictionEvent,order.getDepartmentId(),actorId,order.getDescription(),order.getSeverity(),order.isSafetyHazard(),Instant.now(),bearerToken);
        MaintenanceWorkOrder saved = maintenanceWorkOrderRepository.save(order);
        audit(saved,actorId,"MaintenanceIssueReported",TaskStatus.Pending,saved.getStatus(),"Hotel Ops restriction created");
        if (autoDispatch) {
            dispatchTask(saved.getId());
        }
        return saved;
    }

    @Transactional public MaintenanceWorkOrder startRepair(UUID taskId,UUID technicianId,UUID departmentId){
        StaffTask loaded=getLockedAuthorizedTask(taskId,departmentId); if(!(loaded instanceof MaintenanceWorkOrder order))throw new IllegalArgumentException("Task is not a Maintenance work order.");
        if(!Objects.equals(order.getAssignedEmployeeId(),technicianId))throw new SecurityException("Only the assigned technician can start repair work."); TaskStatus from=order.getStatus(); order.start(); audit(order,technicianId,"RepairStarted",from,order.getStatus(),null); return maintenanceWorkOrderRepository.save(order);
    }

    @Transactional public MaintenanceWorkOrder completeRepair(UUID taskId,UUID technicianId,UUID departmentId,String notes,java.math.BigDecimal actualCost,String parts){
        StaffTask loaded=getLockedAuthorizedTask(taskId,departmentId); if(!(loaded instanceof MaintenanceWorkOrder order))throw new IllegalArgumentException("Task is not a Maintenance work order.");
        if(!Objects.equals(order.getAssignedEmployeeId(),technicianId))throw new SecurityException("Only the assigned technician can complete repair work."); if(actualCost==null||actualCost.signum()<0)throw new IllegalArgumentException("Actual cost cannot be negative.");
        TaskStatus from=order.getStatus(); order.repairCompleted(notes,actualCost,parts); audit(order,technicianId,"RepairCompleted",from,order.getStatus(),notes); return maintenanceWorkOrderRepository.save(order);
    }

    @Transactional public MaintenanceWorkOrder verifyRepair(UUID taskId,UUID managerId,UUID departmentId,boolean approved,String notes,String bearerToken){
        StaffTask loaded=getLockedAuthorizedTask(taskId,departmentId); if(!(loaded instanceof MaintenanceWorkOrder order))throw new IllegalArgumentException("Task is not a Maintenance work order."); TaskStatus from=order.getStatus(); order.verifyRepair(managerId,approved,notes);
        if(approved){UUID event=stableEvent(order.getId(),"maintenance:clear:"+order.getRepairCompletedAt()); maintenanceIntegrationClient.clear(order.getRoomId(),order.getId(),event,departmentId,managerId,order.getVerifiedAt(),bearerToken); order.setClearanceEventId(event); order.setRestrictionClearedAt(Instant.now()); if(order.getAssignedEmployeeId()!=null)employeeProfileRepository.findById(order.getAssignedEmployeeId()).ifPresent(p->{p.decrementActiveTasks(true);employeeProfileRepository.save(p);});}
        audit(order,managerId,approved?"RepairVerified":"RepairRejected",from,order.getStatus(),notes);
        MaintenanceWorkOrder saved = maintenanceWorkOrderRepository.save(order);
        if (approved) taskEventPublisher.publishTaskCompleted(saved);
        return saved;
    }

    @Transactional public MaintenanceWorkOrder approveMaintenanceCost(UUID taskId,UUID managerId,UUID departmentId,String currency,String bearerToken){
        StaffTask loaded=getLockedAuthorizedTask(taskId,departmentId); if(!(loaded instanceof MaintenanceWorkOrder order))throw new IllegalArgumentException("Task is not a Maintenance work order.");
        if(order.getStatus()!=TaskStatus.InspectionApproved)throw new IllegalStateException("Only a verified repair cost can be submitted to Finance."); if(order.getActualCost()==null||order.getActualCost().signum()<=0)throw new IllegalStateException("A positive actual cost is required.");
        if(order.getFinanceExpenseId()!=null)return order;
        try{var result=maintenanceIntegrationClient.submitExpense(order.getId(),departmentId,order.getEstimatedCost(),order.getActualCost(),currency,"Maintenance work order "+order.getId()+"; estimated="+order.getEstimatedCost()+"; actual="+order.getActualCost(),bearerToken); order.setFinanceExpenseId(result.id());order.setFinanceExpenseStatus(result.status());order.setFinanceFailure(null);order.setCostApprovedAt(Instant.now());order.setCostApprovedBy(managerId);audit(order,managerId,"MaintenanceCostSubmitted",order.getStatus(),order.getStatus(),"Finance expense "+result.id());}
        catch(RuntimeException ex){order.setFinanceExpenseStatus("SubmissionFailed");order.setFinanceFailure(ex.getMessage());audit(order,managerId,"MaintenanceCostSubmissionFailed",order.getStatus(),order.getStatus(),ex.getClass().getSimpleName());}
        return maintenanceWorkOrderRepository.save(order);
    }

    @Transactional
    public Optional<EmployeeProfile> dispatchTask(UUID taskId) {
        StaffTask task = staffTaskRepository.findById(taskId)
                .orElseThrow(() -> new IllegalArgumentException("Task not found with ID: " + taskId));

        Set<UUID> excludedEmployees = taskRejectionRegistry.getOrDefault(taskId, Collections.emptySet());
        Optional<EmployeeProfile> allocated = allocationEngine.allocateTask(task, excludedEmployees);

        if (allocated.isPresent()) {
            taskEventPublisher.publishTaskDispatched(task, allocated.get());
        }

        return allocated;
    }

    @Transactional
    public Optional<EmployeeProfile> dispatchTask(UUID taskId, UUID authorizedDepartmentId) {
        StaffTask task = getAuthorizedTask(taskId, authorizedDepartmentId);
        return dispatchTask(task.getId());
    }

    @Transactional(readOnly = true)
    public List<AllocationEngine.CandidateScore> recommendCandidates(UUID taskId, UUID departmentId) {
        StaffTask task = getAuthorizedTask(taskId, departmentId);
        Set<UUID> excluded = taskRejectionRegistry.getOrDefault(taskId, Collections.emptySet());
        return allocationEngine.rankAvailableCandidates(task, excluded);
    }

    @Transactional
    public StaffTask acceptTask(UUID taskId, UUID employeeId) {
        StaffTask task = staffTaskRepository.findById(taskId)
                .orElseThrow(() -> new IllegalArgumentException("Task not found with ID: " + taskId));
        if (!Objects.equals(task.getAssignedEmployeeId(), employeeId)) throw new IllegalStateException("Task is not currently assigned to employee: " + employeeId);
        task.accept();
        return staffTaskRepository.save(task);
    }

    @Transactional
    public StaffTask acceptTask(UUID taskId, UUID employeeId, UUID departmentId) {
        StaffTask task = getLockedAuthorizedTask(taskId, departmentId);

        if (!Objects.equals(task.getAssignedEmployeeId(), employeeId)) {
            throw new IllegalStateException("Task is not currently assigned to employee: " + employeeId);
        }

        TaskStatus from=task.getStatus(); task.accept(); audit(task,employeeId,"Accepted",from,task.getStatus(),null);
        return staffTaskRepository.save(task);
    }

    @Transactional public HousekeepingTask startCleaning(UUID taskId,UUID employeeId,UUID departmentId,String bearerToken){
        StaffTask loaded=getLockedAuthorizedTask(taskId,departmentId); if(!(loaded instanceof HousekeepingTask task))throw new IllegalArgumentException("Task is not a housekeeping task.");
        if(!Objects.equals(task.getAssignedEmployeeId(),employeeId))throw new SecurityException("Only the assigned housekeeper can start cleaning."); if(task.getRoomId()==null)throw new IllegalStateException("Housekeeping task has no room.");
        TaskStatus from=task.getStatus(); UUID eventId=stableEvent(task.getId(),"start:"+(task.getInspectedAt()==null?"initial":task.getInspectedAt().toString())); task.start(); roomReadinessClient.start(task.getRoomId(),task.getId(),eventId,departmentId,employeeId,task.getStartedAt(),bearerToken);
        audit(task,employeeId,"CleaningStarted",from,task.getStatus(),"Hotel Ops room transition requested"); return housekeepingTaskRepository.save(task);
    }

    @Transactional
    public StaffTask rejectTask(UUID taskId, UUID employeeId, String reason) {
        StaffTask task = staffTaskRepository.findById(taskId)
                .orElseThrow(() -> new IllegalArgumentException("Task not found with ID: " + taskId));
        return rejectLoadedTask(task, employeeId, reason);
    }

    @Transactional
    public StaffTask rejectTask(UUID taskId, UUID employeeId, UUID departmentId, String reason) {
        StaffTask task = getAuthorizedTask(taskId, departmentId);
        return rejectLoadedTask(task, employeeId, reason);
    }

    private StaffTask rejectLoadedTask(StaffTask task, UUID employeeId, String reason) {
        UUID taskId = task.getId();

        if (!Objects.equals(task.getAssignedEmployeeId(), employeeId)) {
            throw new IllegalStateException("Task is not assigned to this employee.");
        }

        // Release current employee's active task count
        employeeProfileRepository.findById(employeeId).ifPresent(p -> {
            p.decrementActiveTasks(false);
            employeeProfileRepository.save(p);
        });

        // Record rejection in registry
        taskRejectionRegistry.computeIfAbsent(taskId, k -> new HashSet<>()).add(employeeId);

        boolean isEscalated = task.reject(reason);
        StaffTask updatedTask = staffTaskRepository.save(task);

        if (isEscalated) {
            log.warn("Task {} escalated to Manager after {} rejections! Last rejected by {}",
                    taskId, task.getRejectionCount(), employeeId);
            // Could publish escalation event
        } else {
            // Re-allocate to the next best candidate
            dispatchTask(taskId);
        }

        return updatedTask;
    }

    @Transactional
    public StaffTask completeTask(UUID taskId, UUID employeeId) {
        StaffTask task = staffTaskRepository.findById(taskId)
                .orElseThrow(() -> new IllegalArgumentException("Task not found with ID: " + taskId));
        return completeLoadedTask(task, employeeId);
    }

    @Transactional
    public StaffTask completeTask(UUID taskId, UUID employeeId, UUID departmentId) {
        StaffTask task = getLockedAuthorizedTask(taskId, departmentId);
        if (!Objects.equals(task.getAssignedEmployeeId(), employeeId)) {
            throw new IllegalStateException("Task is not assigned to this employee.");
        }
        if(task instanceof HousekeepingTask housekeeping){TaskStatus from=housekeeping.getStatus(); housekeeping.cleaningCompleted(); audit(task,employeeId,"CleaningCompleted",from,task.getStatus(),"Awaiting Manager inspection"); return housekeepingTaskRepository.save(housekeeping);}
        return completeLoadedTask(task, employeeId);
    }

    @Transactional public HousekeepingTask inspect(UUID taskId,UUID managerId,UUID departmentId,boolean approved,String notes,String bearerToken){
        StaffTask loaded=getLockedAuthorizedTask(taskId,departmentId); if(!(loaded instanceof HousekeepingTask task))throw new IllegalArgumentException("Task is not a housekeeping task."); if(task.getRoomId()==null)throw new IllegalStateException("Housekeeping task has no room.");
        TaskStatus from=task.getStatus(); UUID eventId=stableEvent(task.getId(),(approved?"approve:":"reject:")+task.getCleaningCompletedAt()); Instant now=Instant.now();
        roomReadinessClient.inspect(task.getRoomId(),task.getId(),eventId,departmentId,managerId,approved,notes,now,bearerToken);
        if(approved){task.approveInspection(managerId,notes,eventId); if(task.getAssignedEmployeeId()!=null)employeeProfileRepository.findById(task.getAssignedEmployeeId()).ifPresent(p->{p.decrementActiveTasks(true);employeeProfileRepository.save(p);});} else task.rejectInspection(managerId,notes);
        audit(task,managerId,approved?"InspectionApproved":"InspectionRejected",from,task.getStatus(),notes);
        HousekeepingTask saved = housekeepingTaskRepository.save(task);
        if (approved) taskEventPublisher.publishTaskCompleted(saved);
        return saved;
    }

    @Transactional(readOnly=true) public List<TaskAuditLog> auditHistory(UUID taskId,UUID departmentId){getAuthorizedTask(taskId,departmentId);return taskAuditLogRepository.findByTaskIdOrderByCreatedAtAsc(taskId);}

    private StaffTask completeLoadedTask(StaffTask task, UUID employeeId) {

        task.complete();
        StaffTask completed = staffTaskRepository.save(task);

        // Update employee stats
        if (task.getAssignedEmployeeId() != null) {
            employeeProfileRepository.findById(task.getAssignedEmployeeId()).ifPresent(p -> {
                p.decrementActiveTasks(true);
                employeeProfileRepository.save(p);
            });
        }

        taskEventPublisher.publishTaskCompleted(completed);
        return completed;
    }

    @Transactional(readOnly = true)
    public List<StaffTask> getTasks(TaskStatus status, TaskRole role, UUID employeeId) {
        if (employeeId != null) {
            return staffTaskRepository.findByAssignedEmployeeId(employeeId);
        }
        if (role != null && status != null) {
            return staffTaskRepository.findByRequiredRoleAndStatus(role, status);
        }
        if (status != null) {
            return staffTaskRepository.findByStatus(status);
        }
        return staffTaskRepository.findAll();
    }

    @Transactional(readOnly = true)
    public List<StaffTask> getTasks(UUID departmentId, TaskStatus status, TaskRole role, UUID employeeId) {
        if (departmentId == null) return List.of();
        if (employeeId != null) return staffTaskRepository.findByDepartmentIdAndAssignedEmployeeId(departmentId, employeeId);
        if (role != null && status != null) return staffTaskRepository.findByDepartmentIdAndRequiredRoleAndStatus(departmentId, role, status);
        if (status != null) return staffTaskRepository.findByDepartmentIdAndStatus(departmentId, status);
        return staffTaskRepository.findByDepartmentId(departmentId);
    }

    @Transactional(readOnly = true)
    public Optional<StaffTask> getTaskById(UUID taskId) {
        return staffTaskRepository.findById(taskId);
    }

    @Transactional(readOnly = true)
    public Optional<StaffTask> getTaskById(UUID taskId, UUID departmentId) {
        return staffTaskRepository.findById(taskId).filter(task -> Objects.equals(task.getDepartmentId(), departmentId));
    }

    private StaffTask getAuthorizedTask(UUID taskId, UUID departmentId) {
        if (departmentId == null) throw new SecurityException("Authenticated employee has no department assignment.");
        StaffTask task = staffTaskRepository.findById(taskId)
                .orElseThrow(() -> new IllegalArgumentException("Task not found with ID: " + taskId));
        if (!Objects.equals(task.getDepartmentId(), departmentId)) throw new SecurityException("Cross-department task access is forbidden.");
        return task;
    }
    private StaffTask getLockedAuthorizedTask(UUID taskId,UUID departmentId){if(departmentId==null)throw new SecurityException("Authenticated employee has no department assignment."); StaffTask task=staffTaskRepository.findLockedById(taskId).orElseThrow(()->new IllegalArgumentException("Task not found with ID: "+taskId)); if(!Objects.equals(task.getDepartmentId(),departmentId))throw new SecurityException("Cross-department task access is forbidden."); return task;}
    private void audit(StaffTask task,UUID actor,String action,TaskStatus from,TaskStatus to,String details){taskAuditLogRepository.save(TaskAuditLog.builder().taskId(task.getId()).actorId(actor).action(action).fromStatus(from.name()).toStatus(to.name()).details(details).build());}
    private static UUID stableEvent(UUID taskId,String stage){return UUID.nameUUIDFromBytes((taskId+":"+stage).getBytes(StandardCharsets.UTF_8));}

    private void requireDepartment(StaffTask task) {
        if (task.getDepartmentId() == null) throw new IllegalArgumentException("DepartmentId is required for new tasks.");
    }
}
