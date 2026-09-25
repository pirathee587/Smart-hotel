package com.smarthotel.fieldops.controller;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.*;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.*;
import com.smarthotel.fieldops.domain.repository.*;
import com.smarthotel.fieldops.dto.AttendanceDtos.OvertimeDecisionRequest;
import com.smarthotel.fieldops.dto.LeaveDtos.DecisionRequest;
import com.smarthotel.fieldops.dto.LeaveDtos.SubmitRequest;
import com.smarthotel.fieldops.dto.TaskDtos.CreateHousekeepingTaskRequest;
import com.smarthotel.fieldops.dto.TaskDtos.CreateMaintenanceWorkOrderRequest;
import com.smarthotel.fieldops.service.LeaveService;
import com.smarthotel.fieldops.service.MaintenanceIntegrationClient;
import com.smarthotel.fieldops.service.RoomReadinessClient;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.http.MediaType;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
public class OwnerPermissionAuditTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @Autowired
    private StaffTaskRepository staffTaskRepository;

    @Autowired
    private EmployeeProfileRepository employeeProfileRepository;

    @Autowired
    private AttendanceRecordRepository attendanceRecordRepository;

    @Autowired
    private OvertimeApprovalRepository overtimeApprovalRepository;

    @Autowired
    private LeaveRequestRepository leaveRequestRepository;

    @Autowired
    private LeavePolicyRepository leavePolicyRepository;

    @Autowired
    private MonthlyAttendanceSummaryRepository monthlyAttendanceSummaryRepository;

    @Autowired
    private LeaveService leaveService;

    @MockBean
    private JwtDecoder jwtDecoder;

    @MockBean
    private RabbitTemplate rabbitTemplate;

    @MockBean
    private RoomReadinessClient roomReadinessClient;

    @MockBean
    private MaintenanceIntegrationClient maintenanceIntegrationClient;

    private UUID ownerId;
    private UUID hkDeptId;
    private UUID maintDeptId;
    private UUID housekeeperId;
    private UUID technicianId;

    @BeforeEach
    void setUp() {
        staffTaskRepository.deleteAll();
        attendanceRecordRepository.deleteAll();
        overtimeApprovalRepository.deleteAll();
        leaveRequestRepository.deleteAll();
        monthlyAttendanceSummaryRepository.deleteAll();
        employeeProfileRepository.deleteAll();

        ownerId = UUID.randomUUID();
        hkDeptId = UUID.randomUUID();
        maintDeptId = UUID.randomUUID();
        housekeeperId = UUID.randomUUID();
        technicianId = UUID.randomUUID();

        employeeProfileRepository.save(EmployeeProfile.builder()
                .employeeId(housekeeperId)
                .fullName("Housekeeper Alice")
                .departmentId(hkDeptId)
                .role(TaskRole.Housekeeper)
                .currentFloor(1)
                .activeTasksCount(0)
                .tasksCompletedToday(0)
                .build());

        employeeProfileRepository.save(EmployeeProfile.builder()
                .employeeId(technicianId)
                .fullName("Tech Bob")
                .departmentId(maintDeptId)
                .role(TaskRole.Maintenance)
                .currentFloor(1)
                .activeTasksCount(0)
                .tasksCompletedToday(0)
                .build());
    }

    @Test
    @DisplayName("Owner can create housekeeping task across department")
    void ownerCanCreateHousekeepingTask() throws Exception {
        CreateHousekeepingTaskRequest req = new CreateHousekeepingTaskRequest(
                "VIP Suite Clean", "Deep clean before arrival",
                TaskPriority.High, CleaningType.DeepClean, true, 3,
                UUID.randomUUID(), "301", UUID.randomUUID(), hkDeptId, false
        );

        mockMvc.perform(post("/api/v1/tasks/housekeeping")
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Owner"))
                                .jwt(j -> j.subject(ownerId.toString()).claim("role", "Owner")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req)))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.title").value("VIP Suite Clean"))
                .andExpect(jsonPath("$.departmentId").value(hkDeptId.toString()));
    }

    @Test
    @DisplayName("Owner can view all employee profiles across departments")
    void ownerCanListAllProfiles() throws Exception {
        mockMvc.perform(get("/api/v1/tasks/profiles")
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Owner"))
                                .jwt(j -> j.subject(ownerId.toString()).claim("role", "Owner"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.length()").value(2));
    }

    @Test
    @DisplayName("Owner can view cross-department attendance records")
    void ownerCanViewCrossDepartmentAttendance() throws Exception {
        java.time.Instant now = java.time.Instant.now();
        AttendanceRecord r1 = AttendanceRecord.builder()
                .employeeId(housekeeperId)
                .date(LocalDate.now())
                .clockIn(now.minusSeconds(28800))
                .clockOut(now)
                .status(AttendanceStatus.Normal)
                .hoursWorked(8.0)
                .build();
        AttendanceRecord r2 = AttendanceRecord.builder()
                .employeeId(technicianId)
                .date(LocalDate.now())
                .clockIn(now.minusSeconds(28800))
                .clockOut(now)
                .status(AttendanceStatus.Normal)
                .hoursWorked(8.0)
                .build();
        attendanceRecordRepository.save(r1);
        attendanceRecordRepository.save(r2);

        mockMvc.perform(get("/api/v1/attendance/records")
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Owner"))
                                .jwt(j -> j.subject(ownerId.toString()).claim("role", "Owner"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.length()").value(2));
    }

    @Test
    @DisplayName("Non-Owner cross-department attendance access is forbidden")
    void nonOwnerCrossDepartmentAttendanceIsForbidden() throws Exception {
        UUID managerId = UUID.randomUUID();
        // Manager in housekeeping attempting to query technician's attendance
        mockMvc.perform(get("/api/v1/attendance/records")
                        .param("employeeId", technicianId.toString())
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Manager"))
                                .jwt(j -> j.subject(managerId.toString()).claim("role", "Manager").claim("departmentId", hkDeptId.toString()).claim("departmentCode", "HOUSEKEEPING"))))
                .andExpect(status().isForbidden());
    }

    @Test
    @DisplayName("Self-approval of overtime is blocked even for Owner")
    void selfApprovalOfOvertimeIsBlocked() throws Exception {
        OvertimeApproval approval = OvertimeApproval.builder()
                .attendanceRecordId(UUID.randomUUID())
                .employeeId(ownerId)
                .overtimeHours(2.5)
                .status(OvertimeStatus.Pending)
                .build();
        approval = overtimeApprovalRepository.save(approval);

        OvertimeDecisionRequest req = new OvertimeDecisionRequest("Attempting self approval");

        mockMvc.perform(post("/api/v1/attendance/overtime/" + approval.getId() + "/approve")
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Owner"))
                                .jwt(j -> j.subject(ownerId.toString()).claim("role", "Owner")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req)))
                .andExpect(status().isForbidden());
    }

    @Test
    @DisplayName("Owner cannot perform operational worker action (start cleaning)")
    void ownerCannotStartCleaningTask() throws Exception {
        mockMvc.perform(post("/api/v1/tasks/" + UUID.randomUUID() + "/start-cleaning")
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Owner"))
                                .jwt(j -> j.subject(ownerId.toString()).claim("role", "Owner"))))
                .andExpect(status().isForbidden());
    }

    @Test
    @DisplayName("Owner cannot perform operational worker action (start repair)")
    void ownerCannotStartRepairWorkOrder() throws Exception {
        mockMvc.perform(post("/api/v1/tasks/" + UUID.randomUUID() + "/start-repair")
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Owner"))
                                .jwt(j -> j.subject(ownerId.toString()).claim("role", "Owner"))))
                .andExpect(status().isForbidden());
    }

    @Test
    @DisplayName("Worker cannot inspect own cleaning task")
    void workerCannotInspectOwnCleaning() {
        HousekeepingTask task = HousekeepingTask.builder()
                .title("Clean room")
                .requiredRole(TaskRole.Housekeeper)
                .assignedEmployeeId(housekeeperId)
                .status(TaskStatus.AwaitingInspection)
                .build();

        assertThatThrownBy(() -> task.approveInspection(housekeeperId, "Self approved", UUID.randomUUID()))
                .isInstanceOf(SecurityException.class)
                .hasMessageContaining("cannot inspect their own");
    }

    @Test
    @DisplayName("Technician cannot verify own repair work order")
    void technicianCannotVerifyOwnRepair() {
        MaintenanceWorkOrder order = MaintenanceWorkOrder.builder()
                .title("Repair AC")
                .requiredRole(TaskRole.Maintenance)
                .assignedEmployeeId(technicianId)
                .status(TaskStatus.AwaitingInspection)
                .build();

        assertThatThrownBy(() -> order.verifyRepair(technicianId, true, "Self verified"))
                .isInstanceOf(SecurityException.class)
                .hasMessageContaining("cannot verify their own repairs");
    }

    @Test
    @DisplayName("Owner can view cross-department leave records")
    void ownerCanViewCrossDepartmentLeaves() throws Exception {
        UUID policyId = UUID.randomUUID();
        LeaveRequest l1 = LeaveRequest.builder()
                .employeeId(housekeeperId)
                .departmentId(hkDeptId)
                .policyId(policyId)
                .leaveType("Annual")
                .startDate(LocalDate.now())
                .endDate(LocalDate.now().plusDays(2))
                .status(LeaveRequest.Status.Pending)
                .reason("Vacation")
                .build();
        LeaveRequest l2 = LeaveRequest.builder()
                .employeeId(technicianId)
                .departmentId(maintDeptId)
                .policyId(policyId)
                .leaveType("Medical")
                .startDate(LocalDate.now())
                .endDate(LocalDate.now().plusDays(1))
                .status(LeaveRequest.Status.Pending)
                .reason("Medical appointment")
                .build();
        leaveRequestRepository.save(l1);
        leaveRequestRepository.save(l2);

        mockMvc.perform(get("/api/v1/leaves/department")
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Owner"))
                                .jwt(j -> j.subject(ownerId.toString()).claim("role", "Owner"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.length()").value(2));
    }

    @Test
    @DisplayName("Worker cannot self-approve leave")
    void workerCannotSelfApproveLeave() {
        LeaveRequest leave = LeaveRequest.builder()
                .employeeId(housekeeperId)
                .departmentId(hkDeptId)
                .policyId(UUID.randomUUID())
                .leaveType("Personal")
                .status(LeaveRequest.Status.Pending)
                .startDate(LocalDate.now())
                .endDate(LocalDate.now().plusDays(1))
                .reason("Personal")
                .build();
        leaveRequestRepository.save(leave);

        DecisionRequest req = new DecisionRequest(true, "Self approved");

        assertThatThrownBy(() -> leaveService.decide(leave.getId(), housekeeperId, hkDeptId, req))
                .isInstanceOf(SecurityException.class)
                .hasMessageContaining("cannot approve their own leave");
    }

    @Test
    @DisplayName("Owner can view verified monthly attendance summaries for payroll review")
    void ownerCanViewVerifiedAttendanceSummaries() throws Exception {
        MonthlyAttendanceSummary summary = MonthlyAttendanceSummary.builder()
                .employeeId(housekeeperId)
                .departmentId(hkDeptId)
                .employeeRole("Housekeeper")
                .payrollYear(2026)
                .payrollMonth(9)
                .scheduledDays(20)
                .workedDays(20)
                .attendanceRecordIds("")
                .overtimeRecordIds("")
                .leaveRecordIds("")
                .verificationStatus(MonthlyAttendanceSummary.VerificationStatus.Verified)
                .summaryVersion(1)
                .build();
        monthlyAttendanceSummaryRepository.save(summary);

        mockMvc.perform(get("/api/v1/attendance-summaries/verified")
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Owner"))
                                .jwt(j -> j.subject(ownerId.toString()).claim("role", "Owner"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.length()").value(1));
    }
}
