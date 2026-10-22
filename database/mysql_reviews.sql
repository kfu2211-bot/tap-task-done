-- Run after mysql_tasks_offers.sql on the intended MySQL database.
-- Creates a new table only; no existing task or user data is changed.
CREATE TABLE IF NOT EXISTS marketplace_reviews (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    TaskId BIGINT NOT NULL,
    ClientId INT NOT NULL,
    FreelancerId INT NOT NULL,
    Rating INT NOT NULL,
    Feedback VARCHAR(1000) NOT NULL DEFAULT '',
    CreatedAt DATETIME(6) NOT NULL,
    CONSTRAINT fk_marketplace_review_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
    CONSTRAINT fk_marketplace_review_client FOREIGN KEY (ClientId) REFERENCES tbl_user(Id),
    CONSTRAINT fk_marketplace_review_freelancer FOREIGN KEY (FreelancerId) REFERENCES tbl_user(Id),
    CONSTRAINT ck_marketplace_review_rating CHECK (Rating BETWEEN 1 AND 5),
    UNIQUE KEY uq_marketplace_review_task (TaskId),
    INDEX ix_marketplace_review_freelancer (FreelancerId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
