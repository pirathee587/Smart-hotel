import json
import logging
from typing import Optional
import aio_pika
from app.config import settings
from app.models.schemas import ManagerAlertEvent, ServiceRequestEvent

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
            queue = await self._channel.declare_queue("smarthotel.concierge.task-sync", durable=True)
            await queue.bind(self._exchange, routing_key="task.created")
            await queue.bind(self._exchange, routing_key="task.dispatched")
            await queue.bind(self._exchange, routing_key="task.completed")
            await queue.consume(self._handle_task_status_event)

            logger.info(f"Connected to RabbitMQ at {settings.RABBITMQ_HOST}:{settings.RABBITMQ_PORT} and listening for task status events")
        except Exception as e:
            logger.warning(f"RabbitMQ connection failed (will retry or mock during testing): {e}")

    async def _handle_task_status_event(self, message: aio_pika.IncomingMessage):
        async with message.process():
            try:
                from app.services.conversation_store import conversation_store
                body = json.loads(message.body.decode())
                routing_key = message.routing_key

                event_id = body.get("eventId") or body.get("EventId")
                task_id = body.get("taskId") or body.get("TaskId")

                if not event_id:
                    return

                new_status = "TASK_CREATED"
                if routing_key == "task.dispatched":
                    new_status = "ASSIGNED"
                elif routing_key == "task.completed":
                    new_status = "COMPLETED"

                updated = conversation_store.update_request_status(
                    event_id=event_id,
                    new_status=new_status,
                    task_id=task_id
                )
                if updated:
                    logger.info(f"Updated request {event_id} status to {new_status} (taskId={task_id})")
            except Exception as ex:
                logger.error(f"Error handling task status event in concierge: {ex}")

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
            if settings.ENVIRONMENT == "testing":
                logger.info(f"[TESTING] RabbitMQ mock accepted event: {payload_str}")
                return True
            logger.warning(f"RabbitMQ unavailable. Cannot publish event: {payload_str}")
            return False

    async def publish_manager_alert(self, event: ManagerAlertEvent, routing_key: str) -> bool:
        payload_str = json.dumps(event.model_dump())
        if not self._channel or not self._exchange:
            await self.connect()
        if self._exchange:
            try:
                await self._exchange.publish(
                    aio_pika.Message(
                        body=payload_str.encode(), content_type="application/json",
                        delivery_mode=aio_pika.DeliveryMode.PERSISTENT,
                        message_id=event.eventId,
                        headers={"eventType": event.eventType},
                    ),
                    routing_key=routing_key,
                )
                return True
            except Exception as ex:
                logger.error("Failed to publish manager alert %s: %s", event.eventId, ex)
                return False
        return settings.ENVIRONMENT == "testing"

    async def close(self):
        if self._connection:
            await self._connection.close()


rabbitmq_publisher = RabbitMQEventPublisher()
