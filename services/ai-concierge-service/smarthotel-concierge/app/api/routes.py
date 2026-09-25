import logging
from typing import Optional, List
from fastapi import APIRouter, Depends, HTTPException, Request, status, Query
from fastapi.responses import StreamingResponse
from app.models.schemas import (
    ChatRequest, JWTPayload, ConversationHistoryResponse,
    ChatMessageRecord, ServiceRequestRecord
)
from app.auth.jwks import get_current_user
from app.clients.booking_client import booking_client
from app.services.rate_limiter import customer_rate_limiter
from app.services.concierge_service import concierge_service
from app.services.conversation_store import conversation_store

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/v1/concierge", tags=["Concierge"])


@router.post("/chat/stream")
async def chat_stream(
    chat_request: ChatRequest,
    request: Request,
    user: JWTPayload = Depends(get_current_user)
):
    """
    SSE streaming endpoint for AI Concierge chat.
    Validates JWT (RS256 JWKS), enforces 20 msg/min rate limit per customer,
    fetches verified guest stay context from Booking Service, and streams response.
    """
    # 1. Rate Limit Enforcement (20 msg/min per customer)
    allowed, retry_after = customer_rate_limiter.check_rate_limit(user.sub)
    if not allowed:
        raise HTTPException(
            status_code=status.HTTP_429_TOO_MANY_REQUESTS,
            detail="Rate limit exceeded. Maximum 20 messages per minute allowed.",
            headers={"Retry-After": str(retry_after)}
        )

    # 2. Extract Bearer token from header for downstream service forwarding
    auth_header = request.headers.get("authorization", "")
    token = auth_header.replace("Bearer ", "").strip() if "Bearer " in auth_header else ""

    # 3. Fetch verified guest stay context from Booking Service (server-side authoritative only)
    stay = await booking_client.get_active_stay(customer_id=user.sub, auth_token=token)

    # 4. Stream response via Server-Sent Events (SSE)
    return StreamingResponse(
        concierge_service.stream_chat(
            user_message=chat_request.message,
            stay=stay,
            conversation_id=chat_request.conversation_id
        ),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no"
        }
    )


@router.get("/history", response_model=ConversationHistoryResponse)
async def get_chat_history(
    conversation_id: str = Query(..., description="ID of the conversation session to retrieve"),
    user: JWTPayload = Depends(get_current_user)
):
    """
    Returns stored message history for a conversation session.
    Strictly validates that the requesting guest owns the session.
    """
    try:
        messages = conversation_store.get_history(
            conversation_id=conversation_id,
            customer_id=user.sub
        )
        return ConversationHistoryResponse(
            conversation_id=conversation_id,
            customer_id=user.sub,
            messages=messages
        )
    except PermissionError as pe:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail=str(pe)
        )
    except Exception as ex:
        logger.error(f"Failed to fetch conversation history: {ex}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to retrieve conversation history."
        )


@router.get("/requests", response_model=List[ServiceRequestRecord])
async def get_service_requests(
    user: JWTPayload = Depends(get_current_user)
):
    """
    Returns all service requests submitted by the authenticated guest,
    reflecting their current live lifecycle status.
    """
    try:
        requests = conversation_store.get_customer_requests(customer_id=user.sub)
        return requests
    except Exception as ex:
        logger.error(f"Failed to fetch customer requests: {ex}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to retrieve service requests."
        )


@router.get("/health")
async def health_check():
    return {
        "status": "Healthy",
        "service": "smarthotel-concierge",
        "version": "1.0.0"
    }
