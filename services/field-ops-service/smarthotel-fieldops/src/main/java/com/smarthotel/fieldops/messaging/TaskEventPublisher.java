package com.smarthotel.fieldops.messaging;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.KdsOrder;
import com.smarthotel.fieldops.domain.model.StaffTask;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.time.Instant;
import java.util.Map;

@Component
@RequiredArgsConstructor
@Slf4j
public class TaskEventPublisher {

    private final RabbitTemplate rabbitTemplate;
    private final ObjectMapper objectMapper;

    @Value("${rabbitmq.exchange.events:smarthotel.events}")
    private String eventsExchange;

    public void publishTaskDispatched(StaffTask task, EmployeeProfile employee) {
        try {
            Map<String, Object> payload = Map.of(
                    "taskId", task.getId().toString(),
                    "title", task.getTitle(),
                    "requiredRole", task.getRequiredRole().name(),
                    "priority", task.getPriority().name(),
                    "assignedEmployeeId", employee.getEmployeeId().toString(),
                    "employeeName", employee.getFullName(),
                    "dispatchedAt", Instant.now().toString()
            );

            String routingKey = "task.dispatched";
            rabbitTemplate.convertAndSend(eventsExchange, routingKey, objectMapper.writeValueAsString(payload));
            log.info("Published task.dispatched event for task {}", task.getId());
        } catch (Exception ex) {
            log.warn("Could not publish task.dispatched event: {}", ex.getMessage());
        }
    }

    public void publishTaskCompleted(StaffTask task) {
        try {
            Map<String, Object> payload = Map.of(
                    "taskId", task.getId().toString(),
                    "status", task.getStatus().name(),
                    "assignedEmployeeId", task.getAssignedEmployeeId() != null ? task.getAssignedEmployeeId().toString() : "",
                    "completedAt", task.getCompletedAt() != null ? task.getCompletedAt().toString() : Instant.now().toString()
            );

            String routingKey = "task.completed";
            rabbitTemplate.convertAndSend(eventsExchange, routingKey, objectMapper.writeValueAsString(payload));
            log.info("Published task.completed event for task {}", task.getId());
        } catch (Exception ex) {
            log.warn("Could not publish task.completed event: {}", ex.getMessage());
        }
    }

    public void publishKdsStatusChanged(KdsOrder order) {
        try {
            Map<String, Object> payload = Map.of(
                    "orderId", order.getId().toString(),
                    "orderNumber", order.getOrderNumber(),
                    "status", order.getStatus().name(),
                    "updatedAt", Instant.now().toString()
            );

            String routingKey = "kds.status_changed";
            rabbitTemplate.convertAndSend(eventsExchange, routingKey, objectMapper.writeValueAsString(payload));
            log.info("Published kds.status_changed event for order {}", order.getOrderNumber());
        } catch (Exception ex) {
            log.warn("Could not publish kds.status_changed event: {}", ex.getMessage());
        }
    }
}
