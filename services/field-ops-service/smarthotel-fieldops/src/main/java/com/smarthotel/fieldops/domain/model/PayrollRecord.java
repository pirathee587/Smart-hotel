package com.smarthotel.fieldops.domain.model;

import com.smarthotel.fieldops.domain.model.enums.TaskEnums.PayrollStatus;
import jakarta.persistence.*;
import lombok.*;

import java.math.BigDecimal;
import java.math.RoundingMode;
import java.time.Instant;
import java.time.LocalDate;
import java.util.UUID;

@Entity
@Table(name = "payroll_records")
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class PayrollRecord {

    public static final BigDecimal OVERTIME_MULTIPLIER = new BigDecimal("1.5"); // MVP default, flagged for Sri Lanka labor-law verification

    @Id
    @Builder.Default
    private UUID id = UUID.randomUUID();

    @Column(nullable = false)
    private UUID employeeId;

    @Column(nullable = false)
    private LocalDate payPeriodStart;

    @Column(nullable = false)
    private LocalDate payPeriodEnd;

    @Column(nullable = false)
    private double regularHours;

    @Column(nullable = false)
    private double overtimeHours;

    @Column(nullable = false, precision = 12, scale = 2)
    private BigDecimal hourlyRate;

    @Column(nullable = false, precision = 12, scale = 2)
    private BigDecimal regularPay;

    @Column(nullable = false, precision = 12, scale = 2)
    private BigDecimal overtimePay;

    @Column(nullable = false, precision = 12, scale = 2)
    private BigDecimal grossPay;

    @Column(nullable = false, precision = 12, scale = 2)
    private BigDecimal netPay;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @Builder.Default
    private PayrollStatus status = PayrollStatus.Draft;

    @Builder.Default
    private Instant calculatedAt = Instant.now();

    public static PayrollRecord calculate(
            UUID employeeId,
            LocalDate start,
            LocalDate end,
            double regularHours,
            double approvedOvertimeHours,
            BigDecimal hourlyRate) {

        BigDecimal regHoursBd = BigDecimal.valueOf(regularHours);
        BigDecimal otHoursBd = BigDecimal.valueOf(approvedOvertimeHours);

        BigDecimal regularPay = regHoursBd.multiply(hourlyRate).setScale(2, RoundingMode.HALF_UP);
        BigDecimal overtimePay = otHoursBd.multiply(hourlyRate)
                .multiply(OVERTIME_MULTIPLIER)
                .setScale(2, RoundingMode.HALF_UP);

        BigDecimal grossPay = regularPay.add(overtimePay);
        BigDecimal netPay = grossPay; // Deductions can be plugged in later

        return PayrollRecord.builder()
                .employeeId(employeeId)
                .payPeriodStart(start)
                .payPeriodEnd(end)
                .regularHours(regularHours)
                .overtimeHours(approvedOvertimeHours)
                .hourlyRate(hourlyRate)
                .regularPay(regularPay)
                .overtimePay(overtimePay)
                .grossPay(grossPay)
                .netPay(netPay)
                .status(PayrollStatus.Draft)
                .calculatedAt(Instant.now())
                .build();
    }

    public void approve() {
        this.status = PayrollStatus.Approved;
    }

    public void markPaid() {
        this.status = PayrollStatus.Paid;
    }
}
