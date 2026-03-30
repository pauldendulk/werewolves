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
  FinalScoresRevealVillagers: 'game-over-villagers',
  FinalScoresRevealWerewolves:  'game-over-werewolves',
  NightWarning:         'night-warning',
} as const;

export type AudioKey = typeof AudioKey[keyof typeof AudioKey];
