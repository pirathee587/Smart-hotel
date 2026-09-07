package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.KdsOrder;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.KdsStatus;
import com.smarthotel.fieldops.domain.repository.KdsOrderRepository;
import com.smarthotel.fieldops.messaging.TaskEventPublisher;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class KdsServiceTest {

    @Mock
    private KdsOrderRepository kdsOrderRepository;

    @Mock
    private TaskEventPublisher taskEventPublisher;

    private KdsService kdsService;

    @BeforeEach
    void setUp() {
        kdsService = new KdsService(kdsOrderRepository, taskEventPublisher);
    }

    @Test
    @DisplayName("Create KDS order initializes with Received status and publishes event")
    void createOrder_InitializesStatusReceived() {
        when(kdsOrderRepository.save(any(KdsOrder.class))).thenAnswer(i -> i.getArgument(0));

        KdsOrder order = kdsService.createOrder("ORD-101", "Table 4", "[{\"item\":\"Kottu\",\"qty\":2}]");

        assertThat(order.getOrderNumber()).isEqualTo("ORD-101");
        assertThat(order.getTableOrRoomNumber()).isEqualTo("Table 4");
        assertThat(order.getStatus()).isEqualTo(KdsStatus.Received);
        assertThat(order.getReceivedAt()).isNotNull();

        verify(taskEventPublisher).publishKdsStatusChanged(order);
    }

    @Test
    @DisplayName("Update status advances through Preparing, Ready, Delivered lifecycle")
    void updateStatus_LifecycleProgression() {
        UUID orderId = UUID.randomUUID();
        UUID cookId = UUID.randomUUID();

        KdsOrder order = KdsOrder.builder()
                .id(orderId)
                .orderNumber("ORD-102")
                .tableOrRoomNumber("Room 204")
                .itemsJson("[]")
                .status(KdsStatus.Received)
                .build();

        when(kdsOrderRepository.findById(orderId)).thenReturn(Optional.of(order));
        when(kdsOrderRepository.save(any(KdsOrder.class))).thenAnswer(i -> i.getArgument(0));

        // 1. Preparing
        KdsOrder prep = kdsService.updateStatus(orderId, KdsStatus.Preparing, cookId);
        assertThat(prep.getStatus()).isEqualTo(KdsStatus.Preparing);
        assertThat(prep.getCookEmployeeId()).isEqualTo(cookId);
        assertThat(prep.getPreparingAt()).isNotNull();

        // 2. Ready
        KdsOrder ready = kdsService.updateStatus(orderId, KdsStatus.Ready, null);
        assertThat(ready.getStatus()).isEqualTo(KdsStatus.Ready);
        assertThat(ready.getReadyAt()).isNotNull();

        // 3. Delivered
        KdsOrder delivered = kdsService.updateStatus(orderId, KdsStatus.Delivered, null);
        assertThat(delivered.getStatus()).isEqualTo(KdsStatus.Delivered);
        assertThat(delivered.getDeliveredAt()).isNotNull();
    }

    @Test
    @DisplayName("GetActiveOrders queries only Received, Preparing, and Ready orders")
    void getActiveOrders_QueriesInFlightOrders() {
        KdsOrder o1 = KdsOrder.builder().status(KdsStatus.Received).build();
        KdsOrder o2 = KdsOrder.builder().status(KdsStatus.Preparing).build();

        when(kdsOrderRepository.findByStatusInOrderByReceivedAtAsc(
                List.of(KdsStatus.Received, KdsStatus.Preparing, KdsStatus.Ready)
        )).thenReturn(List.of(o1, o2));

        List<KdsOrder> active = kdsService.getActiveOrders();
        assertThat(active).hasSize(2);
    }
}
