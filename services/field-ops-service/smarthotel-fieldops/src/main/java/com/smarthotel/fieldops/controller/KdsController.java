package com.smarthotel.fieldops.controller;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.smarthotel.fieldops.domain.model.FnbAuditLog;
import com.smarthotel.fieldops.domain.model.KdsOrder;
import com.smarthotel.fieldops.domain.model.MenuItem;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.KdsStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OrderType;
import com.smarthotel.fieldops.dto.KdsDtos.*;
import com.smarthotel.fieldops.service.FinanceChargeClient.FinanceChargeResult;
import com.smarthotel.fieldops.service.KdsService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.*;

import java.util.Collections;
import java.util.List;
import java.util.UUID;

@RestController
@RequestMapping("/api/v1/kds")
@RequiredArgsConstructor
@Slf4j
public class KdsController {

    private final KdsService kdsService;
    private final ObjectMapper objectMapper;

    // -------------------------------------------------------------------------
    // Menu
    // -------------------------------------------------------------------------

    @GetMapping("/menu")
    public ResponseEntity<List<MenuItemDto>> getMenu() {
        List<MenuItem> items = kdsService.getActiveMenu();
        List<MenuItemDto> dtos = items.stream().map(i -> new MenuItemDto(
                i.getId(), i.getCode(), i.getName(), i.getDescription(),
                i.getCategory(), i.getPrice(), i.getCurrency(),
                i.isAvailable(), i.isRoomServiceEligible(), i.getImageUrl()
        )).toList();
        return ResponseEntity.ok(dtos);
    }

    // -------------------------------------------------------------------------
    // Order Placement
    // -------------------------------------------------------------------------

    @PostMapping("/orders")
    public ResponseEntity<KdsOrderResponse> createOrder(
            @Valid @RequestBody CreateStructuredOrderRequest req,
            @AuthenticationPrincipal Jwt jwt,
            @RequestHeader(value = HttpHeaders.AUTHORIZATION, required = false) String token) {

        String role = getRole(jwt);
        UUID userId = getUserId(jwt);

        // Department check for internal staff
        if (!"Guest".equalsIgnoreCase(role) && !"Customer".equalsIgnoreCase(role)) {
            requireFnbDepartment(jwt);
        }

        // Only Waiters, F&B Managers, Admins, Owners, or Customers can create orders
        if (!"Waiter".equalsIgnoreCase(role) && !"Manager".equalsIgnoreCase(role) &&
            !"Admin".equalsIgnoreCase(role) && !"Owner".equalsIgnoreCase(role) &&
            !"Customer".equalsIgnoreCase(role) && !"Guest".equalsIgnoreCase(role)) {
            throw new AccessDeniedException("Unauthorized: Chef cannot create customer orders");
        }

        KdsOrder order = kdsService.createStructuredOrder(req, userId, role, token);
        return ResponseEntity.status(HttpStatus.CREATED).body(mapToResponse(order));
    }

    // Legacy fallback endpoint matching CreateKdsOrderRequest
    @PostMapping("/orders/legacy")
    public ResponseEntity<KdsOrderResponse> createLegacyOrder(
            @Valid @RequestBody CreateKdsOrderRequest req,
            @AuthenticationPrincipal Jwt jwt) {
        requireFnbDepartment(jwt);
        KdsOrder order = kdsService.createOrder(req.orderNumber(), req.tableOrRoomNumber(), req.itemsJson());
        return ResponseEntity.status(HttpStatus.CREATED).body(mapToResponse(order));
    }

    // -------------------------------------------------------------------------
    // Order Queues & Details
    // -------------------------------------------------------------------------

    @GetMapping("/orders/active")
    public ResponseEntity<List<KdsOrderResponse>> getActiveOrders(@AuthenticationPrincipal Jwt jwt) {
        requireFnbDepartment(jwt);
        List<KdsOrder> orders = kdsService.getActiveOrders();
        return ResponseEntity.ok(orders.stream().map(this::mapToResponse).toList());
    }

    @GetMapping("/orders/kitchen")
    public ResponseEntity<List<KdsOrderResponse>> getKitchenQueue(@AuthenticationPrincipal Jwt jwt) {
        requireFnbDepartment(jwt);
        List<KdsOrder> orders = kdsService.getKitchenQueue();
        return ResponseEntity.ok(orders.stream().map(this::mapToResponse).toList());
    }

    @GetMapping("/orders")
    public ResponseEntity<List<KdsOrderResponse>> getOrders(
            @RequestParam(required = false) KdsStatus status,
            @RequestParam(required = false) OrderType orderType,
            @RequestParam(required = false) Boolean delayedOnly,
            @AuthenticationPrincipal Jwt jwt) {
        requireFnbDepartment(jwt);
        List<KdsOrder> orders = kdsService.getOrdersFiltered(status, orderType, delayedOnly);
        return ResponseEntity.ok(orders.stream().map(this::mapToResponse).toList());
    }

    @GetMapping("/orders/{id}")
    public ResponseEntity<KdsOrderResponse> getOrderById(
            @PathVariable UUID id,
            @AuthenticationPrincipal Jwt jwt) {
        requireFnbDepartment(jwt);
        return kdsService.getOrderById(id)
                .map(this::mapToResponse)
                .map(ResponseEntity::ok)
                .orElse(ResponseEntity.notFound().build());
    }

    @GetMapping("/orders/{id}/audit-logs")
    public ResponseEntity<List<FnbAuditLog>> getAuditLogs(
            @PathVariable UUID id,
            @AuthenticationPrincipal Jwt jwt) {
        requireFnbDepartment(jwt);
        return ResponseEntity.ok(kdsService.getOrderAuditLogs(id));
    }

    // -------------------------------------------------------------------------
    // Status Transitions
    // -------------------------------------------------------------------------

    @PatchMapping("/orders/{id}/status")
    public ResponseEntity<KdsOrderResponse> updateStatus(
            @PathVariable UUID id,
            @Valid @RequestBody UpdateKdsStatusRequest req,
            @AuthenticationPrincipal Jwt jwt,
            @RequestHeader(value = HttpHeaders.AUTHORIZATION, required = false) String token) {

        requireFnbDepartment(jwt);
        String role = getRole(jwt);
        UUID userId = getUserId(jwt);
        String userName = getUserName(jwt);

        KdsOrder updated;
        switch (req.status()) {
            case Accepted -> {
                validateRole(role, "Chef", "Manager", "Admin", "Owner");
                updated = kdsService.acceptOrder(id, userId, userName, role);
            }
            case Preparing -> {
                validateRole(role, "Chef", "Manager", "Admin", "Owner");
                updated = kdsService.startPreparing(id, userId, userName, role);
            }
            case Ready -> {
                validateRole(role, "Chef", "Manager", "Admin", "Owner");
                updated = kdsService.markReady(id, userId, role);
            }
            case Collected -> {
                validateRole(role, "Waiter", "Manager", "Admin", "Owner");
                updated = kdsService.markCollected(id, userId, userName, role);
            }
            case Delivered -> {
                validateRole(role, "Waiter", "Manager", "Admin", "Owner");
                updated = kdsService.markDelivered(id, userId, userName, role, token);
            }
            case Cancelled -> {
                validateRole(role, "Manager", "Admin", "Owner");
                updated = kdsService.cancelOrder(id, req.notes(), userId, role);
            }
            default -> {
                validateRole(role, "Manager", "Admin", "Owner");
                updated = kdsService.updateStatus(id, req.status(), req.cookEmployeeId());
            }
        }

        return ResponseEntity.ok(mapToResponse(updated));
    }

    @PostMapping("/orders/{id}/cancel")
    public ResponseEntity<KdsOrderResponse> cancelOrder(
            @PathVariable UUID id,
            @Valid @RequestBody CancelOrderRequest req,
            @AuthenticationPrincipal Jwt jwt) {

        requireFnbDepartment(jwt);
        String role = getRole(jwt);
        validateRole(role, "Manager", "Admin", "Owner");

        KdsOrder updated = kdsService.cancelOrder(id, req.reason(), getUserId(jwt), role);
        return ResponseEntity.ok(mapToResponse(updated));
    }

    @PostMapping("/orders/{id}/assign")
    public ResponseEntity<KdsOrderResponse> assignOrder(
            @PathVariable UUID id,
            @Valid @RequestBody AssignOrderRequest req,
            @AuthenticationPrincipal Jwt jwt) {

        requireFnbDepartment(jwt);
        String role = getRole(jwt);
        validateRole(role, "Manager", "Admin", "Owner");

        KdsOrder updated = kdsService.assignOrder(id, req.employeeId(), req.role(), getUserId(jwt), role);
        return ResponseEntity.ok(mapToResponse(updated));
    }

    @PostMapping("/orders/{id}/retry-charge")
    public ResponseEntity<FinanceChargeResult> retryFinanceCharge(
            @PathVariable UUID id,
            @AuthenticationPrincipal Jwt jwt,
            @RequestHeader(value = HttpHeaders.AUTHORIZATION, required = false) String token) {

        requireFnbDepartment(jwt);
        String role = getRole(jwt);
        validateRole(role, "Waiter", "Manager", "Admin", "Owner");

        FinanceChargeResult result = kdsService.retryFinanceCharge(id, token, getUserId(jwt), role);
        return ResponseEntity.ok(result);
    }

    @GetMapping("/reports/summary")
    public ResponseEntity<FnbSummaryResponse> getSummary(@AuthenticationPrincipal Jwt jwt) {
        requireFnbDepartment(jwt);
        return ResponseEntity.ok(kdsService.getSummary());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void requireFnbDepartment(Jwt jwt) {
        if (jwt == null) throw new AccessDeniedException("Authentication required");
        String role = getRole(jwt);
        if ("Owner".equalsIgnoreCase(role)) return;

        String deptCode = (String) jwt.getClaims().get("departmentCode");
        String deptName = (String) jwt.getClaims().get("departmentName");

        boolean isFnb = "FOODBEVERAGE".equalsIgnoreCase(deptCode) ||
                (deptName != null && deptName.toLowerCase().contains("food"));

        if (!isFnb) {
            throw new AccessDeniedException("Forbidden: User does not belong to the Food & Beverage department");
        }
    }

    private void validateRole(String userRole, String... allowed) {
        for (String a : allowed) {
            if (a.equalsIgnoreCase(userRole)) return;
        }
        throw new AccessDeniedException("Forbidden: Role " + userRole + " is not authorized for this operation");
    }

    private String getRole(Jwt jwt) {
        if (jwt == null) return "Anonymous";
        Object role = jwt.getClaims().get("role");
        if (role instanceof String r) return r;
        return "Employee";
    }

    private UUID getUserId(Jwt jwt) {
        if (jwt == null) return null;
        String sub = jwt.getSubject();
        try {
            return sub != null ? UUID.fromString(sub) : null;
        } catch (Exception e) {
            return null;
        }
    }

    private String getUserName(Jwt jwt) {
        if (jwt == null) return "Staff";
        Object email = jwt.getClaims().get("email");
        return email != null ? String.valueOf(email).split("@")[0] : "Staff";
    }

    private KdsOrderResponse mapToResponse(KdsOrder order) {
        List<OrderItemSnapshot> items = Collections.emptyList();
        if (order.getItemsJson() != null && !order.getItemsJson().isBlank()) {
            try {
                items = objectMapper.readValue(order.getItemsJson(), new TypeReference<List<OrderItemSnapshot>>() {});
            } catch (Exception e) {
                log.debug("Failed to deserialize itemsJson for order {}: {}", order.getId(), e.getMessage());
            }
        }

        return KdsOrderResponse.builder()
                .id(order.getId())
                .orderNumber(order.getOrderNumber())
                .orderType(order.getOrderType())
                .tableOrRoomNumber(order.getTableOrRoomNumber())
                .roomNumber(order.getRoomNumber())
                .bookingId(order.getBookingId())
                .customerId(order.getCustomerId())
                .customerName(order.getCustomerName())
                .itemsJson(order.getItemsJson())
                .items(items)
                .status(order.getStatus())
                .cookEmployeeId(order.getCookEmployeeId())
                .chefEmployeeId(order.getCookEmployeeId())
                .chefName(order.getChefName())
                .waiterEmployeeId(order.getWaiterEmployeeId())
                .waiterName(order.getWaiterName())
                .subtotal(order.getSubtotal())
                .taxAmount(order.getTaxAmount())
                .totalAmount(order.getTotalAmount())
                .currency(order.getCurrency())
                .financeInvoiceId(order.getFinanceInvoiceId())
                .financeInvoiceNumber(order.getFinanceInvoiceNumber())
                .chargeStatus(order.getChargeStatus())
                .chargeError(order.getChargeError())
                .notes(order.getNotes())
                .cancellationReason(order.getCancellationReason())
                .receivedAt(order.getReceivedAt())
                .acceptedAt(order.getAcceptedAt())
                .preparingAt(order.getPreparingAt())
                .readyAt(order.getReadyAt())
                .collectedAt(order.getCollectedAt())
                .deliveredAt(order.getDeliveredAt())
                .cancelledAt(order.getCancelledAt())
                .build();
    }
}
