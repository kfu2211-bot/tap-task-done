-- Select the intended database and back it up before running.
SET @priority_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'safety_reports' AND COLUMN_NAME = 'Priority'), 'SELECT 1', 'ALTER TABLE safety_reports ADD COLUMN Priority INT NOT NULL DEFAULT 0');
PREPARE priority_stmt FROM @priority_sql;
EXECUTE priority_stmt;
DEALLOCATE PREPARE priority_stmt;
-- 0 means unassessed; admins assign a priority after reviewing the report.
