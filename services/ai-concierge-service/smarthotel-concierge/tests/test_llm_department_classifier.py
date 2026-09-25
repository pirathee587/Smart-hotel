import httpx
import asyncio
from app.config import settings
from app.services.llm_provider import classify_task_intent, parse_task_classification


def test_llm_department_classification_accepts_valid_operational_task():
    result = parse_task_classification(
        {
            "is_task": True,
            "department": "Maintenance",
            "priority": "Urgent",
            "description": "Water is flooding from the bathroom pipe",
        },
        "There is water everywhere",
    )
    assert result is not None
    assert result.department == "Maintenance"
    assert result.description == "Water is flooding from the bathroom pipe"
    assert result.priority == "Urgent"
    assert result.sla_minutes == 5
    assert result.requires_manager_attention is True


def test_llm_department_classification_rejects_unknown_department():
    assert parse_task_classification(
        {"is_task": True, "department": "Accounts", "priority": "Normal"},
        "Change my bill",
    ) is None


def test_llm_department_classification_keeps_questions_informational():
    assert parse_task_classification(
        {"is_task": False, "department": "Informational", "priority": "Low"},
        "When does the pool open?",
    ) is None


def test_invalid_priority_uses_server_controlled_normal_sla():
    result = parse_task_classification(
        {"is_task": True, "department": "Housekeeping", "priority": "SUPER-CRITICAL", "description": "Bring towels"},
        "Bring towels",
    )
    assert result is not None
    assert result.priority == "Normal"
    assert result.sla_minutes == 30


def test_deepseek_timeout_uses_deterministic_fallback(monkeypatch):
    class TimeoutClient:
        async def __aenter__(self): return self
        async def __aexit__(self, *args): return None
        async def post(self, *args, **kwargs): raise httpx.TimeoutException("synthetic timeout")

    monkeypatch.setattr(settings, "LLM_PROVIDER", "deepseek")
    monkeypatch.setattr(settings, "DEEPSEEK_API_KEY", "synthetic-test-key")
    monkeypatch.setattr(httpx, "AsyncClient", lambda **kwargs: TimeoutClient())
    result = asyncio.run(classify_task_intent("Please send two towels"))
    assert result is not None
    assert result.department == "Housekeeping"


def test_malformed_deepseek_json_fails_safe_when_fallback_has_no_exact_match(monkeypatch):
    class BadResponse:
        def raise_for_status(self): return None
        def json(self): return {"choices": [{"message": {"content": "not-json"}}]}

    class BadJsonClient:
        async def __aenter__(self): return self
        async def __aexit__(self, *args): return None
        async def post(self, *args, **kwargs): return BadResponse()

    monkeypatch.setattr(settings, "LLM_PROVIDER", "deepseek")
    monkeypatch.setattr(settings, "DEEPSEEK_API_KEY", "synthetic-test-key")
    monkeypatch.setattr(httpx, "AsyncClient", lambda **kwargs: BadJsonClient())
    result = asyncio.run(classify_task_intent("The AC is not working"))
    # No task is dispatched when neither the malformed model output nor the
    # conservative fallback can classify the wording with confidence.
    assert result is None
