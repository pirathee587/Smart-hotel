package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.FnbTaxRule;
import com.smarthotel.fieldops.domain.repository.FnbTaxRuleRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;

@Service
@RequiredArgsConstructor
@Slf4j
public class TaxRuleService {

    private final FnbTaxRuleRepository taxRuleRepository;

    @Value("${fnb.tax.default-rate:0.00}")
    private BigDecimal defaultTaxRate;

    public BigDecimal resolveTaxRate(String category, Instant at) {
        if (category == null || category.isBlank()) {
            category = "FoodAndBeverage";
        }

        Instant effectiveTime = at != null ? at : Instant.now();
        List<FnbTaxRule> rules = taxRuleRepository.findEffectiveRules(category, effectiveTime);

        if (!rules.isEmpty()) {
            BigDecimal rate = rules.get(0).getTaxRate();
            log.debug("Resolved effective tax rate {} for category '{}' at {}", rate, category, effectiveTime);
            return rate;
        }

        // Fallback to general category if specific category not found
        if (!"FoodAndBeverage".equalsIgnoreCase(category)) {
            List<FnbTaxRule> generalRules = taxRuleRepository.findEffectiveRules("FoodAndBeverage", effectiveTime);
            if (!generalRules.isEmpty()) {
                BigDecimal rate = generalRules.get(0).getTaxRate();
                log.debug("Resolved fallback general tax rate {} for category '{}' at {}", rate, category, effectiveTime);
                return rate;
            }
        }

        log.debug("Using configured default tax rate {} for category '{}'", defaultTaxRate, category);
        return defaultTaxRate != null ? defaultTaxRate : BigDecimal.ZERO;
    }
}
