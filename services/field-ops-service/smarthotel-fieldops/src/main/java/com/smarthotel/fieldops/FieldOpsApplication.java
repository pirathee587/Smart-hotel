package com.smarthotel.fieldops;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.scheduling.annotation.EnableScheduling;

@SpringBootApplication
@EnableScheduling
public class FieldOpsApplication {

    public static void main(String[] args) {
        SpringApplication.run(FieldOpsApplication.class, args);
    }
}
