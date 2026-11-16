-- Apply to the existing database before deploying the preferred-hours fields.
SET @start_sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
           AND TABLE_NAME = 'freelancer_profiles' AND COLUMN_NAME = 'PreferredStart'),
    'SELECT ''PreferredStart already exists''',
    'ALTER TABLE freelancer_profiles ADD COLUMN PreferredStart TIME NULL');
PREPARE schedule_stmt FROM @start_sql;
EXECUTE schedule_stmt;
DEALLOCATE PREPARE schedule_stmt;

SET @end_sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
           AND TABLE_NAME = 'freelancer_profiles' AND COLUMN_NAME = 'PreferredEnd'),
    'SELECT ''PreferredEnd already exists''',
    'ALTER TABLE freelancer_profiles ADD COLUMN PreferredEnd TIME NULL');
PREPARE schedule_stmt FROM @end_sql;
EXECUTE schedule_stmt;
DEALLOCATE PREPARE schedule_stmt;
