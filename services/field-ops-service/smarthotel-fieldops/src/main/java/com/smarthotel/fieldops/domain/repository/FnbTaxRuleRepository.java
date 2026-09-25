package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.FnbTaxRule;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.Instant;
import java.util.List;
import java.util.UUID;

public interface FnbTaxRuleRepository extends JpaRepository<FnbTaxRule, UUID> {

    @Query("SELECT r FROM FnbTaxRule r WHERE r.active = true AND r.category = :category " +
           "AND r.effectiveFrom <= :at AND (r.effectiveTo IS NULL OR r.effectiveTo > :at) " +
           "ORDER BY r.effectiveFrom DESC")
    List<FnbTaxRule> findEffectiveRules(@Param("category") String category, @Param("at") Instant at);
}
