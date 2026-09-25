import json
import pytest
from app.clients.booking_client import booking_client
from app.clients.rabbitmq_client import rabbitmq_publisher
from app.models.schemas import VerifiedStayContext
from app.services.conversation_store import conversation_store
from tests.conftest import generate_jwt


def test_chat_stream_persists_messages_and_creates_conversation(client, customer_token, monkeypatch):
    async def mock_stay(*args, **kwargs):
        return VerifiedStayContext(
            customer_id="cust-12345",
            room_number="405",
            booking_reference="TH-2026-PERSIST",
            is_active=True
        )

    monkeypatch.setattr(booking_client, "get_active_stay", mock_stay)

    conv_id = "conv-persist-001"
    response = client.post(
        "/api/v1/concierge/chat/stream",
        json={"message": "What is the checkout time?", "conversationId": conv_id},
        headers={"Authorization": f"Bearer {customer_token}"}
    )
    assert response.status_code == 200

    # Retrieve history via API
    hist_resp = client.get(
        f"/api/v1/concierge/history?conversation_id={conv_id}",
        headers={"Authorization": f"Bearer {customer_token}"}
    )
    assert hist_resp.status_code == 200
    data = hist_resp.json()
    assert data["conversationId"] == conv_id
    assert len(data["messages"]) >= 2

    roles = [m["role"] for m in data["messages"]]
    assert "user" in roles
    assert "assistant" in roles
    assert any("checkout" in m["content"].lower() or "11:00" in m["content"] for m in data["messages"] if m["role"] == "assistant")


def test_history_authorization_rejects_other_customer(client, customer_token):
    conv_id = "conv-tenant-a"
    # Create conversation owned by cust-12345
    conversation_store.save_message(
        conversation_id=conv_id,
        customer_id="cust-12345",
        role="user",
        content="Secret guest message",
        booking_reference="TH-REF-A"
    )

    # Attempt to access with a different customer token
    other_token = generate_jwt(sub="cust-99999", role="Customer")
    resp = client.get(
        f"/api/v1/concierge/history?conversation_id={conv_id}",
        headers={"Authorization": f"Bearer {other_token}"}
    )
    assert resp.status_code == 403
    assert "Access denied" in resp.json()["detail"]


def test_service_requests_status_and_lifecycle(client, customer_token, monkeypatch):
    async def mock_stay(*args, **kwargs):
        return VerifiedStayContext(
            customer_id="cust-12345",
            room_number="302",
            booking_reference="TH-REF-302",
            is_active=True
        )

    published = []
    async def mock_publish(event):
        published.append(event)
        return True

    monkeypatch.setattr(booking_client, "get_active_stay", mock_stay)
    monkeypatch.setattr(rabbitmq_publisher, "publish_service_request", mock_publish)

    conv_id = "conv-request-lifecycle"
    resp = client.post(
        "/api/v1/concierge/chat/stream",
        json={"message": "Please send extra towels to room 302", "conversationId": conv_id},
        headers={"Authorization": f"Bearer {customer_token}"}
    )
    assert resp.status_code == 200

    # Query active requests API
    req_resp = client.get(
        "/api/v1/concierge/requests",
        headers={"Authorization": f"Bearer {customer_token}"}
    )
    assert req_resp.status_code == 200
    requests = req_resp.json()
    assert len(requests) >= 1

    req = requests[0]
    assert req["customerId"] == "cust-12345"
    assert req["roomNumber"] == "302"
    assert req["requestType"] == "Housekeeping"
    assert req["status"] == "REQUEST_PENDING"
    event_id = req["eventId"]

    # Simulate Field Ops task.created event updating request status
    updated = conversation_store.update_request_status(
        event_id=event_id,
        new_status="TASK_CREATED",
        task_id="fieldops-task-9876"
    )
    assert updated is not None
    assert updated.status == "TASK_CREATED"

    # Query again and verify updated status
    req_resp_after = client.get(
        "/api/v1/concierge/requests",
        headers={"Authorization": f"Bearer {customer_token}"}
    )
    req_after = next(r for r in req_resp_after.json() if r["eventId"] == event_id)
    assert req_after["status"] == "TASK_CREATED"
    assert req_after["taskId"] == "fieldops-task-9876"

    # Simulate task completion
    conversation_store.update_request_status(
        event_id=event_id,
        new_status="COMPLETED"
    )
    req_resp_completed = client.get(
        "/api/v1/concierge/requests",
        headers={"Authorization": f"Bearer {customer_token}"}
    )
    req_completed = next(r for r in req_resp_completed.json() if r["eventId"] == event_id)
    assert req_completed["status"] == "COMPLETED"
