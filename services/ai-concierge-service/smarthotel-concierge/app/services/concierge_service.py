import asyncio
import json
import logging
from typing import AsyncGenerator, Dict, Any, List, Optional
from app.models.schemas import VerifiedStayContext, ServiceRequestEvent
from app.rag.vector_store import knowledge_store
from app.services.prompt_guard import prompt_guard
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
        and streams SSE events.
        """
        # 1. Initial metadata event
        yield f"data: {json.dumps({'type': 'context', 'verifiedRoom': stay.room_number, 'isActive': stay.is_active, 'conversationId': conversation_id})}\n\n"
        await asyncio.sleep(0.01)

        # 2. Prompt injection defense check
        is_injection, reason = prompt_guard.detect_prompt_injection(user_message)
        if is_injection:
            refusal = "I apologize, but I cannot fulfill that request. As the SmartHotel Concierge, I am here to assist with hotel amenities, dining, and stay services. How may I assist you with your stay today?"
            yield f"data: {json.dumps({'type': 'chunk', 'content': refusal})}\n\n"
            yield f"data: {json.dumps({'type': 'done'})}\n\n"
            return

        # 3. Retrieve RAG knowledge
        knowledge_chunks = knowledge_store.query(user_message, k=3)

        # 4. Detect operational service intent
        service_intent = self._detect_service_intent(user_message)

        if service_intent:
            req_type, description, priority = service_intent
            tool_args = {
                "request_type": req_type,
                "description": description,
                "priority": priority,
                # Intentionally pass client/prompt override attempt if any to test guard rejection
                "room_number": stay.room_number
            }

            is_valid, error_msg, event = prompt_guard.validate_service_request(tool_args, stay)

            if is_valid and event:
                # Publish event to RabbitMQ
                published = await rabbitmq_publisher.publish_service_request(event)

                yield f"data: {json.dumps({'type': 'tool_call', 'tool': 'create_service_request', 'status': 'dispatched', 'eventId': event.eventId, 'requestType': event.requestType, 'roomNumber': event.roomNumber})}\n\n"
                await asyncio.sleep(0.02)

                confirmation = (
                    f"I have submitted a {event.requestType.lower()} request for '{event.description}' to Room {event.roomNumber}. "
                    f"Our hotel operations team has been notified (Ref: {event.eventId[:8]}) and will attend to it shortly."
                )
                yield f"data: {json.dumps({'type': 'chunk', 'content': confirmation})}\n\n"
            else:
                failure_reply = (
                    f"I understand you would like to request assistance ({description}), but {error_msg} "
                    "Please visit our reception desk in the lobby if you require assistance with your reservation."
                )
                yield f"data: {json.dumps({'type': 'chunk', 'content': failure_reply})}\n\n"

            yield f"data: {json.dumps({'type': 'done'})}\n\n"
            return

        # 5. Informational Question answering based on RAG knowledge
        answer = self._generate_rag_answer(user_message, stay, knowledge_chunks)
        for word in answer.split(" "):
            yield f"data: {json.dumps({'type': 'chunk', 'content': word + ' '})}\n\n"
            await asyncio.sleep(0.01)

        yield f"data: {json.dumps({'type': 'done'})}\n\n"

    def _detect_service_intent(self, message: str) -> Optional[tuple]:
        msg = message.lower()

        # Housekeeping triggers
        if any(w in msg for w in ["towel", "blanket", "pillow", "clean my room", "cleaning", "linen", "toiletries", "shampoo", "soap"]):
            return "Housekeeping", message.strip(), "Normal"

        # Maintenance triggers
        if any(w in msg for w in ["ac not working", "air conditioner", "broken", "leak", "plumbing", "tv not working", "light bulb", "drain"]):
            return "Maintenance", message.strip(), "High"

        # Room service triggers
        if any(w in msg for w in ["order food", "room service", "dinner in room", "bottle of water", "ice bucket"]):
            return "RoomService", message.strip(), "Normal"

        return None

    def _generate_rag_answer(self, query: str, stay: VerifiedStayContext, knowledge: List[str]) -> str:
        q = query.lower()

        if "pool" in q:
            return "Our oceanfront Infinity Swimming Pool is located on Floor 4, open daily from 06:00 AM to 20:00 (8:00 PM). Complimentary towels are provided poolside."
        if "restaurant" in q or "breakfast" in q or "blue harbor" in q or "dining" in q:
            return "Blue Harbor Restaurant is located on Floor 1. Breakfast is served from 06:30 AM to 10:30 AM daily, lunch from 12:30 PM to 3:30 PM, and dinner from 7:00 PM to 11:00 PM. In-room dining is also available 24/7."
        if "bar" in q or "rooftop" in q or "skylounge" in q:
            return "SkyLounge Rooftop Bar is located on Floor 8, open daily from 17:00 (5:00 PM) to 01:00 AM with panoramic sunset views and signature cocktails."
        if "spa" in q or "massage" in q:
            return "Lotus Ayurveda Spa is situated on Floor 2, open daily from 09:00 AM to 21:00 (9:00 PM), offering authentic Ayurvedic wellness therapies and steam baths."
        if "wifi" in q or "internet" in q:
            return "Complimentary high-speed WiFi is available throughout the hotel under the network name 'SmartHotel-Guest'. Simply enter your room number and last name on the portal to connect."
        if "checkout" in q or "check out" in q:
            return f"Standard check-out time is 11:00 AM. Your scheduled check-out date is {stay.check_out_date or 'on file with the front desk'}. Express kiosk checkout is available in the lobby."
        if "checkin" in q or "check in" in q:
            return "Standard check-in time is 14:00 (2:00 PM). Early check-in and complimentary luggage storage are available at the bell desk."

        if knowledge:
            return f"Here is the information from our guest guide:\n{knowledge[0]}"

        return "Welcome to SmartHotel! How may I assist you with our dining, amenities, or stay services today?"


concierge_service = ConciergeService()
