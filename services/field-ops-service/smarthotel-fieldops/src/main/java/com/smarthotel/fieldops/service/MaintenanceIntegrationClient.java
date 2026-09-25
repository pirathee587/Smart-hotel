package com.smarthotel.fieldops.service;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

public interface MaintenanceIntegrationClient {
    void restrict(UUID roomId, UUID workOrderId, UUID eventId, UUID departmentId, UUID actorId,
                  String issue, String severity, boolean safetyHazard, Instant occurredAt, String bearerToken);
    void clear(UUID roomId, UUID workOrderId, UUID eventId, UUID departmentId, UUID managerId,
               Instant occurredAt, String bearerToken);
    FinanceExpenseResult submitExpense(UUID workOrderId, UUID departmentId, BigDecimal estimatedCost,
                                       BigDecimal actualCost, String currency, String description, String bearerToken);
    record FinanceExpenseResult(UUID id, String status) {}
}
