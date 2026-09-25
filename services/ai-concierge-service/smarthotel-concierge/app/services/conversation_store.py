import json
import logging
import uuid
from datetime import datetime, timezone
from typing import List, Optional
from app.database import db_manager
from app.models.schemas import ChatMessageRecord, ServiceRequestRecord

logger = logging.getLogger(__name__)


class ConversationStore:
    """
    Durable, shared repository for guest conversation sessions, messages,
    and service request lifecycle tracking across multi-replica deployments.
    """

    def get_or_create_conversation(
        self,
        conversation_id: Optional[str],
        customer_id: str,
        booking_reference: Optional[str] = None
    ) -> str:
        cid = conversation_id or str(uuid.uuid4())
        now = datetime.now(timezone.utc).isoformat()

        rows = db_manager.execute_query(
            "SELECT customer_id FROM conversations WHERE conversation_id = %s",
            (cid,)
        )

        if rows:
            owner = rows[0]["customer_id"]
            if owner != customer_id:
                logger.warning(f"Security: customer {customer_id} attempted to access conversation {cid} owned by {owner}")
                raise PermissionError("Access denied: conversation belongs to another customer.")
            db_manager.execute_write(
                "UPDATE conversations SET updated_at = %s WHERE conversation_id = %s",
                (now, cid)
            )
        else:
            db_manager.execute_write(
                "INSERT INTO conversations (conversation_id, customer_id, booking_reference, created_at, updated_at) "
                "VALUES (%s, %s, %s, %s, %s)",
                (cid, customer_id, booking_reference or "", now, now)
            )

        return cid

    def save_message(
        self,
        conversation_id: str,
        customer_id: str,
        role: str,
        content: str,
        event_id: Optional[str] = None,
        status: Optional[str] = None,
        metadata: Optional[dict] = None,
        booking_reference: Optional[str] = None
    ) -> ChatMessageRecord:
        # Validate ownership
        self.get_or_create_conversation(conversation_id, customer_id, booking_reference)

        mid = str(uuid.uuid4())
        now = datetime.now(timezone.utc).isoformat()
        meta_json = json.dumps(metadata) if metadata else None

        db_manager.execute_write(
            "INSERT INTO messages (id, conversation_id, role, content, event_id, status, metadata_json, created_at) "
            "VALUES (%s, %s, %s, %s, %s, %s, %s, %s)",
            (mid, conversation_id, role, content, event_id, status, meta_json, now)
        )

        return ChatMessageRecord(
            id=mid,
            conversation_id=conversation_id,
            role=role,
            content=content,
            event_id=event_id,
            status=status,
            metadata_json=meta_json,
            created_at=now
        )

    def get_history(self, conversation_id: str, customer_id: str) -> List[ChatMessageRecord]:
        rows = db_manager.execute_query(
            "SELECT customer_id FROM conversations WHERE conversation_id = %s",
            (conversation_id,)
        )
        if not rows:
            return []

        if rows[0]["customer_id"] != customer_id:
            logger.warning(f"Security: customer {customer_id} denied history for conversation {conversation_id}")
            raise PermissionError("Access denied: conversation belongs to another customer.")

        msg_rows = db_manager.execute_query(
            "SELECT id, conversation_id, role, content, event_id, status, metadata_json, created_at "
            "FROM messages WHERE conversation_id = %s ORDER BY created_at ASC",
            (conversation_id,)
        )

        return [
            ChatMessageRecord(
                id=r["id"],
                conversation_id=r["conversation_id"],
                role=r["role"],
                content=r["content"],
                event_id=r["event_id"],
                status=r["status"],
                metadata_json=r["metadata_json"],
                created_at=r["created_at"]
            )
            for r in msg_rows
        ]

    def record_service_request(
        self,
        request_id: str,
        event_id: str,
        customer_id: str,
        room_number: str,
        request_type: str,
        description: str,
        priority: str = "Normal",
        conversation_id: Optional[str] = None
    ) -> ServiceRequestRecord:
        now = datetime.now(timezone.utc).isoformat()
        db_manager.execute_write(
            "INSERT INTO service_requests (request_id, event_id, conversation_id, customer_id, room_number, "
            "request_type, description, priority, status, created_at, updated_at) "
            "VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)",
            (request_id, event_id, conversation_id, customer_id, room_number,
             request_type, description, priority, "REQUEST_PENDING", now, now)
        )

        return ServiceRequestRecord(
            request_id=request_id,
            event_id=event_id,
            conversation_id=conversation_id,
            customer_id=customer_id,
            room_number=room_number,
            request_type=request_type,
            description=description,
            priority=priority,
            status="REQUEST_PENDING",
            task_id=None,
            created_at=now,
            updated_at=now
        )

    def update_request_status(
        self,
        event_id: str,
        new_status: str,
        task_id: Optional[str] = None
    ) -> Optional[ServiceRequestRecord]:
        now = datetime.now(timezone.utc).isoformat()
        if task_id:
            db_manager.execute_write(
                "UPDATE service_requests SET status = %s, task_id = %s, updated_at = %s WHERE event_id = %s",
                (new_status, task_id, now, event_id)
            )
        else:
            db_manager.execute_write(
                "UPDATE service_requests SET status = %s, updated_at = %s WHERE event_id = %s",
                (new_status, now, event_id)
            )

        rows = db_manager.execute_query(
            "SELECT request_id, event_id, conversation_id, customer_id, room_number, "
            "request_type, description, priority, status, task_id, created_at, updated_at "
            "FROM service_requests WHERE event_id = %s",
            (event_id,)
        )
        if rows:
            r = rows[0]
            return ServiceRequestRecord(
                request_id=r["request_id"],
                event_id=r["event_id"],
                conversation_id=r["conversation_id"],
                customer_id=r["customer_id"],
                room_number=r["room_number"],
                request_type=r["request_type"],
                description=r["description"],
                priority=r["priority"],
                status=r["status"],
                task_id=r["task_id"],
                created_at=r["created_at"],
                updated_at=r["updated_at"]
            )
        return None

    def get_customer_requests(self, customer_id: str) -> List[ServiceRequestRecord]:
        rows = db_manager.execute_query(
            "SELECT request_id, event_id, conversation_id, customer_id, room_number, "
            "request_type, description, priority, status, task_id, created_at, updated_at "
            "FROM service_requests WHERE customer_id = %s ORDER BY created_at DESC",
            (customer_id,)
        )
        return [
            ServiceRequestRecord(
                request_id=r["request_id"],
                event_id=r["event_id"],
                conversation_id=r["conversation_id"],
                customer_id=r["customer_id"],
                room_number=r["room_number"],
                request_type=r["request_type"],
                description=r["description"],
                priority=r["priority"],
                status=r["status"],
                task_id=r["task_id"],
                created_at=r["created_at"],
                updated_at=r["updated_at"]
            )
            for r in rows
        ]


conversation_store = ConversationStore()
