-- Existing tasks keep their category and have no specific service until one is saved.
SET @task_service_sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
           AND TABLE_NAME = 'marketplace_tasks' AND COLUMN_NAME = 'SpecificService'),
    'SELECT ''SpecificService already exists''',
    'ALTER TABLE marketplace_tasks ADD COLUMN SpecificService VARCHAR(60) NULL');
PREPARE task_service_stmt FROM @task_service_sql;
EXECUTE task_service_stmt;
DEALLOCATE PREPARE task_service_stmt;
