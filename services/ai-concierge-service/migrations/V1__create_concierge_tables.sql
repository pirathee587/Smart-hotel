-- Versioned Migration V1: AI Concierge Multi-Replica Schema
-- Target: PostgreSQL / smarthotel_concierge_db

CREATE TABLE IF NOT EXISTS conversations (
    conversation_id VARCHAR(64) PRIMARY KEY,
    customer_id VARCHAR(64) NOT NULL,
    booking_reference VARCHAR(64),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_conversations_customer 
    ON conversations (customer_id, updated_at DESC);

CREATE TABLE IF NOT EXISTS messages (
    id VARCHAR(64) PRIMARY KEY,
    conversation_id VARCHAR(64) NOT NULL REFERENCES conversations(conversation_id) ON DELETE CASCADE,
    role VARCHAR(20) NOT NULL,
    content TEXT NOT NULL,
    event_id VARCHAR(64),
    status VARCHAR(40),
    metadata_json TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_messages_conversation 
    ON messages (conversation_id, created_at ASC);

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
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_service_requests_customer 
    ON service_requests (customer_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_service_requests_event 
    ON service_requests (event_id);

CREATE INDEX IF NOT EXISTS idx_service_requests_task 
    ON service_requests (task_id);
