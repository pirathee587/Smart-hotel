package com.smarthotel.fieldops.controller;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.KdsOrder;
import com.smarthotel.fieldops.domain.model.MenuItem;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.KdsStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OrderType;
import com.smarthotel.fieldops.domain.repository.KdsOrderRepository;
import com.smarthotel.fieldops.domain.repository.MenuItemRepository;
import com.smarthotel.fieldops.dto.KdsDtos.*;
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
import java.util.List;
import java.util.UUID;

import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class KdsControllerSecurityTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @Autowired
    private KdsOrderRepository kdsOrderRepository;

    @Autowired
    private MenuItemRepository menuItemRepository;

    @MockBean
    private JwtDecoder jwtDecoder;

    @MockBean
    private RabbitTemplate rabbitTemplate;

    private UUID testItemId;

    @BeforeEach
    void setUp() {
        kdsOrderRepository.deleteAll();
        menuItemRepository.deleteAll();

        MenuItem item = MenuItem.builder()
                .code("MAIN-KOTTU-TEST")
                .name("Test Kottu")
                .price(new BigDecimal("1500.00"))
                .currency("LKR")
                .category("Mains")
                .available(true)
                .roomServiceEligible(true)
                .build();
        MenuItem saved = menuItemRepository.save(item);
        testItemId = saved.getId();
    }

    @Test
    @DisplayName("Cross-department user (Housekeeper) is rejected with 403 Forbidden")
    void crossDepartmentUser_Housekeeper_Returns403() throws Exception {
        mockMvc.perform(get("/api/v1/kds/orders/active")
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Housekeeper"))
                                .jwt(j -> j.claim("role", "Housekeeper").claim("departmentCode", "HOUSEKEEPING"))))
                .andExpect(status().isForbidden());
    }

    @Test
    @DisplayName("Chef cannot create customer orders (Returns 403 Forbidden)")
    void chef_CannotCreateOrder_Returns403() throws Exception {
        CreateStructuredOrderRequest req = new CreateStructuredOrderRequest(
                OrderType.Restaurant, "Table 1", null, null, null, null, null,
                List.of(new OrderItemRequest(testItemId, 1, null))
        );

        mockMvc.perform(post("/api/v1/kds/orders")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req))
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Chef"))
                                .jwt(j -> j.claim("role", "Chef").claim("departmentCode", "FOODBEVERAGE"))))
                .andExpect(status().isForbidden());
    }

    @Test
    @DisplayName("Waiter cannot accept orders in kitchen queue (Returns 403 Forbidden)")
    void waiter_CannotAcceptKitchenOrder_Returns403() throws Exception {
        KdsOrder order = KdsOrder.builder()
                .orderNumber("FNB-TEST-001")
                .tableOrRoomNumber("Table 2")
                .status(KdsStatus.Received)
                .build();
        KdsOrder saved = kdsOrderRepository.save(order);

        UpdateKdsStatusRequest req = new UpdateKdsStatusRequest(KdsStatus.Accepted, null, null);

        mockMvc.perform(patch("/api/v1/kds/orders/" + saved.getId() + "/status")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req))
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Waiter"))
                                .jwt(j -> j.claim("role", "Waiter").claim("departmentCode", "FOODBEVERAGE"))))
                .andExpect(status().isForbidden());
    }

    @Test
    @DisplayName("Waiter can create order and view active orders")
    void waiter_CanCreateOrderAndGetActive() throws Exception {
        CreateStructuredOrderRequest req = new CreateStructuredOrderRequest(
                OrderType.Restaurant, "Table 4", null, null, null, "Guest Silva", "Mild spicy",
                List.of(new OrderItemRequest(testItemId, 2, null))
        );

        mockMvc.perform(post("/api/v1/kds/orders")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(req))
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Waiter"))
                                .jwt(j -> j.claim("role", "Waiter").claim("departmentCode", "FOODBEVERAGE"))))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.orderNumber").exists())
                .andExpect(jsonPath("$.status").value("Received"))
                .andExpect(jsonPath("$.subtotal").value(3000.0));
    }

    @Test
    @DisplayName("Chef can accept order and start preparation")
    void chef_CanAcceptAndPrepareOrder() throws Exception {
        KdsOrder order = KdsOrder.builder()
                .orderNumber("FNB-TEST-002")
                .tableOrRoomNumber("Table 5")
                .status(KdsStatus.Received)
                .build();
        KdsOrder saved = kdsOrderRepository.save(order);

        // 1. Accept
        UpdateKdsStatusRequest acceptReq = new UpdateKdsStatusRequest(KdsStatus.Accepted, null, null);
        mockMvc.perform(patch("/api/v1/kds/orders/" + saved.getId() + "/status")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(acceptReq))
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Chef"))
                                .jwt(j -> j.claim("role", "Chef").claim("departmentCode", "FOODBEVERAGE"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.status").value("Accepted"));

        // 2. Prepare
        UpdateKdsStatusRequest prepReq = new UpdateKdsStatusRequest(KdsStatus.Preparing, null, null);
        mockMvc.perform(patch("/api/v1/kds/orders/" + saved.getId() + "/status")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(prepReq))
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Chef"))
                                .jwt(j -> j.claim("role", "Chef").claim("departmentCode", "FOODBEVERAGE"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.status").value("Preparing"));
    }

    @Test
    @DisplayName("Manager can cancel order with mandatory reason")
    void manager_CanCancelOrder_WithReason() throws Exception {
        KdsOrder order = KdsOrder.builder()
                .orderNumber("FNB-TEST-003")
                .tableOrRoomNumber("Table 6")
                .status(KdsStatus.Preparing)
                .build();
        KdsOrder saved = kdsOrderRepository.save(order);

        CancelOrderRequest cancelReq = new CancelOrderRequest("Customer had to leave abruptly");

        mockMvc.perform(post("/api/v1/kds/orders/" + saved.getId() + "/cancel")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(cancelReq))
                        .with(jwt().authorities(new SimpleGrantedAuthority("ROLE_Manager"))
                                .jwt(j -> j.claim("role", "Manager").claim("departmentCode", "FOODBEVERAGE"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.status").value("Cancelled"))
                .andExpect(jsonPath("$.cancellationReason").value("Customer had to leave abruptly"));
    }

    @Test
    @DisplayName("Unauthenticated request returns 401 Unauthorized")
    void unauthenticatedRequest_Returns401() throws Exception {
        mockMvc.perform(get("/api/v1/kds/orders/active"))
                .andExpect(status().isUnauthorized());
    }
}
