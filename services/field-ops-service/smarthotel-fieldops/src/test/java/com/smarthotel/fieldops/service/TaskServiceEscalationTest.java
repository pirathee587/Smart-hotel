package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.StaffTask;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskPriority;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import com.smarthotel.fieldops.domain.repository.HousekeepingTaskRepository;
import com.smarthotel.fieldops.domain.repository.MaintenanceWorkOrderRepository;
import com.smarthotel.fieldops.domain.repository.StaffTaskRepository;
import com.smarthotel.fieldops.messaging.TaskEventPublisher;
import com.smarthotel.fieldops.service.allocation.AllocationEngine;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class TaskServiceEscalationTest {

    @Mock
    private StaffTaskRepository staffTaskRepository;

    @Mock
    private HousekeepingTaskRepository housekeepingTaskRepository;

    @Mock
    private MaintenanceWorkOrderRepository maintenanceWorkOrderRepository;

    @Mock
    private EmployeeProfileRepository employeeProfileRepository;

    @Mock
    private AllocationEngine allocationEngine;

    @Mock
    private TaskEventPublisher taskEventPublisher;

    private TaskService taskService;

    @BeforeEach
    void setUp() {
        taskService = new TaskService(
                staffTaskRepository,
                housekeepingTaskRepository,
                maintenanceWorkOrderRepository,
                employeeProfileRepository,
                allocationEngine,
                taskEventPublisher
        );
    }

    @Test
    @DisplayName("Task escalation: 3rd rejection triggers Escalated status and halts auto-redispatch")
    void threeRejections_TriggersEscalation() {
        UUID taskId = UUID.randomUUID();
        UUID emp1 = UUID.randomUUID();
        UUID emp2 = UUID.randomUUID();
        UUID emp3 = UUID.randomUUID();

        StaffTask task = StaffTask.builder()
                .id(taskId)
                .title("Urgent Pipe Leak")
                .requiredRole(TaskRole.Maintenance)
                .priority(TaskPriority.Urgent)
                .status(TaskStatus.Assigned)
                .assignedEmployeeId(emp1)
                .rejectionCount(0)
                .build();

        when(staffTaskRepository.findById(taskId)).thenReturn(Optional.of(task));
        when(staffTaskRepository.save(any(StaffTask.class))).thenAnswer(i -> i.getArgument(0));

        // 1st Rejection
        StaffTask res1 = taskService.rejectTask(taskId, emp1, "Busy on emergency");
        assertThat(res1.getRejectionCount()).isEqualTo(1);
        assertThat(res1.getStatus()).isEqualTo(TaskStatus.Pending);
        verify(allocationEngine, times(1)).allocateTask(eq(task), any());

        // Re-assign to emp2 for 2nd rejection test
        task.assignTo(emp2);

        // 2nd Rejection
        StaffTask res2 = taskService.rejectTask(taskId, emp2, "No spare parts");
        assertThat(res2.getRejectionCount()).isEqualTo(2);
        assertThat(res2.getStatus()).isEqualTo(TaskStatus.Pending);
        verify(allocationEngine, times(2)).allocateTask(eq(task), any());

        // Re-assign to emp3 for 3rd rejection test
        task.assignTo(emp3);

        // 3rd Rejection -> Must trigger Escalated status!
        StaffTask res3 = taskService.rejectTask(taskId, emp3, "Shift ending");
        assertThat(res3.getRejectionCount()).isEqualTo(3);
        assertThat(res3.getStatus()).isEqualTo(TaskStatus.Escalated);
        assertThat(res3.getEscalatedAt()).isNotNull();

        // Must NOT attempt a 4th allocationEngine call
        verify(allocationEngine, times(2)).allocateTask(eq(task), any());
    }

    @Test
    @DisplayName("Staff accepts assigned task -> status changes to InProgress")
    void acceptTask_TransitionsToInProgress() {
        UUID taskId = UUID.randomUUID();
        UUID empId = UUID.randomUUID();

        StaffTask task = StaffTask.builder()
                .id(taskId)
                .title("Clean Suite 501")
                .requiredRole(TaskRole.Housekeeper)
                .status(TaskStatus.Assigned)
                .assignedEmployeeId(empId)
                .build();

        when(staffTaskRepository.findById(taskId)).thenReturn(Optional.of(task));
        when(staffTaskRepository.save(any(StaffTask.class))).thenAnswer(i -> i.getArgument(0));

        StaffTask accepted = taskService.acceptTask(taskId, empId);

        assertThat(accepted.getStatus()).isEqualTo(TaskStatus.InProgress);
    }

    @Test
    @DisplayName("Staff completes task -> updates employee metrics and publishes event")
    void completeTask_UpdatesMetricsAndPublishesEvent() {
        UUID taskId = UUID.randomUUID();
        UUID empId = UUID.randomUUID();

        StaffTask task = StaffTask.builder()
                .id(taskId)
                .title("Clean Suite 501")
                .requiredRole(TaskRole.Housekeeper)
                .status(TaskStatus.InProgress)
                .assignedEmployeeId(empId)
                .build();

        EmployeeProfile profile = EmployeeProfile.builder()
                .employeeId(empId)
                .fullName("Kamal")
                .role(TaskRole.Housekeeper)
                .activeTasksCount(1)
                .tasksCompletedToday(2)
                .build();

        when(staffTaskRepository.findById(taskId)).thenReturn(Optional.of(task));
        when(staffTaskRepository.save(any(StaffTask.class))).thenAnswer(i -> i.getArgument(0));
        when(employeeProfileRepository.findById(empId)).thenReturn(Optional.of(profile));

        StaffTask completed = taskService.completeTask(taskId, empId);

        assertThat(completed.getStatus()).isEqualTo(TaskStatus.Completed);
        assertThat(completed.getCompletedAt()).isNotNull();

        // Employee stats updated
        assertThat(profile.getActiveTasksCount()).isEqualTo(0);
        assertThat(profile.getTasksCompletedToday()).isEqualTo(3);

        verify(taskEventPublisher).publishTaskCompleted(completed);
    }
}
