CREATE TABLE IF NOT EXISTS task_phone_shares (
    ConversationId BIGINT NOT NULL,
    UserId INT NOT NULL,
    PRIMARY KEY (ConversationId, UserId),
    CONSTRAINT fk_task_phone_shares_conversation FOREIGN KEY (ConversationId) REFERENCES task_conversations(Id) ON DELETE CASCADE,
    CONSTRAINT fk_task_phone_shares_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
) ENGINE=InnoDB;
