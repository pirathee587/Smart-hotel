import logging
from fastapi import APIRouter, Depends, HTTPException, Request, status
from fastapi.responses import StreamingResponse
from app.models.schemas import ChatRequest, JWTPayload
from app.auth.jwks import get_current_user
from app.clients.booking_client import booking_client
from app.services.rate_limiter import customer_rate_limiter
from app.services.concierge_service import concierge_service

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

    # 3. Fetch verified guest stay context from Booking Service (server-side only)
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


@router.get("/health")
async def health_check():
    return {
        "status": "Healthy",
        "service": "smarthotel-concierge",
        "version": "1.0.0"
    }
