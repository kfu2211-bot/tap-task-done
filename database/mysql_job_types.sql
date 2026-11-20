-- Live database. Replace defaultdb with taptaskdone for local installations.
USE defaultdb;
-- Adds labels and scheduling boundaries without relabelling existing jobs.
SET @job_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='marketplace_tasks' AND COLUMN_NAME='JobType'), 'SELECT 1', 'ALTER TABLE marketplace_tasks ADD COLUMN JobType VARCHAR(16) NULL');
PREPARE job_stmt FROM @job_sql; EXECUTE job_stmt; DEALLOCATE PREPARE job_stmt;
SET @job_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='marketplace_tasks' AND COLUMN_NAME='WorkEndUtc'), 'SELECT 1', 'ALTER TABLE marketplace_tasks ADD COLUMN WorkEndUtc DATETIME(6) NULL');
PREPARE job_stmt FROM @job_sql; EXECUTE job_stmt; DEALLOCATE PREPARE job_stmt;
SET @job_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='work_agreements' AND COLUMN_NAME='JobType'), 'SELECT 1', 'ALTER TABLE work_agreements ADD COLUMN JobType VARCHAR(16) NULL');
PREPARE job_stmt FROM @job_sql; EXECUTE job_stmt; DEALLOCATE PREPARE job_stmt;
SET @job_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='work_agreements' AND COLUMN_NAME='WorkEndUtc'), 'SELECT 1', 'ALTER TABLE work_agreements ADD COLUMN WorkEndUtc DATETIME(6) NULL');
PREPARE job_stmt FROM @job_sql; EXECUTE job_stmt; DEALLOCATE PREPARE job_stmt;
