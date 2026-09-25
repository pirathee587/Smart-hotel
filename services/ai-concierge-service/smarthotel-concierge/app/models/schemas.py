from typing import Optional, Literal
from pydantic import BaseModel, Field, ConfigDict
from pydantic.alias_generators import to_camel
from datetime import datetime, timezone
import uuid


class CamelModel(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
        serialize_by_alias=True
    )


class ChatRequest(CamelModel):
    message: str = Field(..., min_length=1, max_length=2000, description="Guest message to the AI concierge")
    conversation_id: Optional[str] = Field(None, description="Optional conversation UUID for tracking context")


class VerifiedStayContext(BaseModel):
    booking_id: Optional[str] = None
    booking_reference: Optional[str] = None
    customer_id: str
    room_id: Optional[str] = None
    room_number: Optional[str] = None
    room_type_id: Optional[str] = None
    check_in_date: Optional[str] = None
    check_out_date: Optional[str] = None
    guest_count: int = 1
    is_active: bool = False


ServiceRequestType = Literal["Housekeeping", "Maintenance", "RoomService", "General"]
ServiceRequestPriority = Literal["Low", "Medium", "High", "Urgent"]


class ServiceRequestToolArgs(BaseModel):
    request_type: ServiceRequestType = Field(..., description="Category of the guest operational request")
    description: str = Field(..., min_length=3, max_length=500, description="Clear description of the service needed")
    priority: ServiceRequestPriority = Field("Medium", description="AI-suggested operational priority")


class ServiceRequestEvent(BaseModel):
    eventId: str = Field(default_factory=lambda: str(uuid.uuid4()))
    eventType: str = "task.requested"
    timestamp: str = Field(default_factory=lambda: datetime.now(timezone.utc).isoformat())
    customerId: str
    roomId: Optional[str] = None
    roomNumber: str
    requestType: str
    description: str
    priority: str = "Medium"
    sentiment: str = "Normal"
    language: str = "en"
    slaMinutes: int = 30
    requiresManagerAttention: bool = False
    bookingReference: Optional[str] = None
    conversationId: Optional[str] = None
    requestId: Optional[str] = None


class ManagerAlertEvent(BaseModel):
    eventId: str = Field(default_factory=lambda: str(uuid.uuid4()))
    eventType: str
    alertType: str
    timestamp: str = Field(default_factory=lambda: datetime.now(timezone.utc).isoformat())
    severity: str
    guestId: Optional[str] = None
    roomId: Optional[str] = None
    bookingId: Optional[str] = None
    bookingReference: Optional[str] = None
    taskId: Optional[str] = None
    messageSnippet: str = Field(..., max_length=500)
    conversationId: Optional[str] = None


class JWTPayload(BaseModel):
    sub: str
    role: Optional[str] = "Customer"
    email: Optional[str] = None
    exp: Optional[int] = None


class ChatMessageRecord(CamelModel):
    id: str
    conversation_id: str
    role: str
    content: str
    event_id: Optional[str] = None
    status: Optional[str] = None
    metadata_json: Optional[str] = None
    created_at: str


class ServiceRequestRecord(CamelModel):
    request_id: str
    event_id: str
    conversation_id: Optional[str] = None
    customer_id: str
    room_number: str
    request_type: str
    description: str
    priority: str = "Normal"
    status: str = "REQUEST_PENDING"
    task_id: Optional[str] = None
    created_at: str
    updated_at: str


class ConversationHistoryResponse(CamelModel):
    conversation_id: str
    customer_id: str
    messages: list[ChatMessageRecord]
