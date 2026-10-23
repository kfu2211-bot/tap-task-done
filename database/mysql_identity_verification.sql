-- Run only against a separate local development database, never against production.
CREATE TABLE IF NOT EXISTS identity_requests (
  Id bigint NOT NULL AUTO_INCREMENT PRIMARY KEY,
  UserId int NOT NULL,
  IdLast4 varchar(4) NOT NULL,
  Status varchar(16) NOT NULL,
  CreatedAt datetime(6) NOT NULL,
  ReviewedAt datetime(6) NULL,
  ReviewedById int NULL,
  ReviewNote varchar(500) NULL,
  GovernmentId mediumblob NOT NULL,
  GovernmentType varchar(32) NOT NULL,
  Certification mediumblob NULL,
  CertificationType varchar(32) NULL,
  SupportingDocument mediumblob NULL,
  SupportingType varchar(32) NULL,
  INDEX IX_identity_user (UserId, Id),
  CONSTRAINT FK_identity_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id),
  CONSTRAINT FK_identity_reviewer FOREIGN KEY (ReviewedById) REFERENCES tbl_user(Id)
) ENGINE=InnoDB;
