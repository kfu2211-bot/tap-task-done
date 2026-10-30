-- Local MySQL only: connect to localhost before running this file.
USE taptaskdone;

-- Select the intended schema before running. New table only; existing history stays unchanged.
CREATE TABLE IF NOT EXISTS admin_account_offenses (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  UserId INT NOT NULL,
  AdminId INT NOT NULL,
  IncidentKey VARCHAR(100) NOT NULL,
  Reason VARCHAR(500) NOT NULL,
  Number INT NOT NULL,
  RecoveryTarget INT NOT NULL,
  CreatedAtUtc DATETIME(6) NOT NULL,
  SuspendedUntilUtc DATETIME(6) NULL,
  Permanent TINYINT(1) NOT NULL DEFAULT 0,
  LiftedAtUtc DATETIME(6) NULL,
  UNIQUE KEY ux_offense_incident (UserId, IncidentKey),
  KEY ix_offense_user_id (UserId, Id),
  CONSTRAINT fk_offense_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id),
  CONSTRAINT fk_offense_admin FOREIGN KEY (AdminId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
