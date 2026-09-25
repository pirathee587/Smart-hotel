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
import com.smarthotel.fieldops.service.RoomReadinessClient;
import com.smarthotel.fieldops.service.MaintenanceIntegrationClient;
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
import static org.mockito.Mockito.*;

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
    @MockBean private RoomReadinessClient roomReadinessClient;
    @MockBean private MaintenanceIntegrationClient maintenanceIntegrationClient;

    private UUID staffId;
    private UUID managerId;
    private UUID departmentId;

    @BeforeEach
    void setUp() {
        staffTaskRepository.deleteAll();
        employeeProfileRepository.deleteAll();

        staffId = UUID.randomUUID();
        managerId = UUID.randomUUID();
        departmentId = UUID.randomUUID();

        // Seed staff
        EmployeeProfile staff = EmployeeProfile.builder()
                .employeeId(staffId)
                .fullName("Sunil Shantha")
                .departmentId(departmentId)
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
                departmentId,
                true
        );

        mockMvc.perform(post("/api/v1/tasks/housekeeping")
                        .with(jwt().authorities(new org.springframework.security.core.authority.SimpleGrantedAuthority("ROLE_Manager")).jwt(j -> j.subject(managerId.toString()).claim("role", "Manager").claim("departmentId", departmentId.toString()).claim("departmentCode", "HOUSEKEEPING")))
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
                UUID.randomUUID(),
                "105",
                UUID.randomUUID(),
                "Critical",
                "Exposed conductor",
                new BigDecimal("1500.00"),
                true,
                1,
                UUID.randomUUID(),
                departmentId,
                false
        );

        mockMvc.perform(post("/api/v1/tasks/maintenance")
                        .with(jwt().authorities(new org.springframework.security.core.authority.SimpleGrantedAuthority("ROLE_Manager")).jwt(j -> j.subject(managerId.toString()).claim("role", "Manager").claim("departmentId", departmentId.toString()).claim("departmentCode","MAINTENANCE")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req)))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.title").value("Fix Broken Lamp"))
                .andExpect(jsonPath("$.requiredRole").value("Maintenance"))
                .andExpect(jsonPath("$.status").value("Pending"));
    }

    @Test
    @DisplayName("Staff accepts assigned task -> status Accepted")
    void acceptTask_Success() throws Exception {
        HousekeepingTask task = HousekeepingTask.builder()
                .title("Clean 202")
                .requiredRole(TaskRole.Housekeeper)
                .status(TaskStatus.Assigned)
                .assignedEmployeeId(staffId)
                .departmentId(departmentId)
                .build();
        HousekeepingTask saved = staffTaskRepository.save(task);

        mockMvc.perform(post("/api/v1/tasks/" + saved.getId() + "/accept")
                        .with(jwt().jwt(j -> j.subject(staffId.toString()).claim("departmentId", departmentId.toString())))
                        .contentType(MediaType.APPLICATION_JSON))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.status").value("Accepted"));
    }

    @Test
    @DisplayName("Staff rejects assigned task -> rejectionCount increments")
    void rejectTask_IncrementsCount() throws Exception {
        HousekeepingTask task = HousekeepingTask.builder()
                .title("Clean 203")
                .requiredRole(TaskRole.Housekeeper)
                .status(TaskStatus.Assigned)
                .assignedEmployeeId(staffId)
                .departmentId(departmentId)
                .rejectionCount(0)
                .build();
        HousekeepingTask saved = staffTaskRepository.save(task);

        RejectTaskRequest req = new RejectTaskRequest("Currently busy");

        mockMvc.perform(post("/api/v1/tasks/" + saved.getId() + "/reject")
                        .with(jwt().jwt(j -> j.subject(staffId.toString()).claim("departmentId", departmentId.toString())))
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
                .departmentId(departmentId)
                .build();
        staffTaskRepository.save(task);

        mockMvc.perform(get("/api/v1/tasks/my")
                        .with(jwt().jwt(j -> j.subject(staffId.toString()).claim("departmentId", departmentId.toString()))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.length()").value(1))
                .andExpect(jsonPath("$[0].title").value("Clean 204"));
    }

    @Test
    @DisplayName("Employee cannot access a task from another department")
    void getTaskById_OtherDepartment_IsForbiddenByNonDisclosure() throws Exception {
        HousekeepingTask task = HousekeepingTask.builder()
                .title("Other department task")
                .requiredRole(TaskRole.Housekeeper)
                .departmentId(UUID.randomUUID())
                .build();
        HousekeepingTask saved = staffTaskRepository.save(task);

        mockMvc.perform(get("/api/v1/tasks/" + saved.getId())
                        .with(jwt().jwt(j -> j.subject(staffId.toString()).claim("departmentId", departmentId.toString()))))
                .andExpect(status().isNotFound());
    }

    @Test
    @DisplayName("Manager cannot create a task for another department")
    void createTask_OtherDepartment_IsForbidden() throws Exception {
        CreateHousekeepingTaskRequest req = new CreateHousekeepingTaskRequest(
                "Cross department", null, TaskPriority.High, CleaningType.Turnover, false,
                1, UUID.randomUUID(), "101", UUID.randomUUID(), UUID.randomUUID(), false);

        mockMvc.perform(post("/api/v1/tasks/housekeeping")
                        .with(jwt().authorities(new org.springframework.security.core.authority.SimpleGrantedAuthority("ROLE_Manager"))
                                .jwt(j -> j.subject(managerId.toString()).claim("role", "Manager").claim("departmentId", departmentId.toString()).claim("departmentCode", "HOUSEKEEPING")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req)))
                .andExpect(status().isForbidden());
    }

    @Test
    @DisplayName("Housekeeping workflow requires start, completion and Manager inspection approval")
    void housekeepingLifecycle_Approval() throws Exception {
        UUID roomId=UUID.randomUUID(); HousekeepingTask task=HousekeepingTask.builder().title("Turnover 301").requiredRole(TaskRole.Housekeeper).status(TaskStatus.Assigned).assignedEmployeeId(staffId).departmentId(departmentId).roomId(roomId).build(); HousekeepingTask saved=staffTaskRepository.save(task);
        var housekeeper=jwt().authorities(new org.springframework.security.core.authority.SimpleGrantedAuthority("ROLE_Housekeeper")).jwt(j->j.subject(staffId.toString()).claim("role","Housekeeper").claim("departmentId",departmentId.toString()).claim("departmentCode","HOUSEKEEPING"));
        mockMvc.perform(post("/api/v1/tasks/"+saved.getId()+"/accept").with(housekeeper)).andExpect(status().isOk()).andExpect(jsonPath("$.status").value("Accepted"));
        mockMvc.perform(post("/api/v1/tasks/"+saved.getId()+"/start-cleaning").header("Authorization","Test test-token").with(housekeeper)).andExpect(status().isOk()).andExpect(jsonPath("$.status").value("InProgress"));
        mockMvc.perform(post("/api/v1/tasks/"+saved.getId()+"/complete").with(housekeeper)).andExpect(status().isOk()).andExpect(jsonPath("$.status").value("AwaitingInspection"));
        String decision="{\"approved\":true,\"notes\":\"Room inspected and ready\"}";
        mockMvc.perform(post("/api/v1/tasks/"+saved.getId()+"/inspection").header("Authorization","Test manager-token").with(jwt().authorities(new org.springframework.security.core.authority.SimpleGrantedAuthority("ROLE_Manager")).jwt(j->j.subject(managerId.toString()).claim("role","Manager").claim("departmentId",departmentId.toString()).claim("departmentCode","HOUSEKEEPING"))).contentType(MediaType.APPLICATION_JSON).content(decision)).andExpect(status().isOk()).andExpect(jsonPath("$.status").value("InspectionApproved")).andExpect(jsonPath("$.inspectionPassed").value(true));
        verify(roomReadinessClient).start(eq(roomId),eq(saved.getId()),any(),eq(departmentId),eq(staffId),any(),eq("Test test-token"));
        verify(roomReadinessClient).inspect(eq(roomId),eq(saved.getId()),any(),eq(departmentId),eq(managerId),eq(true),anyString(),any(),eq("Test manager-token"));
    }

    @Test
    void crossDepartmentManagerCannotApproveInspection() throws Exception {
        HousekeepingTask task=HousekeepingTask.builder().title("Turnover 302").requiredRole(TaskRole.Housekeeper).status(TaskStatus.AwaitingInspection).assignedEmployeeId(staffId).departmentId(departmentId).roomId(UUID.randomUUID()).build(); HousekeepingTask saved=staffTaskRepository.save(task);
        mockMvc.perform(post("/api/v1/tasks/"+saved.getId()+"/inspection").header("Authorization","Test bad-token").with(jwt().authorities(new org.springframework.security.core.authority.SimpleGrantedAuthority("ROLE_Manager")).jwt(j->j.subject(UUID.randomUUID().toString()).claim("role","Manager").claim("departmentId",UUID.randomUUID().toString()).claim("departmentCode","FINANCE"))).contentType(MediaType.APPLICATION_JSON).content("{\"approved\":true,\"notes\":\"invalid\"}" )).andExpect(status().isForbidden());
        verifyNoInteractions(roomReadinessClient);
    }
}
