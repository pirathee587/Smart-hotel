package com.smarthotel.fieldops.controller;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.config.DeviceApiKeyFilter;
import com.smarthotel.fieldops.config.SecurityConfig;
import com.smarthotel.fieldops.domain.model.AttendanceRecord;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.AttendanceStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.PunchMethod;
import com.smarthotel.fieldops.domain.repository.AttendanceRecordRepository;
import com.smarthotel.fieldops.dto.AttendanceDtos.BiometricPunchRequest;
import com.smarthotel.fieldops.service.AttendanceService;
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
import org.springframework.test.context.TestPropertySource;
import org.springframework.test.web.servlet.MockMvc;

import java.time.Instant;
import java.time.LocalDate;
import java.util.UUID;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
@TestPropertySource(properties = {
        "biometrics.device-api-key=test_secret_key_123"
})
class AttendanceControllerSecurityTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @MockBean
    private AttendanceService attendanceService;

    @MockBean
    private JwtDecoder jwtDecoder;

    @MockBean
    private RabbitTemplate rabbitTemplate;

    @Test
    @DisplayName("POST /api/v1/attendance/punch without X-Device-Api-Key returns 401 Unauthorized")
    void punchWithoutApiKey_Returns401() throws Exception {
        UUID empId = UUID.randomUUID();
        BiometricPunchRequest req = new BiometricPunchRequest(empId, "DEV-01", PunchMethod.Fingerprint, Instant.now());

        mockMvc.perform(post("/api/v1/attendance/punch")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req)))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.error").value("Unauthorized"))
                .andExpect(jsonPath("$.message").value("Invalid or missing device API key."));
    }

    @Test
    @DisplayName("POST /api/v1/attendance/punch with incorrect X-Device-Api-Key returns 401 Unauthorized")
    void punchWithWrongApiKey_Returns401() throws Exception {
        UUID empId = UUID.randomUUID();
        BiometricPunchRequest req = new BiometricPunchRequest(empId, "DEV-01", PunchMethod.Fingerprint, Instant.now());

        mockMvc.perform(post("/api/v1/attendance/punch")
                        .header("X-Device-Api-Key", "wrong_key_xyz")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req)))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.error").value("Unauthorized"));
    }

    @Test
    @DisplayName("POST /api/v1/attendance/punch with valid X-Device-Api-Key succeeds (bypasses user JWT)")
    void punchWithValidApiKey_Returns201() throws Exception {
        UUID empId = UUID.randomUUID();
        BiometricPunchRequest req = new BiometricPunchRequest(empId, "DEV-01", PunchMethod.Fingerprint, Instant.now());

        AttendanceRecord record = AttendanceRecord.builder()
                .id(UUID.randomUUID())
                .employeeId(empId)
                .date(LocalDate.now())
                .clockIn(Instant.now())
                .status(AttendanceStatus.Normal)
                .build();

        when(attendanceService.processPunch(any(), any(), any(), any()))
                .thenReturn(new AttendanceService.PunchResult(record, false, "Clock-in recorded successfully."));

        mockMvc.perform(post("/api/v1/attendance/punch")
                        .header("X-Device-Api-Key", "test_secret_key_123")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req)))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.employeeId").value(empId.toString()))
                .andExpect(jsonPath("$.duplicate").value(false))
                .andExpect(jsonPath("$.message").value("Clock-in recorded successfully."));
    }
}
