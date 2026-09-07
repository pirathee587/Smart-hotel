package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.AttendanceRecord;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.time.Instant;
import java.time.LocalDate;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

@Repository
public interface AttendanceRecordRepository extends JpaRepository<AttendanceRecord, UUID> {
    Optional<AttendanceRecord> findByEmployeeIdAndDate(UUID employeeId, LocalDate date);
    Optional<AttendanceRecord> findTopByEmployeeIdOrderByClockInDesc(UUID employeeId);
    List<AttendanceRecord> findByClockOutIsNullAndClockInBefore(Instant cutoff);
    List<AttendanceRecord> findByEmployeeIdAndDateBetweenOrderByDateAsc(UUID employeeId, LocalDate start, LocalDate end);
    List<AttendanceRecord> findByDateBetweenOrderByDateAsc(LocalDate start, LocalDate end);
}
