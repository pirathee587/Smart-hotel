-- AI suggestions are advisory. The existing priority column and sla_minutes
-- remain manager-controlled effective values.
ALTER TABLE staff_tasks
    ADD COLUMN ai_suggested_priority VARCHAR(20),
    ADD COLUMN ai_suggested_sla_minutes INTEGER,
    ADD COLUMN sla_minutes INTEGER,
    ADD COLUMN ai_suggestion_generated_at TIMESTAMP(6) WITH TIME ZONE;

ALTER TABLE staff_tasks
    ADD CONSTRAINT ck_staff_tasks_ai_suggested_priority
        CHECK (ai_suggested_priority IS NULL OR ai_suggested_priority IN ('Low', 'Medium', 'High', 'Urgent')),
    ADD CONSTRAINT ck_staff_tasks_ai_suggested_sla_positive
        CHECK (ai_suggested_sla_minutes IS NULL OR ai_suggested_sla_minutes > 0),
    ADD CONSTRAINT ck_staff_tasks_sla_positive
        CHECK (sla_minutes IS NULL OR sla_minutes > 0);
