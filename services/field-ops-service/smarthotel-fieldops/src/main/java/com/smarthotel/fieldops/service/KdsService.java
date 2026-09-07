package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.KdsOrder;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.KdsStatus;
import com.smarthotel.fieldops.domain.repository.KdsOrderRepository;
import com.smarthotel.fieldops.messaging.TaskEventPublisher;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

@Service
@RequiredArgsConstructor
@Slf4j
public class KdsService {

    private final KdsOrderRepository kdsOrderRepository;
    private final TaskEventPublisher taskEventPublisher;

    @Transactional
    public KdsOrder createOrder(String orderNumber, String tableOrRoomNumber, String itemsJson) {
        KdsOrder order = KdsOrder.builder()
                .orderNumber(orderNumber)
                .tableOrRoomNumber(tableOrRoomNumber)
                .itemsJson(itemsJson)
                .status(KdsStatus.Received)
                .build();

        KdsOrder saved = kdsOrderRepository.save(order);
        taskEventPublisher.publishKdsStatusChanged(saved);
        return saved;
    }

    @Transactional
    public KdsOrder updateStatus(UUID orderId, KdsStatus newStatus, UUID cookEmployeeId) {
        KdsOrder order = kdsOrderRepository.findById(orderId)
                .orElseThrow(() -> new IllegalArgumentException("KDS order not found with ID: " + orderId));

        switch (newStatus) {
            case Preparing -> order.startPreparing(cookEmployeeId);
            case Ready -> order.markReady();
            case Delivered -> order.markDelivered();
            case Cancelled -> order.cancel();
            default -> order.setStatus(newStatus);
        }

        KdsOrder updated = kdsOrderRepository.save(order);
        taskEventPublisher.publishKdsStatusChanged(updated);
        return updated;
    }

    @Transactional(readOnly = true)
    public List<KdsOrder> getActiveOrders() {
        return kdsOrderRepository.findByStatusInOrderByReceivedAtAsc(
                List.of(KdsStatus.Received, KdsStatus.Preparing, KdsStatus.Ready)
        );
    }

    @Transactional(readOnly = true)
    public Optional<KdsOrder> getOrderById(UUID orderId) {
        return kdsOrderRepository.findById(orderId);
    }
}
