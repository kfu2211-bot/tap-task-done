-- Select the intended schema first: USE defaultdb; (live) or USE taptaskdone; (local).
-- Additive update; existing internal notes remain private.
SET @review_sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='safety_report_actions' AND COLUMN_NAME='PublicMessage'), 'SELECT 1', 'ALTER TABLE safety_report_actions ADD COLUMN PublicMessage VARCHAR(1000) NULL');
PREPARE review_stmt FROM @review_sql;
EXECUTE review_stmt;
DEALLOCATE PREPARE review_stmt;
SET @review_sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='safety_report_actions' AND COLUMN_NAME='ReviewConfirmed'), 'SELECT 1', 'ALTER TABLE safety_report_actions ADD COLUMN ReviewConfirmed TINYINT(1) NOT NULL DEFAULT 0');
PREPARE review_stmt FROM @review_sql;
EXECUTE review_stmt;
DEALLOCATE PREPARE review_stmt;
