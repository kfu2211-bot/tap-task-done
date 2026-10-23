-- Apply before deploying the resource-table version. Existing documents remain untouched.
CREATE TABLE IF NOT EXISTS identity_resources (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  IdentityRequestId BIGINT NOT NULL,
  Kind VARCHAR(16) NOT NULL,
  ContentType VARCHAR(32) NOT NULL,
  EncryptedData MEDIUMBLOB NOT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  UNIQUE KEY IX_identity_resource_request_kind (IdentityRequestId, Kind),
  CONSTRAINT FK_identity_resource_request FOREIGN KEY (IdentityRequestId) REFERENCES identity_requests(Id)
) ENGINE=InnoDB;

-- New requests store documents in identity_resources; old BLOBs are retained for compatibility.
ALTER TABLE identity_requests MODIFY GovernmentId MEDIUMBLOB NULL, MODIFY GovernmentType VARCHAR(32) NULL;
