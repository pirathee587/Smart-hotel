import asyncio
import json
import logging
import uuid
from typing import AsyncGenerator, Dict, Any, List, Optional
from app.models.schemas import ManagerAlertEvent, VerifiedStayContext
from app.rag.vector_store import knowledge_store
from app.services.prompt_guard import prompt_guard
from app.services.llm_provider import classify_guest_message, get_llm_provider
from app.services.conversation_store import conversation_store
from app.clients.rabbitmq_client import rabbitmq_publisher

logger = logging.getLogger(__name__)


class ConciergeService:
    def __init__(self):
        pass

    async def stream_chat(
        self,
        user_message: str,
        stay: VerifiedStayContext,
        conversation_id: Optional[str] = None
    ) -> AsyncGenerator[str, None]:
        """
        Processes user query, applies prompt injection defense, retrieves RAG context,
        evaluates operational service requests with server-side validation,
        durable multi-replica persistence, and streams SSE events.
        """
        try:
            # 1. Resolve or create conversation session
            cid = conversation_store.get_or_create_conversation(
                conversation_id=conversation_id,
                customer_id=stay.customer_id,
                booking_reference=stay.booking_reference
            )

            # Persist incoming user message
            conversation_store.save_message(
                conversation_id=cid,
                customer_id=stay.customer_id,
                role="user",
                content=user_message
            )

            # Emit initial metadata event
            yield f"data: {json.dumps({'type': 'context', 'verifiedRoom': stay.room_number, 'isActive': stay.is_active, 'conversationId': cid})}\n\n"
            await asyncio.sleep(0.01)

            # 2. Prompt injection defense check
            is_injection, reason = prompt_guard.detect_prompt_injection(user_message)
            if is_injection:
                refusal = (
                    "I apologize, but I cannot fulfill that request. As the SmartHotel Concierge, "
                    "I am here to assist with hotel amenities, dining, and stay services. "
                    "How may I assist you with your stay today?"
                )
                conversation_store.save_message(
                    conversation_id=cid,
                    customer_id=stay.customer_id,
                    role="assistant",
                    content=refusal
                )
                yield f"data: {json.dumps({'type': 'chunk', 'content': refusal})}\n\n"
                yield f"data: {json.dumps({'type': 'done'})}\n\n"
                return

            # 3. One advisory classification call for sentiment and optional task planning.
            classification = await classify_guest_message(user_message)
            if classification.sentiment in {"Frustrated", "Angry"}:
                await rabbitmq_publisher.publish_manager_alert(
                    ManagerAlertEvent(
                        eventType="GuestSentimentEscalated",
                        alertType="GuestSentiment",
                        severity=classification.sentiment,
                        guestId=stay.customer_id,
                        roomId=stay.room_id,
                        bookingId=stay.booking_id,
                        bookingReference=stay.booking_reference,
                        messageSnippet=user_message[:500],
                        conversationId=cid,
                    ),
                    "guest.sentiment.escalated",
                )

            # 4. Retrieve RAG knowledge
            knowledge_chunks = knowledge_store.query(user_message, k=3)

            # 5. Use the same classification result for operational task suggestions.
            service_intent = classification if classification.is_task else None

            if service_intent:
                req_type = service_intent.department
                description = service_intent.description
                priority = service_intent.priority
                tool_args = {
                    "request_type": req_type,
                    "description": description,
                    "priority": priority,
                    "sentiment": service_intent.sentiment,
                    "language": service_intent.language,
                    "sla_minutes": service_intent.sla_minutes,
                    "requires_manager_attention": service_intent.requires_manager_attention,
                    # Server-enforced stay room number; ignores in-prompt spoof attempts
                    "room_number": stay.room_number
                }

                is_valid, error_msg, event = prompt_guard.validate_service_request(tool_args, stay)

                if is_valid and event:
                    request_id = str(uuid.uuid4())
                    event.bookingReference = stay.booking_reference
                    event.conversationId = cid
                    event.requestId = request_id

                    if event.priority == "Urgent":
                        await rabbitmq_publisher.publish_manager_alert(
                            ManagerAlertEvent(
                                eventType="UrgentTaskPrioritySuggested",
                                alertType="UrgentTask",
                                severity="Urgent",
                                guestId=stay.customer_id,
                                roomId=stay.room_id,
                                bookingId=stay.booking_id,
                                bookingReference=stay.booking_reference,
                                messageSnippet=event.description[:500],
                                conversationId=cid,
                            ),
                            "task.priority.urgent",
                        )

                    # 1. Record in shared persistent store as REQUEST_PENDING
                    conversation_store.record_service_request(
                        request_id=request_id,
                        event_id=event.eventId,
                        customer_id=stay.customer_id,
                        room_number=event.roomNumber,
                        request_type=event.requestType,
                        description=event.description,
                        priority=event.priority,
                        conversation_id=cid
                    )

                    # 2. Publish event to RabbitMQ
                    published = await rabbitmq_publisher.publish_service_request(event)

                    if published:
                        # Clear status: REQUEST_PENDING in queue
                        yield f"data: {json.dumps({'type': 'tool_call', 'tool': 'create_service_request', 'status': 'REQUEST_PENDING', 'requestId': request_id, 'eventId': event.eventId, 'requestType': event.requestType, 'roomNumber': event.roomNumber})}\n\n"
                        await asyncio.sleep(0.02)

                        ref_short = request_id[:8]
                        attention_note = " A manager has been alerted due to the urgency of this request." if event.requiresManagerAttention else ""
                        confirmation = (
                            f"I have received your request for '{event.description}' to Room {event.roomNumber} "
                            f"and queued it for our {event.requestType} team (Ref: #{ref_short}). "
                            f"Target response time: {event.slaMinutes} minutes."
                            f"{attention_note} You can track its live status and confirmation on your request card."
                        )
                        conversation_store.save_message(
                            conversation_id=cid,
                            customer_id=stay.customer_id,
                            role="assistant",
                            content=confirmation,
                            event_id=event.eventId,
                            status="REQUEST_PENDING",
                            metadata={"requestId": request_id, "roomNumber": event.roomNumber, "requestType": event.requestType,
                                      "sentiment": event.sentiment, "language": event.language, "slaMinutes": event.slaMinutes,
                                      "requiresManagerAttention": event.requiresManagerAttention}
                        )
                        yield f"data: {json.dumps({'type': 'chunk', 'content': confirmation})}\n\n"
                    else:
                        conversation_store.update_request_status(event.eventId, "FAILED")
                        yield f"data: {json.dumps({'type': 'tool_call', 'tool': 'create_service_request', 'status': 'FAILED', 'requestId': request_id, 'eventId': event.eventId})}\n\n"
                        failure_msg = (
                            "I apologize, but our operations dispatch bus is temporarily offline. "
                            "Your request could not be queued. Please dial extension 0 on your room phone to reach the front desk directly."
                        )
                        conversation_store.save_message(
                            conversation_id=cid,
                            customer_id=stay.customer_id,
                            role="assistant",
                            content=failure_msg,
                            status="FAILED"
                        )
                        yield f"data: {json.dumps({'type': 'chunk', 'content': failure_msg})}\n\n"
                else:
                    failure_reply = (
                        f"I understand you would like to request assistance ({description}), but {error_msg} "
                        "Please visit our reception desk in the lobby if you require assistance with your reservation."
                    )
                    conversation_store.save_message(
                        conversation_id=cid,
                        customer_id=stay.customer_id,
                        role="assistant",
                        content=failure_reply
                    )
                    yield f"data: {json.dumps({'type': 'chunk', 'content': failure_reply})}\n\n"

                yield f"data: {json.dumps({'type': 'done'})}\n\n"
                return

            # 5. Informational Question answering via pluggable LLM provider with RAG
            llm_provider = get_llm_provider()
            collected_chunks = []
            async for token in llm_provider.stream_answer(user_message, stay, knowledge_chunks):
                collected_chunks.append(token)
                yield f"data: {json.dumps({'type': 'chunk', 'content': token})}\n\n"

            # Persist complete assistant answer
            full_answer = "".join(collected_chunks)
            conversation_store.save_message(
                conversation_id=cid,
                customer_id=stay.customer_id,
                role="assistant",
                content=full_answer
            )
            yield f"data: {json.dumps({'type': 'done'})}\n\n"

        except Exception as ex:
            logger.error(f"Error during concierge stream: {ex}", exc_info=True)
            yield f"data: {json.dumps({'type': 'error', 'message': f'Concierge error: {str(ex)}'})}\n\n"
            yield f"data: {json.dumps({'type': 'done'})}\n\n"

concierge_service = ConciergeService()
