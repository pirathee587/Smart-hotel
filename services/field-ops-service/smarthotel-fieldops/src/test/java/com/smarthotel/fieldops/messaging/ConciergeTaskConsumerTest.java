package com.smarthotel.fieldops.messaging;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.HousekeepingTask;
import com.smarthotel.fieldops.domain.model.MaintenanceWorkOrder;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.CleaningType;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskPriority;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import com.smarthotel.fieldops.service.TaskService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.test.util.ReflectionTestUtils;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class ConciergeTaskConsumerTest {

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Mock
    private TaskService taskService;

    @Mock
    private TaskEventPublisher taskEventPublisher;

    private ConciergeTaskConsumer consumer;

    private final UUID housekeepingDeptId = UUID.fromString("22222222-2222-2222-2222-222222222222");
    private final UUID maintenanceDeptId = UUID.fromString("22222222-2222-2222-2222-222222222223");

    @BeforeEach
    void setUp() {
        consumer = new ConciergeTaskConsumer(objectMapper, taskService, taskEventPublisher);
        ReflectionTestUtils.setField(consumer, "housekeepingDepartmentId", housekeepingDeptId.toString());
        ReflectionTestUtils.setField(consumer, "maintenanceDepartmentId", maintenanceDeptId.toString());
    }

    @Test
    @DisplayName("Housekeeping request creates unassigned Pending task and enqueues task.created outbox event")
    void testHousekeepingTaskCreation() {
        UUID eventId = UUID.randomUUID();
        String json = """
            {
                "eventId": "%s",
                "eventType": "task.requested",
                "customerId": "cust-12345",
                "roomNumber": "204",
                "requestType": "Housekeeping",
                "description": "Please send 2 extra bath towels and soap",
                "priority": "Normal",
                "bookingReference": "BK-2026-0922"
            }
            """.formatted(eventId);

        HousekeepingTask saved = HousekeepingTask.builder()
                .id(UUID.randomUUID())
                .title("Guest Housekeeping - Room 204: Please send 2 extra bath towels...")
                .description("Please send 2 extra bath towels and soap")
                .requiredRole(TaskRole.Housekeeper)
                .status(TaskStatus.Pending)
                .roomNumber("204")
                .conciergeEventId(eventId)
                .build();

        when(taskService.createConciergeHousekeepingTask(any(HousekeepingTask.class), eq(eventId)))
                .thenReturn(saved);

        consumer.handleConciergeTaskRequested(json);

        ArgumentCaptor<HousekeepingTask> taskCaptor = ArgumentCaptor.forClass(HousekeepingTask.class);
        verify(taskService).createConciergeHousekeepingTask(taskCaptor.capture(), eq(eventId));

        HousekeepingTask captured = taskCaptor.getValue();
        assertThat(captured.getRoomNumber()).isEqualTo("204");
        assertThat(captured.getRequiredRole()).isEqualTo(TaskRole.Housekeeper);
        assertThat(captured.getCleaningType()).isEqualTo(CleaningType.TouchUp);
        assertThat(captured.getPriority()).isEqualTo(TaskPriority.Medium);
        assertThat(captured.getFloorNumber()).isEqualTo(2);

        // Verify transactional outbox event published
        verify(taskEventPublisher).publishTaskCreated(eq(saved), eq(eventId), eq("cust-12345"), eq("BK-2026-0922"));
    }

    @Test
    @DisplayName("Maintenance request creates unassigned Pending work order and enqueues task.created outbox event")
    void testMaintenanceTaskCreation() {
        UUID eventId = UUID.randomUUID();
        String json = """
            {
                "eventId": "%s",
                "eventType": "task.requested",
                "customerId": "cust-12345",
                "roomNumber": "305",
                "requestType": "Maintenance",
                "description": "The AC is leaking water onto the floor",
                "priority": "High",
                "bookingReference": "BK-2026-0922"
            }
            """.formatted(eventId);

        MaintenanceWorkOrder saved = MaintenanceWorkOrder.builder()
                .id(UUID.randomUUID())
                .title("Guest Maintenance - Room 305: The AC is leaking water onto the floor")
                .description("The AC is leaking water onto the floor")
                .requiredRole(TaskRole.Maintenance)
                .status(TaskStatus.Pending)
                .roomNumber("305")
                .issueEventId(eventId)
                .build();

        when(taskService.createConciergeMaintenanceOrder(any(MaintenanceWorkOrder.class), eq(eventId)))
                .thenReturn(saved);

        consumer.handleConciergeTaskRequested(json);

        ArgumentCaptor<MaintenanceWorkOrder> orderCaptor = ArgumentCaptor.forClass(MaintenanceWorkOrder.class);
        verify(taskService).createConciergeMaintenanceOrder(orderCaptor.capture(), eq(eventId));

        MaintenanceWorkOrder captured = orderCaptor.getValue();
        assertThat(captured.getRoomNumber()).isEqualTo("305");
        assertThat(captured.getRequiredRole()).isEqualTo(TaskRole.Maintenance);
        assertThat(captured.getPriority()).isEqualTo(TaskPriority.High);
        assertThat(captured.getFloorNumber()).isEqualTo(3);

        verify(taskEventPublisher).publishTaskCreated(eq(saved), eq(eventId), eq("cust-12345"), eq("BK-2026-0922"));
    }

    @Test
    @DisplayName("Malformed message with missing eventId is safely discarded")
    void testMissingEventId_Discarded() {
        String json = """
            {
                "requestType": "Housekeeping",
                "description": "Towels",
                "roomNumber": "101"
            }
            """;

        consumer.handleConciergeTaskRequested(json);

        verifyNoInteractions(taskService);
        verifyNoInteractions(taskEventPublisher);
    }

    @Test
    @DisplayName("RoomService requests are deferred without creating tasks")
    void testRoomService_Deferred() {
        UUID eventId = UUID.randomUUID();
        String json = """
            {
                "eventId": "%s",
                "requestType": "RoomService",
                "description": "Order club sandwich and iced tea",
                "roomNumber": "101"
            }
            """.formatted(eventId);

        consumer.handleConciergeTaskRequested(json);

        verifyNoInteractions(taskService);
        verifyNoInteractions(taskEventPublisher);
    }
}
