package com.smarthotel.fieldops.messaging;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.HousekeepingTask;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.CleaningType;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskPriority;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.service.TaskService;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.stereotype.Component;
import org.springframework.beans.factory.annotation.Value;

import java.util.UUID;

@Component
@RequiredArgsConstructor
@Slf4j
public class BookingEventConsumer {

    private final ObjectMapper objectMapper;
    private final TaskService taskService;
    @Value("${departments.housekeeping-id:22222222-2222-2222-2222-222222222222}")
    private String housekeepingDepartmentId;

    @RabbitListener(queues = "${rabbitmq.queue.booking-checkout:smarthotel.fieldops.booking-checkout}")
    public void handleBookingCheckedOut(String message) {
        log.info("Received booking.checkedout event: {}", message);

        try {
            JsonNode root = objectMapper.readTree(message);

            // Handle PascalCase and camelCase properties
            String bookingRef = root.hasNonNull("BookingReference") ? root.get("BookingReference").asText() :
                    (root.hasNonNull("bookingReference") ? root.get("bookingReference").asText() : "N/A");
            String eventText = root.hasNonNull("BookingId") ? root.get("BookingId").asText() : (root.hasNonNull("bookingId") ? root.get("bookingId").asText() : null);
            if(eventText==null) throw new IllegalArgumentException("BookingId is required for idempotent checkout processing."); UUID checkoutEventId=UUID.fromString(eventText);

            UUID roomId = null;
            if (root.hasNonNull("RoomId")) {
                roomId = UUID.fromString(root.get("RoomId").asText());
            } else if (root.hasNonNull("roomId")) {
                roomId = UUID.fromString(root.get("roomId").asText());
            }

            UUID departmentId = UUID.fromString(housekeepingDepartmentId == null ? "22222222-2222-2222-2222-222222222222" : housekeepingDepartmentId);

            HousekeepingTask turnoverTask = HousekeepingTask.builder()
                    .title("Turnover Cleaning - " + bookingRef)
                    .description("Automated turnover cleaning task triggered upon guest checkout for reservation " + bookingRef)
                    .cleaningType(CleaningType.Turnover)
                    .priority(TaskPriority.High)
                    .requiredRole(TaskRole.Housekeeper)
                    .roomId(roomId)
                    .departmentId(departmentId)
                    .floorNumber(1)
                    .linenChanged(false)
                    .bookingReference(bookingRef)
                    .build();

            taskService.createTurnoverTask(turnoverTask, checkoutEventId, true);
            log.info("Successfully created and auto-dispatched turnover cleaning task for booking {}", bookingRef);

        } catch (Exception ex) {
            log.error("Error processing booking.checkedout message: {}", ex.getMessage(), ex);
        }
    }
}
