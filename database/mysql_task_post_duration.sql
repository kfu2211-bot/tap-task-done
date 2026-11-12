-- Select the intended database and back it up before running.
SET @duration_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'marketplace_tasks' AND COLUMN_NAME = 'PostDurationDays'), 'SELECT 1', 'ALTER TABLE marketplace_tasks ADD COLUMN PostDurationDays INT NULL');
PREPARE duration_stmt FROM @duration_sql;
EXECUTE duration_stmt;
DEALLOCATE PREPARE duration_stmt;
SET @expiry_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'marketplace_tasks' AND COLUMN_NAME = 'PostExpiresUtc'), 'SELECT 1', 'ALTER TABLE marketplace_tasks ADD COLUMN PostExpiresUtc DATETIME(6) NULL');
PREPARE expiry_stmt FROM @expiry_sql;
EXECUTE expiry_stmt;
DEALLOCATE PREPARE expiry_stmt;
-- Existing tasks keep NULL and do not expire automatically.
