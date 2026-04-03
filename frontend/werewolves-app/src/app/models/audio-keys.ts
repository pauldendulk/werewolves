export const AudioKey = {
  RoleReveal:                       'role-reveal',
  NightAnnouncement:                'night-announcement',
  WerewolvesMeetingCloseEyes:       'werewolves-meeting-close-eyes',
  WerewolvesTurn:                   'werewolves-turn',
  CupidTurn:                        'cupid-turn',
  CupidCloseEyes:                   'cupid-close-eyes',
  LoverReveal:                      'lover-reveal',
  SeerTurn:                         'seer-turn',
  SeerCloseEyes:                    'seer-close-eyes',
  WitchTurn:                        'witch-turn',
  WitchCloseEyes:                   'witch-close-eyes',
  DayAnnouncement:                  'day-announcement',
  HunterTurn:                       'hunter-turn',
  NightEndNoDeaths:                 'night-end-no-deaths',
  NightEndOneDeath:                 'night-end-one-death',
  NightEndManyDeaths:               'night-end-many-deaths',
  Discussion:                       'discussion',
  TiebreakDiscussion:               'tiebreak-discussion',
  DayEliminationRevealTie:          'day-elimination-tie',
  DayEliminationReveal:             'day-elimination',
  FinalScoresRevealVillagers:       'game-over-villagers',
  FinalScoresRevealWerewolves:      'game-over-werewolves',
  WolvesCloseEyes:                  'wolves-close-eyes',
} as const;

export type AudioKey = typeof AudioKey[keyof typeof AudioKey];

export interface AudioClipConfig {
  key: AudioKey;
  forCreatorOnly: boolean;
}

export const PhaseAudio: { [phase: string]: AudioClipConfig[] } = {
  RoleReveal: [
    { key: AudioKey.RoleReveal, forCreatorOnly: false },
  ],
  NightAnnouncement: [
    { key: AudioKey.NightAnnouncement, forCreatorOnly: false },
  ],
  WerewolvesMeeting: [
    { key: AudioKey.WerewolvesMeetingCloseEyes, forCreatorOnly: true },
  ],
  WerewolvesTurn: [
    { key: AudioKey.WerewolvesTurn, forCreatorOnly: true },
  ],
  CupidTurn: [
    { key: AudioKey.CupidTurn, forCreatorOnly: true },
  ],
  CupidCloseEyes: [
    { key: AudioKey.CupidCloseEyes, forCreatorOnly: true },
  ],
  LoverReveal: [
    { key: AudioKey.LoverReveal, forCreatorOnly: false },
  ],
  SeerTurn: [
    { key: AudioKey.SeerTurn, forCreatorOnly: true },
  ],
  SeerCloseEyes: [
    { key: AudioKey.SeerCloseEyes, forCreatorOnly: true },
  ],
  WitchTurn: [
    { key: AudioKey.WitchTurn, forCreatorOnly: true },
  ],
  WitchCloseEyes: [
    { key: AudioKey.WitchCloseEyes, forCreatorOnly: true },
  ],
  DayAnnouncement: [
    { key: AudioKey.DayAnnouncement, forCreatorOnly: false },
  ],
  HunterTurn: [
    { key: AudioKey.HunterTurn, forCreatorOnly: true },
  ],
  Discussion: [
    { key: AudioKey.Discussion, forCreatorOnly: true },
  ],
  TiebreakDiscussion: [
    { key: AudioKey.TiebreakDiscussion, forCreatorOnly: true },
  ],
  WolvesCloseEyes: [
    { key: AudioKey.WolvesCloseEyes, forCreatorOnly: true },
  ],
};
