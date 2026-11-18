-- Apply once to the intended database before deploying two-way reviews.
-- Existing reviews remain client-to-freelancer.
ALTER TABLE marketplace_reviews
    ADD COLUMN ByFreelancer TINYINT(1) NOT NULL DEFAULT 0,
    DROP INDEX uq_marketplace_review_task,
    ADD UNIQUE KEY uq_marketplace_review_direction (TaskId, ByFreelancer);
