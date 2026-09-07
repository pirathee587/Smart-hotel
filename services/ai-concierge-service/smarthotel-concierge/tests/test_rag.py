import pytest
from app.rag.vector_store import knowledge_store
from app.rag.knowledge_base import HOTEL_DOCUMENTS


def test_rag_seeding_and_retrieval():
    knowledge_store.reset()

    # Query pool
    pool_results = knowledge_store.query("Where is the swimming pool located and what are the hours?", k=2)
    assert len(pool_results) <= 2
    assert any("Infinity Swimming Pool" in doc or "Floor 4" in doc for doc in pool_results)

    # Query dining / Blue Harbor
    dining_results = knowledge_store.query("What are the breakfast buffet hours at Blue Harbor restaurant?", k=2)
    assert any("Blue Harbor Restaurant" in doc or "Breakfast Buffet" in doc for doc in dining_results)

    # Query spa
    spa_results = knowledge_store.query("Can I get a massage or herbal treatment at the spa?", k=2)
    assert any("Lotus Ayurveda Spa" in doc for doc in spa_results)

    # Query wifi
    wifi_results = knowledge_store.query("How do I connect to hotel WiFi?", k=2)
    assert any("SmartHotel-Guest" in doc for doc in wifi_results)


def test_rag_respects_top_k_parameter():
    knowledge_store.reset()

    res_1 = knowledge_store.query("hotel policy", k=1)
    assert len(res_1) == 1

    res_3 = knowledge_store.query("hotel amenities", k=3)
    assert len(res_3) == 3
