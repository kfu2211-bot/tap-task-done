CREATE TABLE IF NOT EXISTS role_agreement_acceptances (
    UserId INT NOT NULL,
    Role VARCHAR(16) NOT NULL,
    Version INT NOT NULL,
    AcceptedAtUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (UserId, Role, Version),
    CONSTRAINT fk_role_agreement_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
