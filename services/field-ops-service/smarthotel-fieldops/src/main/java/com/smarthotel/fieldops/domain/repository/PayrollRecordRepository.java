package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.PayrollRecord;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.time.LocalDate;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

@Repository
public interface PayrollRecordRepository extends JpaRepository<PayrollRecord, UUID> {
    Optional<PayrollRecord> findByEmployeeIdAndPayPeriodStartAndPayPeriodEnd(UUID employeeId, LocalDate start, LocalDate end);
    List<PayrollRecord> findByEmployeeIdOrderByPayPeriodEndDesc(UUID employeeId);
}
