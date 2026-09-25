package com.smarthotel.fieldops.dto;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.ChargeStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.KdsStatus;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OrderType;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import lombok.Builder;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

public class KdsDtos {

    public record MenuItemDto(
            UUID id,
            String code,
            String name,
            String description,
            String category,
            BigDecimal price,
            String currency,
            boolean available,
            boolean roomServiceEligible,
            String imageUrl
    ) {}

    public record OrderItemRequest(
            @NotNull UUID menuItemId,
            @Min(1) int quantity,
            String specialInstructions
    ) {}

    public record CreateStructuredOrderRequest(
            @NotNull OrderType orderType,
            String tableOrRoomNumber,
            String roomNumber,
            UUID bookingId,
            UUID customerId,
            String customerName,
            String notes,
            @NotEmpty List<OrderItemRequest> items
    ) {}

    public record CreateKdsOrderRequest(
            @NotBlank String orderNumber,
            @NotBlank String tableOrRoomNumber,
            @NotBlank String itemsJson
    ) {}

    public record UpdateKdsStatusRequest(
            @NotNull KdsStatus status,
            UUID cookEmployeeId,
            String notes
    ) {}

    public record CancelOrderRequest(
            @NotBlank String reason
    ) {}

    public record AssignOrderRequest(
            @NotNull UUID employeeId,
            @NotBlank String role // "Chef" or "Waiter"
    ) {}

    public record OrderItemSnapshot(
            UUID menuItemId,
            String itemCode,
            String name,
            BigDecimal unitPrice,
            int quantity,
            BigDecimal lineSubtotal,
            BigDecimal taxRate,
            BigDecimal taxAmount,
            BigDecimal lineTotal,
            String specialInstructions
    ) {}

    @Builder
    public record KdsOrderResponse(
            UUID id,
            String orderNumber,
            OrderType orderType,
            String tableOrRoomNumber,
            String roomNumber,
            UUID bookingId,
            UUID customerId,
            String customerName,
            String itemsJson,
            List<OrderItemSnapshot> items,
            KdsStatus status,
            UUID cookEmployeeId,
            UUID chefEmployeeId,
            String chefName,
            UUID waiterEmployeeId,
            String waiterName,
            BigDecimal subtotal,
            BigDecimal taxAmount,
            BigDecimal totalAmount,
            String currency,
            UUID financeInvoiceId,
            String financeInvoiceNumber,
            ChargeStatus chargeStatus,
            String chargeError,
            String notes,
            String cancellationReason,
            Instant receivedAt,
            Instant acceptedAt,
            Instant preparingAt,
            Instant readyAt,
            Instant collectedAt,
            Instant deliveredAt,
            Instant cancelledAt
    ) {}

    @Builder
    public record FnbSummaryResponse(
            long totalOrdersToday,
            long activeOrders,
            long kitchenQueueCount,
            long preparingCount,
            long readyForPickupCount,
            long deliveredCount,
            long delayedOrdersCount,
            BigDecimal totalRevenueToday,
            String currency,
            double averagePrepTimeMinutes
    ) {}
}
