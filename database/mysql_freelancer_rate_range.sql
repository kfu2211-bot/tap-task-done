-- Select the intended database before running. Back up production first.
SET @rate_max_sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'freelancer_profiles' AND COLUMN_NAME = 'RateMaximum'),
    'SELECT ''RateMaximum already exists''',
    'ALTER TABLE freelancer_profiles ADD COLUMN RateMaximum INT NULL');
PREPARE rate_max_stmt FROM @rate_max_sql;
EXECUTE rate_max_stmt;
DEALLOCATE PREPARE rate_max_stmt;
-- Existing single rates are interpreted as both ends by the application.
