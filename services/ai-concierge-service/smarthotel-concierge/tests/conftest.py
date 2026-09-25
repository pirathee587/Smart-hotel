import pytest
import time
from typing import Dict, Any
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.primitives import serialization
import jwt
from fastapi.testclient import TestClient

from app.main import app
from app.config import settings
from app.auth.jwks import jwks_manager
from app.database import db_manager
from app.rag.vector_store import knowledge_store
from app.services.rate_limiter import customer_rate_limiter
from app.clients.rabbitmq_client import rabbitmq_publisher

# Force testing environment so EphemeralClient is used
settings.ENVIRONMENT = "testing"

# Generate persistent RSA key pair for testing
_private_key = rsa.generate_private_key(
    public_exponent=65537,
    key_size=2048,
)
_public_key = _private_key.public_key()
TEST_KEY_ID = "test-concierge-key-1"


def generate_jwt(
    sub: str = "00000000-0000-0000-0000-000000000001",
    role: str = "Customer",
    expired: bool = False,
    issuer: str = settings.JWT_ISSUER,
    audience: str = settings.JWT_AUDIENCE
) -> str:
    now = int(time.time())
    payload = {
        "sub": sub,
        "role": role,
        "iss": issuer,
        "aud": audience,
        "iat": now - 100 if not expired else now - 3600,
        "exp": now + 3600 if not expired else now - 10,
    }
    headers = {"kid": TEST_KEY_ID}
    return jwt.encode(payload, _private_key, algorithm="RS256", headers=headers)


@pytest.fixture(autouse=True)
def setup_test_environment():
    # 1. Register test public key with JWKS manager
    jwks_manager.set_key_for_testing(TEST_KEY_ID, _public_key)

    # 2. Reset in-memory rate limiter
    customer_rate_limiter.reset()

    # 3. Disable real RabbitMQ connection during unit tests
    rabbitmq_publisher._disabled = True

    # 4. Initialize knowledge store
    knowledge_store.initialize()

    # 5. Reset database manager for test isolation
    db_manager.reset_for_testing()



@pytest.fixture
def client():
    return TestClient(app)


@pytest.fixture
def customer_token():
    return generate_jwt(sub="cust-12345", role="Customer")


@pytest.fixture
def active_stay_payload() -> Dict[str, Any]:
    return {
        "bookingId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
        "bookingReference": "TH-2026-ACTIVE",
        "customerId": "cust-12345",
        "roomId": "11111111-1111-1111-1111-111111111111",
        "roomNumber": "204",
        "roomTypeId": "22222222-2222-2222-2222-222222222222",
        "checkInDate": "2026-09-06",
        "checkOutDate": "2026-09-10",
        "guestCount": 2,
        "status": "CheckedIn",
        "isActive": True
    }
