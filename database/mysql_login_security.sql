-- Select your intended schema first: USE defaultdb; for Aiven or USE taptaskdone; locally.
-- Additive migration. Does not change existing users, passwords, messages or tasks.
CREATE TABLE IF NOT EXISTS login_challenges (
 Id VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
 UserId INT NOT NULL, CodeHash VARCHAR(64) NOT NULL, PasswordVersion VARCHAR(64) NOT NULL,
 CreatedUtc DATETIME(6) NOT NULL, ExpiresUtc DATETIME(6) NOT NULL,
 Attempts INT NOT NULL DEFAULT 0, Consumed TINYINT(1) NOT NULL DEFAULT 0,
 INDEX ix_login_challenge_user (UserId, CreatedUtc),
 CONSTRAINT fk_login_challenge_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS login_sessions (
 Id VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
 UserId INT NOT NULL, PasswordVersion VARCHAR(64) NOT NULL, Device VARCHAR(100) NOT NULL,
 CreatedUtc DATETIME(6) NOT NULL, LastSeenUtc DATETIME(6) NOT NULL,
 ExpiresUtc DATETIME(6) NOT NULL, RevokedUtc DATETIME(6) NULL,
 INDEX ix_login_session_user (UserId, ExpiresUtc),
 CONSTRAINT fk_login_session_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS trusted_login_devices (
 Id VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
 UserId INT NOT NULL, PasswordVersion VARCHAR(64) NOT NULL, Device VARCHAR(100) NOT NULL,
 ExpiresUtc DATETIME(6) NOT NULL,
 INDEX ix_trusted_login_user (UserId, ExpiresUtc),
 CONSTRAINT fk_trusted_login_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS login_alerts (
 Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY, UserId INT NOT NULL,
 SessionId VARCHAR(64) NOT NULL, Device VARCHAR(100) NOT NULL,
 CreatedUtc DATETIME(6) NOT NULL, ReadUtc DATETIME(6) NULL,
 EmailSent TINYINT(1) NOT NULL DEFAULT 0,
 INDEX ix_login_alert_user (UserId, CreatedUtc),
 CONSTRAINT fk_login_alert_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
SELECT DATABASE() AS selected_database;
SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()
 AND TABLE_NAME IN ('login_challenges','login_sessions','trusted_login_devices','login_alerts');
