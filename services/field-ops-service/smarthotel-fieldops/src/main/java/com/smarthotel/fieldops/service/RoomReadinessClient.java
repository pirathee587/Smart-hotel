package com.smarthotel.fieldops.service;
import java.time.Instant; import java.util.UUID;
public interface RoomReadinessClient {
 void start(UUID roomId,UUID taskId,UUID eventId,UUID departmentId,UUID housekeeperId,Instant occurredAt,String bearerToken);
 void inspect(UUID roomId,UUID taskId,UUID eventId,UUID departmentId,UUID managerId,boolean approved,String notes,Instant occurredAt,String bearerToken);
}
