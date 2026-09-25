-- V2 Migration: Add Concierge Event Idempotency and Transactional Task Outbox
-- Database: smarthotel_fieldops

-- 1. Idempotency column for Housekeeping Tasks
ALTER TABLE housekeeping_tasks ADD COLUMN IF NOT EXISTS concierge_event_id UUID;
CREATE UNIQUE INDEX IF NOT EXISTS uq_housekeeping_concierge_event 
    ON housekeeping_tasks (concierge_event_id) 
    WHERE concierge_event_id IS NOT NULL;

-- 2. Transactional Task Event Outbox Table
CREATE TABLE IF NOT EXISTS task_outbox (
    id UUID PRIMARY KEY,
    aggregate_id UUID NOT NULL,
    event_type VARCHAR(64) NOT NULL,
    payload_json TEXT NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'Pending',
    retry_count INT NOT NULL DEFAULT 0,
    next_retry_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    last_error TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    confirmed_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX IF NOT EXISTS idx_task_outbox_status_retry 
    ON task_outbox (status, next_retry_at);

CREATE INDEX IF NOT EXISTS idx_task_outbox_aggregate 
    ON task_outbox (aggregate_id);
