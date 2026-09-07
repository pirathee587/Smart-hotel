package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.HousekeepingTask;
import com.smarthotel.fieldops.domain.model.MaintenanceWorkOrder;
import com.smarthotel.fieldops.domain.model.StaffTask;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import com.smarthotel.fieldops.domain.repository.HousekeepingTaskRepository;
import com.smarthotel.fieldops.domain.repository.MaintenanceWorkOrderRepository;
import com.smarthotel.fieldops.domain.repository.StaffTaskRepository;
import com.smarthotel.fieldops.messaging.TaskEventPublisher;
import com.smarthotel.fieldops.service.allocation.AllocationEngine;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;

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

    // Track employee IDs who have rejected each specific task to prevent repeat allocation
    private final Map<UUID, Set<UUID>> taskRejectionRegistry = new HashMap<>();

    @Transactional
    public StaffTask createTask(StaffTask task, boolean autoDispatch) {
        StaffTask savedTask = staffTaskRepository.save(task);

        if (autoDispatch) {
            dispatchTask(savedTask.getId());
        }

        return savedTask;
    }

    @Transactional
    public HousekeepingTask createHousekeepingTask(HousekeepingTask task, boolean autoDispatch) {
        HousekeepingTask saved = housekeepingTaskRepository.save(task);
        if (autoDispatch) {
            dispatchTask(saved.getId());
        }
        return saved;
    }

    @Transactional
    public MaintenanceWorkOrder createMaintenanceWorkOrder(MaintenanceWorkOrder order, boolean autoDispatch) {
        MaintenanceWorkOrder saved = maintenanceWorkOrderRepository.save(order);
        if (autoDispatch) {
            dispatchTask(saved.getId());
        }
        return saved;
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
    public StaffTask acceptTask(UUID taskId, UUID employeeId) {
        StaffTask task = staffTaskRepository.findById(taskId)
                .orElseThrow(() -> new IllegalArgumentException("Task not found with ID: " + taskId));

        if (!Objects.equals(task.getAssignedEmployeeId(), employeeId)) {
            throw new IllegalStateException("Task is not currently assigned to employee: " + employeeId);
        }

        task.accept();
        return staffTaskRepository.save(task);
    }

    @Transactional
    public StaffTask rejectTask(UUID taskId, UUID employeeId, String reason) {
        StaffTask task = staffTaskRepository.findById(taskId)
                .orElseThrow(() -> new IllegalArgumentException("Task not found with ID: " + taskId));

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
    public Optional<StaffTask> getTaskById(UUID taskId) {
        return staffTaskRepository.findById(taskId);
    }
}
