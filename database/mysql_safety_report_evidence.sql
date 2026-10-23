-- Run after mysql_safety_reports.sql on the selected database.
CREATE TABLE IF NOT EXISTS safety_report_evidence (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    ReportId BIGINT NOT NULL,
    ContentType VARCHAR(32) NOT NULL,
    Data MEDIUMBLOB NOT NULL,
    INDEX ix_safety_evidence_report (ReportId),
    CONSTRAINT fk_safety_evidence_report FOREIGN KEY (ReportId) REFERENCES safety_reports(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
