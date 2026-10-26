-- Select the intended schema before running. Existing task statuses are preserved.
CREATE TABLE IF NOT EXISTS task_progress_events (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  TaskId BIGINT NOT NULL,
  ActorId INT NOT NULL,
  Status VARCHAR(24) NOT NULL,
  Note VARCHAR(1000) NULL,
  Photo MEDIUMBLOB NULL,
  PhotoType VARCHAR(32) NULL,
  CreatedAtUtc DATETIME(6) NOT NULL,
  INDEX IX_progress_task (TaskId, Id),
  CONSTRAINT FK_progress_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
  CONSTRAINT FK_progress_actor FOREIGN KEY (ActorId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB;
