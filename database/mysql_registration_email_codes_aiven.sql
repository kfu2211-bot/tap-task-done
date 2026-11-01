USE defaultdb;

SELECT userName, COUNT(*) AS duplicate_count FROM tbl_user
 WHERE userName IS NOT NULL GROUP BY userName HAVING COUNT(*) > 1;
SET @registration_duplicate_emails = (SELECT COUNT(*) FROM
 (SELECT userName FROM tbl_user WHERE userName IS NOT NULL GROUP BY userName HAVING COUNT(*) > 1) duplicate_emails);
SELECT @registration_duplicate_emails AS duplicate_email_groups;
SET @registration_unique_email = (SELECT COUNT(*) FROM
 (SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE()
 AND TABLE_NAME='tbl_user' AND NON_UNIQUE=0 GROUP BY INDEX_NAME
 HAVING COUNT(*)=1 AND MAX(COLUMN_NAME)='userName') email_indexes);
SET @registration_index_sql = IF(@registration_unique_email > 0, 'SELECT ''Unique email index already exists'' AS email_index_status',
 IF(@registration_duplicate_emails = 0, 'CREATE UNIQUE INDEX ux_tbl_user_email ON tbl_user (userName)',
 'SELECT ''STOP: resolve duplicate userName values before enabling verification'' AS email_index_status'));
PREPARE registration_email_index FROM @registration_index_sql;
EXECUTE registration_email_index;
DEALLOCATE PREPARE registration_email_index;
CREATE TABLE IF NOT EXISTS registration_email_codes (
 UserId INT NOT NULL PRIMARY KEY,
 TokenHash VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 PasswordVersion VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 Email VARCHAR(45) NOT NULL,
 CodeHash VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 ExpiresUtc DATETIME(6) NOT NULL,
 SessionExpiresUtc DATETIME(6) NOT NULL,
 LastSentUtc DATETIME(6) NULL,
 SendWindowUtc DATETIME(6) NOT NULL,
 SendCount INT NOT NULL DEFAULT 0,
 Attempts INT NOT NULL DEFAULT 0,
 VerifiedUtc DATETIME(6) NULL,
 FirstLoginHash VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
 FirstLoginExpiresUtc DATETIME(6) NULL,
 UNIQUE KEY ux_registration_token (TokenHash),
 CONSTRAINT fk_registration_code_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
SELECT DATABASE() AS selected_database;
SHOW COLUMNS FROM registration_email_codes;
