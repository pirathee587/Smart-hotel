package com.smarthotel.fieldops.service;

import com.smarthotel.fieldops.domain.model.TaskOutbox;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.OutboxStatus;
import com.smarthotel.fieldops.domain.repository.TaskOutboxRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Duration;
import java.time.Instant;
import java.util.List;

@Service
@RequiredArgsConstructor
@Slf4j
public class TaskOutboxProcessor {

    private final TaskOutboxRepository outboxRepository;
    private final RabbitTemplate rabbitTemplate;

    @Value("${rabbitmq.exchange.events:smarthotel.events}")
    private String eventsExchange;

    @Scheduled(fixedDelay = 5000)
    public void processPendingOutbox() {
        List<TaskOutbox> pending = outboxRepository.findByStatusInAndNextRetryAtLessThanEqualOrderByCreatedAtAsc(
                List.of(OutboxStatus.Pending), Instant.now());

        if (pending.isEmpty()) {
            return;
        }

        log.debug("Processing {} pending Task Outbox event records", pending.size());
        for (TaskOutbox outbox : pending) {
            processSingleOutbox(outbox);
        }
    }

    @Transactional
    public boolean processSingleOutbox(TaskOutbox outbox) {
        try {
            rabbitTemplate.convertAndSend(eventsExchange, outbox.getEventType(), outbox.getPayloadJson());
            outbox.markConfirmed();
            outboxRepository.save(outbox);
            log.info("Published Task Outbox event {} ({}) for aggregate {}",
                    outbox.getId(), outbox.getEventType(), outbox.getAggregateId());
            return true;
        } catch (Exception ex) {
            long delaySeconds = Math.min(300, (long) Math.pow(2, outbox.getRetryCount() + 1));
            Instant nextRetry = Instant.now().plus(Duration.ofSeconds(delaySeconds));
            outbox.recordFailure(ex.getMessage(), nextRetry);
            outboxRepository.save(outbox);
            log.warn("Task Outbox publish failed for {} (retry={}): {}. Next retry at {}",
                    outbox.getId(), outbox.getRetryCount(), ex.getMessage(), nextRetry);
            return false;
        }
    }
}
