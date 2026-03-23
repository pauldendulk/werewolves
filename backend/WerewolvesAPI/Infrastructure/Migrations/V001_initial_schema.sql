-- V001: Initial schema
--
-- Three layers:
--   1. tournaments    — an evening of multiple games
--   2. tournament_participants — a player's membership in a tournament (accumulated score)
--   3. games          — a single werewolf game
--   4. game_players   — a player's role and result in one game
--
-- NOTE: A global `users` table is intentionally omitted until authentication is introduced.
--       Player identity is currently tracked by an opaque player_id UUID string.

CREATE TABLE IF NOT EXISTS tournaments (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT,
    host_player_id TEXT     NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_premium  BOOLEAN     NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS tournament_participants (
    tournament_id UUID        NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
    player_id     TEXT        NOT NULL,
    display_name  TEXT        NOT NULL,
    total_score   INT         NOT NULL DEFAULT 0,
    joined_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (tournament_id, player_id)
);

CREATE TABLE IF NOT EXISTS games (
    id            TEXT        PRIMARY KEY,
    tournament_id UUID        REFERENCES tournaments(id) ON DELETE SET NULL,
    join_code     TEXT        NOT NULL,
    status        TEXT        NOT NULL,
    winner        TEXT,
    settings      JSONB       NOT NULL DEFAULT '{}',
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ended_at      TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS game_players (
    game_id              TEXT        NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    player_id            TEXT        NOT NULL,
    display_name         TEXT        NOT NULL,
    role                 TEXT,
    skill                TEXT,
    is_eliminated        BOOLEAN     NOT NULL DEFAULT FALSE,
    elimination_cause    TEXT,
    score                INT         NOT NULL DEFAULT 0,
    is_creator           BOOLEAN     NOT NULL DEFAULT FALSE,
    is_moderator         BOOLEAN     NOT NULL DEFAULT FALSE,
    participation_status TEXT        NOT NULL,
    joined_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (game_id, player_id)
);
