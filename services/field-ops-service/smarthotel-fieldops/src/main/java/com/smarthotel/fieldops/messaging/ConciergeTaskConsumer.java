package com.smarthotel.fieldops.messaging;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.HousekeepingTask;
import com.smarthotel.fieldops.domain.model.MaintenanceWorkOrder;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.CleaningType;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskPriority;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.service.TaskService;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.util.UUID;

@Component
@RequiredArgsConstructor
@Slf4j
public class ConciergeTaskConsumer {

    private final ObjectMapper objectMapper;
    private final TaskService taskService;
    private final TaskEventPublisher taskEventPublisher;

    @Value("${departments.housekeeping-id:22222222-2222-2222-2222-222222222222}")
    private String housekeepingDepartmentId;

    @Value("${departments.maintenance-id:22222222-2222-2222-2222-222222222223}")
    private String maintenanceDepartmentId;

    @RabbitListener(queues = "${rabbitmq.queue.concierge-task:smarthotel.fieldops.task-requested}")
    public void handleConciergeTaskRequested(String message) {
        log.info("Received task.requested event from AI Concierge: {}", message);

        try {
            JsonNode root = objectMapper.readTree(message);

            // Extract eventId (PascalCase and camelCase)
            String eventIdStr = getTextField(root, "eventId", "EventId");
            if (eventIdStr == null || eventIdStr.isBlank()) {
                log.warn("Discarding task.requested event: missing required eventId");
                return;
            }
            UUID eventId = UUID.fromString(eventIdStr);

            // Extract customer and room context
            String customerId = getTextField(root, "customerId", "CustomerId");
            String roomNumber = getTextField(root, "roomNumber", "RoomNumber");
            String roomIdStr = getTextField(root, "roomId", "RoomId");
            UUID roomId = (roomIdStr != null && !roomIdStr.isBlank()) ? UUID.fromString(roomIdStr) : null;

            String requestType = getTextField(root, "requestType", "RequestType");
            String description = getTextField(root, "description", "Description");
            String priorityStr = getTextField(root, "priority", "Priority");
            String bookingReference = getTextField(root, "bookingReference", "BookingReference");
            boolean managerAttention = root.path("requiresManagerAttention").asBoolean(false);
            int slaMinutes = root.path("slaMinutes").asInt(30);
            String sentiment = getTextField(root, "sentiment", "Sentiment");

            if (requestType == null || description == null || roomNumber == null) {
                log.warn("Discarding malformed task.requested event {}: missing type, room, or description", eventId);
                return;
            }

            TaskPriority priority = mapPriority(priorityStr);
            int floorNumber = deriveFloorNumber(roomNumber);

            if ("Housekeeping".equalsIgnoreCase(requestType)) {
                UUID deptId = UUID.fromString(housekeepingDepartmentId);
                String title = buildTitle("Housekeeping", roomNumber, description, managerAttention);

                HousekeepingTask task = HousekeepingTask.builder()
                        .title(title)
                        .description(description)
                        .cleaningType(CleaningType.TouchUp)
                        .priority(priority)
                        .requiredRole(TaskRole.Housekeeper)
                        .roomId(roomId)
                        .roomNumber(roomNumber)
                        .departmentId(deptId)
                        .floorNumber(floorNumber)
                        .bookingReference(bookingReference)
                        .build();

                HousekeepingTask created = taskService.createConciergeHousekeepingTask(task, eventId);
                log.info("Created concierge housekeeping task {} for room {}", created.getId(), roomNumber);

                // Publish task.created via transactional outbox
                taskEventPublisher.publishTaskCreated(created, eventId, customerId, bookingReference);

            } else if ("Maintenance".equalsIgnoreCase(requestType)) {
                UUID deptId = UUID.fromString(maintenanceDepartmentId);
                String title = buildTitle("Maintenance", roomNumber, description, managerAttention);
                boolean isSafety = checkSafetyHazard(description);

                MaintenanceWorkOrder order = MaintenanceWorkOrder.builder()
                        .title(title)
                        .description(description)
                        .assetName("Room " + roomNumber + " Fixtures")
                        .location("Room " + roomNumber)
                        .roomId(roomId)
                        .roomNumber(roomNumber)
                        .departmentId(deptId)
                        .floorNumber(floorNumber)
                        .priority(priority)
                        .requiredRole(TaskRole.Maintenance)
                        .severity(isSafety ? "High" : "Medium")
                        .safetyHazard(isSafety)
                        .hazardDetails(isSafety ? "Guest reported potential safety hazard: " + description : null)
                        .build();

                MaintenanceWorkOrder created = taskService.createConciergeMaintenanceOrder(order, eventId);
                log.info("Created concierge maintenance work order {} for room {}", created.getId(), roomNumber);

                // Publish task.created via transactional outbox
                taskEventPublisher.publishTaskCreated(created, eventId, customerId, bookingReference);

            } else if ("RoomService".equalsIgnoreCase(requestType)) {
                log.warn("RoomService request {} received via concierge. Defers to authorized F&B order flow; no task created directly.", eventId);
            } else {
                log.warn("Unknown service requestType '{}' in event {}", requestType, eventId);
            }

        } catch (Exception ex) {
            log.error("Error processing concierge task.requested message: {}", ex.getMessage(), ex);
        }
    }

    private String getTextField(JsonNode root, String camel, String pascal) {
        if (root.hasNonNull(camel)) return root.get(camel).asText();
        if (root.hasNonNull(pascal)) return root.get(pascal).asText();
        return null;
    }

    private TaskPriority mapPriority(String priorityStr) {
        if (priorityStr == null) return TaskPriority.Medium;
        return switch (priorityStr.toLowerCase()) {
            case "low" -> TaskPriority.Low;
            case "high" -> TaskPriority.High;
            case "urgent" -> TaskPriority.Urgent;
            default -> TaskPriority.Medium;
        };
    }

    private int deriveFloorNumber(String roomNumber) {
        try {
            if (roomNumber != null && roomNumber.length() >= 2) {
                char first = roomNumber.charAt(0);
                if (Character.isDigit(first)) {
                    return Character.getNumericValue(first);
                }
            }
        } catch (Exception ignored) {}
        return 1;
    }

    private String buildTitle(String category, String roomNumber, String desc, boolean managerAttention) {
        String cleanDesc = desc.trim();
        if (cleanDesc.length() > 50) {
            cleanDesc = cleanDesc.substring(0, 47) + "...";
        }
        String prefix = managerAttention ? "[MANAGER ATTENTION] " : "";
        return prefix + "Guest " + category + " - Room " + roomNumber + ": " + cleanDesc;
    }

    private boolean checkSafetyHazard(String description) {
        String lower = description.toLowerCase();
        return lower.contains("smoke") || lower.contains("fire") || lower.contains("gas") ||
               lower.contains("spark") || lower.contains("flood") || lower.contains("electric");
    }
}
