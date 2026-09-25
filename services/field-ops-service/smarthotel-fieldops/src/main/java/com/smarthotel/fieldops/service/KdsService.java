package com.smarthotel.fieldops.service;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.*;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.*;
import com.smarthotel.fieldops.domain.repository.*;
import com.smarthotel.fieldops.dto.KdsDtos.*;
import com.smarthotel.fieldops.messaging.TaskEventPublisher;
import com.smarthotel.fieldops.service.FinanceChargeClient.CreateFnbChargePayload;
import com.smarthotel.fieldops.service.FinanceChargeClient.FinanceChargeResult;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.math.RoundingMode;
import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.util.*;

@Service
@RequiredArgsConstructor
@Slf4j
public class KdsService {

    private final KdsOrderRepository kdsOrderRepository;
    private final MenuItemRepository menuItemRepository;
    private final FnbAuditLogRepository auditLogRepository;
    private final FnbChargeOutboxRepository outboxRepository;
    private final TaxRuleService taxRuleService;
    private final RoomServiceEligibilityClient roomServiceEligibilityClient;
    private final FnbChargeOutboxProcessor outboxProcessor;
    private final TaskEventPublisher taskEventPublisher;
    private final ObjectMapper objectMapper;

    // -------------------------------------------------------------------------
    // Order Creation
    // -------------------------------------------------------------------------

    @Transactional
    public KdsOrder createStructuredOrder(
            CreateStructuredOrderRequest req,
            UUID actorUserId,
            String actorRole,
            String token) {

        if (req.items() == null || req.items().isEmpty()) {
            throw new IllegalArgumentException("Order must contain at least one menu item");
        }

        // 1. Room service validation (fail closed)
        UUID resolvedBookingId = req.bookingId();
        String resolvedGuestName = req.customerName();
        String roomNumber = req.roomNumber();

        if (req.orderType() == OrderType.RoomService) {
            if (roomNumber == null || roomNumber.isBlank()) {
                roomNumber = req.tableOrRoomNumber();
            }
            if (roomNumber == null || roomNumber.isBlank()) {
                throw new IllegalArgumentException("Room number is required for room-service orders");
            }

            var eligibility = roomServiceEligibilityClient.verifyEligibility(roomNumber, resolvedBookingId, token);
            if (!eligibility.eligible()) {
                throw new IllegalStateException("Room service order rejected: " + eligibility.reason());
            }

            if (resolvedBookingId == null) {
                resolvedBookingId = eligibility.bookingId();
            }
            if (resolvedGuestName == null) {
                resolvedGuestName = eligibility.guestName();
            }
        }

        // 2. Validate menu items and snapshot authoritative prices
        List<OrderItemSnapshot> snapshots = new ArrayList<>();
        BigDecimal orderSubtotal = BigDecimal.ZERO;
        BigDecimal orderTax = BigDecimal.ZERO;

        Instant now = Instant.now();
        BigDecimal applicableTaxRate = taxRuleService.resolveTaxRate(req.orderType().name(), now);

        for (OrderItemRequest itemReq : req.items()) {
            MenuItem menuItem = menuItemRepository.findById(itemReq.menuItemId())
                    .orElseThrow(() -> new IllegalArgumentException("Menu item not found with ID: " + itemReq.menuItemId()));

            if (!menuItem.isAvailable()) {
                throw new IllegalStateException("Menu item '" + menuItem.getName() + "' is currently unavailable");
            }

            if (req.orderType() == OrderType.RoomService && !menuItem.isRoomServiceEligible()) {
                throw new IllegalStateException("Menu item '" + menuItem.getName() + "' is not eligible for room service");
            }

            BigDecimal unitPrice = menuItem.getPrice();
            BigDecimal lineSubtotal = unitPrice.multiply(BigDecimal.valueOf(itemReq.quantity()));
            BigDecimal lineTax = lineSubtotal.multiply(applicableTaxRate).setScale(2, RoundingMode.HALF_UP);
            BigDecimal lineTotal = lineSubtotal.add(lineTax);

            snapshots.add(new OrderItemSnapshot(
                    menuItem.getId(),
                    menuItem.getCode(),
                    menuItem.getName(),
                    unitPrice,
                    itemReq.quantity(),
                    lineSubtotal,
                    applicableTaxRate,
                    lineTax,
                    lineTotal,
                    itemReq.specialInstructions()
            ));

            orderSubtotal = orderSubtotal.add(lineSubtotal);
            orderTax = orderTax.add(lineTax);
        }

        BigDecimal orderTotal = orderSubtotal.add(orderTax);

        String itemsJson;
        try {
            itemsJson = objectMapper.writeValueAsString(snapshots);
        } catch (Exception e) {
            throw new RuntimeException("Failed to serialize order items snapshot: " + e.getMessage(), e);
        }

        String orderNumber = generateOrderNumber();
        String location = req.orderType() == OrderType.RoomService
                ? "Room " + roomNumber
                : (req.tableOrRoomNumber() != null ? req.tableOrRoomNumber() : "Table");

        KdsOrder order = KdsOrder.builder()
                .orderNumber(orderNumber)
                .orderType(req.orderType())
                .tableOrRoomNumber(location)
                .roomNumber(roomNumber)
                .bookingId(resolvedBookingId)
                .customerId(req.customerId())
                .customerName(resolvedGuestName)
                .itemsJson(itemsJson)
                .status(KdsStatus.Received)
                .subtotal(orderSubtotal)
                .taxAmount(orderTax)
                .totalAmount(orderTotal)
                .currency("LKR")
                .notes(req.notes())
                .chargeStatus(ChargeStatus.PendingSubmission)
                .build();

        if ("Waiter".equalsIgnoreCase(actorRole) && actorUserId != null) {
            order.setWaiterEmployeeId(actorUserId);
        }

        KdsOrder saved = kdsOrderRepository.save(order);

        logAudit(saved.getId(), saved.getOrderNumber(), "OrderCreated", actorUserId, actorRole,
                "Order created for " + location + " with " + snapshots.size() + " items. Total: " + orderTotal + " LKR",
                null, null, KdsStatus.Received.name());

        taskEventPublisher.publishKdsStatusChanged(saved);
        return saved;
    }

    // Legacy method for existing tests/compatibility
    @Transactional
    public KdsOrder createOrder(String orderNumber, String tableOrRoomNumber, String itemsJson) {
        KdsOrder order = KdsOrder.builder()
                .orderNumber(orderNumber)
                .tableOrRoomNumber(tableOrRoomNumber)
                .itemsJson(itemsJson)
                .status(KdsStatus.Received)
                .chargeStatus(ChargeStatus.PendingSubmission)
                .build();

        KdsOrder saved = kdsOrderRepository.save(order);
        taskEventPublisher.publishKdsStatusChanged(saved);
        return saved;
    }

    // -------------------------------------------------------------------------
    // Order Lifecycle State Machine
    // -------------------------------------------------------------------------

    @Transactional
    public KdsOrder acceptOrder(UUID orderId, UUID chefId, String chefName, String actorRole) {
        validateChefOrManagerRole(actorRole, "accept order");

        KdsOrder order = findOrderOrThrow(orderId);
        String prev = order.getStatus().name();
        order.accept(chefId, chefName);

        KdsOrder updated = kdsOrderRepository.save(order);
        logAudit(order.getId(), order.getOrderNumber(), "OrderAccepted", chefId, actorRole,
                "Chef " + chefName + " accepted order", null, prev, KdsStatus.Accepted.name());

        taskEventPublisher.publishKdsStatusChanged(updated);
        return updated;
    }

    @Transactional
    public KdsOrder startPreparing(UUID orderId, UUID chefId, String chefName, String actorRole) {
        validateChefOrManagerRole(actorRole, "start preparing order");

        KdsOrder order = findOrderOrThrow(orderId);
        String prev = order.getStatus().name();
        order.startPreparing(chefId, chefName);

        KdsOrder updated = kdsOrderRepository.save(order);
        logAudit(order.getId(), order.getOrderNumber(), "OrderPreparing", chefId, actorRole,
                "Kitchen started preparing order", null, prev, KdsStatus.Preparing.name());

        taskEventPublisher.publishKdsStatusChanged(updated);
        return updated;
    }

    @Transactional
    public KdsOrder markReady(UUID orderId, UUID actorUserId, String actorRole) {
        validateChefOrManagerRole(actorRole, "mark order ready");

        KdsOrder order = findOrderOrThrow(orderId);
        String prev = order.getStatus().name();
        order.markReady();

        KdsOrder updated = kdsOrderRepository.save(order);
        logAudit(order.getId(), order.getOrderNumber(), "OrderReady", actorUserId, actorRole,
                "Order is ready at hot pass for collection", null, prev, KdsStatus.Ready.name());

        taskEventPublisher.publishKdsStatusChanged(updated);
        return updated;
    }

    @Transactional
    public KdsOrder markCollected(UUID orderId, UUID waiterId, String waiterName, String actorRole) {
        validateWaiterOrManagerRole(actorRole, "collect order");

        KdsOrder order = findOrderOrThrow(orderId);
        String prev = order.getStatus().name();
        order.markCollected(waiterId, waiterName);

        KdsOrder updated = kdsOrderRepository.save(order);
        logAudit(order.getId(), order.getOrderNumber(), "OrderCollected", waiterId, actorRole,
                "Waiter " + waiterName + " collected order for delivery", null, prev, KdsStatus.Collected.name());

        taskEventPublisher.publishKdsStatusChanged(updated);
        return updated;
    }

    @Transactional
    public KdsOrder markDelivered(UUID orderId, UUID waiterId, String waiterName, String actorRole, String token) {
        validateWaiterOrManagerRole(actorRole, "deliver order");

        KdsOrder order = findOrderOrThrow(orderId);
        if (order.getStatus() == KdsStatus.Delivered) {
            // Idempotent delivery
            return order;
        }

        String prev = order.getStatus().name();
        order.markDelivered(waiterId, waiterName);

        // Transactionally create durable outbox record for Finance posting
        String chargeRef = "fnb:order:" + order.getId();
        CreateFnbChargePayload payload = new CreateFnbChargePayload(
                order.getId(),
                order.getOrderNumber(),
                order.getOrderType().name(),
                order.getTableOrRoomNumber(),
                order.getBookingId(),
                order.getCustomerName(),
                order.getSubtotal() != null ? order.getSubtotal() : BigDecimal.ZERO,
                order.getTaxAmount() != null ? order.getTaxAmount() : BigDecimal.ZERO,
                order.getTotalAmount() != null ? order.getTotalAmount() : BigDecimal.ZERO,
                order.getCurrency() != null ? order.getCurrency() : "LKR",
                chargeRef
        );

        String payloadJson;
        try {
            payloadJson = objectMapper.writeValueAsString(payload);
        } catch (Exception e) {
            throw new RuntimeException("Failed to serialize charge outbox payload: " + e.getMessage(), e);
        }

        FnbChargeOutbox outbox = outboxRepository.findByOrderId(order.getId())
                .orElse(FnbChargeOutbox.builder()
                        .orderId(order.getId())
                        .chargeReference(chargeRef)
                        .payloadJson(payloadJson)
                        .status(OutboxStatus.Pending)
                        .build());

        outbox.setPayloadJson(payloadJson);
        outboxRepository.save(outbox);

        KdsOrder saved = kdsOrderRepository.save(order);

        logAudit(saved.getId(), saved.getOrderNumber(), "OrderDelivered", waiterId, actorRole,
                "Order delivered. Charge outbox queued for Finance posting.", null, prev, KdsStatus.Delivered.name());

        taskEventPublisher.publishKdsStatusChanged(saved);

        // Attempt immediate dispatch (if it fails, outbox processor will retry; order remains Delivered)
        try {
            outboxProcessor.processSingleOutbox(outbox, token);
        } catch (Exception e) {
            log.warn("Immediate Finance posting failed for order {}: {}. Will be retried by outbox processor.",
                    saved.getOrderNumber(), e.getMessage());
        }

        return kdsOrderRepository.findById(orderId).orElse(saved);
    }

    @Transactional
    public KdsOrder cancelOrder(UUID orderId, String reason, UUID actorId, String actorRole) {
        validateManagerOrOwnerRole(actorRole, "cancel order");

        if (reason == null || reason.trim().length() < 5) {
            throw new IllegalArgumentException("Cancellation requires a detailed reason of at least 5 characters");
        }

        KdsOrder order = findOrderOrThrow(orderId);
        String prev = order.getStatus().name();
        order.cancel(reason, actorId, actorRole);

        KdsOrder updated = kdsOrderRepository.save(order);
        logAudit(order.getId(), order.getOrderNumber(), "OrderCancelled", actorId, actorRole,
                "Order cancelled by manager: " + reason, reason, prev, KdsStatus.Cancelled.name());

        taskEventPublisher.publishKdsStatusChanged(updated);
        return updated;
    }

    @Transactional
    public KdsOrder assignOrder(UUID orderId, UUID employeeId, String role, UUID actorId, String actorRole) {
        validateManagerOrOwnerRole(actorRole, "assign order staff");

        KdsOrder order = findOrderOrThrow(orderId);

        if ("Chef".equalsIgnoreCase(role)) {
            order.setCookEmployeeId(employeeId);
            order.setChefName("Chef " + employeeId.toString().substring(0, 8));
        } else if ("Waiter".equalsIgnoreCase(role)) {
            order.setWaiterEmployeeId(employeeId);
            order.setWaiterName("Waiter " + employeeId.toString().substring(0, 8));
        } else {
            throw new IllegalArgumentException("Target role must be 'Chef' or 'Waiter'");
        }

        KdsOrder updated = kdsOrderRepository.save(order);
        logAudit(order.getId(), order.getOrderNumber(), "OrderAssigned", actorId, actorRole,
                "Assigned " + role + " (" + employeeId + ") to order", null, null, null);

        taskEventPublisher.publishKdsStatusChanged(updated);
        return updated;
    }

    @Transactional
    public FinanceChargeResult retryFinanceCharge(UUID orderId, String token, UUID actorId, String actorRole) {
        validateWaiterOrManagerRole(actorRole, "retry finance charge");

        KdsOrder order = findOrderOrThrow(orderId);
        if (order.getStatus() != KdsStatus.Delivered) {
            throw new IllegalStateException("Finance charge can only be posted for Delivered orders. Current: " + order.getStatus());
        }

        FinanceChargeResult result = outboxProcessor.retryOrderCharge(orderId, token);

        logAudit(order.getId(), order.getOrderNumber(), "FinanceChargeRetry", actorId, actorRole,
                "Manual retry finance charge: " + result.message(), null, null, null);

        return result;
    }

    // Legacy updateStatus for backward compatibility
    @Transactional
    public KdsOrder updateStatus(UUID orderId, KdsStatus newStatus, UUID cookEmployeeId) {
        KdsOrder order = findOrderOrThrow(orderId);

        switch (newStatus) {
            case Accepted -> order.accept(cookEmployeeId, null);
            case Preparing -> order.startPreparing(cookEmployeeId);
            case Ready -> order.markReady();
            case Collected -> order.markCollected(null, null);
            case Delivered -> order.markDelivered();
            case Cancelled -> order.cancel();
            default -> order.setStatus(newStatus);
        }

        KdsOrder updated = kdsOrderRepository.save(order);
        taskEventPublisher.publishKdsStatusChanged(updated);
        return updated;
    }

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    @Transactional(readOnly = true)
    public List<KdsOrder> getActiveOrders() {
        return kdsOrderRepository.findByStatusInOrderByReceivedAtAsc(
                List.of(KdsStatus.Received, KdsStatus.Accepted, KdsStatus.Preparing, KdsStatus.Ready, KdsStatus.Collected)
        );
    }

    @Transactional(readOnly = true)
    public List<KdsOrder> getKitchenQueue() {
        return kdsOrderRepository.findByStatusInOrderByReceivedAtAsc(
                List.of(KdsStatus.Received, KdsStatus.Accepted, KdsStatus.Preparing, KdsStatus.Ready)
        );
    }

    @Transactional(readOnly = true)
    public List<KdsOrder> getOrdersFiltered(KdsStatus status, OrderType type, Boolean delayedOnly) {
        List<KdsOrder> all = kdsOrderRepository.findAll();
        Instant now = Instant.now();

        return all.stream()
                .filter(o -> status == null || o.getStatus() == status)
                .filter(o -> type == null || o.getOrderType() == type)
                .filter(o -> {
                    if (delayedOnly == null || !delayedOnly) return true;
                    if (o.getStatus() == KdsStatus.Delivered || o.getStatus() == KdsStatus.Cancelled) return false;
                    long elapsedMinutes = Duration.between(o.getReceivedAt(), now).toMinutes();
                    return elapsedMinutes >= 20;
                })
                .sorted(Comparator.comparing(KdsOrder::getReceivedAt).reversed())
                .toList();
    }

    @Transactional(readOnly = true)
    public Optional<KdsOrder> getOrderById(UUID orderId) {
        return kdsOrderRepository.findById(orderId);
    }

    @Transactional(readOnly = true)
    public List<MenuItem> getActiveMenu() {
        return menuItemRepository.findByAvailableTrueOrderByCategoryAscNameAsc();
    }

    @Transactional(readOnly = true)
    public List<FnbAuditLog> getOrderAuditLogs(UUID orderId) {
        return auditLogRepository.findByOrderIdOrderByTimestampDesc(orderId);
    }

    @Transactional(readOnly = true)
    public FnbSummaryResponse getSummary() {
        List<KdsOrder> all = kdsOrderRepository.findAll();
        Instant startOfDay = LocalDate.now(ZoneOffset.UTC).atStartOfDay().toInstant(ZoneOffset.UTC);
        Instant now = Instant.now();

        long totalToday = 0;
        long active = 0;
        long kitchenQueue = 0;
        long preparing = 0;
        long ready = 0;
        long delivered = 0;
        long delayed = 0;
        BigDecimal revenueToday = BigDecimal.ZERO;
        long totalPrepSeconds = 0;
        long prepCount = 0;

        for (KdsOrder o : all) {
            boolean isToday = o.getReceivedAt() != null && !o.getReceivedAt().isBefore(startOfDay);
            if (isToday) {
                totalToday++;
                if (o.getStatus() == KdsStatus.Delivered && o.getTotalAmount() != null) {
                    revenueToday = revenueToday.add(o.getTotalAmount());
                }
            }

            if (o.getStatus() != KdsStatus.Delivered && o.getStatus() != KdsStatus.Cancelled) {
                active++;
                long elapsedMinutes = Duration.between(o.getReceivedAt(), now).toMinutes();
                if (elapsedMinutes >= 20) {
                    delayed++;
                }
            }

            if (o.getStatus() == KdsStatus.Received || o.getStatus() == KdsStatus.Accepted) {
                kitchenQueue++;
            } else if (o.getStatus() == KdsStatus.Preparing) {
                preparing++;
            } else if (o.getStatus() == KdsStatus.Ready || o.getStatus() == KdsStatus.Collected) {
                ready++;
            } else if (o.getStatus() == KdsStatus.Delivered) {
                delivered++;
                if (o.getReceivedAt() != null && o.getReadyAt() != null) {
                    totalPrepSeconds += Duration.between(o.getReceivedAt(), o.getReadyAt()).toSeconds();
                    prepCount++;
                }
            }
        }

        double avgPrepMinutes = prepCount > 0 ? (totalPrepSeconds / (double) prepCount) / 60.0 : 0.0;

        return FnbSummaryResponse.builder()
                .totalOrdersToday(totalToday)
                .activeOrders(active)
                .kitchenQueueCount(kitchenQueue)
                .preparingCount(preparing)
                .readyForPickupCount(ready)
                .deliveredCount(delivered)
                .delayedOrdersCount(delayed)
                .totalRevenueToday(revenueToday)
                .currency("LKR")
                .averagePrepTimeMinutes(Math.round(avgPrepMinutes * 10.0) / 10.0)
                .build();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private KdsOrder findOrderOrThrow(UUID id) {
        return kdsOrderRepository.findById(id)
                .orElseThrow(() -> new IllegalArgumentException("KDS order not found with ID: " + id));
    }

    private String generateOrderNumber() {
        String datePart = DateTimeFormatter.ofPattern("yyyyMMdd").withZone(ZoneOffset.UTC).format(Instant.now());
        String randomSuffix = UUID.randomUUID().toString().substring(0, 4).toUpperCase();
        return "FNB-" + datePart + "-" + randomSuffix;
    }

    private void logAudit(
            UUID orderId, String orderNumber, String action, UUID actorId, String actorRole,
            String details, String reason, String prevStatus, String newStatus) {
        try {
            FnbAuditLog log = FnbAuditLog.builder()
                    .orderId(orderId)
                    .orderNumber(orderNumber)
                    .action(action)
                    .actorUserId(actorId)
                    .actorRole(actorRole)
                    .details(details)
                    .reason(reason)
                    .previousState(prevStatus)
                    .newState(newStatus)
                    .timestamp(Instant.now())
                    .build();
            auditLogRepository.save(log);
        } catch (Exception e) {
            log.error("Failed to write F&B audit log: {}", e.getMessage());
        }
    }

    private void validateChefOrManagerRole(String role, String action) {
        if (!"Chef".equalsIgnoreCase(role) && !"Manager".equalsIgnoreCase(role) &&
            !"Admin".equalsIgnoreCase(role) && !"Owner".equalsIgnoreCase(role)) {
            throw new SecurityException("Unauthorized: Only Kitchen Staff (Chef) or F&B Management can " + action);
        }
    }

    private void validateWaiterOrManagerRole(String role, String action) {
        if (!"Waiter".equalsIgnoreCase(role) && !"Manager".equalsIgnoreCase(role) &&
            !"Admin".equalsIgnoreCase(role) && !"Owner".equalsIgnoreCase(role)) {
            throw new SecurityException("Unauthorized: Only Waiters or F&B Management can " + action);
        }
    }

    private void validateManagerOrOwnerRole(String role, String action) {
        if (!"Manager".equalsIgnoreCase(role) && !"Admin".equalsIgnoreCase(role) && !"Owner".equalsIgnoreCase(role)) {
            throw new SecurityException("Unauthorized: Only F&B Management or Owner can " + action);
        }
    }
}
