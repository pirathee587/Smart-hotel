package com.smarthotel.fieldops.service;

import java.math.BigDecimal;
import java.util.UUID;

public interface FinanceChargeClient {

    record CreateFnbChargePayload(
            UUID orderId,
            String orderNumber,
            String orderType,
            String tableOrRoomNumber,
            UUID bookingId,
            String customerName,
            BigDecimal subtotal,
            BigDecimal taxAmount,
            BigDecimal totalAmount,
            String currency,
            String idempotencyKey
    ) {}

    record FinanceChargeResult(
            boolean success,
            UUID invoiceId,
            String invoiceNumber,
            String status,
            String message
    ) {}

    FinanceChargeResult submitFnbCharge(CreateFnbChargePayload payload, String token);
}
