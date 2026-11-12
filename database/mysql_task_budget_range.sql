-- Select the intended database and back it up before running.
SET @budget_max_sql = IF(
    EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
           AND TABLE_NAME = 'marketplace_tasks' AND COLUMN_NAME = 'BudgetMaximum'),
    'SELECT ''BudgetMaximum already exists''',
    'ALTER TABLE marketplace_tasks ADD COLUMN BudgetMaximum DECIMAL(12,2) NULL');
PREPARE budget_max_stmt FROM @budget_max_sql;
EXECUTE budget_max_stmt;
DEALLOCATE PREPARE budget_max_stmt;
-- NULL keeps existing fixed budgets unchanged.
