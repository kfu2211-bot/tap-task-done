-- Key records only; keep the certificate and password outside the database.
CREATE TABLE IF NOT EXISTS data_protection_keys (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FriendlyName VARCHAR(255) NOT NULL,
    Xml LONGTEXT NOT NULL
) ENGINE=InnoDB;
