package com.smarthotel.fieldops.messaging;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.HousekeepingTask;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.CleaningType;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskPriority;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.service.TaskService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.verify;

@ExtendWith(MockitoExtension.class)
class BookingEventConsumerTest {

    @Mock
    private TaskService taskService;

    private BookingEventConsumer consumer;
    private ObjectMapper objectMapper;

    @BeforeEach
    void setUp() {
        objectMapper = new ObjectMapper();
        consumer = new BookingEventConsumer(objectMapper, taskService);
    }

    @Test
    @DisplayName("Booking checkout event triggers automatic Turnover HousekeepingTask creation")
    void handleBookingCheckedOut_CreatesTurnoverTask() {
        UUID roomId = UUID.randomUUID();
        UUID departmentId = UUID.randomUUID();
        String json = """
            {
                "BookingId": "a1b2c3d4-e5f6-7890-1234-56789abcdef0",
                "BookingReference": "TH-2026-XYZ123",
                "RoomId": "%s",
                "DepartmentId": "%s",
                "CheckedOutAt": "2026-09-06T11:00:00Z"
            }
        """.formatted(roomId, departmentId);

        consumer.handleBookingCheckedOut(json);

        ArgumentCaptor<HousekeepingTask> taskCaptor = ArgumentCaptor.forClass(HousekeepingTask.class);
        verify(taskService).createTurnoverTask(taskCaptor.capture(), eq(UUID.fromString("a1b2c3d4-e5f6-7890-1234-56789abcdef0")), eq(true));

        HousekeepingTask task = taskCaptor.getValue();
        assertThat(task.getTitle()).contains("Turnover Cleaning - TH-2026-XYZ123");
        assertThat(task.getCleaningType()).isEqualTo(CleaningType.Turnover);
        assertThat(task.getPriority()).isEqualTo(TaskPriority.High);
        assertThat(task.getRequiredRole()).isEqualTo(TaskRole.Housekeeper);
        assertThat(task.getRoomId()).isEqualTo(roomId);
        assertThat(task.getDepartmentId()).isEqualTo(UUID.fromString("22222222-2222-2222-2222-222222222222"));
    }
}
