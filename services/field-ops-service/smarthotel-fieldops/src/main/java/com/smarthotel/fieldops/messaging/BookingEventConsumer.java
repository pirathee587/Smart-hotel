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

import java.util.UUID;

@Component
@RequiredArgsConstructor
@Slf4j
public class BookingEventConsumer {

    private final ObjectMapper objectMapper;
    private final TaskService taskService;

    @RabbitListener(queues = "${rabbitmq.queue.booking-checkout:smarthotel.fieldops.booking-checkout}")
    public void handleBookingCheckedOut(String message) {
        log.info("Received booking.checkedout event: {}", message);

        try {
            JsonNode root = objectMapper.readTree(message);

            // Handle PascalCase and camelCase properties
            String bookingRef = root.hasNonNull("BookingReference") ? root.get("BookingReference").asText() :
                    (root.hasNonNull("bookingReference") ? root.get("bookingReference").asText() : "N/A");

            UUID roomId = null;
            if (root.hasNonNull("RoomId")) {
                roomId = UUID.fromString(root.get("RoomId").asText());
            } else if (root.hasNonNull("roomId")) {
                roomId = UUID.fromString(root.get("roomId").asText());
            }

            HousekeepingTask turnoverTask = HousekeepingTask.builder()
                    .title("Turnover Cleaning - " + bookingRef)
                    .description("Automated turnover cleaning task triggered upon guest checkout for reservation " + bookingRef)
                    .cleaningType(CleaningType.Turnover)
                    .priority(TaskPriority.High)
                    .requiredRole(TaskRole.Housekeeper)
                    .roomId(roomId)
                    .floorNumber(1)
                    .linenChanged(false)
                    .build();

            taskService.createHousekeepingTask(turnoverTask, true);
            log.info("Successfully created and auto-dispatched turnover cleaning task for booking {}", bookingRef);

        } catch (Exception ex) {
            log.error("Error processing booking.checkedout message: {}", ex.getMessage(), ex);
        }
    }
}
