-- Apply before deploying specific services. Existing category selections stay intact.
SET @specialty_sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
           AND TABLE_NAME = 'freelancer_profiles' AND COLUMN_NAME = 'ServiceSpecialties'),
    'SELECT ''ServiceSpecialties already exists''',
    'ALTER TABLE freelancer_profiles ADD COLUMN ServiceSpecialties TEXT NULL');
PREPARE specialty_stmt FROM @specialty_sql;
EXECUTE specialty_stmt;
DEALLOCATE PREPARE specialty_stmt;
