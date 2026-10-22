-- Run after mysql_tasks_offers.sql. Creates only a new table.
CREATE TABLE IF NOT EXISTS marketplace_notifications (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    TaskId BIGINT NOT NULL,
    Kind VARCHAR(30) NOT NULL,
    Message VARCHAR(255) NOT NULL,
    IsRead TINYINT(1) NOT NULL DEFAULT 0,
    CreatedAt DATETIME(6) NOT NULL,
    CONSTRAINT fk_notification_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id),
    CONSTRAINT fk_notification_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
    INDEX ix_notification_user_read_id (UserId, IsRead, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
