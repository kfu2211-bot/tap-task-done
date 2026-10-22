-- Run after mysql_tasks_offers.sql. No existing data is changed.
CREATE TABLE IF NOT EXISTS safety_reports (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    ReporterId INT NOT NULL, TaskId BIGINT NULL,
    Target VARCHAR(80) NOT NULL, Reason VARCHAR(80) NOT NULL,
    Description VARCHAR(1000) NOT NULL, Status VARCHAR(24) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    CONSTRAINT fk_safety_report_user FOREIGN KEY (ReporterId) REFERENCES tbl_user(Id),
    CONSTRAINT fk_safety_report_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
    INDEX ix_safety_report_reporter_id (ReporterId, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS safety_report_actions (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    ReportId BIGINT NOT NULL, AdminId INT NOT NULL,
    Status VARCHAR(24) NOT NULL, Note VARCHAR(1000) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    CONSTRAINT fk_safety_action_report FOREIGN KEY (ReportId) REFERENCES safety_reports(Id),
    CONSTRAINT fk_safety_action_admin FOREIGN KEY (AdminId) REFERENCES tbl_user(Id),
    INDEX ix_safety_action_report (ReportId, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
