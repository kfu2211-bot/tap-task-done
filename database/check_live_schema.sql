-- READ ONLY: does not create, alter, delete, or copy any records.
-- Run on the intended database. Checks names only, not types, indexes, foreign keys or existing data.
USE defaultdb;

SELECT DATABASE() AS selected_database;

SELECT required.table_name, required.column_name,
       CASE WHEN t.TABLE_NAME IS NULL THEN 'MISSING TABLE' ELSE 'MISSING COLUMN' END AS finding
FROM (
SELECT 'admin_account_actions' AS table_name, 'Action' AS column_name
UNION ALL SELECT 'admin_account_actions' AS table_name, 'AdminId' AS column_name
UNION ALL SELECT 'admin_account_actions' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'admin_account_actions' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'admin_account_actions' AS table_name, 'ReadAt' AS column_name
UNION ALL SELECT 'admin_account_actions' AS table_name, 'Reason' AS column_name
UNION ALL SELECT 'admin_account_actions' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'admin_account_restrictions' AS table_name, 'IsRestricted' AS column_name
UNION ALL SELECT 'admin_account_restrictions' AS table_name, 'UpdatedAt' AS column_name
UNION ALL SELECT 'admin_account_restrictions' AS table_name, 'UpdatedById' AS column_name
UNION ALL SELECT 'admin_account_restrictions' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'data_protection_keys' AS table_name, 'FriendlyName' AS column_name
UNION ALL SELECT 'data_protection_keys' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'data_protection_keys' AS table_name, 'Xml' AS column_name
UNION ALL SELECT 'email_verification_requests' AS table_name, 'Email' AS column_name
UNION ALL SELECT 'email_verification_requests' AS table_name, 'ExpiresUtc' AS column_name
UNION ALL SELECT 'email_verification_requests' AS table_name, 'TokenHash' AS column_name
UNION ALL SELECT 'email_verification_requests' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'freelancer_profiles' AS table_name, 'Availability' AS column_name
UNION ALL SELECT 'freelancer_profiles' AS table_name, 'AvailableDays' AS column_name
UNION ALL SELECT 'freelancer_profiles' AS table_name, 'Location' AS column_name
UNION ALL SELECT 'freelancer_profiles' AS table_name, 'Rate' AS column_name
UNION ALL SELECT 'freelancer_profiles' AS table_name, 'Service' AS column_name
UNION ALL SELECT 'freelancer_profiles' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'freelancer_profiles' AS table_name, 'WorkRole' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'Certification' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'CertificationType' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'GovernmentId' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'GovernmentType' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'IdLast4' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'ReviewedAt' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'ReviewedById' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'ReviewNote' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'Status' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'SupportingDocument' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'SupportingType' AS column_name
UNION ALL SELECT 'identity_requests' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'identity_resources' AS table_name, 'ContentType' AS column_name
UNION ALL SELECT 'identity_resources' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'identity_resources' AS table_name, 'EncryptedData' AS column_name
UNION ALL SELECT 'identity_resources' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'identity_resources' AS table_name, 'IdentityRequestId' AS column_name
UNION ALL SELECT 'identity_resources' AS table_name, 'Kind' AS column_name
UNION ALL SELECT 'marketplace_bookings' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'marketplace_bookings' AS table_name, 'FreelancerId' AS column_name
UNION ALL SELECT 'marketplace_bookings' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'marketplace_bookings' AS table_name, 'Status' AS column_name
UNION ALL SELECT 'marketplace_bookings' AS table_name, 'TaskId' AS column_name
UNION ALL SELECT 'marketplace_disputes' AS table_name, 'ClosedAt' AS column_name
UNION ALL SELECT 'marketplace_disputes' AS table_name, 'ClosedById' AS column_name
UNION ALL SELECT 'marketplace_disputes' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'marketplace_disputes' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'marketplace_disputes' AS table_name, 'OpenedById' AS column_name
UNION ALL SELECT 'marketplace_disputes' AS table_name, 'PreviousTaskStatus' AS column_name
UNION ALL SELECT 'marketplace_disputes' AS table_name, 'Reason' AS column_name
UNION ALL SELECT 'marketplace_disputes' AS table_name, 'Resolution' AS column_name
UNION ALL SELECT 'marketplace_disputes' AS table_name, 'Status' AS column_name
UNION ALL SELECT 'marketplace_disputes' AS table_name, 'TaskId' AS column_name
UNION ALL SELECT 'marketplace_messages' AS table_name, 'Body' AS column_name
UNION ALL SELECT 'marketplace_messages' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'marketplace_messages' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'marketplace_messages' AS table_name, 'SenderId' AS column_name
UNION ALL SELECT 'marketplace_messages' AS table_name, 'TaskId' AS column_name
UNION ALL SELECT 'marketplace_notifications' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'marketplace_notifications' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'marketplace_notifications' AS table_name, 'IsRead' AS column_name
UNION ALL SELECT 'marketplace_notifications' AS table_name, 'Kind' AS column_name
UNION ALL SELECT 'marketplace_notifications' AS table_name, 'Message' AS column_name
UNION ALL SELECT 'marketplace_notifications' AS table_name, 'TaskId' AS column_name
UNION ALL SELECT 'marketplace_notifications' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'marketplace_offers' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'marketplace_offers' AS table_name, 'FreelancerId' AS column_name
UNION ALL SELECT 'marketplace_offers' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'marketplace_offers' AS table_name, 'Message' AS column_name
UNION ALL SELECT 'marketplace_offers' AS table_name, 'Price' AS column_name
UNION ALL SELECT 'marketplace_offers' AS table_name, 'Status' AS column_name
UNION ALL SELECT 'marketplace_offers' AS table_name, 'TaskId' AS column_name
UNION ALL SELECT 'marketplace_offers' AS table_name, 'WithdrawalReason' AS column_name
UNION ALL SELECT 'marketplace_reviews' AS table_name, 'ClientId' AS column_name
UNION ALL SELECT 'marketplace_reviews' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'marketplace_reviews' AS table_name, 'Feedback' AS column_name
UNION ALL SELECT 'marketplace_reviews' AS table_name, 'FreelancerId' AS column_name
UNION ALL SELECT 'marketplace_reviews' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'marketplace_reviews' AS table_name, 'Rating' AS column_name
UNION ALL SELECT 'marketplace_reviews' AS table_name, 'TaskId' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'Budget' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'CancellationReason' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'Category' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'ClientId' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'Description' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'Location' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'ScheduleText' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'Status' AS column_name
UNION ALL SELECT 'marketplace_tasks' AS table_name, 'Title' AS column_name
UNION ALL SELECT 'mobile_location_updates' AS table_name, 'AccuracyMeters' AS column_name
UNION ALL SELECT 'mobile_location_updates' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'mobile_location_updates' AS table_name, 'Latitude' AS column_name
UNION ALL SELECT 'mobile_location_updates' AS table_name, 'Longitude' AS column_name
UNION ALL SELECT 'mobile_location_updates' AS table_name, 'RecordedAtUtc' AS column_name
UNION ALL SELECT 'mobile_location_updates' AS table_name, 'TaskId' AS column_name
UNION ALL SELECT 'mobile_location_updates' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'password_reset_code_limits' AS table_name, 'Attempts' AS column_name
UNION ALL SELECT 'password_reset_code_limits' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'password_reset_code_limits' AS table_name, 'WindowStartedUtc' AS column_name
UNION ALL SELECT 'password_reset_codes' AS table_name, 'Attempts' AS column_name
UNION ALL SELECT 'password_reset_codes' AS table_name, 'ChallengeHash' AS column_name
UNION ALL SELECT 'password_reset_codes' AS table_name, 'CodeHash' AS column_name
UNION ALL SELECT 'password_reset_codes' AS table_name, 'ExpiresUtc' AS column_name
UNION ALL SELECT 'password_reset_codes' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'password_reset_codes' AS table_name, 'Verified' AS column_name
UNION ALL SELECT 'password_reset_requests' AS table_name, 'ExpiresUtc' AS column_name
UNION ALL SELECT 'password_reset_requests' AS table_name, 'TokenHash' AS column_name
UNION ALL SELECT 'password_reset_requests' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'safety_report_actions' AS table_name, 'AdminId' AS column_name
UNION ALL SELECT 'safety_report_actions' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'safety_report_actions' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'safety_report_actions' AS table_name, 'Note' AS column_name
UNION ALL SELECT 'safety_report_actions' AS table_name, 'ReportId' AS column_name
UNION ALL SELECT 'safety_report_actions' AS table_name, 'Status' AS column_name
UNION ALL SELECT 'safety_report_evidence' AS table_name, 'ContentType' AS column_name
UNION ALL SELECT 'safety_report_evidence' AS table_name, 'Data' AS column_name
UNION ALL SELECT 'safety_report_evidence' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'safety_report_evidence' AS table_name, 'ReportId' AS column_name
UNION ALL SELECT 'safety_reports' AS table_name, 'CreatedAt' AS column_name
UNION ALL SELECT 'safety_reports' AS table_name, 'Description' AS column_name
UNION ALL SELECT 'safety_reports' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'safety_reports' AS table_name, 'Reason' AS column_name
UNION ALL SELECT 'safety_reports' AS table_name, 'ReporterId' AS column_name
UNION ALL SELECT 'safety_reports' AS table_name, 'Status' AS column_name
UNION ALL SELECT 'safety_reports' AS table_name, 'Target' AS column_name
UNION ALL SELECT 'safety_reports' AS table_name, 'TaskId' AS column_name
UNION ALL SELECT 'tbl_user' AS table_name, 'DateOfBirth' AS column_name
UNION ALL SELECT 'tbl_user' AS table_name, 'FirstName' AS column_name
UNION ALL SELECT 'tbl_user' AS table_name, 'Id' AS column_name
UNION ALL SELECT 'tbl_user' AS table_name, 'LastName' AS column_name
UNION ALL SELECT 'tbl_user' AS table_name, 'Password' AS column_name
UNION ALL SELECT 'tbl_user' AS table_name, 'ProfilePicture' AS column_name
UNION ALL SELECT 'tbl_user' AS table_name, 'role' AS column_name
UNION ALL SELECT 'tbl_user' AS table_name, 'userName' AS column_name
UNION ALL SELECT 'verified_emails' AS table_name, 'Email' AS column_name
UNION ALL SELECT 'verified_emails' AS table_name, 'UserId' AS column_name
UNION ALL SELECT 'verified_emails' AS table_name, 'VerifiedUtc' AS column_name
) AS required
LEFT JOIN information_schema.TABLES t ON t.TABLE_SCHEMA=DATABASE() AND t.TABLE_NAME=required.table_name
LEFT JOIN information_schema.COLUMNS c ON c.TABLE_SCHEMA=DATABASE() AND c.TABLE_NAME=required.table_name AND c.COLUMN_NAME=required.column_name
WHERE c.COLUMN_NAME IS NULL
ORDER BY required.table_name, required.column_name;

-- Zero rows above means all listed table/column names exist.
-- This alone does not confirm compatible types, constraints or a working app.

SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_KEY
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA=DATABASE()
ORDER BY TABLE_NAME, ORDINAL_POSITION;
