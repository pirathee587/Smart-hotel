package com.smarthotel.fieldops.domain.repository;

import com.smarthotel.fieldops.domain.model.MenuItem;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface MenuItemRepository extends JpaRepository<MenuItem, UUID> {
    Optional<MenuItem> findByCode(String code);
    List<MenuItem> findByAvailableTrueOrderByCategoryAscNameAsc();
    List<MenuItem> findByCategoryIgnoreCaseAndAvailableTrue(String category);
}
