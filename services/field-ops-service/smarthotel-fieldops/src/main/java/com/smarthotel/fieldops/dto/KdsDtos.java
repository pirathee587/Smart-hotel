package com.smarthotel.fieldops.dto;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.KdsStatus;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.Builder;

import java.time.Instant;
import java.util.UUID;

public class KdsDtos {

    public record CreateKdsOrderRequest(
            @NotBlank String orderNumber,
            @NotBlank String tableOrRoomNumber,
            @NotBlank String itemsJson
    ) {}

    public record UpdateKdsStatusRequest(
            @NotNull KdsStatus status,
            UUID cookEmployeeId
    ) {}

    @Builder
    public record KdsOrderResponse(
            UUID id,
            String orderNumber,
            String tableOrRoomNumber,
            String itemsJson,
            KdsStatus status,
            UUID cookEmployeeId,
            Instant receivedAt,
            Instant preparingAt,
            Instant readyAt,
            Instant deliveredAt
    ) {}
}
