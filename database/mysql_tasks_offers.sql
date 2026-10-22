-- New tables only: this does not modify browser-local tasks or existing users.
CREATE TABLE IF NOT EXISTS marketplace_tasks (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    ClientId INT NOT NULL,
    Title VARCHAR(120) NOT NULL,
    Description TEXT NOT NULL,
    Category VARCHAR(60) NOT NULL,
    Location VARCHAR(120) NOT NULL,
    Budget DECIMAL(12,2) NOT NULL,
    ScheduleText VARCHAR(120) NOT NULL,
    Status VARCHAR(24) NOT NULL DEFAULT 'pending-confirmation',
    CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_marketplace_task_client FOREIGN KEY (ClientId) REFERENCES tbl_user(Id),
    INDEX ix_marketplace_task_status (Status),
    INDEX ix_marketplace_task_client (ClientId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS marketplace_offers (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    TaskId BIGINT NOT NULL,
    FreelancerId INT NOT NULL,
    Price DECIMAL(12,2) NOT NULL,
    Message VARCHAR(500) NOT NULL DEFAULT '',
    Status VARCHAR(24) NOT NULL DEFAULT 'pending',
    CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_marketplace_offer_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
    CONSTRAINT fk_marketplace_offer_freelancer FOREIGN KEY (FreelancerId) REFERENCES tbl_user(Id),
    UNIQUE KEY uq_marketplace_offer_task_freelancer (TaskId, FreelancerId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
