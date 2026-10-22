-- Run after mysql_tasks_offers.sql and mysql_notifications.sql.
CREATE TABLE IF NOT EXISTS marketplace_bookings (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    TaskId BIGINT NOT NULL,
    FreelancerId INT NOT NULL,
    Status VARCHAR(24) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    CONSTRAINT fk_booking_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
    CONSTRAINT fk_booking_freelancer FOREIGN KEY (FreelancerId) REFERENCES tbl_user(Id),
    UNIQUE KEY uq_booking_task_freelancer (TaskId, FreelancerId),
    INDEX ix_booking_freelancer (FreelancerId, Status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
