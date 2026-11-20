-- Live database. For a local installation, replace defaultdb with taptaskdone.
USE defaultdb;
-- Run against the application database before deploying this batch.
SET @duration_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='marketplace_tasks' AND COLUMN_NAME='EstimatedMinutes'),
 'SELECT 1', 'ALTER TABLE marketplace_tasks ADD COLUMN EstimatedMinutes INT NULL');
PREPARE batch_stmt FROM @duration_sql;
EXECUTE batch_stmt;
DEALLOCATE PREPARE batch_stmt;
SET @duration_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='work_agreements' AND COLUMN_NAME='EstimatedMinutes'),
 'SELECT 1', 'ALTER TABLE work_agreements ADD COLUMN EstimatedMinutes INT NULL');
PREPARE batch_stmt FROM @duration_sql;
EXECUTE batch_stmt;
DEALLOCATE PREPARE batch_stmt;
