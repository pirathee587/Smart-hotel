package com.smarthotel.fieldops.service;

import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.core.ParameterizedTypeReference;
import org.springframework.http.HttpHeaders;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;

import java.util.Map;
import java.util.UUID;

@Component
@Slf4j
public class HttpFinanceChargeClient implements FinanceChargeClient {

    private final RestClient financeClient;

    public HttpFinanceChargeClient(
            RestClient.Builder builder,
            @Value("${services.finance-url:http://booking-payments-service:5003}") String financeUrl) {
        this.financeClient = builder.clone().baseUrl(financeUrl).build();
    }

    @Override
    public FinanceChargeResult submitFnbCharge(CreateFnbChargePayload payload, String token) {
        try {
            var req = financeClient.post()
                    .uri("/api/v1/finance/operations/charges/fnb");

            if (token != null && token.startsWith("Bearer ")) {
                req.header(HttpHeaders.AUTHORIZATION, token);
            }

            Map<String, Object> response = req.body(payload)
                    .retrieve()
                    .body(new ParameterizedTypeReference<Map<String, Object>>() {});

            if (response != null) {
                UUID invoiceId = null;
                if (response.get("id") != null) {
                    invoiceId = UUID.fromString(String.valueOf(response.get("id")));
                }
                String invoiceNumber = response.get("invoiceNumber") != null
                        ? String.valueOf(response.get("invoiceNumber"))
                        : null;
                String status = response.get("status") != null
                        ? String.valueOf(response.get("status"))
                        : "Validated";

                log.info("Successfully posted F&B charge to Finance: Invoice {} (ID: {})", invoiceNumber, invoiceId);
                return new FinanceChargeResult(true, invoiceId, invoiceNumber, status, "Successfully invoiced");
            }

            return new FinanceChargeResult(false, null, null, "Failed", "Empty response from Finance service");

        } catch (Exception e) {
            log.error("Failed to post F&B charge to Finance for order {}: {}", payload.orderNumber(), e.getMessage());
            return new FinanceChargeResult(false, null, null, "Failed", "Finance posting error: " + e.getMessage());
        }
    }
}
