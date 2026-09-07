package com.smarthotel.fieldops.controller;

import com.smarthotel.fieldops.domain.model.KdsOrder;
import com.smarthotel.fieldops.dto.KdsDtos.CreateKdsOrderRequest;
import com.smarthotel.fieldops.dto.KdsDtos.KdsOrderResponse;
import com.smarthotel.fieldops.dto.KdsDtos.UpdateKdsStatusRequest;
import com.smarthotel.fieldops.service.KdsService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.*;

import java.util.List;
import java.util.UUID;

@RestController
@RequestMapping("/api/v1/kds/orders")
@RequiredArgsConstructor
@Slf4j
public class KdsController {

    private final KdsService kdsService;

    @PostMapping
    @PreAuthorize("hasAnyRole('Admin', 'Manager', 'Staff', 'Cook')")
    public ResponseEntity<KdsOrderResponse> createOrder(@Valid @RequestBody CreateKdsOrderRequest req) {
        KdsOrder order = kdsService.createOrder(req.orderNumber(), req.tableOrRoomNumber(), req.itemsJson());
        return ResponseEntity.status(HttpStatus.CREATED).body(mapToResponse(order));
    }

    @GetMapping("/active")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<List<KdsOrderResponse>> getActiveOrders() {
        List<KdsOrder> orders = kdsService.getActiveOrders();
        return ResponseEntity.ok(orders.stream().map(this::mapToResponse).toList());
    }

    @GetMapping("/{id}")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<KdsOrderResponse> getOrderById(@PathVariable UUID id) {
        return kdsService.getOrderById(id)
                .map(this::mapToResponse)
                .map(ResponseEntity::ok)
                .orElse(ResponseEntity.notFound().build());
    }

    @PatchMapping("/{id}/status")
    @PreAuthorize("hasAnyRole('Admin', 'Manager', 'Cook', 'Staff')")
    public ResponseEntity<KdsOrderResponse> updateStatus(
            @PathVariable UUID id,
            @Valid @RequestBody UpdateKdsStatusRequest req) {

        KdsOrder updated = kdsService.updateStatus(id, req.status(), req.cookEmployeeId());
        return ResponseEntity.ok(mapToResponse(updated));
    }

    private KdsOrderResponse mapToResponse(KdsOrder order) {
        return KdsOrderResponse.builder()
                .id(order.getId())
                .orderNumber(order.getOrderNumber())
                .tableOrRoomNumber(order.getTableOrRoomNumber())
                .itemsJson(order.getItemsJson())
                .status(order.getStatus())
                .cookEmployeeId(order.getCookEmployeeId())
                .receivedAt(order.getReceivedAt())
                .preparingAt(order.getPreparingAt())
                .readyAt(order.getReadyAt())
                .deliveredAt(order.getDeliveredAt())
                .build();
    }
}
