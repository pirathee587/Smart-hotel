from typing import Optional, Literal
from pydantic import BaseModel, Field
from datetime import datetime, timezone
import uuid


class ChatRequest(BaseModel):
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
ServiceRequestPriority = Literal["Low", "Normal", "High", "Urgent"]


class ServiceRequestToolArgs(BaseModel):
    request_type: ServiceRequestType = Field(..., description="Category of the guest operational request")
    description: str = Field(..., min_length=3, max_length=500, description="Clear description of the service needed")
    priority: ServiceRequestPriority = Field("Normal", description="Operational priority")


class ServiceRequestEvent(BaseModel):
    eventId: str = Field(default_factory=lambda: str(uuid.uuid4()))
    eventType: str = "task.requested"
    timestamp: str = Field(default_factory=lambda: datetime.now(timezone.utc).isoformat())
    customerId: str
    roomId: Optional[str] = None
    roomNumber: str
    requestType: str
    description: str
    priority: str = "Normal"


class JWTPayload(BaseModel):
    sub: str
    role: Optional[str] = "Customer"
    email: Optional[str] = None
    exp: Optional[int] = None
