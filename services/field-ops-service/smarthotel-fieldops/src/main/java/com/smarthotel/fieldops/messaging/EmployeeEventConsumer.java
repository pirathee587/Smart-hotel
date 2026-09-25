package com.smarthotel.fieldops.messaging;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.stereotype.Component;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

@Component
@RequiredArgsConstructor
@Slf4j
public class EmployeeEventConsumer {

    private final ObjectMapper objectMapper;
    private final EmployeeProfileRepository employeeProfileRepository;

    @RabbitListener(queues = "${rabbitmq.queue.employee-sync:smarthotel.fieldops.employee-sync}")
    public void handleEmployeeEvent(String message) {
        log.info("Received employee event: {}", message);

        try {
            JsonNode root = objectMapper.readTree(message);

            // Extract employeeId
            String idStr = root.hasNonNull("EmployeeId") ? root.get("EmployeeId").asText() :
                    (root.hasNonNull("employeeId") ? root.get("employeeId").asText() :
                            (root.hasNonNull("Id") ? root.get("Id").asText() : null));

            if (idStr == null) {
                log.warn("Ignoring employee event with missing EmployeeId: {}", message);
                return;
            }

            UUID employeeId = UUID.fromString(idStr);

            EmployeeProfile profile = employeeProfileRepository.findById(employeeId)
                    .orElse(EmployeeProfile.builder().employeeId(employeeId).build());

            String departmentId = root.hasNonNull("DepartmentId") ? root.get("DepartmentId").asText() :
                    (root.hasNonNull("departmentId") ? root.get("departmentId").asText() : null);
            if (departmentId != null && !departmentId.isBlank()) {
                profile.setDepartmentId(UUID.fromString(departmentId));
            }

            if (root.hasNonNull("FullName")) {
                profile.setFullName(root.get("FullName").asText());
            } else if (root.hasNonNull("fullName")) {
                profile.setFullName(root.get("fullName").asText());
            }

            // Role
            String roleStr = root.hasNonNull("Role") ? root.get("Role").asText() :
                    (root.hasNonNull("role") ? root.get("role").asText() : null);
            if (roleStr != null) {
                try {
                    profile.setRole(TaskRole.valueOf(roleStr));
                } catch (IllegalArgumentException e) {
                    profile.setRole(TaskRole.Staff);
                }
            } else if (profile.getRole() == null) {
                profile.setRole(TaskRole.Staff);
            }

            // Hourly rate
            if (root.hasNonNull("HourlyRate")) {
                profile.setHourlyRate(new BigDecimal(root.get("HourlyRate").asText()));
            } else if (root.hasNonNull("hourlyRate")) {
                profile.setHourlyRate(new BigDecimal(root.get("hourlyRate").asText()));
            }

            // Bank details (Option B persistence)
            if (root.hasNonNull("BankName")) {
                profile.setBankName(root.get("BankName").asText());
            } else if (root.hasNonNull("bankName")) {
                profile.setBankName(root.get("bankName").asText());
            }

            if (root.hasNonNull("BankAccountNumber")) {
                profile.setBankAccountNumber(root.get("BankAccountNumber").asText());
            } else if (root.hasNonNull("bankAccountNumber")) {
                profile.setBankAccountNumber(root.get("bankAccountNumber").asText());
            }

            if (root.hasNonNull("BankBranch")) {
                profile.setBankBranch(root.get("BankBranch").asText());
            } else if (root.hasNonNull("bankBranch")) {
                profile.setBankBranch(root.get("bankBranch").asText());
            }

            // Proficiency level
            if (root.hasNonNull("ProficiencyLevel")) {
                profile.setProficiencyLevel(root.get("ProficiencyLevel").asInt(5));
            } else if (root.hasNonNull("proficiencyLevel")) {
                profile.setProficiencyLevel(root.get("proficiencyLevel").asInt(5));
            }

            // Active status
            if (root.hasNonNull("IsActive")) {
                profile.setActive(root.get("IsActive").asBoolean());
            } else if (root.hasNonNull("isActive")) {
                profile.setActive(root.get("isActive").asBoolean());
            } else if (root.hasNonNull("active")) {
                profile.setActive(root.get("active").asBoolean());
            }

            profile.setUpdatedAt(Instant.now());
            employeeProfileRepository.save(profile);
            log.info("Successfully synced EmployeeProfile for employee {}", employeeId);

        } catch (Exception ex) {
            log.error("Error processing employee sync event: {}", ex.getMessage(), ex);
        }
    }
}
