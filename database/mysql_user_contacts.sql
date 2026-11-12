-- Select the intended database before running; back up production first.
CREATE TABLE IF NOT EXISTS user_contacts (
    UserId INT NOT NULL PRIMARY KEY,
    PhoneNumber VARCHAR(25) NOT NULL DEFAULT '',
    ShareWithAssignedUser TINYINT(1) NOT NULL DEFAULT 0,
    CONSTRAINT fk_user_contacts_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
