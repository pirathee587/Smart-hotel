package com.smarthotel.fieldops.service;

import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.core.ParameterizedTypeReference;
import org.springframework.http.HttpHeaders;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;

import java.util.*;

@Component
@Slf4j
public class HttpRoomServiceEligibilityClient implements RoomServiceEligibilityClient {

    private final RestClient hotelOpsClient;
    private final RestClient bookingClient;

    public HttpRoomServiceEligibilityClient(
            RestClient.Builder builder,
            @Value("${services.hotel-ops-url:http://hotel-ops-service:5002}") String hotelOpsUrl,
            @Value("${services.booking-url:http://booking-payments-service:5003}") String bookingUrl) {
        this.hotelOpsClient = builder.clone().baseUrl(hotelOpsUrl).build();
        this.bookingClient = builder.clone().baseUrl(bookingUrl).build();
    }

    @Override
    public EligibilityResult verifyEligibility(String roomNumber, UUID expectedBookingId, String token) {
        if (roomNumber == null || roomNumber.isBlank()) {
            return new EligibilityResult(false, "Room number is required for room-service orders", null, null);
        }

        try {
            // 1. Verify Room in Hotel Ops
            var req = hotelOpsClient.get().uri("/api/v1/rooms");
            if (token != null && token.startsWith("Bearer ")) {
                req.header(HttpHeaders.AUTHORIZATION, token);
            }

            List<Map<String, Object>> rooms = req.retrieve()
                    .body(new ParameterizedTypeReference<List<Map<String, Object>>>() {});

            if (rooms == null || rooms.isEmpty()) {
                log.warn("Room lookup returned empty list from Hotel Ops");
                return new EligibilityResult(false, "No rooms found in hotel ops", null, null);
            }

            Map<String, Object> targetRoom = rooms.stream()
                    .filter(r -> roomNumber.trim().equalsIgnoreCase(String.valueOf(r.get("roomNumber"))))
                    .findFirst()
                    .orElse(null);

            if (targetRoom == null) {
                return new EligibilityResult(false, "Room " + roomNumber + " does not exist in hotel inventory", null, null);
            }

            String status = String.valueOf(targetRoom.get("status"));
            if (!"Occupied".equalsIgnoreCase(status)) {
                return new EligibilityResult(false, 
                        "Room " + roomNumber + " is currently " + status + ". Room service requires an Occupied room with active guest.", 
                        null, null);
            }

            // 2. Look up active checked-in booking
            UUID resolvedBookingId = expectedBookingId;
            String guestName = null;

            try {
                var bookingReq = bookingClient.get().uri("/api/v1/bookings?status=CheckedIn");
                if (token != null && token.startsWith("Bearer ")) {
                    bookingReq.header(HttpHeaders.AUTHORIZATION, token);
                }

                List<Map<String, Object>> bookings = bookingReq.retrieve()
                        .body(new ParameterizedTypeReference<List<Map<String, Object>>>() {});

                if (bookings != null) {
                    Map<String, Object> matchingBooking = bookings.stream()
                            .filter(b -> roomNumber.trim().equalsIgnoreCase(String.valueOf(b.get("roomNumber"))))
                            .findFirst()
                            .orElse(null);

                    if (matchingBooking != null) {
                        if (matchingBooking.get("id") != null) {
                            resolvedBookingId = UUID.fromString(String.valueOf(matchingBooking.get("id")));
                        }
                        if (matchingBooking.get("guestName") != null) {
                            guestName = String.valueOf(matchingBooking.get("guestName"));
                        } else if (matchingBooking.get("customerName") != null) {
                            guestName = String.valueOf(matchingBooking.get("customerName"));
                        }
                    }
                }
            } catch (Exception e) {
                log.warn("Could not query checked-in bookings from Booking service: {}", e.getMessage());
                // If hotel ops confirms room is Occupied, proceed with room verification
            }

            return new EligibilityResult(true, "Room " + roomNumber + " is occupied and eligible for room service", resolvedBookingId, guestName);

        } catch (Exception e) {
            log.error("Authoritative room service eligibility verification failed (downstream service error): {}", e.getMessage(), e);
            // Fail closed as mandated:
            throw new IllegalStateException("Authoritative room service eligibility verification failed: " + e.getMessage(), e);
        }
    }
}
