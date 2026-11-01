-- LOCAL MYSQL ONLY. Open the Local instance connection in Workbench.
USE taptaskdone;
-- Preserve the original setting even if a previous run stopped before restoring it.
SET @tts_workflow_safe_updates = COALESCE(@tts_workflow_safe_updates, @@SESSION.SQL_SAFE_UPDATES);
SET SESSION SQL_SAFE_UPDATES = 0;

CREATE TABLE IF NOT EXISTS task_destinations (
  TaskId BIGINT NOT NULL PRIMARY KEY,
  Latitude DOUBLE NOT NULL,
  Longitude DOUBLE NOT NULL,
  UpdatedAtUtc DATETIME(6) NOT NULL,
  CONSTRAINT FK_destination_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id)
) ENGINE=InnoDB;

-- Select the intended schema first (e.g. USE defaultdb; on Aiven).
-- Run before deploying this update. Existing marketplace and task_destinations tables are required.
CREATE TABLE IF NOT EXISTS task_conversations (
 Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 TaskId BIGINT NOT NULL,
 FreelancerId INT NOT NULL,
 CreatedAtUtc DATETIME(6) NOT NULL,
 UNIQUE KEY ux_task_conversation (TaskId, FreelancerId),
 CONSTRAINT fk_conversation_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
 CONSTRAINT fk_conversation_freelancer FOREIGN KEY (FreelancerId) REFERENCES tbl_user(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS work_agreements (
 Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 TaskId BIGINT NOT NULL,
 Version INT NOT NULL,
 ProposedById INT NOT NULL,
 Price DECIMAL(12,2) NOT NULL,
 ScheduleText VARCHAR(120) NOT NULL,
 Scope VARCHAR(5000) NOT NULL,
 Location VARCHAR(120) NOT NULL,
 Latitude DOUBLE NOT NULL,
 Longitude DOUBLE NOT NULL,
 CreatedAtUtc DATETIME(6) NOT NULL,
 ClientConfirmedAtUtc DATETIME(6) NULL,
 FreelancerConfirmedAtUtc DATETIME(6) NULL,
 UNIQUE KEY ux_agreement_version (TaskId, Version),
 CONSTRAINT fk_agreement_task FOREIGN KEY (TaskId) REFERENCES marketplace_tasks(Id),
 CONSTRAINT fk_agreement_proposer FOREIGN KEY (ProposedById) REFERENCES tbl_user(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SET @ddl = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='marketplace_tasks' AND COLUMN_NAME='IsPrivate')=0,
 'ALTER TABLE marketplace_tasks ADD COLUMN IsPrivate TINYINT(1) NOT NULL DEFAULT 0', 'SELECT ''IsPrivate exists''');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='marketplace_messages' AND COLUMN_NAME='ConversationId')=0,
 'ALTER TABLE marketplace_messages ADD COLUMN ConversationId BIGINT NULL, ADD INDEX ix_message_conversation (ConversationId,Id), ADD CONSTRAINT fk_message_conversation FOREIGN KEY (ConversationId) REFERENCES task_conversations(Id)', 'SELECT ''Message ConversationId exists''');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='marketplace_notifications' AND COLUMN_NAME='ConversationId')=0,
 'ALTER TABLE marketplace_notifications ADD COLUMN ConversationId BIGINT NULL, ADD INDEX ix_notification_conversation (ConversationId), ADD CONSTRAINT fk_notification_conversation FOREIGN KEY (ConversationId) REFERENCES task_conversations(Id)', 'SELECT ''Notification ConversationId exists''');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Preserve privacy for recognizable existing direct bookings, including withdrawn requests.
UPDATE marketplace_tasks t SET t.IsPrivate=1
WHERE t.Status='booking-pending' OR EXISTS (
 SELECT 1 FROM marketplace_offers o WHERE o.TaskId=t.Id AND o.Message='Direct booking at the agreed request budget.');
UPDATE marketplace_tasks t SET t.Status='cancelled', t.CancellationReason='Private request closed after the selected freelancer withdrew.'
WHERE t.IsPrivate=1 AND t.Status='open';

-- Create one private thread for each existing offer/booking relationship; do not duplicate existing threads.
INSERT INTO task_conversations (TaskId,FreelancerId,CreatedAtUtc)
SELECT seed.TaskId,seed.FreelancerId,UTC_TIMESTAMP(6)
FROM (
 SELECT TaskId,FreelancerId FROM marketplace_offers WHERE Status IN ('accepted','pending','withdrawn')
 UNION SELECT TaskId,FreelancerId FROM marketplace_bookings WHERE Status IN ('pending','accepted','withdrawn')
) seed LEFT JOIN task_conversations c ON c.TaskId=seed.TaskId AND c.FreelancerId=seed.FreelancerId
WHERE c.Id IS NULL;

-- Only unambiguous legacy messages are attached to a selected pair.
-- Messages from tasks with a previous different assigned freelancer remain untouched (ConversationId NULL)
-- for manual migration review; guessing their recipient could expose private conversations.
UPDATE marketplace_messages m
JOIN marketplace_tasks t ON t.Id=m.TaskId
JOIN marketplace_offers o ON o.TaskId=t.Id AND o.Status='accepted'
JOIN task_conversations c ON c.TaskId=t.Id AND c.FreelancerId=o.FreelancerId
SET m.ConversationId=c.Id
WHERE m.ConversationId IS NULL AND m.SenderId IN (t.ClientId,o.FreelancerId)
 AND NOT EXISTS (SELECT 1 FROM marketplace_offers other WHERE other.TaskId=t.Id AND other.FreelancerId<>o.FreelancerId AND other.Status IN ('accepted','withdrawn'));

SELECT DATABASE() AS selected_database;
SELECT COUNT(*) AS unassigned_legacy_messages FROM marketplace_messages WHERE ConversationId IS NULL;


-- Restore this connection's original safe-update setting.
SET SESSION SQL_SAFE_UPDATES = @tts_workflow_safe_updates;
SET @tts_workflow_safe_updates = NULL;
SELECT DATABASE() AS selected_database, @@SESSION.SQL_SAFE_UPDATES AS safe_update_mode;