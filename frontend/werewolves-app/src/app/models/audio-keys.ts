export const AudioKey = {
  RoleReveal:           'role-reveal',
  WerewolvesMeeting:    'werewolves-meeting',
  WerewolvesTurn:       'werewolves-turn',
  CupidTurn:            'cupid-turn',
  LoverReveal:          'lover-reveal',
  SeerTurn:             'seer-turn',
  WitchTurn:            'witch-turn',
  HunterTurn:           'hunter-turn',
  NightEndNoDeaths:     'night-end-no-deaths',
  NightEndOneDeath:     'night-end-one-death',
  NightEndManyDeaths:   'night-end-many-deaths',
  Discussion:           'discussion',
  TiebreakDiscussion:   'tiebreak-discussion',
  DayEliminationRevealTie: 'day-elimination-tie',
  DayEliminationReveal:    'day-elimination',
  GameOverVillagers:    'game-over-villagers',
  GameOverWerewolves:   'game-over-werewolves',
  NightWarning:         'night-warning',
} as const;

export type AudioKey = typeof AudioKey[keyof typeof AudioKey];
