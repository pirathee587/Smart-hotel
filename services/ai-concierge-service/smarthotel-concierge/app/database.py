import logging
import sqlite3
import threading
from typing import Optional, Any, List, Tuple
from app.config import settings

logger = logging.getLogger(__name__)

MIGRATION_SQL = """
CREATE TABLE IF NOT EXISTS conversations (
    conversation_id VARCHAR(64) PRIMARY KEY,
    customer_id VARCHAR(64) NOT NULL,
    booking_reference VARCHAR(64),
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_conversations_customer 
    ON conversations (customer_id);

CREATE TABLE IF NOT EXISTS messages (
    id VARCHAR(64) PRIMARY KEY,
    conversation_id VARCHAR(64) NOT NULL,
    role VARCHAR(20) NOT NULL,
    content TEXT NOT NULL,
    event_id VARCHAR(64),
    status VARCHAR(40),
    metadata_json TEXT,
    created_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_messages_conversation 
    ON messages (conversation_id);

CREATE TABLE IF NOT EXISTS service_requests (
    request_id VARCHAR(64) PRIMARY KEY,
    event_id VARCHAR(64) NOT NULL UNIQUE,
    conversation_id VARCHAR(64),
    customer_id VARCHAR(64) NOT NULL,
    room_number VARCHAR(32) NOT NULL,
    request_type VARCHAR(40) NOT NULL,
    description VARCHAR(500) NOT NULL,
    priority VARCHAR(20) NOT NULL DEFAULT 'Normal',
    status VARCHAR(40) NOT NULL DEFAULT 'REQUEST_PENDING',
    task_id VARCHAR(64),
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_service_requests_customer 
    ON service_requests (customer_id);

CREATE INDEX IF NOT EXISTS idx_service_requests_event 
    ON service_requests (event_id);
"""


class DatabaseManager:
    """
    Multi-replica shared database manager.
    Connects to shared PostgreSQL in container/cluster environments (via pg8000).
    Falls back gracefully to in-memory SQLite during testing or offline runs.
    """

    def __init__(self):
        self._is_postgres = False
        self._pg_conn = None
        self._sqlite_conn: Optional[sqlite3.Connection] = None
        self._lock = threading.Lock()
        self._initialized = False

    def initialize(self):
        with self._lock:
            if self._initialized:
                return

            if settings.ENVIRONMENT != "testing":
                try:
                    import pg8000.dbapi
                    self._pg_conn = pg8000.dbapi.connect(
                        user=settings.POSTGRES_USER,
                        password=settings.POSTGRES_PASSWORD,
                        host=settings.POSTGRES_HOST,
                        port=settings.POSTGRES_PORT,
                        database=settings.POSTGRES_DB
                    )
                    self._is_postgres = True
                    logger.info(f"Connected to multi-replica PostgreSQL at {settings.POSTGRES_HOST}:{settings.POSTGRES_PORT}/{settings.POSTGRES_DB}")
                except Exception as ex:
                    logger.warning(f"PostgreSQL connection unavailable ({ex}). Falling back to isolated memory storage for testing/offline mode.")
                    self._is_postgres = False

            if not self._is_postgres:
                self._sqlite_conn = sqlite3.connect(":memory:", check_same_thread=False)
                self._sqlite_conn.row_factory = sqlite3.Row
                # Run versioned schema migration
                self._sqlite_conn.executescript(MIGRATION_SQL)
                self._sqlite_conn.commit()

            self._initialized = True

    def execute_write(self, query: str, params: Tuple = ()) -> None:
        if not self._initialized:
            self.initialize()

        with self._lock:
            if self._is_postgres and self._pg_conn:
                cursor = self._pg_conn.cursor()
                cursor.execute(query, params)
                self._pg_conn.commit()
                cursor.close()
            elif self._sqlite_conn:
                cursor = self._sqlite_conn.cursor()
                # Adapt %s placeholder to ? for sqlite if needed
                adapted_query = query.replace("%s", "?")
                cursor.execute(adapted_query, params)
                self._sqlite_conn.commit()
                cursor.close()

    def execute_query(self, query: str, params: Tuple = ()) -> List[dict]:
        if not self._initialized:
            self.initialize()

        with self._lock:
            if self._is_postgres and self._pg_conn:
                cursor = self._pg_conn.cursor()
                cursor.execute(query, params)
                columns = [desc[0] for desc in cursor.description] if cursor.description else []
                rows = cursor.fetchall()
                cursor.close()
                return [dict(zip(columns, row)) for row in rows]
            elif self._sqlite_conn:
                cursor = self._sqlite_conn.cursor()
                adapted_query = query.replace("%s", "?")
                cursor.execute(adapted_query, params)
                rows = cursor.fetchall()
                results = [dict(row) for row in rows]
                cursor.close()
                return results
            return []

    def reset_for_testing(self):
        """Allows test fixtures to reset to a clean state."""
        with self._lock:
            self._is_postgres = False
            self._sqlite_conn = sqlite3.connect(":memory:", check_same_thread=False)
            self._sqlite_conn.row_factory = sqlite3.Row
            self._sqlite_conn.executescript(MIGRATION_SQL)
            self._sqlite_conn.commit()
            self._initialized = True

    def close(self):
        with self._lock:
            if self._pg_conn:
                try:
                    self._pg_conn.close()
                except Exception:
                    pass
                self._pg_conn = None
            if self._sqlite_conn:
                try:
                    self._sqlite_conn.close()
                except Exception:
                    pass
                self._sqlite_conn = None
            self._initialized = False


db_manager = DatabaseManager()

