import pytest
from app.services.rate_limiter import customer_rate_limiter
from tests.conftest import generate_jwt


def test_rate_limiter_unit_allows_20_and_blocks_21st():
    customer_id = "test-rate-customer-1"
    customer_rate_limiter.reset(customer_id)

    # First 20 requests succeed
    for i in range(20):
        allowed, retry_after = customer_rate_limiter.check_rate_limit(customer_id)
        assert allowed is True, f"Request {i+1} should be allowed"
        assert retry_after == 0

    # 21st request fails
    allowed, retry_after = customer_rate_limiter.check_rate_limit(customer_id)
    assert allowed is False
    assert retry_after > 0


def test_rate_limiter_partitions_by_customer():
    cust_a = "customer-alpha"
    cust_b = "customer-beta"
    customer_rate_limiter.reset(cust_a)
    customer_rate_limiter.reset(cust_b)

    # Exhaust Customer A's limit
    for _ in range(20):
        customer_rate_limiter.check_rate_limit(cust_a)

    allowed_a, _ = customer_rate_limiter.check_rate_limit(cust_a)
    assert allowed_a is False

    # Customer B should still have full quota
    allowed_b, retry_after = customer_rate_limiter.check_rate_limit(cust_b)
    assert allowed_b is True
    assert retry_after == 0


def test_rate_limiter_http_endpoint_returns_429(client, monkeypatch):
    from app.clients.booking_client import booking_client
    from app.models.schemas import VerifiedStayContext

    async def mock_active_stay(*args, **kwargs):
        return VerifiedStayContext(customer_id="cust-spam", room_number="101", is_active=True)

    monkeypatch.setattr(booking_client, "get_active_stay", mock_active_stay)

    token = generate_jwt(sub="cust-spam")
    headers = {"Authorization": f"Bearer {token}"}

    # First 20 requests succeed
    for _ in range(20):
        res = client.post(
            "/api/v1/concierge/chat/stream",
            json={"message": "What time is pool open?"},
            headers=headers
        )
        assert res.status_code == 200

    # 21st request returns 429
    res = client.post(
        "/api/v1/concierge/chat/stream",
        json={"message": "What time is pool open?"},
        headers=headers
    )
    assert res.status_code == 429
    assert "Rate limit exceeded" in res.json().get("detail", "")
    assert "Retry-After" in res.headers
