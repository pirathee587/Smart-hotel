package com.smarthotel.fieldops.config;

import org.springframework.amqp.core.*;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class RabbitMQConfig {

    @Value("${rabbitmq.exchange.events:smarthotel.events}")
    private String eventsExchange;

    @Value("${rabbitmq.queue.booking-checkout:smarthotel.fieldops.booking-checkout}")
    private String bookingCheckoutQueue;

    @Value("${rabbitmq.routing-key.booking-checkout:booking.checkedout}")
    private String bookingCheckoutRoutingKey;

    @Value("${rabbitmq.queue.employee-sync:smarthotel.fieldops.employee-sync}")
    private String employeeSyncQueue;

    @Value("${rabbitmq.routing-key.employee-sync:employee.*}")
    private String employeeSyncRoutingKey;

    @Value("${rabbitmq.queue.concierge-task:smarthotel.fieldops.task-requested}")
    private String conciergeTaskQueue;

    @Value("${rabbitmq.routing-key.concierge-task:task.requested}")
    private String conciergeTaskRoutingKey;

    @Bean
    public TopicExchange eventsExchange() {
        return new TopicExchange(eventsExchange, true, false);
    }

    @Bean
    public Queue bookingCheckoutQueue() {
        return QueueBuilder.durable(bookingCheckoutQueue).build();
    }

    @Bean
    public Binding bookingCheckoutBinding(Queue bookingCheckoutQueue, TopicExchange eventsExchange) {
        return BindingBuilder.bind(bookingCheckoutQueue)
                .to(eventsExchange)
                .with(bookingCheckoutRoutingKey);
    }

    @Bean
    public Queue employeeSyncQueue() {
        return QueueBuilder.durable(employeeSyncQueue).build();
    }

    @Bean
    public Binding employeeSyncBinding(Queue employeeSyncQueue, TopicExchange eventsExchange) {
        return BindingBuilder.bind(employeeSyncQueue)
                .to(eventsExchange)
                .with(employeeSyncRoutingKey);
    }

    @Bean
    public Queue conciergeTaskQueue() {
        return QueueBuilder.durable(conciergeTaskQueue).build();
    }

    @Bean
    public Binding conciergeTaskBinding(Queue conciergeTaskQueue, TopicExchange eventsExchange) {
        return BindingBuilder.bind(conciergeTaskQueue)
                .to(eventsExchange)
                .with(conciergeTaskRoutingKey);
    }
}
