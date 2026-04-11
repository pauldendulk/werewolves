-- V006: Rename is_premium → is_tournament_mode_unlocked for clarity
ALTER TABLE tournaments RENAME COLUMN is_premium TO is_tournament_mode_unlocked;
