-- Run once on the target database after mysql_tasks_offers.sql.
-- This adds a nullable column and does not remove existing data.
ALTER TABLE marketplace_tasks ADD COLUMN CancellationReason VARCHAR(500) NULL;
