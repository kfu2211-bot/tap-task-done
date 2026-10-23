CREATE TABLE IF NOT EXISTS password_reset_codes (
  ChallengeHash VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
  UserId INT NOT NULL,
  CodeHash VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  ExpiresUtc DATETIME(6) NOT NULL,
  Attempts INT NOT NULL DEFAULT 0,
  Verified BOOLEAN NOT NULL DEFAULT FALSE,
  INDEX IX_password_reset_codes_UserId (UserId),
  CONSTRAINT FK_password_reset_codes_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS password_reset_code_limits (
  UserId INT NOT NULL PRIMARY KEY,
  WindowStartedUtc DATETIME(6) NOT NULL,
  Attempts INT NOT NULL DEFAULT 0,
  CONSTRAINT FK_password_reset_limits_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB;
