package com.smarthotel.fieldops.controller;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.HousekeepingTask;
import com.smarthotel.fieldops.domain.model.MaintenanceWorkOrder;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.CleaningType;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskPriority;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import com.smarthotel.fieldops.domain.repository.StaffTaskRepository;
import com.smarthotel.fieldops.dto.TaskDtos.CreateHousekeepingTaskRequest;
import com.smarthotel.fieldops.dto.TaskDtos.CreateMaintenanceWorkOrderRequest;
import com.smarthotel.fieldops.dto.TaskDtos.RejectTaskRequest;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.http.MediaType;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;

import java.math.BigDecimal;
import java.util.UUID;

import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class TaskControllerIntegrationTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @Autowired
    private StaffTaskRepository staffTaskRepository;

    @Autowired
    private EmployeeProfileRepository employeeProfileRepository;

    @MockBean
    private JwtDecoder jwtDecoder;

    @MockBean
    private RabbitTemplate rabbitTemplate;

    private UUID staffId;
    private UUID managerId;

    @BeforeEach
    void setUp() {
        staffTaskRepository.deleteAll();
        employeeProfileRepository.deleteAll();

        staffId = UUID.randomUUID();
        managerId = UUID.randomUUID();

        // Seed staff
        EmployeeProfile staff = EmployeeProfile.builder()
                .employeeId(staffId)
                .fullName("Sunil Shantha")
                .role(TaskRole.Housekeeper)
                .currentFloor(2)
                .activeTasksCount(0)
                .tasksCompletedToday(0)
                .build();
        employeeProfileRepository.save(staff);
    }

    @Test
    @DisplayName("POST /api/v1/tasks/housekeeping creates task and auto-dispatches to eligible staff")
    void createHousekeepingTask_AutoDispatches() throws Exception {
        CreateHousekeepingTaskRequest req = new CreateHousekeepingTaskRequest(
                "Turnover Room 201",
                "Deep clean after checkout",
                TaskPriority.High,
                CleaningType.Turnover,
                true,
                2,
                UUID.randomUUID(),
                "201",
                UUID.randomUUID(),
                true
        );

        mockMvc.perform(post("/api/v1/tasks/housekeeping")
                        .with(jwt().authorities(new org.springframework.security.core.authority.SimpleGrantedAuthority("ROLE_Manager")).jwt(j -> j.subject(managerId.toString())))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req)))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.title").value("Turnover Room 201"))
                .andExpect(jsonPath("$.status").value("Assigned"))
                .andExpect(jsonPath("$.assignedEmployeeId").value(staffId.toString()));
    }

    @Test
    @DisplayName("POST /api/v1/tasks/maintenance creates work order with safety hazard and role check")
    void createMaintenanceWorkOrder_Success() throws Exception {
        CreateMaintenanceWorkOrderRequest req = new CreateMaintenanceWorkOrderRequest(
                "Fix Broken Lamp",
                "Exposed wiring in room",
                TaskPriority.Urgent,
                "Table Lamp",
                "Room 105",
                new BigDecimal("1500.00"),
                true,
                1,
                UUID.randomUUID(),
                false
        );

        mockMvc.perform(post("/api/v1/tasks/maintenance")
                        .with(jwt().authorities(new org.springframework.security.core.authority.SimpleGrantedAuthority("ROLE_Manager")).jwt(j -> j.subject(managerId.toString())))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req)))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.title").value("Fix Broken Lamp"))
                .andExpect(jsonPath("$.requiredRole").value("Maintenance"))
                .andExpect(jsonPath("$.status").value("Pending"));
    }

    @Test
    @DisplayName("Staff accepts assigned task -> status InProgress")
    void acceptTask_Success() throws Exception {
        HousekeepingTask task = HousekeepingTask.builder()
                .title("Clean 202")
                .requiredRole(TaskRole.Housekeeper)
                .status(TaskStatus.Assigned)
                .assignedEmployeeId(staffId)
                .build();
        HousekeepingTask saved = staffTaskRepository.save(task);

        mockMvc.perform(post("/api/v1/tasks/" + saved.getId() + "/accept")
                        .with(jwt().jwt(j -> j.subject(staffId.toString())))
                        .contentType(MediaType.APPLICATION_JSON))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.status").value("InProgress"));
    }

    @Test
    @DisplayName("Staff rejects assigned task -> rejectionCount increments")
    void rejectTask_IncrementsCount() throws Exception {
        HousekeepingTask task = HousekeepingTask.builder()
                .title("Clean 203")
                .requiredRole(TaskRole.Housekeeper)
                .status(TaskStatus.Assigned)
                .assignedEmployeeId(staffId)
                .rejectionCount(0)
                .build();
        HousekeepingTask saved = staffTaskRepository.save(task);

        RejectTaskRequest req = new RejectTaskRequest("Currently busy");

        mockMvc.perform(post("/api/v1/tasks/" + saved.getId() + "/reject")
                        .with(jwt().jwt(j -> j.subject(staffId.toString())))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.rejectionCount").value(1));
    }

    @Test
    @DisplayName("GET /api/v1/tasks/my returns tasks assigned to caller")
    void getMyTasks_ReturnsAssignedList() throws Exception {
        HousekeepingTask task = HousekeepingTask.builder()
                .title("Clean 204")
                .requiredRole(TaskRole.Housekeeper)
                .status(TaskStatus.Assigned)
                .assignedEmployeeId(staffId)
                .build();
        staffTaskRepository.save(task);

        mockMvc.perform(get("/api/v1/tasks/my")
                        .with(jwt().jwt(j -> j.subject(staffId.toString()))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.length()").value(1))
                .andExpect(jsonPath("$[0].title").value("Clean 204"));
    }
}
