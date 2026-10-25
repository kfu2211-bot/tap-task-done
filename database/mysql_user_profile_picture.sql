-- Run on the intended database BEFORE deploying code that maps this column.
-- Existing accounts keep NULL; existing profile/account data is preserved.
SET @profile_picture_sql = IF(
  EXISTS(SELECT 1 FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tbl_user' AND COLUMN_NAME = 'ProfilePicture'),
  'SELECT 1',
  'ALTER TABLE tbl_user ADD COLUMN ProfilePicture MEDIUMBLOB NULL'
);
PREPARE profile_picture_statement FROM @profile_picture_sql;
EXECUTE profile_picture_statement;
DEALLOCATE PREPARE profile_picture_statement;
