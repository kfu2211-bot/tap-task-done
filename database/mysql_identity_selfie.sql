-- Run against the intended database before deploying the selfie update.
-- Adds notification read state only; selfie/consent use existing encrypted identity_resources.
SET @has_review_read = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'identity_requests' AND COLUMN_NAME = 'ReviewReadAt');
SET @identity_sql = IF(@has_review_read = 0, 'ALTER TABLE identity_requests ADD COLUMN ReviewReadAt DATETIME(6) NULL', 'SELECT ''ReviewReadAt already exists''');
PREPARE identity_stmt FROM @identity_sql;
EXECUTE identity_stmt;
DEALLOCATE PREPARE identity_stmt;
