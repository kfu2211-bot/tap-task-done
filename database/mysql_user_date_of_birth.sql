-- Run once on the intended MySQL database before starting the updated application.
-- Existing users retain NULL because their birthdays were not previously saved.
ALTER TABLE tbl_user ADD COLUMN DateOfBirth DATE NULL;
