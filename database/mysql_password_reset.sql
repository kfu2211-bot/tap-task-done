CREATE TABLE IF NOT EXISTS password_reset_requests (
    TokenHash varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    UserId int NOT NULL,
    ExpiresUtc datetime(6) NOT NULL,
    INDEX IX_password_reset_requests_UserId (UserId),
    CONSTRAINT FK_password_reset_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB;
