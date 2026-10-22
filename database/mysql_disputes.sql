CREATE TABLE IF NOT EXISTS marketplace_disputes (
 Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY, TaskId BIGINT NOT NULL,
 OpenedById INT NOT NULL, Reason VARCHAR(1000) NOT NULL,
 PreviousTaskStatus VARCHAR(24) NOT NULL, Status VARCHAR(24) NOT NULL,
 CreatedAt DATETIME(6) NOT NULL, ClosedById INT NULL,
 Resolution VARCHAR(1000) NULL, ClosedAt DATETIME(6) NULL,
 CONSTRAINT fk_dispute_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
 CONSTRAINT fk_dispute_opener FOREIGN KEY (OpenedById) REFERENCES tbl_user(Id),
 CONSTRAINT fk_dispute_closer FOREIGN KEY (ClosedById) REFERENCES tbl_user(Id),
 INDEX ix_dispute_task (TaskId, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
