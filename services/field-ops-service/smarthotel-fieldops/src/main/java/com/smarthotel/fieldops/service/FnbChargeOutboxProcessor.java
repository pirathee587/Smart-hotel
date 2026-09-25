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
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.List;
import java.util.UUID;

@Service
@RequiredArgsConstructor
@Slf4j
public class FnbChargeOutboxProcessor {

    private final FnbChargeOutboxRepository outboxRepository;
    private final KdsOrderRepository orderRepository;
    private final FinanceChargeClient financeChargeClient;
    private final ObjectMapper objectMapper;

    @Scheduled(fixedDelay = 20000)
    public void processPendingOutbox() {
        List<FnbChargeOutbox> pending = outboxRepository.findByStatusInAndNextRetryAtLessThanEqualOrderByCreatedAtAsc(
                List.of(OutboxStatus.Pending), Instant.now());

        if (pending.isEmpty()) {
            return;
        }

        log.info("Processing {} pending F&B Finance charge outbox records", pending.size());
        for (FnbChargeOutbox outbox : pending) {
            processSingleOutbox(outbox, null);
        }
    }

    @Transactional
    public FinanceChargeResult processSingleOutbox(FnbChargeOutbox outbox, String token) {
        try {
            CreateFnbChargePayload payload = objectMapper.readValue(
                    outbox.getPayloadJson(), CreateFnbChargePayload.class);

            FinanceChargeResult result = financeChargeClient.submitFnbCharge(payload, token);

            if (result.success()) {
                outbox.markConfirmed(result.invoiceId(), result.invoiceNumber());
                outboxRepository.save(outbox);

                orderRepository.findById(outbox.getOrderId()).ifPresent(order -> {
                    order.setChargeStatus(ChargeStatus.Invoiced);
                    order.setFinanceInvoiceId(result.invoiceId());
                    order.setFinanceInvoiceNumber(result.invoiceNumber());
                    order.setChargeError(null);
                    orderRepository.save(order);
                });

                log.info("Successfully processed outbox charge for order {}. Invoice: {}",
                        payload.orderNumber(), result.invoiceNumber());
                return result;
            } else {
                long backoffSeconds = Math.min(3600, (long) Math.pow(2, outbox.getRetryCount() + 1) * 15);
                Instant nextRetry = Instant.now().plusSeconds(backoffSeconds);
                outbox.recordFailure(result.message(), nextRetry);
                outboxRepository.save(outbox);

                orderRepository.findById(outbox.getOrderId()).ifPresent(order -> {
                    order.setChargeError(result.message());
                    orderRepository.save(order);
                });

                log.warn("F&B charge posting failed for order {}: {}. Next retry at {}",
                        payload.orderNumber(), result.message(), nextRetry);
                return result;
            }

        } catch (Exception e) {
            log.error("Exception processing F&B outbox record {}: {}", outbox.getId(), e.getMessage(), e);
            long backoffSeconds = Math.min(3600, (long) Math.pow(2, outbox.getRetryCount() + 1) * 15);
            outbox.recordFailure("Outbox processing error: " + e.getMessage(), Instant.now().plusSeconds(backoffSeconds));
            outboxRepository.save(outbox);
            return new FinanceChargeResult(false, null, null, "Failed", e.getMessage());
        }
    }

    @Transactional
    public FinanceChargeResult retryOrderCharge(UUID orderId, String token) {
        FnbChargeOutbox outbox = outboxRepository.findByOrderId(orderId)
                .orElseThrow(() -> new IllegalArgumentException("No charge outbox record found for order: " + orderId));

        if (outbox.getStatus() == OutboxStatus.Confirmed) {
            return new FinanceChargeResult(true, outbox.getFinanceInvoiceId(),
                    outbox.getFinanceInvoiceNumber(), "Confirmed", "Charge already confirmed");
        }

        outbox.setNextRetryAt(Instant.now());
        return processSingleOutbox(outbox, token);
    }
}
