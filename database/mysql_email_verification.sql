CREATE TABLE IF NOT EXISTS email_verification_requests (
    TokenHash varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    UserId int NOT NULL,
    Email varchar(45) NOT NULL,
    ExpiresUtc datetime(6) NOT NULL,
    INDEX IX_email_verification_UserId (UserId),
    CONSTRAINT FK_email_verification_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS verified_emails (
    UserId int NOT NULL PRIMARY KEY,
    Email varchar(45) NOT NULL,
    VerifiedUtc datetime(6) NOT NULL,
    CONSTRAINT FK_verified_email_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB;
