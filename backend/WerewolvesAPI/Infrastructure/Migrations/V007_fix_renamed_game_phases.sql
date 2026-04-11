-- V007: Fix renamed GamePhase values in live state JSON.
-- The C# enum was renamed LoverReveal → LoversReveal; update stored JSONB to match.
UPDATE game_live_state
SET state = jsonb_set(state, '{Phase}', '"LoversReveal"')
WHERE state->>'Phase' = 'LoverReveal';
