package com.smarthotel.fieldops.service;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.FnbChargeOutbox;
import com.smarthotel.fieldops.domain.model.KdsOrder;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.ChargeStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OutboxStatus;
import com.smarthotel.fieldops.domain.repository.FnbChargeOutboxRepository;
import com.smarthotel.fieldops.domain.repository.KdsOrderRepository;
import com.smarthotel.fieldops.service.FinanceChargeClient.CreateFnbChargePayload;
import com.smarthotel.fieldops.service.FinanceChargeClient.FinanceChargeResult;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class FnbChargeOutboxTest {

    @Mock
    private FnbChargeOutboxRepository outboxRepository;

    @Mock
    private KdsOrderRepository orderRepository;

    @Mock
    private FinanceChargeClient financeChargeClient;

    private ObjectMapper objectMapper;
    private FnbChargeOutboxProcessor processor;

    @BeforeEach
    void setUp() {
        objectMapper = new ObjectMapper();
        processor = new FnbChargeOutboxProcessor(
                outboxRepository,
                orderRepository,
                financeChargeClient,
                objectMapper
        );
    }

    @Test
    @DisplayName("Outbox processor: Confirms outbox and sets order to Invoiced when Finance succeeds")
    void processOutbox_WhenFinanceSucceeds_MarksConfirmedAndInvoiced() throws Exception {
        UUID orderId = UUID.randomUUID();
        UUID invoiceId = UUID.randomUUID();
        String invoiceNumber = "INV-FNB-20260918-001";

        CreateFnbChargePayload payload = new CreateFnbChargePayload(
                orderId, "FNB-001", "Restaurant", "Table 1", null, "Guest",
                new BigDecimal("2000.00"), BigDecimal.ZERO, new BigDecimal("2000.00"),
                "LKR", "fnb:order:" + orderId
        );

        FnbChargeOutbox outbox = FnbChargeOutbox.builder()
                .id(UUID.randomUUID())
                .orderId(orderId)
                .chargeReference("fnb:order:" + orderId)
                .payloadJson(objectMapper.writeValueAsString(payload))
                .status(OutboxStatus.Pending)
                .build();

        KdsOrder order = KdsOrder.builder()
                .id(orderId)
                .chargeStatus(ChargeStatus.PendingFinanceConfirmation)
                .build();

        when(financeChargeClient.submitFnbCharge(any(), any()))
                .thenReturn(new FinanceChargeResult(true, invoiceId, invoiceNumber, "Validated", "Success"));
        when(outboxRepository.save(any())).thenAnswer(i -> i.getArgument(0));
        when(orderRepository.findById(orderId)).thenReturn(Optional.of(order));
        when(orderRepository.save(any())).thenAnswer(i -> i.getArgument(0));

        FinanceChargeResult result = processor.processSingleOutbox(outbox, "token");

        assertThat(result.success()).isTrue();
        assertThat(outbox.getStatus()).isEqualTo(OutboxStatus.Confirmed);
        assertThat(outbox.getFinanceInvoiceNumber()).isEqualTo(invoiceNumber);
        assertThat(order.getChargeStatus()).isEqualTo(ChargeStatus.Invoiced);
        assertThat(order.getFinanceInvoiceNumber()).isEqualTo(invoiceNumber);
    }

    @Test
    @DisplayName("Outbox processor: Handles Finance 503 outage, increments retry count, and preserves pending status")
    void processOutbox_WhenFinanceOutage_IncrementsRetryAndPreservesPending() throws Exception {
        UUID orderId = UUID.randomUUID();

        CreateFnbChargePayload payload = new CreateFnbChargePayload(
                orderId, "FNB-002", "Restaurant", "Table 2", null, "Guest",
                new BigDecimal("1500.00"), BigDecimal.ZERO, new BigDecimal("1500.00"),
                "LKR", "fnb:order:" + orderId
        );

        FnbChargeOutbox outbox = FnbChargeOutbox.builder()
                .id(UUID.randomUUID())
                .orderId(orderId)
                .chargeReference("fnb:order:" + orderId)
                .payloadJson(objectMapper.writeValueAsString(payload))
                .status(OutboxStatus.Pending)
                .retryCount(1)
                .build();

        KdsOrder order = KdsOrder.builder()
                .id(orderId)
                .chargeStatus(ChargeStatus.PendingFinanceConfirmation)
                .build();

        when(financeChargeClient.submitFnbCharge(any(), any()))
                .thenReturn(new FinanceChargeResult(false, null, null, "Failed", "503 Service Unavailable"));
        when(outboxRepository.save(any())).thenAnswer(i -> i.getArgument(0));
        when(orderRepository.findById(orderId)).thenReturn(Optional.of(order));

        FinanceChargeResult result = processor.processSingleOutbox(outbox, null);

        assertThat(result.success()).isFalse();
        assertThat(outbox.getRetryCount()).isEqualTo(2);
        assertThat(outbox.getStatus()).isEqualTo(OutboxStatus.Pending);
        assertThat(outbox.getNextRetryAt()).isAfter(Instant.now());
        assertThat(order.getChargeStatus()).isEqualTo(ChargeStatus.PendingFinanceConfirmation);
        assertThat(order.getChargeError()).contains("503 Service Unavailable");
    }

    @Test
    @DisplayName("Reconciliation: Idempotent duplicate posting returns existing invoice without duplication")
    void reconciliation_DuplicateCharge_ReturnsExistingInvoice() throws Exception {
        UUID orderId = UUID.randomUUID();
        UUID invoiceId = UUID.randomUUID();

        CreateFnbChargePayload payload = new CreateFnbChargePayload(
                orderId, "FNB-003", "Restaurant", "Table 3", null, "Guest",
                new BigDecimal("2500.00"), BigDecimal.ZERO, new BigDecimal("2500.00"),
                "LKR", "fnb:order:" + orderId
        );

        FnbChargeOutbox outbox = FnbChargeOutbox.builder()
                .id(UUID.randomUUID())
                .orderId(orderId)
                .chargeReference("fnb:order:" + orderId)
                .payloadJson(objectMapper.writeValueAsString(payload))
                .status(OutboxStatus.Pending)
                .retryCount(2)
                .build();

        KdsOrder order = KdsOrder.builder()
                .id(orderId)
                .chargeStatus(ChargeStatus.PendingFinanceConfirmation)
                .build();

        // Finance acknowledges idempotency with existing invoice
        when(financeChargeClient.submitFnbCharge(any(), any()))
                .thenReturn(new FinanceChargeResult(true, invoiceId, "INV-FNB-EXISTING", "Validated", "Existing invoice returned"));
        when(outboxRepository.save(any())).thenAnswer(i -> i.getArgument(0));
        when(orderRepository.findById(orderId)).thenReturn(Optional.of(order));
        when(orderRepository.save(any())).thenAnswer(i -> i.getArgument(0));

        FinanceChargeResult result = processor.processSingleOutbox(outbox, null);

        assertThat(result.success()).isTrue();
        assertThat(outbox.getStatus()).isEqualTo(OutboxStatus.Confirmed);
        assertThat(outbox.getFinanceInvoiceNumber()).isEqualTo("INV-FNB-EXISTING");
        assertThat(order.getChargeStatus()).isEqualTo(ChargeStatus.Invoiced);
    }
}
