-- Additive fallback for a database user without CREATE permission.
CREATE TABLE IF NOT EXISTS password_reset_send_states (
  EmailHash VARCHAR(64) CHARACTER SET ascii NOT NULL PRIMARY KEY,
  WindowUtc DATETIME(6) NOT NULL, LastSentUtc DATETIME(6) NOT NULL,
  SendCount INT NOT NULL, INDEX IX_reset_send_last (LastSentUtc)
) ENGINE=InnoDB;
