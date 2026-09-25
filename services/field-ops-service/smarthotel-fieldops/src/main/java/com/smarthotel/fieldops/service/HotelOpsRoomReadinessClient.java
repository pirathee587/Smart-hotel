package com.smarthotel.fieldops.service;
import org.springframework.beans.factory.annotation.Value; import org.springframework.http.HttpHeaders; import org.springframework.stereotype.Component; import org.springframework.web.client.RestClient; import java.time.Instant; import java.util.*;
@Component
public class HotelOpsRoomReadinessClient implements RoomReadinessClient {
 private final RestClient client; public HotelOpsRoomReadinessClient(RestClient.Builder builder,@Value("${services.hotel-ops-url:http://hotel-ops-service:5002}") String baseUrl){client=builder.baseUrl(baseUrl).build();}
 public void start(UUID roomId,UUID taskId,UUID eventId,UUID departmentId,UUID housekeeperId,Instant occurredAt,String token){post(roomId,"start",Map.of("taskId",taskId,"eventId",eventId,"departmentId",departmentId,"housekeeperId",housekeeperId,"occurredAtUtc",occurredAt),token);}
 public void inspect(UUID roomId,UUID taskId,UUID eventId,UUID departmentId,UUID managerId,boolean approved,String notes,Instant occurredAt,String token){post(roomId,"inspection",Map.of("taskId",taskId,"eventId",eventId,"departmentId",departmentId,"managerId",managerId,"approved",approved,"notes",notes,"occurredAtUtc",occurredAt),token);}
 private void post(UUID roomId,String action,Object body,String token){if(token==null||!token.startsWith("Bearer "))throw new SecurityException("Validated bearer token is required for Hotel Ops integration."); client.post().uri("/api/v1/rooms/{roomId}/housekeeping/{action}",roomId,action).header(HttpHeaders.AUTHORIZATION,token).body(body).retrieve().toBodilessEntity();}
}
