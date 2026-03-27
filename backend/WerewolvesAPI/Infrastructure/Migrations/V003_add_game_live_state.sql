-- V003: Add game_live_state table for server-restart recovery.
-- Holds the serialized GameState for each active tournament.
-- Written on every phase transition; read once on startup to rebuild the in-memory dictionary.
-- The row is overwritten (not deleted) as the game progresses, so it always reflects the latest state.
CREATE TABLE IF NOT EXISTS game_live_state (
    tournament_code TEXT        PRIMARY KEY,
    state           JSONB       NOT NULL,
    saved_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
