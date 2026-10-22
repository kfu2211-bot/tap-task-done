-- Run once on the taptaskdone database after mysql_tasks_offers.sql.
-- This creates a new table and does not change existing data.
CREATE TABLE IF NOT EXISTS marketplace_messages (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    TaskId BIGINT NOT NULL,
    SenderId INT NOT NULL,
    Body VARCHAR(2000) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    CONSTRAINT fk_marketplace_message_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
    CONSTRAINT fk_marketplace_message_sender FOREIGN KEY (SenderId) REFERENCES tbl_user(Id),
    INDEX ix_marketplace_message_task_id (TaskId, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
