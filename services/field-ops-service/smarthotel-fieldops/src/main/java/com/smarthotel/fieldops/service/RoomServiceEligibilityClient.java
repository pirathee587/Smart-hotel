package com.smarthotel.fieldops.service;

import java.util.UUID;

public interface RoomServiceEligibilityClient {
    record EligibilityResult(boolean eligible, String reason, UUID bookingId, String guestName) {}

    EligibilityResult verifyEligibility(String roomNumber, UUID expectedBookingId, String token);
}
