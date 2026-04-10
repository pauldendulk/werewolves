-- V005: Add promo codes table
--
-- Codes are generated on demand by the admin and redeemed once by users
-- to unlock tournament (premium) mode without paying.

CREATE TABLE promo_codes (
    code         TEXT        PRIMARY KEY,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    redeemed_at  TIMESTAMPTZ NULL
);
