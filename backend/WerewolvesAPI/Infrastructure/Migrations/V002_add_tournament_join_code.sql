-- V002: Add join_code to tournaments for use as a stable, short URL identifier
ALTER TABLE tournaments ADD COLUMN IF NOT EXISTS join_code TEXT;
CREATE UNIQUE INDEX IF NOT EXISTS tournaments_join_code_idx ON tournaments(join_code);
