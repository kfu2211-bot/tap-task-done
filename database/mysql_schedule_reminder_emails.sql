-- Additive fallback if the application database user cannot CREATE tables.
-- Select the intended schema before running. Never includes sender credentials.
CREATE TABLE IF NOT EXISTS schedule_reminder_emails (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  TaskId BIGINT NOT NULL, UserId INT NOT NULL, AgreementVersion INT NOT NULL,
  Kind VARCHAR(30) NOT NULL, Status VARCHAR(16) NOT NULL,
  Attempts INT NOT NULL DEFAULT 0, NextAttemptUtc DATETIME(6) NOT NULL, CreatedUtc DATETIME(6) NOT NULL,
  UNIQUE KEY UX_schedule_email (TaskId, UserId, AgreementVersion, Kind),
  INDEX IX_schedule_email_due (Status, NextAttemptUtc),
  CONSTRAINT FK_schedule_email_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id) ON DELETE CASCADE,
  CONSTRAINT FK_schedule_email_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
) ENGINE=InnoDB;
