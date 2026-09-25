package com.smarthotel.fieldops.controller;

import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.PayrollRecord;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import com.smarthotel.fieldops.dto.PayrollDtos.GeneratePayrollRequest;
import com.smarthotel.fieldops.dto.PayrollDtos.PayrollResponse;
import com.smarthotel.fieldops.service.PayrollService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.*;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

@RestController
@RequestMapping("/api/v1/payroll")
@RequiredArgsConstructor
@Slf4j
public class PayrollController {

    private final PayrollService payrollService;
    private final EmployeeProfileRepository employeeProfileRepository;

    @PostMapping("/generate")
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<PayrollResponse> generatePayroll(@Valid @RequestBody GeneratePayrollRequest req) {
        log.info("Request to generate payroll for employee {} ({} to {})", req.employeeId(), req.startDate(), req.endDate());
        PayrollRecord record = payrollService.generatePayroll(req.employeeId(), req.startDate(), req.endDate());
        return ResponseEntity.status(HttpStatus.CREATED).body(mapToResponse(record));
    }

    @GetMapping("/my")
    @PreAuthorize("isAuthenticated()")
    public ResponseEntity<List<PayrollResponse>> getMyPayrollHistory(@AuthenticationPrincipal Jwt jwt) {
        UUID employeeId = getUserId(jwt);
        List<PayrollRecord> records = payrollService.getEmployeePayrollHistory(employeeId);
        return ResponseEntity.ok(records.stream().map(this::mapToResponse).toList());
    }

    @GetMapping("/{employeeId}")
    @PreAuthorize("hasAnyRole('Owner', 'Admin', 'Manager')")
    public ResponseEntity<List<PayrollResponse>> getEmployeePayrollHistory(@PathVariable UUID employeeId) {
        List<PayrollRecord> records = payrollService.getEmployeePayrollHistory(employeeId);
        return ResponseEntity.ok(records.stream().map(this::mapToResponse).toList());
    }

    private UUID getUserId(Jwt jwt) {
        if (jwt != null && jwt.getSubject() != null) {
            try {
                return UUID.fromString(jwt.getSubject());
            } catch (IllegalArgumentException ignored) {}
        }
        throw new IllegalStateException("Unable to resolve employee ID from token");
    }

    private PayrollResponse mapToResponse(PayrollRecord record) {
        Optional<EmployeeProfile> profileOpt = employeeProfileRepository.findById(record.getEmployeeId());

        String bankName = profileOpt.map(EmployeeProfile::getBankName).orElse("N/A");
        String maskedAccount = profileOpt
                .map(EmployeeProfile::getBankAccountNumber)
                .map(this::maskBankAccount)
                .orElse("N/A");

        return PayrollResponse.builder()
                .id(record.getId())
                .employeeId(record.getEmployeeId())
                .payPeriodStart(record.getPayPeriodStart())
                .payPeriodEnd(record.getPayPeriodEnd())
                .regularHours(record.getRegularHours())
                .overtimeHours(record.getOvertimeHours())
                .hourlyRate(record.getHourlyRate())
                .regularPay(record.getRegularPay())
                .overtimePay(record.getOvertimePay())
                .grossPay(record.getGrossPay())
                .netPay(record.getNetPay())
                .status(record.getStatus())
                .calculatedAt(record.getCalculatedAt())
                .bankName(bankName)
                .maskedBankAccount(maskedAccount)
                .build();
    }

    private String maskBankAccount(String accountNumber) {
        if (accountNumber == null || accountNumber.isBlank()) {
            return "N/A";
        }
        if (accountNumber.length() <= 4) {
            return "****" + accountNumber;
        }
        return "******" + accountNumber.substring(accountNumber.length() - 4);
    }
}
