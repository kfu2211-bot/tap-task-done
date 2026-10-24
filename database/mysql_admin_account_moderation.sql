-- Apply to the intended database before deploying production account moderation.
-- Back up the target database first. Required by login and marketplace enforcement.
-- This script creates new tables; it does not delete or alter existing account records.
CREATE TABLE IF NOT EXISTS admin_account_restrictions (
    UserId INT NOT NULL PRIMARY KEY,
    IsRestricted TINYINT(1) NOT NULL DEFAULT 0,
    UpdatedAt DATETIME(6) NOT NULL,
    UpdatedById INT NOT NULL,
    CONSTRAINT fk_account_restriction_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id),
    CONSTRAINT fk_account_restriction_admin FOREIGN KEY (UpdatedById) REFERENCES tbl_user(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS admin_account_actions (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    AdminId INT NOT NULL,
    Action VARCHAR(16) NOT NULL,
    Reason VARCHAR(500) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    ReadAt DATETIME(6) NULL,
    CONSTRAINT fk_account_action_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id),
    CONSTRAINT fk_account_action_admin FOREIGN KEY (AdminId) REFERENCES tbl_user(Id),
    INDEX ix_account_action_user_id (UserId, Id),
    INDEX ix_account_action_admin_id (AdminId, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
