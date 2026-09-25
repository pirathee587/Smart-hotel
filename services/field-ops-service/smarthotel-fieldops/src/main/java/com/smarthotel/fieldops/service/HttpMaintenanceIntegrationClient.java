package com.smarthotel.fieldops.service;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.HttpHeaders;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.Map;
import java.util.UUID;

@Component
public class HttpMaintenanceIntegrationClient implements MaintenanceIntegrationClient {
    private final RestClient hotelOps; private final RestClient finance;
    public HttpMaintenanceIntegrationClient(RestClient.Builder builder,
      @Value("${services.hotel-ops-url:http://hotel-ops-service:5002}") String hotelOpsUrl,
      @Value("${services.finance-url:http://booking-payments-service:5003}") String financeUrl) {
        hotelOps=builder.clone().baseUrl(hotelOpsUrl).build(); finance=builder.clone().baseUrl(financeUrl).build();
    }
    public void restrict(UUID roomId,UUID workOrderId,UUID eventId,UUID departmentId,UUID actorId,String issue,String severity,boolean hazard,Instant at,String token){
        require(token); hotelOps.post().uri("/api/v1/rooms/{roomId}/maintenance/restrict",roomId).header(HttpHeaders.AUTHORIZATION,token)
          .body(Map.of("workOrderId",workOrderId,"eventId",eventId,"departmentId",departmentId,"actorId",actorId,"issue",issue,"severity",severity,"safetyHazard",hazard,"occurredAtUtc",at)).retrieve().toBodilessEntity();
    }
    public void clear(UUID roomId,UUID workOrderId,UUID eventId,UUID departmentId,UUID managerId,Instant at,String token){
        require(token); hotelOps.post().uri("/api/v1/rooms/{roomId}/maintenance/clear",roomId).header(HttpHeaders.AUTHORIZATION,token)
          .body(Map.of("workOrderId",workOrderId,"eventId",eventId,"departmentId",departmentId,"managerId",managerId,"occurredAtUtc",at)).retrieve().toBodilessEntity();
    }
    public FinanceExpenseResult submitExpense(UUID workOrderId,UUID departmentId,BigDecimal estimated,BigDecimal actual,String currency,String description,String token){
        require(token); var body=Map.of("amount",actual,"currency",currency,"description",description,"category","Maintenance","vendorReference","maintenance:"+workOrderId,"idempotencyKey","maintenance:"+workOrderId+":actual-cost","departmentId",departmentId);
        return finance.post().uri("/api/v1/finance/expenses").header(HttpHeaders.AUTHORIZATION,token).body(body).retrieve().body(FinanceExpenseResult.class);
    }
    private static void require(String token){if(token==null||!token.startsWith("Bearer "))throw new SecurityException("Validated bearer token is required for service integration.");}
}
