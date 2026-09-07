import pytest
from tests.conftest import generate_jwt


def test_chat_stream_missing_auth_header_returns_401(client):
    response = client.post(
        "/api/v1/concierge/chat/stream",
        json={"message": "What time is checkout?"}
    )
    assert response.status_code == 401
    assert "Missing Bearer token" in response.json().get("detail", "")


def test_chat_stream_expired_jwt_returns_401(client):
    token = generate_jwt(sub="cust-001", expired=True)
    response = client.post(
        "/api/v1/concierge/chat/stream",
        json={"message": "What time is checkout?"},
        headers={"Authorization": f"Bearer {token}"}
    )
    assert response.status_code == 401
    assert "expired" in response.json().get("detail", "").lower()


def test_chat_stream_invalid_signature_returns_401(client):
    token = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.invalid_signature"
    response = client.post(
        "/api/v1/concierge/chat/stream",
        json={"message": "Hello"},
        headers={"Authorization": f"Bearer {token}"}
    )
    assert response.status_code == 401


def test_chat_stream_valid_jwt_passes_auth(client, customer_token, monkeypatch):
    from app.clients.booking_client import booking_client
    from app.models.schemas import VerifiedStayContext

    # Mock booking client so network call is avoided
    async def mock_active_stay(*args, **kwargs):
        return VerifiedStayContext(customer_id="cust-12345", room_number="204", is_active=True)

    monkeypatch.setattr(booking_client, "get_active_stay", mock_active_stay)

    response = client.post(
        "/api/v1/concierge/chat/stream",
        json={"message": "What time is breakfast?"},
        headers={"Authorization": f"Bearer {customer_token}"}
    )
    assert response.status_code == 200
    assert "text/event-stream" in response.headers.get("content-type", "")
