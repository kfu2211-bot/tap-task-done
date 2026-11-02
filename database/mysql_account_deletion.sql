-- Select the intended schema first: USE defaultdb; on live, USE taptaskdone; locally.
-- Additive schema only. Running this script does NOT delete any account or approve a request.
CREATE TABLE IF NOT EXISTS account_deletion_requests (
  UserId INT NOT NULL PRIMARY KEY,
  Status VARCHAR(16) NOT NULL DEFAULT 'verifying',
  RequestedUtc DATETIME(6) NULL,
  EligibleUtc DATETIME(6) NULL,
  UpdatedUtc DATETIME(6) NOT NULL,
  ReviewedById INT NULL,
  ReviewReason VARCHAR(1000) NULL,
  DeletedUtc DATETIME(6) NULL,
  CodeHash VARCHAR(64) NOT NULL DEFAULT '',
  CodeSalt VARCHAR(64) NOT NULL DEFAULT '',
  PasswordVersion VARCHAR(64) NOT NULL DEFAULT '',
  CodeExpiresUtc DATETIME(6) NULL,
  LastCodeSentUtc DATETIME(6) NULL,
  SendWindowUtc DATETIME(6) NOT NULL,
  SendCount INT NOT NULL DEFAULT 0,
  Attempts INT NOT NULL DEFAULT 0,
  INDEX ix_deletion_due (Status, EligibleUtc),
  CONSTRAINT fk_deletion_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id),
  CONSTRAINT fk_deletion_admin FOREIGN KEY (ReviewedById) REFERENCES tbl_user(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS account_deletion_events (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  UserId INT NOT NULL,
  ActorId INT NULL,
  Action VARCHAR(20) NOT NULL,
  Reason VARCHAR(1000) NOT NULL,
  CreatedUtc DATETIME(6) NOT NULL,
  INDEX ix_deletion_history (UserId, Id),
  CONSTRAINT fk_deletion_event_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id),
  CONSTRAINT fk_deletion_event_actor FOREIGN KEY (ActorId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('account_deletion_requests','account_deletion_events');
