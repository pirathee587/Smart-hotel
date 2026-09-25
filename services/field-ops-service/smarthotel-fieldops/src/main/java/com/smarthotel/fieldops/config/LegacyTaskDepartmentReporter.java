package com.smarthotel.fieldops.config;

import com.smarthotel.fieldops.domain.repository.StaffTaskRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.stereotype.Component;

@Component
@RequiredArgsConstructor
@Slf4j
public class LegacyTaskDepartmentReporter implements ApplicationRunner {
    private final StaffTaskRepository repository;

    @Override
    public void run(ApplicationArguments args) {
        long count = repository.countByDepartmentIdIsNull();
        if (count > 0) {
            log.warn("DEPARTMENT MIGRATION REQUIRED: {} legacy tasks have no DepartmentId. They remain preserved and inaccessible until explicitly assigned.", count);
        } else {
            log.info("Department task migration check complete: zero tasks without DepartmentId.");
        }
    }
}
