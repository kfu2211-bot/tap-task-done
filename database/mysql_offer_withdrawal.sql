-- Run once after creating marketplace_offers. Existing data is preserved.
ALTER TABLE marketplace_offers ADD COLUMN WithdrawalReason VARCHAR(500) NULL;
