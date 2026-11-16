CREATE TABLE IF NOT EXISTS post_moderation_actions (
    Id BIGINT NOT NULL AUTO_INCREMENT,
    TaskId BIGINT NOT NULL,
    ActorId INT NOT NULL,
    Decision VARCHAR(16) NOT NULL,
    Reason VARCHAR(100) NOT NULL,
    CreatedAtUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (Id),
    KEY IX_post_moderation_task (TaskId, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
