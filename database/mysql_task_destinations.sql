CREATE TABLE IF NOT EXISTS task_destinations (
  TaskId BIGINT NOT NULL PRIMARY KEY,
  Latitude DOUBLE NOT NULL,
  Longitude DOUBLE NOT NULL,
  UpdatedAtUtc DATETIME(6) NOT NULL,
  CONSTRAINT FK_destination_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id)
) ENGINE=InnoDB;
