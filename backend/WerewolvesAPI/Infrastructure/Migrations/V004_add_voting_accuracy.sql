-- V004: Add voting accuracy tracking columns to game_players.
-- votes_cast    – number of day-phase votes the player submitted across the game.
-- votes_correct – number of those votes that targeted an actual werewolf.
ALTER TABLE game_players ADD COLUMN IF NOT EXISTS votes_cast    INT NOT NULL DEFAULT 0;
ALTER TABLE game_players ADD COLUMN IF NOT EXISTS votes_correct INT NOT NULL DEFAULT 0;
