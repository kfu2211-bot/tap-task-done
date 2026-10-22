-- Apply to the existing taptaskdone database; does not alter existing accounts.
CREATE TABLE IF NOT EXISTS freelancer_profiles (
    UserId INT NOT NULL PRIMARY KEY,
    Service VARCHAR(60) NOT NULL DEFAULT '',
    Location VARCHAR(80) NOT NULL DEFAULT '',
    Rate INT NULL,
    WorkRole VARCHAR(16) NOT NULL DEFAULT 'client',
    Availability VARCHAR(16) NOT NULL DEFAULT 'Available',
    AvailableDays VARCHAR(40) NOT NULL DEFAULT '',
    CONSTRAINT fk_freelancer_profile_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
