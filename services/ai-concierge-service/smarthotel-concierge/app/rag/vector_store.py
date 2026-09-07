import math
import re
import logging
from typing import List, Dict, Optional, Any
import chromadb
from chromadb.api.types import EmbeddingFunction, Documents, Embeddings
from app.config import settings
from app.rag.knowledge_base import HOTEL_DOCUMENTS

import zlib

logger = logging.getLogger(__name__)

STOPWORDS = {
    "what", "are", "the", "at", "is", "and", "to", "in", "of", "for",
    "a", "an", "or", "can", "i", "do", "how", "where", "get", "with"
}


class TermHashingEmbeddingFunction(EmbeddingFunction):
    """
    Lightweight, fast, deterministic embedding function for ChromaDB.
    Maps words and character n-grams into a normalized 256-dimensional vector space using crc32.
    Eliminates external model downloads, runs in sub-millisecond time, and is 100% offline-safe and seed-stable.
    """
    def __init__(self, dim: int = 256):
        self.dim = dim

    def name(self) -> str:
        return "term_hashing_embedding"

    def get_config(self) -> Dict[str, Any]:
        return {"dim": self.dim}

    def __call__(self, input: Documents) -> Embeddings:
        embeddings: List[List[float]] = []
        for text in input:
            vec = [0.0] * self.dim
            tokens = re.findall(r'\b[a-zA-Z0-9_-]+\b', text.lower())
            if not tokens:
                embeddings.append([0.0] * self.dim)
                continue

            for token in tokens:
                w = 0.1 if token in STOPWORDS else 1.0
                # Deterministic unigram hash
                h1 = zlib.crc32(token.encode('utf-8')) % self.dim
                vec[h1] += w
                # Deterministic prefix/bigram hash if length > 3
                if len(token) > 3:
                    h2 = zlib.crc32(token[:4].encode('utf-8')) % self.dim
                    vec[h2] += 0.5 * w

            # L2 normalize
            norm = math.sqrt(sum(v * v for v in vec))
            if norm > 0:
                vec = [v / norm for v in vec]
            embeddings.append(vec)
        return embeddings


class ChromaKnowledgeStore:
    def __init__(self, in_memory: bool = False):
        self.in_memory = in_memory
        self.embedding_fn = TermHashingEmbeddingFunction(dim=256)
        self._client: Optional[chromadb.ClientAPI] = None
        self._collection = None

    def initialize(self):
        if self._collection is not None:
            return

        if self.in_memory or settings.ENVIRONMENT == "testing":
            self._client = chromadb.EphemeralClient()
        else:
            self._client = chromadb.PersistentClient(path=settings.CHROMA_PERSIST_DIR)

        self._collection = self._client.get_or_create_collection(
            name="smarthotel_knowledge",
            embedding_function=self.embedding_fn,
            metadata={"description": "SmartHotel guest policies, dining, and amenities"}
        )

        # Seed knowledge base if empty
        count = self._collection.count()
        if count == 0:
            self.seed_knowledge(HOTEL_DOCUMENTS)

    def seed_knowledge(self, documents: List[Dict[str, str]]):
        if not self._collection:
            self.initialize()

        ids = [doc["id"] for doc in documents]
        texts = [f"{doc['title']}\n{doc['content']}" for doc in documents]
        metadatas = [{"category": doc.get("category", "general"), "title": doc.get("title", "")} for doc in documents]

        self._collection.upsert(
            ids=ids,
            documents=texts,
            metadatas=metadatas
        )
        logger.info(f"Seeded {len(documents)} hotel knowledge documents into ChromaDB")

    def query(self, query_text: str, k: int = 3) -> List[str]:
        if not self._collection:
            self.initialize()

        count = self._collection.count()
        if count == 0:
            return []

        n_results = min(k, count)
        results = self._collection.query(
            query_texts=[query_text],
            n_results=n_results
        )

        documents = results.get("documents", [[]])
        return documents[0] if documents else []

    def reset(self):
        """Used by test fixtures to re-initialize an ephemeral state."""
        self._client = chromadb.EphemeralClient()
        self._collection = self._client.get_or_create_collection(
            name="smarthotel_knowledge",
            embedding_function=self.embedding_fn
        )
        self.seed_knowledge(HOTEL_DOCUMENTS)


knowledge_store = ChromaKnowledgeStore()
