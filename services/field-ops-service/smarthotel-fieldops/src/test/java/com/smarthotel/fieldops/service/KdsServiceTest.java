package com.smarthotel.fieldops.service;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.*;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.*;
import com.smarthotel.fieldops.domain.repository.*;
import com.smarthotel.fieldops.dto.KdsDtos.*;
import com.smarthotel.fieldops.messaging.TaskEventPublisher;
import com.smarthotel.fieldops.service.FinanceChargeClient.CreateFnbChargePayload;
import com.smarthotel.fieldops.service.FinanceChargeClient.FinanceChargeResult;
import com.smarthotel.fieldops.service.RoomServiceEligibilityClient.EligibilityResult;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.*;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class KdsServiceTest {

    @Mock
    private KdsOrderRepository kdsOrderRepository;

    @Mock
    private MenuItemRepository menuItemRepository;

    @Mock
    private FnbAuditLogRepository auditLogRepository;

    @Mock
    private FnbChargeOutboxRepository outboxRepository;

    @Mock
    private TaxRuleService taxRuleService;

    @Mock
    private RoomServiceEligibilityClient roomServiceEligibilityClient;

    @Mock
    private FnbChargeOutboxProcessor outboxProcessor;

    @Mock
    private TaskEventPublisher taskEventPublisher;

    private ObjectMapper objectMapper;
    private KdsService kdsService;

    @BeforeEach
    void setUp() {
        objectMapper = new ObjectMapper();
        kdsService = new KdsService(
                kdsOrderRepository,
                menuItemRepository,
                auditLogRepository,
                outboxRepository,
                taxRuleService,
                roomServiceEligibilityClient,
                outboxProcessor,
                taskEventPublisher,
                objectMapper
        );
    }

    // -------------------------------------------------------------------------
    // Legacy compatibility test
    // -------------------------------------------------------------------------

    @Test
    @DisplayName("Create legacy KDS order initializes with Received status and publishes event")
    void createOrder_InitializesStatusReceived() {
        when(kdsOrderRepository.save(any(KdsOrder.class))).thenAnswer(i -> i.getArgument(0));

        KdsOrder order = kdsService.createOrder("ORD-101", "Table 4", "[{\"item\":\"Kottu\",\"qty\":2}]");

        assertThat(order.getOrderNumber()).isEqualTo("ORD-101");
        assertThat(order.getTableOrRoomNumber()).isEqualTo("Table 4");
        assertThat(order.getStatus()).isEqualTo(KdsStatus.Received);
        assertThat(order.getReceivedAt()).isNotNull();

        verify(taskEventPublisher).publishKdsStatusChanged(order);
    }

    // -------------------------------------------------------------------------
    // Structured Order Creation & Pricing
    // -------------------------------------------------------------------------

    @Test
    @DisplayName("Create structured order computes authoritative price, tax snapshot, and persists Received status")
    void createStructuredOrder_ComputesAuthoritativePriceAndTax() {
        UUID itemId = UUID.randomUUID();
        MenuItem kottu = MenuItem.builder()
                .id(itemId)
                .code("MAIN-KOTTU-01")
                .name("Chicken Kottu Roti")
                .price(new BigDecimal("1800.00"))
                .currency("LKR")
                .available(true)
                .roomServiceEligible(true)
                .build();

        when(menuItemRepository.findById(itemId)).thenReturn(Optional.of(kottu));
        when(taxRuleService.resolveTaxRate(eq("Restaurant"), any(Instant.class))).thenReturn(new BigDecimal("0.05")); // 5% configurable tax
        when(kdsOrderRepository.save(any(KdsOrder.class))).thenAnswer(i -> i.getArgument(0));

        CreateStructuredOrderRequest req = new CreateStructuredOrderRequest(
                OrderType.Restaurant,
                "Table 5",
                null,
                null,
                null,
                "Guest Silva",
                "Spicy",
                List.of(new OrderItemRequest(itemId, 2, "Extra chillies"))
        );

        UUID waiterId = UUID.randomUUID();
        KdsOrder order = kdsService.createStructuredOrder(req, waiterId, "Waiter", null);

        // Subtotal = 1800 * 2 = 3600.00
        // Tax = 3600 * 0.05 = 180.00
        // Total = 3780.00
        assertThat(order.getSubtotal()).isEqualByComparingTo("3600.00");
        assertThat(order.getTaxAmount()).isEqualByComparingTo("180.00");
        assertThat(order.getTotalAmount()).isEqualByComparingTo("3780.00");
        assertThat(order.getStatus()).isEqualTo(KdsStatus.Received);
        assertThat(order.getWaiterEmployeeId()).isEqualTo(waiterId);

        verify(auditLogRepository).save(any(FnbAuditLog.class));
        verify(taskEventPublisher).publishKdsStatusChanged(order);
    }

    @Test
    @DisplayName("Historical snapshot immutability: Menu price changes do not affect already created orders")
    void historicalSnapshotImmutability_WhenMenuPriceChanges() {
        UUID itemId = UUID.randomUUID();
        MenuItem item = MenuItem.builder()
                .id(itemId)
                .code("BEV-TEA-01")
                .name("Ceylon Tea")
                .price(new BigDecimal("400.00"))
                .currency("LKR")
                .available(true)
                .roomServiceEligible(true)
                .build();

        when(menuItemRepository.findById(itemId)).thenReturn(Optional.of(item));
        when(taxRuleService.resolveTaxRate(any(), any())).thenReturn(BigDecimal.ZERO);
        when(kdsOrderRepository.save(any(KdsOrder.class))).thenAnswer(i -> i.getArgument(0));

        CreateStructuredOrderRequest req = new CreateStructuredOrderRequest(
                OrderType.Restaurant, "Table 1", null, null, null, null, null,
                List.of(new OrderItemRequest(itemId, 1, null))
        );

        KdsOrder order = kdsService.createStructuredOrder(req, UUID.randomUUID(), "Waiter", null);
        assertThat(order.getTotalAmount()).isEqualByComparingTo("400.00");

        // Simulate menu item price increase in database
        item.setPrice(new BigDecimal("600.00"));

        // Order total remains authoritative 400.00
        assertThat(order.getTotalAmount()).isEqualByComparingTo("400.00");
        assertThat(order.getItemsJson()).contains("\"unitPrice\":400.00");
    }

    // -------------------------------------------------------------------------
    // Room Service Eligibility & Fail Closed
    // -------------------------------------------------------------------------

    @Test
    @DisplayName("Room service order succeeds when room is occupied and verified by authoritative services")
    void createRoomServiceOrder_WhenRoomOccupied_Succeeds() {
        UUID itemId = UUID.randomUUID();
        MenuItem item = MenuItem.builder()
                .id(itemId)
                .name("Burger")
                .price(new BigDecimal("2000.00"))
                .available(true)
                .roomServiceEligible(true)
                .build();

        when(menuItemRepository.findById(itemId)).thenReturn(Optional.of(item));
        when(taxRuleService.resolveTaxRate(any(), any())).thenReturn(BigDecimal.ZERO);
        when(kdsOrderRepository.save(any(KdsOrder.class))).thenAnswer(i -> i.getArgument(0));

        UUID bookingId = UUID.randomUUID();
        when(roomServiceEligibilityClient.verifyEligibility(eq("301"), any(), any()))
                .thenReturn(new EligibilityResult(true, "Occupied and checked in", bookingId, "Mr. Perera"));

        CreateStructuredOrderRequest req = new CreateStructuredOrderRequest(
                OrderType.RoomService, "Room 301", "301", null, null, null, null,
                List.of(new OrderItemRequest(itemId, 1, null))
        );

        KdsOrder order = kdsService.createStructuredOrder(req, UUID.randomUUID(), "Waiter", "Bearer token");
        assertThat(order.getOrderType()).isEqualTo(OrderType.RoomService);
        assertThat(order.getRoomNumber()).isEqualTo("301");
        assertThat(order.getBookingId()).isEqualTo(bookingId);
        assertThat(order.getCustomerName()).isEqualTo("Mr. Perera");
    }

    @Test
    @DisplayName("Room service order fails closed when room is vacant or dirty")
    void createRoomServiceOrder_WhenRoomVacant_RejectsOrder() {
        when(roomServiceEligibilityClient.verifyEligibility(eq("102"), any(), any()))
                .thenReturn(new EligibilityResult(false, "Room 102 is currently Available. Room service requires an Occupied room.", null, null));

        CreateStructuredOrderRequest req = new CreateStructuredOrderRequest(
                OrderType.RoomService, "Room 102", "102", null, null, null, null,
                List.of(new OrderItemRequest(UUID.randomUUID(), 1, null))
        );

        assertThatThrownBy(() -> kdsService.createStructuredOrder(req, UUID.randomUUID(), "Waiter", null))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("Room service order rejected");
    }

    @Test
    @DisplayName("Room service order fails closed when downstream service throws exception")
    void createRoomServiceOrder_WhenServiceDown_FailsClosed() {
        when(roomServiceEligibilityClient.verifyEligibility(eq("205"), any(), any()))
                .thenThrow(new IllegalStateException("Authoritative room service eligibility verification failed: Connection refused"));

        CreateStructuredOrderRequest req = new CreateStructuredOrderRequest(
                OrderType.RoomService, "Room 205", "205", null, null, null, null,
                List.of(new OrderItemRequest(UUID.randomUUID(), 1, null))
        );

        assertThatThrownBy(() -> kdsService.createStructuredOrder(req, UUID.randomUUID(), "Waiter", null))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("Connection refused");
    }

    // -------------------------------------------------------------------------
    // Operational Workflow State Transitions
    // -------------------------------------------------------------------------

    @Test
    @DisplayName("Complete operational lifecycle: Accept -> Prepare -> Ready -> Collect -> Deliver")
    void completeOperationalLifecycle() {
        UUID orderId = UUID.randomUUID();
        UUID chefId = UUID.randomUUID();
        UUID waiterId = UUID.randomUUID();

        KdsOrder order = KdsOrder.builder()
                .id(orderId)
                .orderNumber("FNB-20260918-A1B2")
                .status(KdsStatus.Received)
                .subtotal(new BigDecimal("3000.00"))
                .taxAmount(BigDecimal.ZERO)
                .totalAmount(new BigDecimal("3000.00"))
                .currency("LKR")
                .tableOrRoomNumber("Table 3")
                .chargeStatus(ChargeStatus.PendingSubmission)
                .build();

        when(kdsOrderRepository.findById(orderId)).thenReturn(Optional.of(order));
        when(kdsOrderRepository.save(any(KdsOrder.class))).thenAnswer(i -> i.getArgument(0));

        // 1. Chef Accepts
        KdsOrder accepted = kdsService.acceptOrder(orderId, chefId, "Chef Gordon", "Chef");
        assertThat(accepted.getStatus()).isEqualTo(KdsStatus.Accepted);
        assertThat(accepted.getCookEmployeeId()).isEqualTo(chefId);

        // 2. Chef Starts Preparing
        KdsOrder preparing = kdsService.startPreparing(orderId, chefId, "Chef Gordon", "Chef");
        assertThat(preparing.getStatus()).isEqualTo(KdsStatus.Preparing);

        // 3. Chef Marks Ready
        KdsOrder ready = kdsService.markReady(orderId, chefId, "Chef");
        assertThat(ready.getStatus()).isEqualTo(KdsStatus.Ready);

        // 4. Waiter Collects
        KdsOrder collected = kdsService.markCollected(orderId, waiterId, "Waiter Sunil", "Waiter");
        assertThat(collected.getStatus()).isEqualTo(KdsStatus.Collected);
        assertThat(collected.getWaiterEmployeeId()).isEqualTo(waiterId);

        // 5. Waiter Delivers (persists durable outbox record)
        when(outboxProcessor.processSingleOutbox(any(FnbChargeOutbox.class), any()))
                .thenReturn(new FinanceChargeResult(true, UUID.randomUUID(), "INV-101", "Validated", "Invoiced"));

        KdsOrder delivered = kdsService.markDelivered(orderId, waiterId, "Waiter Sunil", "Waiter", "token");
        assertThat(delivered.getStatus()).isEqualTo(KdsStatus.Delivered);
        assertThat(delivered.getDeliveredAt()).isNotNull();

        // Verify outbox record was transactionally created
        ArgumentCaptor<FnbChargeOutbox> outboxCaptor = ArgumentCaptor.forClass(FnbChargeOutbox.class);
        verify(outboxRepository).save(outboxCaptor.capture());
        FnbChargeOutbox capturedOutbox = outboxCaptor.getValue();
        assertThat(capturedOutbox.getOrderId()).isEqualTo(orderId);
        assertThat(capturedOutbox.getChargeReference()).isEqualTo("fnb:order:" + orderId);
    }

    @Test
    @DisplayName("Delivery does not mark financially settled before Finance confirms it")
    void markDelivered_WhenFinanceFails_PreservesDeliveredStatusAndPendingConfirmation() {
        UUID orderId = UUID.randomUUID();
        KdsOrder order = KdsOrder.builder()
                .id(orderId)
                .orderNumber("FNB-20260918-FAIL")
                .status(KdsStatus.Ready)
                .subtotal(new BigDecimal("1500.00"))
                .totalAmount(new BigDecimal("1500.00"))
                .currency("LKR")
                .tableOrRoomNumber("Table 2")
                .chargeStatus(ChargeStatus.PendingSubmission)
                .build();

        when(kdsOrderRepository.findById(orderId)).thenReturn(Optional.of(order));
        when(kdsOrderRepository.save(any(KdsOrder.class))).thenAnswer(i -> i.getArgument(0));

        // Simulate Finance service returning failure (e.g. 503 or network timeout)
        when(outboxProcessor.processSingleOutbox(any(FnbChargeOutbox.class), any()))
                .thenReturn(new FinanceChargeResult(false, null, null, "Failed", "Finance 503 Service Unavailable"));

        KdsOrder delivered = kdsService.markDelivered(orderId, UUID.randomUUID(), "Waiter", "Waiter", "token");

        // Order is Delivered, but chargeStatus is NOT settled
        assertThat(delivered.getStatus()).isEqualTo(KdsStatus.Delivered);
        assertThat(delivered.getChargeStatus()).isEqualTo(ChargeStatus.PendingFinanceConfirmation);
        verify(outboxRepository).save(any(FnbChargeOutbox.class));
    }

    @Test
    @DisplayName("Invalid state transitions are rejected with IllegalStateException")
    void invalidStatusTransitions_ThrowException() {
        UUID orderId = UUID.randomUUID();
        KdsOrder order = KdsOrder.builder()
                .id(orderId)
                .status(KdsStatus.Received)
                .build();

        when(kdsOrderRepository.findById(orderId)).thenReturn(Optional.of(order));

        // Cannot mark ready directly from Received
        assertThatThrownBy(() -> kdsService.markReady(orderId, UUID.randomUUID(), "Chef"))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("Order can only be marked ready when in Preparing status");

        // Cannot collect an order that is Received
        assertThatThrownBy(() -> kdsService.markCollected(orderId, UUID.randomUUID(), "Waiter", "Waiter"))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("Order can only be collected when in Ready status");
    }

    // -------------------------------------------------------------------------
    // Role Boundary & Manager Override Enforcement
    // -------------------------------------------------------------------------

    @Test
    @DisplayName("Chef cannot deliver orders, and Waiter cannot accept kitchen orders")
    void roleEnforcement_ChefAndWaiterBoundaries() {
        UUID orderId = UUID.randomUUID();
        KdsOrder order = KdsOrder.builder().id(orderId).status(KdsStatus.Received).build();

        // Chef cannot deliver
        assertThatThrownBy(() -> kdsService.markDelivered(orderId, UUID.randomUUID(), "Chef", "Chef", null))
                .isInstanceOf(SecurityException.class)
                .hasMessageContaining("Only Waiters or F&B Management");

        // Waiter cannot accept
        assertThatThrownBy(() -> kdsService.acceptOrder(orderId, UUID.randomUUID(), "Waiter", "Waiter"))
                .isInstanceOf(SecurityException.class)
                .hasMessageContaining("Only Kitchen Staff (Chef) or F&B Management");
    }

    @Test
    @DisplayName("Manager cancellation requires mandatory reason of at least 5 characters")
    void cancelOrder_RequiresValidReason() {
        UUID orderId = UUID.randomUUID();

        assertThatThrownBy(() -> kdsService.cancelOrder(orderId, "No", UUID.randomUUID(), "Manager"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("at least 5 characters");
    }

    @Test
    @DisplayName("Delivered orders cannot be cancelled directly without credit-note coordination")
    void cancelOrder_WhenDelivered_RejectsDirectCancellation() {
        UUID orderId = UUID.randomUUID();
        KdsOrder order = KdsOrder.builder()
                .id(orderId)
                .status(KdsStatus.Delivered)
                .build();

        when(kdsOrderRepository.findById(orderId)).thenReturn(Optional.of(order));

        assertThatThrownBy(() -> kdsService.cancelOrder(orderId, "Customer changed mind after delivery", UUID.randomUUID(), "Manager"))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("Delivered orders cannot be cancelled directly");
    }
}
