CREATE TABLE IF NOT EXISTS mobile_location_updates (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  UserId INT NOT NULL,
  TaskId BIGINT NOT NULL,
  Latitude DOUBLE NOT NULL,
  Longitude DOUBLE NOT NULL,
  AccuracyMeters DOUBLE NULL,
  RecordedAtUtc DATETIME(6) NOT NULL,
  INDEX IX_mobile_location_user_time (UserId, RecordedAtUtc),
  INDEX IX_mobile_location_task_time (TaskId, RecordedAtUtc),
  CONSTRAINT FK_mobile_location_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
  CONSTRAINT FK_mobile_location_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB;
