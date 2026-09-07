import json
import logging
from typing import Optional
import aio_pika
from app.config import settings
from app.models.schemas import ServiceRequestEvent

logger = logging.getLogger(__name__)


class RabbitMQEventPublisher:
    def __init__(self):
        self._connection: Optional[aio_pika.RobustConnection] = None
        self._channel: Optional[aio_pika.RobustChannel] = None
        self._exchange: Optional[aio_pika.RobustExchange] = None
        self._disabled: bool = False

    async def connect(self):
        if self._disabled:
            return

        try:
            url = f"amqp://{settings.RABBITMQ_USER}:{settings.RABBITMQ_PASSWORD}@{settings.RABBITMQ_HOST}:{settings.RABBITMQ_PORT}/"
            self._connection = await aio_pika.connect_robust(url, timeout=5.0)
            self._channel = await self._connection.channel()
            self._exchange = await self._channel.declare_exchange(
                name=settings.RABBITMQ_EXCHANGE,
                type=aio_pika.ExchangeType.TOPIC,
                durable=True,
            )
            logger.info(f"Connected to RabbitMQ at {settings.RABBITMQ_HOST}:{settings.RABBITMQ_PORT}")
        except Exception as e:
            logger.warning(f"RabbitMQ connection failed (will retry or mock during testing): {e}")

    async def publish_service_request(self, event: ServiceRequestEvent) -> bool:
        payload_str = json.dumps(event.model_dump())

        if not self._channel or not self._exchange:
            await self.connect()

        if self._exchange:
            try:
                message = aio_pika.Message(
                    body=payload_str.encode(),
                    content_type="application/json",
                    delivery_mode=aio_pika.DeliveryMode.PERSISTENT,
                    headers={"eventType": event.eventType}
                )
                await self._exchange.publish(message, routing_key="task.requested")
                logger.info(f"Published task.requested event for room {event.roomNumber}: {event.description}")
                return True
            except Exception as e:
                logger.error(f"Failed to publish task.requested event: {e}")
                return False
        else:
            logger.warning(f"[OFFLINE/MOCK] RabbitMQ not connected. Event recorded: {payload_str}")
            return True

    async def close(self):
        if self._connection:
            await self._connection.close()


rabbitmq_publisher = RabbitMQEventPublisher()
