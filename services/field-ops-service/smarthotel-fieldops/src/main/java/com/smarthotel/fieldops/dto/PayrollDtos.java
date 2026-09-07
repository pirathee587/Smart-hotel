package com.smarthotel.fieldops.dto;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.PayrollStatus;
import jakarta.validation.constraints.NotNull;
import lombok.Builder;

import java.math.BigDecimal;
import java.time.Instant;
import java.time.LocalDate;
import java.util.UUID;

public class PayrollDtos {

    public record GeneratePayrollRequest(
            @NotNull UUID employeeId,
            @NotNull LocalDate startDate,
            @NotNull LocalDate endDate
    ) {}

    @Builder
    public record PayrollResponse(
            UUID id,
            UUID employeeId,
            LocalDate payPeriodStart,
            LocalDate payPeriodEnd,
            double regularHours,
            double overtimeHours,
            BigDecimal hourlyRate,
            BigDecimal regularPay,
            BigDecimal overtimePay,
            BigDecimal grossPay,
            BigDecimal netPay,
            PayrollStatus status,
            Instant calculatedAt,
            String bankName,
            String maskedBankAccount
    ) {}
}
