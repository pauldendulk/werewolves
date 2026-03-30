using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WerewolvesAPI.Repositories;
using WerewolvesAPI.Services;

namespace WerewolvesAPI.Tests;

public class GameServiceTests
{
    private readonly GameService _gameService;
    private readonly Mock<ILogger<GameService>> _loggerMock;

    public GameServiceTests()
    {
        _loggerMock = new Mock<ILogger<GameService>>();
        var gameRepositoryMock = new Mock<IGameRepository>();
        var tournamentRepositoryMock = new Mock<ITournamentRepository>();
        var configurationMock = new Mock<IConfiguration>();
        _gameService = new GameService(_loggerMock.Object, gameRepositoryMock.Object, tournamentRepositoryMock.Object, configurationMock.Object);
    }

    [Fact]
    public void CreateGame_ShouldCreateGameWithCreator()
    {
        var game = _gameService.CreateGame("John", 30, "http://localhost:4200");

        game.Should().NotBeNull();        game.MaxPlayers.Should().Be(30);
        game.Players.Should().HaveCount(1);
        game.Players.First().DisplayName.Should().Be("John");
        game.Players.First().IsCreator.Should().BeTrue();
        game.Players.First().IsModerator.Should().BeTrue();
    }

    [Fact]
    public void JoinGame_ShouldAddPlayerToGame()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");

        var (success, message, player) = _gameService.JoinGame(game.TournamentCode, "Alice");

        success.Should().BeTrue();
        player.Should().NotBeNull();
        player!.DisplayName.Should().Be("Alice");
        player.IsCreator.Should().BeFalse();
        _gameService.GetGame(game.TournamentCode)!.Players.Should().HaveCount(2);
    }

    [Fact]
    public void JoinGame_WhenGameFull_ShouldReturnFailure()
    {
        var game = _gameService.CreateGame("Creator", 2, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Player1");

        var (success, message, player) = _gameService.JoinGame(game.TournamentCode, "Player2");

        success.Should().BeFalse();
        message.Should().Contain("full");
        player.Should().BeNull();
    }

    [Fact]
    public void JoinGame_WhenPlayerRejoins_ShouldUpdateStatus()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.LeaveGame(game.TournamentCode, player!.PlayerId);

        var (success, _, rejoinedPlayer) = _gameService.JoinGame(game.TournamentCode, "Alice", player.PlayerId);

        success.Should().BeTrue();
        rejoinedPlayer!.PlayerId.Should().Be(player.PlayerId);
        rejoinedPlayer.IsConnected.Should().BeTrue();
        rejoinedPlayer.ParticipationStatus.Should().Be(Models.ParticipationStatus.Participating);
    }

    [Fact]
    public void LeaveGame_ShouldUpdatePlayerStatus()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.TournamentCode, "Alice");

        _gameService.LeaveGame(game.TournamentCode, player!.PlayerId);

        var leftPlayer = _gameService.GetGame(game.TournamentCode)!.Players.First(p => p.PlayerId == player.PlayerId);
        leftPlayer.ParticipationStatus.Should().Be(Models.ParticipationStatus.Left);
    }

    [Fact]
    public void RemovePlayer_ByModerator_ShouldUpdatePlayerStatus()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.TournamentCode, "Alice");

        _gameService.RemovePlayer(game.TournamentCode, player!.PlayerId, game.CreatorId);

        _gameService.GetGame(game.TournamentCode)!.Players.First(p => p.PlayerId == player.PlayerId)
            .ParticipationStatus.Should().Be(Models.ParticipationStatus.Removed);
    }

    [Fact]
    public void UpdateMaxPlayers_ByCreator_ShouldUpdateSettings()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");

        _gameService.UpdateMaxPlayers(game.TournamentCode, 30, game.CreatorId).Should().BeTrue();
        _gameService.GetGame(game.TournamentCode)!.MaxPlayers.Should().Be(30);
    }

    [Fact]
    public void UpdateMaxPlayers_ByNonCreator_ShouldFail()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.TournamentCode, "Alice");

        _gameService.UpdateMaxPlayers(game.TournamentCode, 30, player!.PlayerId).Should().BeFalse();
    }

    [Fact]
    public void UpdateEnabledSkills_ByCreator_ShouldUpdateSkills()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");

        _gameService.UpdateEnabledSkills(game.TournamentCode, new List<string> { "Seer", "Witch" }, game.CreatorId).Should().BeTrue();

        var updated = _gameService.GetGame(game.TournamentCode)!;
        updated.EnabledSkills.Should().HaveCount(2);
        updated.EnabledSkills.Should().Contain(Models.PlayerSkill.Seer);
        updated.EnabledSkills.Should().Contain(Models.PlayerSkill.Witch);
    }

    [Fact]
    public void HasDuplicateNames_WhenNoDuplicates_ShouldReturnFalse()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.JoinGame(game.TournamentCode, "Bob");

        _gameService.HasDuplicateNames(game.TournamentCode).Should().BeFalse();
    }

    [Fact]
    public void HasDuplicateNames_WhenDuplicates_ShouldReturnTrue()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.JoinGame(game.TournamentCode, "Alice");

        _gameService.HasDuplicateNames(game.TournamentCode).Should().BeTrue();
    }

    [Fact]
    public void UpdatePlayerName_WhenCreatorChangesName_ShouldUpdateCreatorNameInDTO()
    {
        var game = _gameService.CreateGame("OriginalCreator", 40, "http://localhost");

        _gameService.UpdatePlayerName(game.TournamentCode, game.CreatorId, "UpdatedCreator").Should().BeTrue();

        _gameService.GetGame(game.TournamentCode)!.Players.First(p => p.PlayerId == game.CreatorId)
            .DisplayName.Should().Be("UpdatedCreator");
    }

    [Fact]
    public void GetGame_ShouldDeriveCreatorNameFromPlayer()
    {
        var game = _gameService.CreateGame("OriginalName", 40, "http://localhost");
        _gameService.UpdatePlayerName(game.TournamentCode, game.CreatorId, "NewName");

        _gameService.GetGame(game.TournamentCode)!.Players.First(p => p.PlayerId == game.CreatorId)
            .DisplayName.Should().Be("NewName");
    }

    [Fact]
    public void CreateGame_ShouldStartAtVersionOne()
    {
        _gameService.CreateGame("Creator", 40, "http://localhost").Version.Should().Be(1);
    }

    [Fact]
    public void JoinGame_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var before = game.Version;
        _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.GetGame(game.TournamentCode)!.Version.Should().Be(before + 1);
    }

    [Fact]
    public void LeaveGame_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.TournamentCode, "Alice");
        var after = _gameService.GetGame(game.TournamentCode)!.Version;
        _gameService.LeaveGame(game.TournamentCode, player!.PlayerId);
        _gameService.GetGame(game.TournamentCode)!.Version.Should().Be(after + 1);
    }

    [Fact]
    public void RemovePlayer_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.TournamentCode, "Alice");
        var after = _gameService.GetGame(game.TournamentCode)!.Version;
        _gameService.RemovePlayer(game.TournamentCode, player!.PlayerId, game.CreatorId);
        _gameService.GetGame(game.TournamentCode)!.Version.Should().Be(after + 1);
    }

    [Fact]
    public void UpdateMaxPlayers_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var before = game.Version;
        _gameService.UpdateMaxPlayers(game.TournamentCode, 30, game.CreatorId);
        _gameService.GetGame(game.TournamentCode)!.Version.Should().Be(before + 1);
    }

    [Fact]
    public void UpdateMinPlayers_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var before = game.Version;
        _gameService.UpdateMinPlayers(game.TournamentCode, 3, game.CreatorId);
        _gameService.GetGame(game.TournamentCode)!.Version.Should().Be(before + 1);
    }

    [Fact]
    public void UpdatePlayerName_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var before = game.Version;
        _gameService.UpdatePlayerName(game.TournamentCode, game.CreatorId, "New Name");
        _gameService.GetGame(game.TournamentCode)!.Version.Should().Be(before + 1);
    }

    [Fact]
    public void CreateGame_ShouldStartAtWaitingForPlayers()
    {
        _gameService.CreateGame("Creator", 40, "http://localhost")
            .Status.Should().Be(Models.GameStatus.WaitingForPlayers);
    }

    [Fact]
    public void JoinGame_WhenEnoughPlayers_ShouldTransitionToReadyToStart()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.JoinGame(game.TournamentCode, "Bob");
        _gameService.JoinGame(game.TournamentCode, "Charlie");

        _gameService.GetGame(game.TournamentCode)!.Status.Should().Be(Models.GameStatus.ReadyToStart);
    }

    [Fact]
    public void LeaveGame_WhenBelowMinPlayers_ShouldTransitionBackToWaitingForPlayers()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, alice) = _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.JoinGame(game.TournamentCode, "Bob");
        _gameService.GetGame(game.TournamentCode)!.Status.Should().Be(Models.GameStatus.ReadyToStart);

        _gameService.LeaveGame(game.TournamentCode, alice!.PlayerId);

        _gameService.GetGame(game.TournamentCode)!.Status.Should().Be(Models.GameStatus.WaitingForPlayers);
    }

    // ── Session / phase tests (no skills) ─────────────────────────────────
    //
    // CreateReadyGame disables all skills so the night flow is:
    //   Round 1 : RoleReveal → WerewolvesMeeting → Discussion
    //   Round 2+: DayEliminationReveal → WerewolvesTurn → NightEliminationReveal → Discussion

    private Models.GameState CreateReadyGame(int extraPlayers = 3)
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        for (int i = 0; i < extraPlayers; i++)
            _gameService.JoinGame(game.TournamentCode, $"Player{i + 1}");
        // Disable all skills so phase transitions stay simple
        _gameService.UpdateEnabledSkills(game.TournamentCode, new List<string>(), game.CreatorId);
        return _gameService.GetGame(game.TournamentCode)!;
    }

    [Fact]
    public void StartGame_ShouldAssignRoles()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var updated = _gameService.GetGame(game.TournamentCode)!;
        updated.Players.Should().OnlyContain(p => p.Role != null);
        updated.Players.Count(p => p.Role == Models.PlayerRole.Werewolf).Should().Be(game.NumberOfWerewolves);
        updated.Players.Count(p => p.Role == Models.PlayerRole.Villager).Should().Be(updated.Players.Count - game.NumberOfWerewolves);
    }

    [Fact]
    public void StartGame_ShouldSetPhaseToRoleReveal()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var updated = _gameService.GetGame(game.TournamentCode)!;
        updated.Status.Should().Be(Models.GameStatus.InProgress);
        updated.Phase.Should().Be(Models.GamePhase.RoleReveal);
        updated.RoundNumber.Should().Be(1);
        updated.PhaseEndsAt.Should().BeNull();
    }

    [Fact]
    public void StartGame_ByNonCreator_ShouldFail()
    {
        var game = CreateReadyGame();
        var (_, _, player) = _gameService.JoinGame(game.TournamentCode, "Extra");

        var (success, error) = _gameService.StartGame(game.TournamentCode, player!.PlayerId);

        success.Should().BeFalse();
        error.Should().Contain("creator");
    }

    [Fact]
    public void StartGame_WhenNotReadyToStart_ShouldFail()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");

        var (success, _) = _gameService.StartGame(game.TournamentCode, game.CreatorId);

        success.Should().BeFalse();
    }

    [Fact]
    public void ForceAdvancePhase_FromRoleReveal_ShouldTransitionToWerewolvesMeeting()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId);

        // No skills → round 1 goes directly to WerewolvesMeeting
        _gameService.GetGame(game.TournamentCode)!.Phase.Should().Be(Models.GamePhase.WerewolvesMeeting);
    }

    [Fact]
    public void ForceAdvancePhase_FromWerewolvesMeeting_Round1_ShouldGoToDiscussion()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.TournamentCode, game.CreatorId);
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesMeeting

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → Discussion

        _gameService.GetGame(game.TournamentCode)!.Phase.Should().Be(Models.GamePhase.Discussion);
    }

    [Fact]
    public void CastVote_DuringDiscussion_ShouldRecordVote()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.TournamentCode, game.CreatorId);
        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        var voter  = game.Players.First(p => p.PlayerId != game.CreatorId);
        var target = game.Players.First(p => p.PlayerId != voter.PlayerId);

        var (success, _) = _gameService.CastVote(game.TournamentCode, voter.PlayerId, target.PlayerId);

        success.Should().BeTrue();
        _gameService.GetGame(game.TournamentCode)!.DayVotes[voter.PlayerId].Should().Be(target.PlayerId);
    }

    [Fact]
    public void CastVote_DuringWerewolvesTurn_AllWolvesVote_AutoAdvancesPhase()
    {
        // This test intentionally does NOT call ForceAdvancePhase after CastVote.
        // It verifies that the phase transitions automatically when all werewolves
        // have cast their night vote — i.e. the mechanism real players rely on.
        var game = CreateReadyGame();
        _gameService.StartGame(game.TournamentCode, game.CreatorId);
        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        var updated = _gameService.GetGame(game.TournamentCode)!;
        var werewolf = updated.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = updated.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        foreach (var voter in updated.Players.Where(p => p.PlayerId != villagers[0].PlayerId))
            _gameService.CastVote(game.TournamentCode, voter.PlayerId, villagers[0].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn (round 2)

        var before = _gameService.GetGame(game.TournamentCode)!;
        before.Phase.Should().Be(Models.GamePhase.WerewolvesTurn);

        // Single wolf votes — no ForceAdvancePhase
        _gameService.CastVote(game.TournamentCode, werewolf.PlayerId, villagers[1].PlayerId);

        var after = _gameService.GetGame(game.TournamentCode)!;
        after.Phase.Should().NotBe(Models.GamePhase.WerewolvesTurn,
            "phase must advance automatically once all werewolves have voted");
    }

    [Fact]
    public void CastVote_DuringWerewolvesTurn_ByVillager_ShouldFail()
    {
        // WerewolvesTurn only happens from round 2 onwards
        var game = CreateReadyGame();
        _gameService.StartGame(game.TournamentCode, game.CreatorId);
        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        var updated = _gameService.GetGame(game.TournamentCode)!;
        var werewolf = updated.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var sacrificeVillager = updated.Players.First(p => p.Role == Models.PlayerRole.Villager && !p.IsEliminated);
        // Vote out a villager (not the werewolf) to reach Night round 2 with wolf still alive
        foreach (var voter in updated.Players.Where(p => p.PlayerId != sacrificeVillager.PlayerId))
            _gameService.CastVote(game.TournamentCode, voter.PlayerId, sacrificeVillager.PlayerId);
        // All votes go to one player — no tiebreak — advance
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn (round 2)

        var state = _gameService.GetGame(game.TournamentCode)!;
        state.Phase.Should().Be(Models.GamePhase.WerewolvesTurn);

        var villager = state.Players.First(p => p.Role == Models.PlayerRole.Villager && !p.IsEliminated);
        var target   = state.Players.First(p => p.PlayerId != villager.PlayerId && !p.IsEliminated);

        var (success, error) = _gameService.CastVote(game.TournamentCode, villager.PlayerId, target.PlayerId);

        success.Should().BeFalse();
    }

    [Fact]
    public void MarkDone_AllPlayers_ShouldAdvanceFromRoleReveal()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        foreach (var player in _gameService.GetGame(game.TournamentCode)!.Players)
            _gameService.MarkDone(game.TournamentCode, player.PlayerId);

        // No skills → advances to WerewolvesMeeting
        _gameService.GetGame(game.TournamentCode)!.Phase.Should().Be(Models.GamePhase.WerewolvesMeeting);
    }

    [Fact]
    public void VillagersWin_WhenAllWerewolvesEliminated()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.TournamentCode, game.CreatorId);
        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        var updated  = _gameService.GetGame(game.TournamentCode)!;
        var werewolf = updated.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        foreach (var voter in updated.Players.Where(p => p.PlayerId != werewolf.PlayerId))
            _gameService.CastVote(game.TournamentCode, voter.PlayerId, werewolf.PlayerId);

        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal

        var final = _gameService.GetGame(game.TournamentCode)!;
        final.Phase.Should().Be(Models.GamePhase.DayEliminationReveal);
        final.DayDeaths.Should().ContainSingle(e => e.PlayerId == werewolf.PlayerId);
        final.Winner.Should().Be("Villagers");
    }

    [Fact]
    public void WerewolvesWin_WhenAllVillagersAreEliminated()
    {
        var game = CreateReadyGame(2); // 3 total: 1W + 2V
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var started  = _gameService.GetGame(game.TournamentCode)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        // Day 1: vote out V[0]
        _gameService.CastVote(game.TournamentCode, werewolf.PlayerId, villagers[0].PlayerId);
        _gameService.CastVote(game.TournamentCode, villagers[1].PlayerId, villagers[0].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal

        _gameService.GetGame(game.TournamentCode)!.Winner.Should().BeNull("1W + 1V is not a win yet");

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn round 2

        // Night 2: wolf kills last villager
        _gameService.CastVote(game.TournamentCode, werewolf.PlayerId, villagers[1].PlayerId);
        // CastVote auto-advances to NightEliminationReveal when all wolves have voted

        var afterNight = _gameService.GetGame(game.TournamentCode)!;
        afterNight.Phase.Should().Be(Models.GamePhase.NightEliminationReveal);
        afterNight.NightDeaths.Should().ContainSingle(e => e.PlayerId == villagers[1].PlayerId);
        afterNight.Winner.Should().Be("Werewolves");

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → GameOver
        _gameService.GetGame(game.TournamentCode)!.Phase.Should().Be(Models.GamePhase.FinalScoresReveal);
    }

    [Fact]
    public void EliminatedPlayers_CanVoteDuringDay()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var started  = _gameService.GetGame(game.TournamentCode)!;
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        var werewolf  = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);

        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        // Vote out V[0]
        foreach (var voter in started.Players.Where(p => p.PlayerId != villagers[0].PlayerId))
            _gameService.CastVote(game.TournamentCode, voter.PlayerId, villagers[0].PlayerId);

        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → NightEliminationReveal (no wolf vote)
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → Discussion

        _gameService.GetGame(game.TournamentCode)!.Phase.Should().Be(Models.GamePhase.Discussion);

        // Eliminated player can still cast a day vote
        var (success, error) = _gameService.CastVote(game.TournamentCode, villagers[0].PlayerId, werewolf.PlayerId);
        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void FinalScoresReveal_VersionIsBumped_SoPollingClientsReceiveTheTransition()
    {
        var game = CreateReadyGame(2); // 3 total: 1W + 2V
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var started  = _gameService.GetGame(game.TournamentCode)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        _gameService.CastVote(game.TournamentCode, werewolf.PlayerId, villagers[0].PlayerId);
        _gameService.CastVote(game.TournamentCode, villagers[1].PlayerId, villagers[0].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn round 2

        _gameService.CastVote(game.TournamentCode, werewolf.PlayerId, villagers[1].PlayerId);
        // CastVote auto-advances to NightEliminationReveal when all wolves have voted

        var vAtNightElim = _gameService.GetGame(game.TournamentCode)!.Version;

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → GameOver
        var afterGameOver = _gameService.GetGame(game.TournamentCode)!;

        afterGameOver.Phase.Should().Be(Models.GamePhase.FinalScoresReveal);
        afterGameOver.Winner.Should().Be("Werewolves");
        afterGameOver.Version.Should().BeGreaterThan(vAtNightElim);
    }

    [Fact]
    public void FullGameScenario_SixPlayers_TwoWerewolves_WerewolvesWinAfterFourRounds()
    {
        var game = CreateReadyGame(5); // 6 total
        _gameService.UpdateNumberOfWerewolves(game.TournamentCode, 2, game.CreatorId);
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var state = _gameService.GetGame(game.TournamentCode)!;
        var W = state.Players.Where(p => p.Role == Models.PlayerRole.Werewolf).ToList();
        var V = state.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        W.Should().HaveCount(2);
        V.Should().HaveCount(4);

        // ── Round 1 ──────────────────────────────────────────────────────────
        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        // Day 1: eliminate V[0]
        foreach (var p in state.Players.Where(p => p.PlayerId != V[0].PlayerId))
            _gameService.CastVote(game.TournamentCode, p.PlayerId, V[0].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal

        state = _gameService.GetGame(game.TournamentCode)!;
        state.DayDeaths.Should().ContainSingle(e => e.PlayerId == V[0].PlayerId);
        state.Winner.Should().BeNull();

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn round 2

        // ── Round 2 ──────────────────────────────────────────────────────────
        _gameService.CastVote(game.TournamentCode, W[0].PlayerId, V[1].PlayerId);
        _gameService.CastVote(game.TournamentCode, W[1].PlayerId, V[1].PlayerId);
        // CastVote auto-advances to NightEliminationReveal when all wolves have voted

        state = _gameService.GetGame(game.TournamentCode)!;
        state.NightDeaths.Should().ContainSingle(e => e.PlayerId == V[1].PlayerId);
        state.Winner.Should().BeNull();

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → Discussion

        // Day 2: V[2] and V[3] vote W[0]; wolves split
        _gameService.CastVote(game.TournamentCode, W[0].PlayerId, V[2].PlayerId);
        _gameService.CastVote(game.TournamentCode, W[1].PlayerId, V[3].PlayerId);
        _gameService.CastVote(game.TournamentCode, V[2].PlayerId, W[0].PlayerId);
        _gameService.CastVote(game.TournamentCode, V[3].PlayerId, W[0].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal

        state = _gameService.GetGame(game.TournamentCode)!;
        state.DayDeaths.Should().ContainSingle(e => e.PlayerId == W[0].PlayerId);
        state.Winner.Should().BeNull();

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn round 3

        // ── Round 3 ──────────────────────────────────────────────────────────
        _gameService.CastVote(game.TournamentCode, W[1].PlayerId, V[2].PlayerId);
        // CastVote auto-advances to NightEliminationReveal when all wolves have voted

        state = _gameService.GetGame(game.TournamentCode)!;
        state.NightDeaths.Should().ContainSingle(e => e.PlayerId == V[2].PlayerId);
        state.Winner.Should().BeNull();

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → Discussion

        // Day 3: W[1] ↔ V[3] tie → TiebreakDiscussion
        _gameService.CastVote(game.TournamentCode, W[1].PlayerId, V[3].PlayerId);
        _gameService.CastVote(game.TournamentCode, V[3].PlayerId, W[1].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → TiebreakDiscussion

        _gameService.GetGame(game.TournamentCode)!.Phase.Should().Be(Models.GamePhase.TiebreakDiscussion);

        // Tiebreak: tie again → no elimination
        _gameService.CastVote(game.TournamentCode, W[1].PlayerId, V[3].PlayerId);
        _gameService.CastVote(game.TournamentCode, V[3].PlayerId, W[1].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal

        state = _gameService.GetGame(game.TournamentCode)!;
        state.DayDeaths.Should().BeEmpty("double tie = no elimination");
        state.Winner.Should().BeNull();

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn round 4

        // ── Round 4 ──────────────────────────────────────────────────────────
        _gameService.CastVote(game.TournamentCode, W[1].PlayerId, V[3].PlayerId);
        // CastVote auto-advances to NightEliminationReveal when all wolves have voted

        state = _gameService.GetGame(game.TournamentCode)!;
        state.Phase.Should().Be(Models.GamePhase.NightEliminationReveal);
        state.NightDeaths.Should().ContainSingle(e => e.PlayerId == V[3].PlayerId);
        state.Winner.Should().Be("Werewolves");

        var vAtFinalNight = state.Version;
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → GameOver

        var final = _gameService.GetGame(game.TournamentCode)!;
        final.Phase.Should().Be(Models.GamePhase.FinalScoresReveal);
        final.Status.Should().Be(Models.GameStatus.Ended);
        final.Winner.Should().Be("Werewolves");
        final.Version.Should().BeGreaterThan(vAtFinalNight);
    }

    // ── N-way tiebreaker tests ────────────────────────────────────────────

    [Fact]
    public void ThreeWayTie_ShouldIncludeAllThreeTiedPlayersInTiebreakCandidates()
    {
        // 3 players: Creator + Player1 + Player2 (MinPlayers = 3 by default)
        var game = CreateReadyGame(2);
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        // Advance to Discussion (Round 1: RoleReveal → WerewolvesMeeting → Discussion)
        MarkAllAliveDone(game.TournamentCode);  // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // WerewolvesMeeting → Discussion

        var state = _gameService.GetGame(game.TournamentCode)!;
        state.Phase.Should().Be(Models.GamePhase.Discussion);

        // Circular vote: A→B, B→C, C→A so every player receives exactly 1 vote
        var players = state.Players
            .Where(p => !p.IsEliminated)
            .OrderBy(p => p.PlayerId)
            .ToList();
        for (int i = 0; i < players.Count; i++)
            _gameService.CastVote(game.TournamentCode, players[i].PlayerId, players[(i + 1) % players.Count].PlayerId);

        MarkAllAliveDone(game.TournamentCode); // Discussion → TiebreakDiscussion

        var after = _gameService.GetGame(game.TournamentCode)!;
        after.Phase.Should().Be(Models.GamePhase.TiebreakDiscussion);
        after.TiebreakCandidates.Should().HaveCount(3,
            "all three players are tied with 1 vote each");
        after.TiebreakCandidates.Should().BeEquivalentTo(
            players.Select(p => p.PlayerId),
            "every tied player must be a tiebreak candidate");
    }

    [Fact]
    public void ThreeWayTie_TiebreakVote_ShouldAcceptVoteForAnyCandidate()
    {
        var game = CreateReadyGame(2);
        _gameService.StartGame(game.TournamentCode, game.CreatorId);
        MarkAllAliveDone(game.TournamentCode);
        MarkAliveWolvesDone(game.TournamentCode);

        var players = _gameService.GetGame(game.TournamentCode)!.Players
            .Where(p => !p.IsEliminated)
            .OrderBy(p => p.PlayerId)
            .ToList();

        // Set up circular tie
        for (int i = 0; i < players.Count; i++)
            _gameService.CastVote(game.TournamentCode, players[i].PlayerId, players[(i + 1) % players.Count].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → TiebreakDiscussion

        // Each player should be able to vote for any of the other two candidates
        var state = _gameService.GetGame(game.TournamentCode)!;
        state.TiebreakCandidates.Should().HaveCount(3);

        // Verify every player can vote for each of the other tied candidates
        foreach (var voter in players)
        {
            foreach (var candidate in players.Where(c => c.PlayerId != voter.PlayerId))
            {
                var (success, error) = _gameService.CastVote(game.TournamentCode, voter.PlayerId, candidate.PlayerId);
                success.Should().BeTrue(
                    $"player {voter.DisplayName} should be able to vote for tied candidate {candidate.DisplayName}, but got: {error}");
            }
        }
    }

    [Fact]
    public void FourWayCircularTie_ShouldIncludeAllFourTiedPlayersInTiebreakCandidates()
    {
        // 4 players: Creator + Player1 + Player2 + Player3
        var game = CreateReadyGame(3);
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        MarkAllAliveDone(game.TournamentCode);  // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // WerewolvesMeeting → Discussion

        var state = _gameService.GetGame(game.TournamentCode)!;
        var players = state.Players
            .Where(p => !p.IsEliminated)
            .OrderBy(p => p.PlayerId)
            .ToList();

        // 4-way circular vote: each player votes for the next → all get 1 vote
        for (int i = 0; i < players.Count; i++)
            _gameService.CastVote(game.TournamentCode, players[i].PlayerId, players[(i + 1) % players.Count].PlayerId);

        MarkAllAliveDone(game.TournamentCode); // → TiebreakDiscussion

        var after = _gameService.GetGame(game.TournamentCode)!;
        after.Phase.Should().Be(Models.GamePhase.TiebreakDiscussion);
        after.TiebreakCandidates.Should().HaveCount(4,
            "all four players are tied with 1 vote each");
        after.TiebreakCandidates.Should().BeEquivalentTo(
            players.Select(p => p.PlayerId),
            "every tied player must be a tiebreak candidate");
    }

    // ── Scoring tests ──────────────────────────────────────────────────────

    [Fact]
    public void CorrectDayVote_AwardsOnePoint()
    {
        var game = CreateReadyGame(3); // 4 players: 1W + 3V
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var started  = _gameService.GetGame(game.TournamentCode)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        // All 3 villagers vote for the werewolf (correct); werewolf votes for V[0] (wrong)
        foreach (var v in villagers)
            _gameService.CastVote(game.TournamentCode, v.PlayerId, werewolf.PlayerId);
        _gameService.CastVote(game.TournamentCode, werewolf.PlayerId, villagers[0].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal

        var result = _gameService.GetGame(game.TournamentCode)!;
        result.DayDeaths.Should().ContainSingle(e => e.PlayerId == werewolf.PlayerId);

        foreach (var v in villagers)
            result.Players.First(p => p.PlayerId == v.PlayerId).Score.Should().Be(1, "correct voter earns 1 point");
        result.Players.First(p => p.PlayerId == werewolf.PlayerId).Score.Should().Be(0, "wrong voter earns 0 points");
    }

    [Fact]
    public void TeamWin_AwardsEightPoints_ToWinningTeam()
    {
        var game = CreateReadyGame(2); // 3 players: 1W + 2V
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var started  = _gameService.GetGame(game.TournamentCode)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        // All villagers vote for wolf; wolf votes for V[0]
        foreach (var v in villagers)
            _gameService.CastVote(game.TournamentCode, v.PlayerId, werewolf.PlayerId);
        _gameService.CastVote(game.TournamentCode, werewolf.PlayerId, villagers[0].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal (Villagers win)

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → GameOver

        var result = _gameService.GetGame(game.TournamentCode)!;
        result.Phase.Should().Be(Models.GamePhase.FinalScoresReveal);
        result.Winner.Should().Be("Villagers");

        // Villagers: 1 point (correct vote) + 8 points (team win) = 9
        foreach (var v in villagers)
            result.Players.First(p => p.PlayerId == v.PlayerId).Score.Should().Be(9);
        // Werewolf: 0 (wrong vote) + 0 (losing team) = 0
        result.Players.First(p => p.PlayerId == werewolf.PlayerId).Score.Should().Be(0);
    }

    [Fact]
    public void EliminatedPlayerVote_DoesNotCountForElimination_ButAwardsPoints()
    {
        // Setup: 4 players (1W + 3V).
        // Round 1: eliminate V[0].
        // Round 2: V[0] (dead) votes for V[1].
        //          Alive votes: V[1]→W, V[2]→W, W→V[1].
        //          If dead vote counted: W=2, V[1]=2 → tie (no elimination).
        //          With only alive votes: W=2, V[1]=1 → W eliminated (correct result).
        var game = CreateReadyGame(3); // 4 players: 1W + 3V
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var started  = _gameService.GetGame(game.TournamentCode)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        // Round 1: V[1], V[2], W vote for V[0] → V[0] eliminated
        _gameService.CastVote(game.TournamentCode, villagers[1].PlayerId, villagers[0].PlayerId);
        _gameService.CastVote(game.TournamentCode, villagers[2].PlayerId, villagers[0].PlayerId);
        _gameService.CastVote(game.TournamentCode, werewolf.PlayerId,     villagers[0].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal

        _gameService.GetGame(game.TournamentCode)!.Players
            .First(p => p.PlayerId == villagers[0].PlayerId).IsEliminated.Should().BeTrue();

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → NightEliminationReveal
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → Discussion (round 2)

        // Round 2: dead V[0] votes for V[1] (this would cause a tie if counted)
        _gameService.CastVote(game.TournamentCode, villagers[0].PlayerId, villagers[1].PlayerId);
        _gameService.CastVote(game.TournamentCode, villagers[1].PlayerId, werewolf.PlayerId);
        _gameService.CastVote(game.TournamentCode, villagers[2].PlayerId, werewolf.PlayerId);
        _gameService.CastVote(game.TournamentCode, werewolf.PlayerId,     villagers[1].PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal

        var result = _gameService.GetGame(game.TournamentCode)!;

        // W should be eliminated (dead vote did not create a tie)
        result.DayDeaths.Should().ContainSingle(e => e.PlayerId == werewolf.PlayerId,
            "eliminated player's vote must not count toward the tally");

        // V[0] voted for V[1] but W was eliminated → 0 correct-vote points in round 2
        // (V[0] had no vote in round 1)  → total 0
        result.Players.First(p => p.PlayerId == villagers[0].PlayerId).Score.Should().Be(0);
        // V[1] and V[2] voted correctly in both rounds → 2 points each from voting
        result.Players.First(p => p.PlayerId == villagers[1].PlayerId).Score.Should().Be(2);
        result.Players.First(p => p.PlayerId == villagers[2].PlayerId).Score.Should().Be(2);
    }

    // ── Skill mechanics tests ──────────────────────────────────────────────

    [Fact]
    public void TwoGamesInRow_TournamentCodePersists_PlayersAndStatusResetCorrectly()
    {
        // 3 players (1W + 2V), no special skills so the game ends as fast as possible.
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.JoinGame(game.TournamentCode, "Bob");
        _gameService.UpdateEnabledSkills(game.TournamentCode, new List<string>(), game.CreatorId);

        var tc = game.TournamentCode;

        // ── GAME 1 ──────────────────────────────────────────────────────────
        _gameService.StartGame(tc, game.CreatorId);

        var g1 = _gameService.GetGame(tc)!;
        g1.Status.Should().Be(Models.GameStatus.InProgress);

        var wolf1  = g1.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var vills1 = g1.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(tc);    // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(tc); // WerewolvesMeeting → Discussion

        // Villagers vote out the werewolf on the first day
        foreach (var v in vills1)
            _gameService.CastVote(tc, v.PlayerId, wolf1.PlayerId);
        _gameService.CastVote(tc, wolf1.PlayerId, vills1[0].PlayerId);
        MarkAllAliveDone(tc); // Discussion → DayEliminationReveal (Villagers win)

        var afterElim1 = _gameService.GetGame(tc)!;
        afterElim1.Phase.Should().Be(Models.GamePhase.DayEliminationReveal);
        afterElim1.Winner.Should().Be("Villagers");

        _gameService.ForceAdvancePhase(tc, game.CreatorId); // DayEliminationReveal → GameOver

        var gameOver1 = _gameService.GetGame(tc)!;
        gameOver1.Phase.Should().Be(Models.GamePhase.FinalScoresReveal);
        gameOver1.Status.Should().Be(Models.GameStatus.Ended);
        gameOver1.PhaseEndsAt.Should().NotBeNull("1-minute auto-reset timer must be set");

        var g1GameId = gameOver1.GameId;

        // Alive players press "Done — back to lobby"
        foreach (var p in gameOver1.Players.Where(p => !p.IsEliminated && p.ParticipationStatus == Models.ParticipationStatus.Participating))
            _gameService.MarkDone(tc, p.PlayerId).Success.Should().BeTrue();

        // Fast-forward the auto-reset timer and fire the poll check
        gameOver1.PhaseEndsAt = DateTime.UtcNow.AddSeconds(-1);
        _gameService.TryAdvancePhaseIfExpired(tc);

        // ── Between games: back in lobby ────────────────────────────────────
        var lobby = _gameService.GetGame(tc)!;
        lobby.TournamentCode.Should().Be(tc, "tournament code must survive across games");
        lobby.GameId.Should().NotBe(g1GameId, "a new per-game ID must be issued for the second game");
        lobby.Status.Should().Be(Models.GameStatus.ReadyToStart, "3 players are still present");
        lobby.Winner.Should().BeNull();
        lobby.Phase.Should().Be(Models.GamePhase.RoleReveal);
        lobby.Players.Should().HaveCount(3);
        lobby.Players.Should().AllSatisfy(p =>
        {
            p.Role.Should().BeNull("roles must be cleared between games");
            p.IsEliminated.Should().BeFalse("elimination must be reset");
            p.IsDone.Should().BeFalse("done flag must be reset");
            p.Score.Should().Be(0, "scores must be reset");
        });

        // ── GAME 2 ──────────────────────────────────────────────────────────
        lobby.IsPremium = true; // bypass premium gate in test
        _gameService.StartGame(tc, game.CreatorId);

        var g2 = _gameService.GetGame(tc)!;
        g2.Status.Should().Be(Models.GameStatus.InProgress);
        g2.Players.Should().HaveCount(3, "same 3 players carry over");
        g2.TournamentCode.Should().Be(tc);

        var wolf2  = g2.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var vills2 = g2.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(tc);    // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(tc); // WerewolvesMeeting → Discussion

        // Villagers vote out the werewolf again
        foreach (var v in vills2)
            _gameService.CastVote(tc, v.PlayerId, wolf2.PlayerId);
        _gameService.CastVote(tc, wolf2.PlayerId, vills2[0].PlayerId);
        MarkAllAliveDone(tc); // Discussion → DayEliminationReveal (Villagers win)

        var afterElim2 = _gameService.GetGame(tc)!;
        afterElim2.Phase.Should().Be(Models.GamePhase.DayEliminationReveal);
        afterElim2.Winner.Should().Be("Villagers");

        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → GameOver

        var gameOver2 = _gameService.GetGame(tc)!;
        gameOver2.Phase.Should().Be(Models.GamePhase.FinalScoresReveal);
        gameOver2.Status.Should().Be(Models.GameStatus.Ended);
        gameOver2.TournamentCode.Should().Be(tc, "tournament code survives both games");
        gameOver2.GameId.Should().NotBe(g1GameId, "game 2 has its own per-game ID");
    }

    // ── Tournament multi-game tests ────────────────────────────────────────

    [Fact]
    public void TwoGamesInRow_WithSkills_SkillStateIsResetBetweenGames()
    {
        // Verify that WitchHealUsed and Lover1Id/Lover2Id are cleared after a reset.
        // If those fields survived, game 2 would be silently broken (witch can't heal;
        // lover cascade fires on the wrong players).
        //
        // Game plan (4 players: wolf, cupid/V, witch/V, extra/V):
        //   Lovers: wolf + witch. Day 1: eliminate extra. Night 2: wolf kills cupid,
        //   witch saves (WitchHealUsed=true). Day 2: village votes wolf out →
        //   lover cascade kills witch → Villagers win → GameOver.
        var game = CreateReadyGameWithSkills(3, new List<string> { "Witch", "Cupid" }); // 4 players
        _gameService.StartGame(game.TournamentCode, game.CreatorId);
        var tc = game.TournamentCode;

        var state = _gameService.GetGame(tc)!;
        var wolf  = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var cupid = state.Players.First(p => p.Skill == Models.PlayerSkill.Cupid);
        var witch = state.Players.First(p => p.Skill == Models.PlayerSkill.Witch);
        var extra = state.Players.First(p => p.Role == Models.PlayerRole.Villager && p.Skill == Models.PlayerSkill.None);

        // RoleReveal → WerewolvesMeeting → CupidTurn
        MarkAllAliveDone(tc);
        MarkAliveWolvesDone(tc);

        // Cupid links wolf + witch as lovers → LoverReveal → Discussion
        _gameService.CupidAction(tc, cupid.PlayerId, wolf.PlayerId, witch.PlayerId);
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // LoverReveal → Discussion

        // Day 1: everyone votes to eliminate extra
        foreach (var p in state.Players.Where(p => p.PlayerId != extra.PlayerId))
            _gameService.CastVote(tc, p.PlayerId, extra.PlayerId);
        MarkAllAliveDone(tc); // → DayEliminationReveal (extra out)
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → WerewolvesTurn round 2

        // Night 2: wolf kills cupid; witch saves cupid (WitchHealUsed = true)
        _gameService.CastVote(tc, wolf.PlayerId, cupid.PlayerId);
        // CastVote auto-advances to WitchTurn when all wolves have voted
        _gameService.WitchAction(tc, witch.PlayerId, "save", null); // → NightEliminationReveal (cupid saved)
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → Discussion round 2

        // Sanity: skill state is active before reset
        var beforeReset = _gameService.GetGame(tc)!;
        beforeReset.WitchHealUsed.Should().BeTrue("sanity: heal was used in night 2");
        beforeReset.Lover1Id.Should().NotBeNull("sanity: lovers were set by cupid");

        // Day 2: cupid + witch vote wolf; wolf votes cupid
        // Wolf eliminated → lover cascade kills witch → Villagers win
        _gameService.CastVote(tc, cupid.PlayerId, wolf.PlayerId);
        _gameService.CastVote(tc, witch.PlayerId, wolf.PlayerId);
        _gameService.CastVote(tc, wolf.PlayerId, cupid.PlayerId);
        MarkAllAliveDone(tc); // → DayEliminationReveal (wolf + witch out, Villagers win)
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → GameOver

        var gameOver = _gameService.GetGame(tc)!;
        gameOver.Phase.Should().Be(Models.GamePhase.FinalScoresReveal);

        // Trigger reset
        gameOver.PhaseEndsAt = DateTime.UtcNow.AddSeconds(-1);
        _gameService.TryAdvancePhaseIfExpired(tc);

        var lobby = _gameService.GetGame(tc)!;
        lobby.WitchHealUsed.Should().BeFalse("WitchHealUsed must be cleared for game 2");
        lobby.WitchPoisonUsed.Should().BeFalse("WitchPoisonUsed must be cleared for game 2");
        lobby.Lover1Id.Should().BeNull("Lover1Id must be cleared — lover cascade must not fire in game 2");
        lobby.Lover2Id.Should().BeNull("Lover2Id must be cleared — lover cascade must not fire in game 2");
        lobby.NightKillTargetId.Should().BeNull();
        lobby.HunterMustShoot.Should().BeFalse();
    }

    [Fact]
    public void TwoGamesInRow_TotalScore_IsPreservedBetweenGames()
    {
        // After game 1 ends and the lobby resets, TotalScore must equal what Score was at
        // the end of game 1, and Score itself must be zeroed.
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.JoinGame(game.TournamentCode, "Bob");
        _gameService.UpdateEnabledSkills(game.TournamentCode, new List<string>(), game.CreatorId);
        var tc = game.TournamentCode;

        _gameService.StartGame(tc, game.CreatorId);
        var g1 = _gameService.GetGame(tc)!;
        var wolf = g1.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var vills = g1.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(tc);    // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(tc); // WerewolvesMeeting → Discussion
        foreach (var v in vills)
            _gameService.CastVote(tc, v.PlayerId, wolf.PlayerId);
        _gameService.CastVote(tc, wolf.PlayerId, vills[0].PlayerId);
        MarkAllAliveDone(tc);    // → DayEliminationReveal (Villagers win)
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → GameOver

        var gameOver = _gameService.GetGame(tc)!;
        var scoresGameOne = gameOver.Players.ToDictionary(p => p.PlayerId, p => p.Score);

        gameOver.PhaseEndsAt = DateTime.UtcNow.AddSeconds(-1);
        _gameService.TryAdvancePhaseIfExpired(tc);

        var lobby = _gameService.GetGame(tc)!;
        foreach (var p in lobby.Players)
        {
            p.TotalScore.Should().Be(scoresGameOne[p.PlayerId],
                $"{p.DisplayName}: TotalScore must equal game 1 score after reset");
            p.Score.Should().Be(0, $"{p.DisplayName}: per-game Score must be zeroed after reset");
        }
    }

    [Fact]
    public void TwoGamesInRow_TotalScore_AccumulatesAcrossGames()
    {
        // TotalScore must equal the sum of Score earned in both games.
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.JoinGame(game.TournamentCode, "Bob");
        _gameService.UpdateEnabledSkills(game.TournamentCode, new List<string>(), game.CreatorId);
        var tc = game.TournamentCode;

        // ── GAME 1 ──────────────────────────────────────────────────────────
        _gameService.StartGame(tc, game.CreatorId);
        var g1 = _gameService.GetGame(tc)!;
        var wolf1 = g1.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var vills1 = g1.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(tc);
        MarkAliveWolvesDone(tc);
        foreach (var v in vills1)
            _gameService.CastVote(tc, v.PlayerId, wolf1.PlayerId);
        _gameService.CastVote(tc, wolf1.PlayerId, vills1[0].PlayerId);
        MarkAllAliveDone(tc);
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → GameOver

        var afterGame1 = _gameService.GetGame(tc)!;
        var scoresGame1 = afterGame1.Players.ToDictionary(p => p.PlayerId, p => p.Score);

        afterGame1.PhaseEndsAt = DateTime.UtcNow.AddSeconds(-1);
        _gameService.TryAdvancePhaseIfExpired(tc);

        // ── GAME 2 ──────────────────────────────────────────────────────────
        _gameService.GetGame(tc)!.IsPremium = true; // bypass premium gate in test
        _gameService.StartGame(tc, game.CreatorId);
        var g2 = _gameService.GetGame(tc)!;
        var wolf2 = g2.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var vills2 = g2.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(tc);
        MarkAliveWolvesDone(tc);
        foreach (var v in vills2)
            _gameService.CastVote(tc, v.PlayerId, wolf2.PlayerId);
        _gameService.CastVote(tc, wolf2.PlayerId, vills2[0].PlayerId);
        MarkAllAliveDone(tc);
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → GameOver

        var afterGame2 = _gameService.GetGame(tc)!;
        foreach (var p in afterGame2.Players)
        {
            var expectedTotal = scoresGame1[p.PlayerId] + p.Score;
            p.TotalScore.Should().Be(expectedTotal,
                $"{p.DisplayName}: TotalScore must be the sum of scores from both games");
        }
    }

    [Fact]
    public void TwoGamesInRow_GameIndex_IncrementsAfterReset()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.JoinGame(game.TournamentCode, "Bob");
        _gameService.UpdateEnabledSkills(game.TournamentCode, new List<string>(), game.CreatorId);
        var tc = game.TournamentCode;

        game.GameIndex.Should().Be(1);

        _gameService.StartGame(tc, game.CreatorId);
        var g1 = _gameService.GetGame(tc)!;
        var wolf = g1.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var vills = g1.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(tc);
        MarkAliveWolvesDone(tc);
        foreach (var v in vills)
            _gameService.CastVote(tc, v.PlayerId, wolf.PlayerId);
        _gameService.CastVote(tc, wolf.PlayerId, vills[0].PlayerId);
        MarkAllAliveDone(tc);
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → GameOver

        var gameOver = _gameService.GetGame(tc)!;
        gameOver.PhaseEndsAt = DateTime.UtcNow.AddSeconds(-1);
        _gameService.TryAdvancePhaseIfExpired(tc);

        _gameService.GetGame(tc)!.GameIndex.Should().Be(2, "GameIndex must increment after the first reset");
    }

    [Fact]
    public void TwoGamesInRow_TournamentId_IsPreservedAfterReset()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.JoinGame(game.TournamentCode, "Bob");
        _gameService.UpdateEnabledSkills(game.TournamentCode, new List<string>(), game.CreatorId);
        var tc = game.TournamentCode;
        var originalTournamentId = _gameService.GetGame(tc)!.TournamentId;

        _gameService.StartGame(tc, game.CreatorId);
        var g1 = _gameService.GetGame(tc)!;
        var wolf = g1.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var vills = g1.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(tc);
        MarkAliveWolvesDone(tc);
        foreach (var v in vills)
            _gameService.CastVote(tc, v.PlayerId, wolf.PlayerId);
        _gameService.CastVote(tc, wolf.PlayerId, vills[0].PlayerId);
        MarkAllAliveDone(tc);
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → GameOver

        var gameOver = _gameService.GetGame(tc)!;
        gameOver.PhaseEndsAt = DateTime.UtcNow.AddSeconds(-1);
        _gameService.TryAdvancePhaseIfExpired(tc);

        _gameService.GetGame(tc)!.TournamentId.Should().Be(originalTournamentId,
            "TournamentId must survive across game resets so DB records link to the same tournament");
    }

    [Fact]
    public void TwoGamesInRow_PlayerWhoLeft_RemainsExcludedInGame2()
    {
        // 4 players so the game is still valid after one leaves (3 remain, meeting MinPlayers).
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Alice");
        var (_, _, bob) = _gameService.JoinGame(game.TournamentCode, "Bob");
        _gameService.JoinGame(game.TournamentCode, "Charlie");
        _gameService.UpdateEnabledSkills(game.TournamentCode, new List<string>(), game.CreatorId);
        var tc = game.TournamentCode;

        _gameService.LeaveGame(tc, bob!.PlayerId);

        _gameService.StartGame(tc, game.CreatorId);
        var g1 = _gameService.GetGame(tc)!;
        g1.Players.First(p => p.PlayerId == bob.PlayerId).Role.Should().BeNull("Left player must not be assigned a role");

        var wolf = g1.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var vills = g1.Players.Where(p => p.Role == Models.PlayerRole.Villager
            && p.ParticipationStatus == Models.ParticipationStatus.Participating).ToList();

        MarkAllAliveDone(tc);
        MarkAliveWolvesDone(tc);
        foreach (var v in vills)
            _gameService.CastVote(tc, v.PlayerId, wolf.PlayerId);
        _gameService.CastVote(tc, wolf.PlayerId, vills[0].PlayerId);
        MarkAllAliveDone(tc);
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → GameOver

        var gameOver = _gameService.GetGame(tc)!;
        gameOver.PhaseEndsAt = DateTime.UtcNow.AddSeconds(-1);
        _gameService.TryAdvancePhaseIfExpired(tc);

        var lobby = _gameService.GetGame(tc)!;
        var bobAfterReset = lobby.Players.First(p => p.PlayerId == bob.PlayerId);
        bobAfterReset.ParticipationStatus.Should().Be(Models.ParticipationStatus.Left,
            "Left status must survive the game reset");
        bobAfterReset.Role.Should().BeNull("Left player must not receive a role after reset");

        // Game 2 — Left player must still be excluded
        _gameService.StartGame(tc, game.CreatorId);
        var g2 = _gameService.GetGame(tc)!;
        g2.Players.First(p => p.PlayerId == bob.PlayerId).Role.Should().BeNull(
            "Left player must not be assigned a role in game 2");
    }

    [Fact]
    public void TwoGamesInRow_AllParticipatingPlayersDone_TriggersEarlyReset()
    {
        // When every participating player (alive and eliminated) marks done during GameOver,
        // the reset must fire immediately without waiting for the timer.
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.TournamentCode, "Alice");
        _gameService.JoinGame(game.TournamentCode, "Bob");
        _gameService.UpdateEnabledSkills(game.TournamentCode, new List<string>(), game.CreatorId);
        var tc = game.TournamentCode;

        _gameService.StartGame(tc, game.CreatorId);
        var g1 = _gameService.GetGame(tc)!;
        var wolf = g1.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var vills = g1.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(tc);
        MarkAliveWolvesDone(tc);
        foreach (var v in vills)
            _gameService.CastVote(tc, v.PlayerId, wolf.PlayerId);
        _gameService.CastVote(tc, wolf.PlayerId, vills[0].PlayerId);
        MarkAllAliveDone(tc);
        _gameService.ForceAdvancePhase(tc, game.CreatorId); // → GameOver

        var gameOver = _gameService.GetGame(tc)!;
        gameOver.Phase.Should().Be(Models.GamePhase.FinalScoresReveal);

        // All participating players mark done (including the eliminated wolf) — no timer manipulation
        foreach (var p in gameOver.Players.Where(p => p.ParticipationStatus == Models.ParticipationStatus.Participating))
            _gameService.MarkDone(tc, p.PlayerId);

        var afterDone = _gameService.GetGame(tc)!;
        afterDone.Phase.Should().NotBe(Models.GamePhase.FinalScoresReveal,
            "early reset must fire as soon as all participating players are done, without waiting for the timer");
        afterDone.Status.Should().Be(Models.GameStatus.ReadyToStart);
        afterDone.Winner.Should().BeNull("reset must clear the winner");
    }

    private Models.GameState CreateReadyGameWithSkills(int extraPlayers, List<string> skills)
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        for (int i = 0; i < extraPlayers; i++)
            _gameService.JoinGame(game.TournamentCode, $"Player{i + 1}");
        _gameService.UpdateEnabledSkills(game.TournamentCode, skills, game.CreatorId);
        return _gameService.GetGame(game.TournamentCode)!;
    }

    /// <summary>
    /// Natural mechanism to advance RoleReveal or Discussion/TiebreakDiscussion:
    /// every alive player presses "I'm done".
    /// </summary>
    private void MarkAllAliveDone(string gameId)
    {
        var alive = _gameService.GetGame(gameId)!.Players
            .Where(p => !p.IsEliminated && p.ParticipationStatus == Models.ParticipationStatus.Participating)
            .ToList();
        foreach (var p in alive)
            _gameService.MarkDone(gameId, p.PlayerId);
    }

    /// <summary>
    /// Natural mechanism to advance WerewolvesMeeting:
    /// every alive werewolf presses "I'm done".
    /// </summary>
    private void MarkAliveWolvesDone(string gameId)
    {
        var wolves = _gameService.GetGame(gameId)!.Players
            .Where(p => p.Role == Models.PlayerRole.Werewolf && !p.IsEliminated && p.ParticipationStatus == Models.ParticipationStatus.Participating)
            .ToList();
        foreach (var p in wolves)
            _gameService.MarkDone(gameId, p.PlayerId);
    }

    [Fact]
    public void StartGame_ShouldAssignSkillsToVillagers()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Seer", "Witch" }); // 5 players: 1W + 4V
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var updated  = _gameService.GetGame(game.TournamentCode)!;
        var villagers = updated.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        villagers.Count(p => p.Skill == Models.PlayerSkill.Seer).Should().Be(1);
        villagers.Count(p => p.Skill == Models.PlayerSkill.Witch).Should().Be(1);
        updated.Players.Where(p => p.Role == Models.PlayerRole.Werewolf)
            .Should().OnlyContain(p => p.Skill == Models.PlayerSkill.None,
            "werewolves never get skills");
    }

    [Fact]
    public void CupidAction_SetsLoversAndAdvancesToLoverReveal()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Cupid" });
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var state = _gameService.GetGame(game.TournamentCode)!;
        state.Phase.Should().Be(Models.GamePhase.RoleReveal);

        MarkAllAliveDone(game.TournamentCode); // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // WerewolvesMeeting → CupidTurn
        state = _gameService.GetGame(game.TournamentCode)!;
        state.Phase.Should().Be(Models.GamePhase.CupidTurn);

        var cupid = state.Players.First(p => p.Skill == Models.PlayerSkill.Cupid);
        var others = state.Players.Where(p => p.PlayerId != cupid.PlayerId).Take(2).ToList();

        var (success, error) = _gameService.CupidAction(game.TournamentCode, cupid.PlayerId, others[0].PlayerId, others[1].PlayerId);

        success.Should().BeTrue();
        var updated = _gameService.GetGame(game.TournamentCode)!;
        updated.Phase.Should().Be(Models.GamePhase.LoverReveal);
        updated.PhaseEndsAt.Should().NotBeNull();
        updated.Lover1Id.Should().Be(others[0].PlayerId);
        updated.Lover2Id.Should().Be(others[1].PlayerId);
    }

    [Fact]
    public void CupidAction_SameLover_ShouldFail()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Cupid" });
        _gameService.StartGame(game.TournamentCode, game.CreatorId);
        MarkAllAliveDone(game.TournamentCode); // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // WerewolvesMeeting → CupidTurn

        var state = _gameService.GetGame(game.TournamentCode)!;
        var cupid = state.Players.First(p => p.Skill == Models.PlayerSkill.Cupid);

        var (success, error) = _gameService.CupidAction(game.TournamentCode, cupid.PlayerId, cupid.PlayerId, cupid.PlayerId);

        success.Should().BeFalse();
    }

    [Fact]
    public void CupidSkip_WhenCupidDoesNotAct_SkipsLoverReveal()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Cupid" });
        _gameService.StartGame(game.TournamentCode, game.CreatorId);
        MarkAllAliveDone(game.TournamentCode); // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // WerewolvesMeeting → CupidTurn
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // skip CupidTurn without action

        // No lovers chosen → should skip LoverReveal and go to Discussion
        _gameService.GetGame(game.TournamentCode)!.Phase.Should().Be(Models.GamePhase.Discussion);
    }

    [Fact]
    public void SeerAction_ReturnsTrueRevealingTargetRoleAndSkill()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Seer" }); // 5 players, 1W, 4V (Seer + 3 plain)
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var state = _gameService.GetGame(game.TournamentCode)!;
        // Get to WerewolvesTurn round 2
        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        // Eliminate a villager to reach round 2
        var wolf    = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = state.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        var seer    = state.Players.First(p => p.Skill == Models.PlayerSkill.Seer);
        var toElim  = villagers.First(p => p.Skill == Models.PlayerSkill.None && p.PlayerId != seer.PlayerId);

        foreach (var v in state.Players.Where(p => p.PlayerId != toElim.PlayerId))
            _gameService.CastVote(game.TournamentCode, v.PlayerId, toElim.PlayerId);
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn round 2

        // Wolf votes
        _gameService.CastVote(game.TournamentCode, wolf.PlayerId, villagers.First(p => p.PlayerId != toElim.PlayerId && p.PlayerId != seer.PlayerId).PlayerId);
        // CastVote auto-advances to SeerTurn when all wolves have voted

        _gameService.GetGame(game.TournamentCode)!.Phase.Should().Be(Models.GamePhase.SeerTurn);

        // Seer inspects the wolf
        var (success, error, result) = _gameService.SeerAction(game.TournamentCode, seer.PlayerId, wolf.PlayerId);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.IsWerewolf.Should().BeTrue();
        result.Skill.Should().BeNull(); // wolf has no special skill
    }

    [Fact]
    public void WitchAction_SavesNightKillTarget()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Witch" });
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var state = _gameService.GetGame(game.TournamentCode)!;
        var wolf  = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var witch = state.Players.First(p => p.Skill == Models.PlayerSkill.Witch);
        var victim = state.Players.First(p => p.Role == Models.PlayerRole.Villager && p.PlayerId != witch.PlayerId);

        // Get to WerewolvesTurn round 2
        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion

        // Survive round 1 discussion without elimination: all players mark done, no winner
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal (no votes)
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn round 2

        // Wolf votes for victim
        _gameService.CastVote(game.TournamentCode, wolf.PlayerId, victim.PlayerId);
        // CastVote auto-advances to WitchTurn when all wolves have voted

        _gameService.GetGame(game.TournamentCode)!.Phase.Should().Be(Models.GamePhase.WitchTurn);
        _gameService.GetGame(game.TournamentCode)!.NightKillTargetId.Should().Be(victim.PlayerId);

        // Witch saves the victim
        var (success, error) = _gameService.WitchAction(game.TournamentCode, witch.PlayerId, "save", null);

        success.Should().BeTrue();
        var updated = _gameService.GetGame(game.TournamentCode)!;
        updated.Phase.Should().Be(Models.GamePhase.NightEliminationReveal);
        updated.NightDeaths.Should().BeEmpty("witch saved the victim");
        updated.WitchHealUsed.Should().BeTrue();

        // Victim should NOT be eliminated
        updated.Players.First(p => p.PlayerId == victim.PlayerId).IsEliminated.Should().BeFalse();
    }

    [Fact]
    public void WitchAction_PoisonKillsTarget()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Witch" });
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var state = _gameService.GetGame(game.TournamentCode)!;
        var wolf  = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var witch = state.Players.First(p => p.Skill == Models.PlayerSkill.Witch);
        var villagers = state.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        var poisonTarget = villagers.First(p => p.PlayerId != witch.PlayerId);

        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal (no kill)
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn round 2

        // Wolf votes anyone else
        var wolfVictim = villagers.First(p => p.PlayerId != witch.PlayerId && p.PlayerId != poisonTarget.PlayerId);
        _gameService.CastVote(game.TournamentCode, wolf.PlayerId, wolfVictim.PlayerId);
        // CastVote auto-advances to WitchTurn when all wolves have voted

        // Witch poisons a different target
        var (success, _) = _gameService.WitchAction(game.TournamentCode, witch.PlayerId, "poison", poisonTarget.PlayerId);

        success.Should().BeTrue();
        var updated = _gameService.GetGame(game.TournamentCode)!;
        updated.NightDeaths.Should().Contain(e => e.PlayerId == poisonTarget.PlayerId &&
            e.Cause == Models.EliminationCause.WitchPoison);
        updated.Players.First(p => p.PlayerId == poisonTarget.PlayerId).IsEliminated.Should().BeTrue();
    }

    [Fact]
    public void HunterAction_ShootsTargetWhenEliminated()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Hunter" });
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var state  = _gameService.GetGame(game.TournamentCode)!;
        var wolf   = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var hunter = state.Players.First(p => p.Skill == Models.PlayerSkill.Hunter);
        var bystander = state.Players.First(p => p.Role == Models.PlayerRole.Villager && p.Skill == Models.PlayerSkill.None);

        // Get to WerewolvesTurn round 2
        MarkAllAliveDone(game.TournamentCode); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // → Discussion
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal (no kill)
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn round 2

        // Wolf kills the Hunter
        _gameService.CastVote(game.TournamentCode, wolf.PlayerId, hunter.PlayerId);
        // CastVote auto-advances to NightEliminationReveal when all wolves have voted

        var afterNight = _gameService.GetGame(game.TournamentCode)!;
        afterNight.Phase.Should().Be(Models.GamePhase.NightEliminationReveal);
        afterNight.HunterMustShoot.Should().BeTrue();

        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → HunterTurn

        var inHunterTurn = _gameService.GetGame(game.TournamentCode)!;
        inHunterTurn.Phase.Should().Be(Models.GamePhase.HunterTurn);

        // Hunter shoots the bystander
        var (success, error) = _gameService.HunterAction(game.TournamentCode, hunter.PlayerId, bystander.PlayerId);

        success.Should().BeTrue();
        var final = _gameService.GetGame(game.TournamentCode)!;
        final.Phase.Should().Be(Models.GamePhase.Discussion);
        final.NightDeaths.Should().Contain(e => e.PlayerId == bystander.PlayerId &&
            e.Cause == Models.EliminationCause.HunterShot);
        final.Players.First(p => p.PlayerId == bystander.PlayerId).IsEliminated.Should().BeTrue();
    }

    [Fact]
    public void LoversWin_WhenOnlyTwoLoversRemain()
    {
        // 4 players: Creator (wolf), Player1 (villager/cupid), Player2 (villager), Player3 (villager)
        var game = CreateReadyGameWithSkills(3, new List<string> { "Cupid" });
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var state = _gameService.GetGame(game.TournamentCode)!;
        var wolf  = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var cupid = state.Players.First(p => p.Skill == Models.PlayerSkill.Cupid);
        var others = state.Players.Where(p => p.PlayerId != cupid.PlayerId).ToList();

        MarkAllAliveDone(game.TournamentCode); // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // WerewolvesMeeting → CupidTurn

        // Cupid links wolf and one villager as lovers
        var lover1 = wolf;
        var lover2 = others.First(p => p.Role == Models.PlayerRole.Villager);
        _gameService.CupidAction(game.TournamentCode, cupid.PlayerId, lover1.PlayerId, lover2.PlayerId);

        // → LoverReveal → Discussion
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // LoverReveal → Discussion

        // Eliminate everyone except the 2 lovers
        var toElims = state.Players.Where(p => p.PlayerId != lover1.PlayerId && p.PlayerId != lover2.PlayerId).ToList();
        foreach (var target in toElims)
        {
            // vote out each non-lover one by one (force advance after each round)
            var alive = _gameService.GetGame(game.TournamentCode)!.Players.Where(p => !p.IsEliminated && p.ParticipationStatus == Models.ParticipationStatus.Participating).ToList();
            if (alive.Count <= 2) break;
            foreach (var voter in alive.Where(p => p.PlayerId != target.PlayerId))
                _gameService.CastVote(game.TournamentCode, voter.PlayerId, target.PlayerId);
            MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal
            _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn or Discussion
            // If we hit WerewolvesTurn, advance past it
            var g2 = _gameService.GetGame(game.TournamentCode)!;
            if (g2.Phase == Models.GamePhase.WerewolvesTurn)
            {
                _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → NightEliminationReveal (no kill)
                _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → Discussion
            }
        }

        var finalState = _gameService.GetGame(game.TournamentCode)!;
        finalState.Winner.Should().Be("Lovers");
    }

    [Fact]
    public void LoverCascade_KillingOneLoverAlsoKillsOther()
    {
        var game = CreateReadyGameWithSkills(3, new List<string> { "Cupid" }); // 4 players: 1W + 3V
        _gameService.StartGame(game.TournamentCode, game.CreatorId);

        var state = _gameService.GetGame(game.TournamentCode)!;
        var wolf  = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var cupid = state.Players.First(p => p.Skill == Models.PlayerSkill.Cupid);
        var villagers = state.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        var loverVillager1 = villagers[0];
        var loverVillager2 = villagers[1];

        MarkAllAliveDone(game.TournamentCode); // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.TournamentCode); // WerewolvesMeeting → CupidTurn
        // Link two villagers as lovers
        _gameService.CupidAction(game.TournamentCode, cupid.PlayerId, loverVillager1.PlayerId, loverVillager2.PlayerId);
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // LoverReveal → Discussion
        MarkAllAliveDone(game.TournamentCode); // → DayEliminationReveal (no kill)
        _gameService.ForceAdvancePhase(game.TournamentCode, game.CreatorId); // → WerewolvesTurn round 2

        // Wolf kills loverVillager1 — loverVillager2 should die in cascade
        _gameService.CastVote(game.TournamentCode, wolf.PlayerId, loverVillager1.PlayerId);
        // CastVote auto-advances to NightEliminationReveal when all wolves have voted

        var final = _gameService.GetGame(game.TournamentCode)!;
        final.NightDeaths.Should().Contain(e => e.PlayerId == loverVillager1.PlayerId);
        final.NightDeaths.Should().Contain(e => e.PlayerId == loverVillager2.PlayerId &&
            e.Cause == Models.EliminationCause.LoverDeath);
        final.Players.First(p => p.PlayerId == loverVillager2.PlayerId).IsEliminated.Should().BeTrue();
    }
}

