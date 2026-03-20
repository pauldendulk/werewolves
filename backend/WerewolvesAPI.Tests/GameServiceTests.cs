using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WerewolvesAPI.Services;

namespace WerewolvesAPI.Tests;

public class GameServiceTests
{
    private readonly GameService _gameService;
    private readonly Mock<ILogger<GameService>> _loggerMock;

    public GameServiceTests()
    {
        _loggerMock = new Mock<ILogger<GameService>>();
        _gameService = new GameService(_loggerMock.Object);
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

        var (success, message, player) = _gameService.JoinGame(game.GameId, "Alice");

        success.Should().BeTrue();
        player.Should().NotBeNull();
        player!.DisplayName.Should().Be("Alice");
        player.IsCreator.Should().BeFalse();
        _gameService.GetGame(game.GameId)!.Players.Should().HaveCount(2);
    }

    [Fact]
    public void JoinGame_WhenGameFull_ShouldReturnFailure()
    {
        var game = _gameService.CreateGame("Creator", 2, "http://localhost");
        _gameService.JoinGame(game.GameId, "Player1");

        var (success, message, player) = _gameService.JoinGame(game.GameId, "Player2");

        success.Should().BeFalse();
        message.Should().Contain("full");
        player.Should().BeNull();
    }

    [Fact]
    public void JoinGame_WhenPlayerRejoins_ShouldUpdateStatus()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");
        _gameService.LeaveGame(game.GameId, player!.PlayerId);

        var (success, _, rejoinedPlayer) = _gameService.JoinGame(game.GameId, "Alice", player.PlayerId);

        success.Should().BeTrue();
        rejoinedPlayer!.PlayerId.Should().Be(player.PlayerId);
        rejoinedPlayer.IsConnected.Should().BeTrue();
        rejoinedPlayer.ParticipationStatus.Should().Be(Models.ParticipationStatus.Participating);
    }

    [Fact]
    public void LeaveGame_ShouldUpdatePlayerStatus()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");

        _gameService.LeaveGame(game.GameId, player!.PlayerId);

        var leftPlayer = _gameService.GetGame(game.GameId)!.Players.First(p => p.PlayerId == player.PlayerId);
        leftPlayer.ParticipationStatus.Should().Be(Models.ParticipationStatus.Left);
    }

    [Fact]
    public void RemovePlayer_ByModerator_ShouldUpdatePlayerStatus()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");

        _gameService.RemovePlayer(game.GameId, player!.PlayerId, game.CreatorId);

        _gameService.GetGame(game.GameId)!.Players.First(p => p.PlayerId == player.PlayerId)
            .ParticipationStatus.Should().Be(Models.ParticipationStatus.Removed);
    }

    [Fact]
    public void UpdateMaxPlayers_ByCreator_ShouldUpdateSettings()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");

        _gameService.UpdateMaxPlayers(game.GameId, 30, game.CreatorId).Should().BeTrue();
        _gameService.GetGame(game.GameId)!.MaxPlayers.Should().Be(30);
    }

    [Fact]
    public void UpdateMaxPlayers_ByNonCreator_ShouldFail()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");

        _gameService.UpdateMaxPlayers(game.GameId, 30, player!.PlayerId).Should().BeFalse();
    }

    [Fact]
    public void UpdateEnabledSkills_ByCreator_ShouldUpdateSkills()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");

        _gameService.UpdateEnabledSkills(game.GameId, new List<string> { "Seer", "Witch" }, game.CreatorId).Should().BeTrue();

        var updated = _gameService.GetGame(game.GameId)!;
        updated.EnabledSkills.Should().HaveCount(2);
        updated.EnabledSkills.Should().Contain(Models.PlayerSkill.Seer);
        updated.EnabledSkills.Should().Contain(Models.PlayerSkill.Witch);
    }

    [Fact]
    public void HasDuplicateNames_WhenNoDuplicates_ShouldReturnFalse()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.GameId, "Alice");
        _gameService.JoinGame(game.GameId, "Bob");

        _gameService.HasDuplicateNames(game.GameId).Should().BeFalse();
    }

    [Fact]
    public void HasDuplicateNames_WhenDuplicates_ShouldReturnTrue()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        _gameService.JoinGame(game.GameId, "Alice");
        _gameService.JoinGame(game.GameId, "Alice");

        _gameService.HasDuplicateNames(game.GameId).Should().BeTrue();
    }

    [Fact]
    public void UpdatePlayerName_WhenCreatorChangesName_ShouldUpdateCreatorNameInDTO()
    {
        var game = _gameService.CreateGame("OriginalCreator", 40, "http://localhost");

        _gameService.UpdatePlayerName(game.GameId, game.CreatorId, "UpdatedCreator").Should().BeTrue();

        _gameService.GetGame(game.GameId)!.Players.First(p => p.PlayerId == game.CreatorId)
            .DisplayName.Should().Be("UpdatedCreator");
    }

    [Fact]
    public void GetGame_ShouldDeriveCreatorNameFromPlayer()
    {
        var game = _gameService.CreateGame("OriginalName", 40, "http://localhost");
        _gameService.UpdatePlayerName(game.GameId, game.CreatorId, "NewName");

        _gameService.GetGame(game.GameId)!.Players.First(p => p.PlayerId == game.CreatorId)
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
        _gameService.JoinGame(game.GameId, "Alice");
        _gameService.GetGame(game.GameId)!.Version.Should().Be(before + 1);
    }

    [Fact]
    public void LeaveGame_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");
        var after = _gameService.GetGame(game.GameId)!.Version;
        _gameService.LeaveGame(game.GameId, player!.PlayerId);
        _gameService.GetGame(game.GameId)!.Version.Should().Be(after + 1);
    }

    [Fact]
    public void RemovePlayer_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");
        var after = _gameService.GetGame(game.GameId)!.Version;
        _gameService.RemovePlayer(game.GameId, player!.PlayerId, game.CreatorId);
        _gameService.GetGame(game.GameId)!.Version.Should().Be(after + 1);
    }

    [Fact]
    public void UpdateMaxPlayers_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var before = game.Version;
        _gameService.UpdateMaxPlayers(game.GameId, 30, game.CreatorId);
        _gameService.GetGame(game.GameId)!.Version.Should().Be(before + 1);
    }

    [Fact]
    public void UpdateMinPlayers_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var before = game.Version;
        _gameService.UpdateMinPlayers(game.GameId, 3, game.CreatorId);
        _gameService.GetGame(game.GameId)!.Version.Should().Be(before + 1);
    }

    [Fact]
    public void UpdatePlayerName_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var before = game.Version;
        _gameService.UpdatePlayerName(game.GameId, game.CreatorId, "New Name");
        _gameService.GetGame(game.GameId)!.Version.Should().Be(before + 1);
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
        _gameService.JoinGame(game.GameId, "Alice");
        _gameService.JoinGame(game.GameId, "Bob");
        _gameService.JoinGame(game.GameId, "Charlie");

        _gameService.GetGame(game.GameId)!.Status.Should().Be(Models.GameStatus.ReadyToStart);
    }

    [Fact]
    public void LeaveGame_WhenBelowMinPlayers_ShouldTransitionBackToWaitingForPlayers()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        var (_, _, alice) = _gameService.JoinGame(game.GameId, "Alice");
        _gameService.JoinGame(game.GameId, "Bob");
        _gameService.GetGame(game.GameId)!.Status.Should().Be(Models.GameStatus.ReadyToStart);

        _gameService.LeaveGame(game.GameId, alice!.PlayerId);

        _gameService.GetGame(game.GameId)!.Status.Should().Be(Models.GameStatus.WaitingForPlayers);
    }

    // ── Session / phase tests (no skills) ─────────────────────────────────
    //
    // CreateReadyGame disables all skills so the night flow is:
    //   Round 1 : RoleReveal → WerewolvesMeeting → Discussion
    //   Round 2+: DayElimination → WerewolvesTurn → NightElimination → Discussion

    private Models.GameState CreateReadyGame(int extraPlayers = 3)
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        for (int i = 0; i < extraPlayers; i++)
            _gameService.JoinGame(game.GameId, $"Player{i + 1}");
        // Disable all skills so phase transitions stay simple
        _gameService.UpdateEnabledSkills(game.GameId, new List<string>(), game.CreatorId);
        return _gameService.GetGame(game.GameId)!;
    }

    [Fact]
    public void StartGame_ShouldAssignRoles()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);

        var updated = _gameService.GetGame(game.GameId)!;
        updated.Players.Should().OnlyContain(p => p.Role != null);
        updated.Players.Count(p => p.Role == Models.PlayerRole.Werewolf).Should().Be(game.NumberOfWerewolves);
        updated.Players.Count(p => p.Role == Models.PlayerRole.Villager).Should().Be(updated.Players.Count - game.NumberOfWerewolves);
    }

    [Fact]
    public void StartGame_ShouldSetPhaseToRoleReveal()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);

        var updated = _gameService.GetGame(game.GameId)!;
        updated.Status.Should().Be(Models.GameStatus.InProgress);
        updated.Phase.Should().Be(Models.GamePhase.RoleReveal);
        updated.RoundNumber.Should().Be(1);
        updated.PhaseEndsAt.Should().BeNull();
    }

    [Fact]
    public void StartGame_ByNonCreator_ShouldFail()
    {
        var game = CreateReadyGame();
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Extra");

        var (success, error) = _gameService.StartGame(game.GameId, player!.PlayerId);

        success.Should().BeFalse();
        error.Should().Contain("creator");
    }

    [Fact]
    public void StartGame_WhenNotReadyToStart_ShouldFail()
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");

        var (success, _) = _gameService.StartGame(game.GameId, game.CreatorId);

        success.Should().BeFalse();
    }

    [Fact]
    public void ForceAdvancePhase_FromRoleReveal_ShouldTransitionToWerewolvesMeeting()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);

        // No skills → round 1 goes directly to WerewolvesMeeting
        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.WerewolvesMeeting);
    }

    [Fact]
    public void ForceAdvancePhase_FromWerewolvesMeeting_Round1_ShouldGoToDiscussion()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesMeeting

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Discussion

        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.Discussion);
    }

    [Fact]
    public void CastVote_DuringDiscussion_ShouldRecordVote()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);
        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        var voter  = game.Players.First(p => p.PlayerId != game.CreatorId);
        var target = game.Players.First(p => p.PlayerId != voter.PlayerId);

        var (success, _) = _gameService.CastVote(game.GameId, voter.PlayerId, target.PlayerId);

        success.Should().BeTrue();
        _gameService.GetGame(game.GameId)!.DayVotes[voter.PlayerId].Should().Be(target.PlayerId);
    }

    [Fact]
    public void CastVote_DuringWerewolvesTurn_AllWolvesVote_AutoAdvancesPhase()
    {
        // This test intentionally does NOT call ForceAdvancePhase after CastVote.
        // It verifies that the phase transitions automatically when all werewolves
        // have cast their night vote — i.e. the mechanism real players rely on.
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);
        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        var updated = _gameService.GetGame(game.GameId)!;
        var werewolf = updated.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = updated.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        foreach (var voter in updated.Players.Where(p => p.PlayerId != villagers[0].PlayerId))
            _gameService.CastVote(game.GameId, voter.PlayerId, villagers[0].PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn (round 2)

        var before = _gameService.GetGame(game.GameId)!;
        before.Phase.Should().Be(Models.GamePhase.WerewolvesTurn);

        // Single wolf votes — no ForceAdvancePhase
        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[1].PlayerId);

        var after = _gameService.GetGame(game.GameId)!;
        after.Phase.Should().NotBe(Models.GamePhase.WerewolvesTurn,
            "phase must advance automatically once all werewolves have voted");
    }

    [Fact]
    public void CastVote_DuringWerewolvesTurn_ByVillager_ShouldFail()
    {
        // WerewolvesTurn only happens from round 2 onwards
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);
        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        var updated = _gameService.GetGame(game.GameId)!;
        var werewolf = updated.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var sacrificeVillager = updated.Players.First(p => p.Role == Models.PlayerRole.Villager && !p.IsEliminated);
        // Vote out a villager (not the werewolf) to reach Night round 2 with wolf still alive
        foreach (var voter in updated.Players.Where(p => p.PlayerId != sacrificeVillager.PlayerId))
            _gameService.CastVote(game.GameId, voter.PlayerId, sacrificeVillager.PlayerId);
        // All votes go to one player — no tiebreak — advance
        MarkAllAliveDone(game.GameId); // → DayElimination
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn (round 2)

        var state = _gameService.GetGame(game.GameId)!;
        state.Phase.Should().Be(Models.GamePhase.WerewolvesTurn);

        var villager = state.Players.First(p => p.Role == Models.PlayerRole.Villager && !p.IsEliminated);
        var target   = state.Players.First(p => p.PlayerId != villager.PlayerId && !p.IsEliminated);

        var (success, error) = _gameService.CastVote(game.GameId, villager.PlayerId, target.PlayerId);

        success.Should().BeFalse();
    }

    [Fact]
    public void MarkDone_AllPlayers_ShouldAdvanceFromRoleReveal()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);

        foreach (var player in _gameService.GetGame(game.GameId)!.Players)
            _gameService.MarkDone(game.GameId, player.PlayerId);

        // No skills → advances to WerewolvesMeeting
        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.WerewolvesMeeting);
    }

    [Fact]
    public void VillagersWin_WhenAllWerewolvesEliminated()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);
        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        var updated  = _gameService.GetGame(game.GameId)!;
        var werewolf = updated.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        foreach (var voter in updated.Players.Where(p => p.PlayerId != werewolf.PlayerId))
            _gameService.CastVote(game.GameId, voter.PlayerId, werewolf.PlayerId);

        MarkAllAliveDone(game.GameId); // → DayElimination

        var final = _gameService.GetGame(game.GameId)!;
        final.Phase.Should().Be(Models.GamePhase.DayElimination);
        final.DayDeaths.Should().ContainSingle(e => e.PlayerId == werewolf.PlayerId);
        final.Winner.Should().Be("Villagers");
    }

    [Fact]
    public void WerewolvesWin_WhenAllVillagersAreEliminated()
    {
        var game = CreateReadyGame(2); // 3 total: 1W + 2V
        _gameService.StartGame(game.GameId, game.CreatorId);

        var started  = _gameService.GetGame(game.GameId)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        // Day 1: vote out V[0]
        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[0].PlayerId);
        _gameService.CastVote(game.GameId, villagers[1].PlayerId, villagers[0].PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination

        _gameService.GetGame(game.GameId)!.Winner.Should().BeNull("1W + 1V is not a win yet");

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn round 2

        // Night 2: wolf kills last villager
        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[1].PlayerId);
        // CastVote auto-advances to NightElimination when all wolves have voted

        var afterNight = _gameService.GetGame(game.GameId)!;
        afterNight.Phase.Should().Be(Models.GamePhase.NightElimination);
        afterNight.NightDeaths.Should().ContainSingle(e => e.PlayerId == villagers[1].PlayerId);
        afterNight.Winner.Should().Be("Werewolves");

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → GameOver
        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.GameOver);
    }

    [Fact]
    public void EliminatedPlayers_CanVoteDuringDay()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);

        var started  = _gameService.GetGame(game.GameId)!;
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        var werewolf  = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);

        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        // Vote out V[0]
        foreach (var voter in started.Players.Where(p => p.PlayerId != villagers[0].PlayerId))
            _gameService.CastVote(game.GameId, voter.PlayerId, villagers[0].PlayerId);

        MarkAllAliveDone(game.GameId); // → DayElimination
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → NightElimination (no wolf vote)
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Discussion

        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.Discussion);

        // Eliminated player can still cast a day vote
        var (success, error) = _gameService.CastVote(game.GameId, villagers[0].PlayerId, werewolf.PlayerId);
        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void GameOver_VersionIsBumped_SoPollingClientsReceiveTheTransition()
    {
        var game = CreateReadyGame(2); // 3 total: 1W + 2V
        _gameService.StartGame(game.GameId, game.CreatorId);

        var started  = _gameService.GetGame(game.GameId)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[0].PlayerId);
        _gameService.CastVote(game.GameId, villagers[1].PlayerId, villagers[0].PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn round 2

        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[1].PlayerId);
        // CastVote auto-advances to NightElimination when all wolves have voted

        var vAtNightElim = _gameService.GetGame(game.GameId)!.Version;

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → GameOver
        var afterGameOver = _gameService.GetGame(game.GameId)!;

        afterGameOver.Phase.Should().Be(Models.GamePhase.GameOver);
        afterGameOver.Winner.Should().Be("Werewolves");
        afterGameOver.Version.Should().BeGreaterThan(vAtNightElim);
    }

    [Fact]
    public void FullGameScenario_SixPlayers_TwoWerewolves_WerewolvesWinAfterFourRounds()
    {
        var game = CreateReadyGame(5); // 6 total
        _gameService.UpdateNumberOfWerewolves(game.GameId, 2, game.CreatorId);
        _gameService.StartGame(game.GameId, game.CreatorId);

        var state = _gameService.GetGame(game.GameId)!;
        var W = state.Players.Where(p => p.Role == Models.PlayerRole.Werewolf).ToList();
        var V = state.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        W.Should().HaveCount(2);
        V.Should().HaveCount(4);

        // ── Round 1 ──────────────────────────────────────────────────────────
        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        // Day 1: eliminate V[0]
        foreach (var p in state.Players.Where(p => p.PlayerId != V[0].PlayerId))
            _gameService.CastVote(game.GameId, p.PlayerId, V[0].PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination

        state = _gameService.GetGame(game.GameId)!;
        state.DayDeaths.Should().ContainSingle(e => e.PlayerId == V[0].PlayerId);
        state.Winner.Should().BeNull();

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn round 2

        // ── Round 2 ──────────────────────────────────────────────────────────
        _gameService.CastVote(game.GameId, W[0].PlayerId, V[1].PlayerId);
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[1].PlayerId);
        // CastVote auto-advances to NightElimination when all wolves have voted

        state = _gameService.GetGame(game.GameId)!;
        state.NightDeaths.Should().ContainSingle(e => e.PlayerId == V[1].PlayerId);
        state.Winner.Should().BeNull();

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Discussion

        // Day 2: V[2] and V[3] vote W[0]; wolves split
        _gameService.CastVote(game.GameId, W[0].PlayerId, V[2].PlayerId);
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[3].PlayerId);
        _gameService.CastVote(game.GameId, V[2].PlayerId, W[0].PlayerId);
        _gameService.CastVote(game.GameId, V[3].PlayerId, W[0].PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination

        state = _gameService.GetGame(game.GameId)!;
        state.DayDeaths.Should().ContainSingle(e => e.PlayerId == W[0].PlayerId);
        state.Winner.Should().BeNull();

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn round 3

        // ── Round 3 ──────────────────────────────────────────────────────────
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[2].PlayerId);
        // CastVote auto-advances to NightElimination when all wolves have voted

        state = _gameService.GetGame(game.GameId)!;
        state.NightDeaths.Should().ContainSingle(e => e.PlayerId == V[2].PlayerId);
        state.Winner.Should().BeNull();

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Discussion

        // Day 3: W[1] ↔ V[3] tie → TiebreakDiscussion
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[3].PlayerId);
        _gameService.CastVote(game.GameId, V[3].PlayerId, W[1].PlayerId);
        MarkAllAliveDone(game.GameId); // → TiebreakDiscussion

        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.TiebreakDiscussion);

        // Tiebreak: tie again → no elimination
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[3].PlayerId);
        _gameService.CastVote(game.GameId, V[3].PlayerId, W[1].PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination

        state = _gameService.GetGame(game.GameId)!;
        state.DayDeaths.Should().BeEmpty("double tie = no elimination");
        state.Winner.Should().BeNull();

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn round 4

        // ── Round 4 ──────────────────────────────────────────────────────────
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[3].PlayerId);
        // CastVote auto-advances to NightElimination when all wolves have voted

        state = _gameService.GetGame(game.GameId)!;
        state.Phase.Should().Be(Models.GamePhase.NightElimination);
        state.NightDeaths.Should().ContainSingle(e => e.PlayerId == V[3].PlayerId);
        state.Winner.Should().Be("Werewolves");

        var vAtFinalNight = state.Version;
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → GameOver

        var final = _gameService.GetGame(game.GameId)!;
        final.Phase.Should().Be(Models.GamePhase.GameOver);
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
        _gameService.StartGame(game.GameId, game.CreatorId);

        // Advance to Discussion (Round 1: RoleReveal → WerewolvesMeeting → Discussion)
        MarkAllAliveDone(game.GameId);  // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // WerewolvesMeeting → Discussion

        var state = _gameService.GetGame(game.GameId)!;
        state.Phase.Should().Be(Models.GamePhase.Discussion);

        // Circular vote: A→B, B→C, C→A so every player receives exactly 1 vote
        var players = state.Players
            .Where(p => !p.IsEliminated)
            .OrderBy(p => p.PlayerId)
            .ToList();
        for (int i = 0; i < players.Count; i++)
            _gameService.CastVote(game.GameId, players[i].PlayerId, players[(i + 1) % players.Count].PlayerId);

        MarkAllAliveDone(game.GameId); // Discussion → TiebreakDiscussion

        var after = _gameService.GetGame(game.GameId)!;
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
        _gameService.StartGame(game.GameId, game.CreatorId);
        MarkAllAliveDone(game.GameId);
        MarkAliveWolvesDone(game.GameId);

        var players = _gameService.GetGame(game.GameId)!.Players
            .Where(p => !p.IsEliminated)
            .OrderBy(p => p.PlayerId)
            .ToList();

        // Set up circular tie
        for (int i = 0; i < players.Count; i++)
            _gameService.CastVote(game.GameId, players[i].PlayerId, players[(i + 1) % players.Count].PlayerId);
        MarkAllAliveDone(game.GameId); // → TiebreakDiscussion

        // Each player should be able to vote for any of the other two candidates
        var state = _gameService.GetGame(game.GameId)!;
        state.TiebreakCandidates.Should().HaveCount(3);

        // Verify every player can vote for each of the other tied candidates
        foreach (var voter in players)
        {
            foreach (var candidate in players.Where(c => c.PlayerId != voter.PlayerId))
            {
                var (success, error) = _gameService.CastVote(game.GameId, voter.PlayerId, candidate.PlayerId);
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
        _gameService.StartGame(game.GameId, game.CreatorId);

        MarkAllAliveDone(game.GameId);  // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // WerewolvesMeeting → Discussion

        var state = _gameService.GetGame(game.GameId)!;
        var players = state.Players
            .Where(p => !p.IsEliminated)
            .OrderBy(p => p.PlayerId)
            .ToList();

        // 4-way circular vote: each player votes for the next → all get 1 vote
        for (int i = 0; i < players.Count; i++)
            _gameService.CastVote(game.GameId, players[i].PlayerId, players[(i + 1) % players.Count].PlayerId);

        MarkAllAliveDone(game.GameId); // → TiebreakDiscussion

        var after = _gameService.GetGame(game.GameId)!;
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
        _gameService.StartGame(game.GameId, game.CreatorId);

        var started  = _gameService.GetGame(game.GameId)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        // All 3 villagers vote for the werewolf (correct); werewolf votes for V[0] (wrong)
        foreach (var v in villagers)
            _gameService.CastVote(game.GameId, v.PlayerId, werewolf.PlayerId);
        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[0].PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination

        var result = _gameService.GetGame(game.GameId)!;
        result.DayDeaths.Should().ContainSingle(e => e.PlayerId == werewolf.PlayerId);

        foreach (var v in villagers)
            result.Players.First(p => p.PlayerId == v.PlayerId).Score.Should().Be(1, "correct voter earns 1 point");
        result.Players.First(p => p.PlayerId == werewolf.PlayerId).Score.Should().Be(0, "wrong voter earns 0 points");
    }

    [Fact]
    public void TeamWin_AwardsEightPoints_ToWinningTeam()
    {
        var game = CreateReadyGame(2); // 3 players: 1W + 2V
        _gameService.StartGame(game.GameId, game.CreatorId);

        var started  = _gameService.GetGame(game.GameId)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        // All villagers vote for wolf; wolf votes for V[0]
        foreach (var v in villagers)
            _gameService.CastVote(game.GameId, v.PlayerId, werewolf.PlayerId);
        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[0].PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination (Villagers win)

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → GameOver

        var result = _gameService.GetGame(game.GameId)!;
        result.Phase.Should().Be(Models.GamePhase.GameOver);
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
        _gameService.StartGame(game.GameId, game.CreatorId);

        var started  = _gameService.GetGame(game.GameId)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        // Round 1: V[1], V[2], W vote for V[0] → V[0] eliminated
        _gameService.CastVote(game.GameId, villagers[1].PlayerId, villagers[0].PlayerId);
        _gameService.CastVote(game.GameId, villagers[2].PlayerId, villagers[0].PlayerId);
        _gameService.CastVote(game.GameId, werewolf.PlayerId,     villagers[0].PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination

        _gameService.GetGame(game.GameId)!.Players
            .First(p => p.PlayerId == villagers[0].PlayerId).IsEliminated.Should().BeTrue();

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → NightElimination
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Discussion (round 2)

        // Round 2: dead V[0] votes for V[1] (this would cause a tie if counted)
        _gameService.CastVote(game.GameId, villagers[0].PlayerId, villagers[1].PlayerId);
        _gameService.CastVote(game.GameId, villagers[1].PlayerId, werewolf.PlayerId);
        _gameService.CastVote(game.GameId, villagers[2].PlayerId, werewolf.PlayerId);
        _gameService.CastVote(game.GameId, werewolf.PlayerId,     villagers[1].PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination

        var result = _gameService.GetGame(game.GameId)!;

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

    private Models.GameState CreateReadyGameWithSkills(int extraPlayers, List<string> skills)
    {
        var game = _gameService.CreateGame("Creator", 40, "http://localhost");
        for (int i = 0; i < extraPlayers; i++)
            _gameService.JoinGame(game.GameId, $"Player{i + 1}");
        _gameService.UpdateEnabledSkills(game.GameId, skills, game.CreatorId);
        return _gameService.GetGame(game.GameId)!;
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
        _gameService.StartGame(game.GameId, game.CreatorId);

        var updated  = _gameService.GetGame(game.GameId)!;
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
        _gameService.StartGame(game.GameId, game.CreatorId);

        var state = _gameService.GetGame(game.GameId)!;
        state.Phase.Should().Be(Models.GamePhase.RoleReveal);

        MarkAllAliveDone(game.GameId); // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // WerewolvesMeeting → CupidTurn
        state = _gameService.GetGame(game.GameId)!;
        state.Phase.Should().Be(Models.GamePhase.CupidTurn);

        var cupid = state.Players.First(p => p.Skill == Models.PlayerSkill.Cupid);
        var others = state.Players.Where(p => p.PlayerId != cupid.PlayerId).Take(2).ToList();

        var (success, error) = _gameService.CupidAction(game.GameId, cupid.PlayerId, others[0].PlayerId, others[1].PlayerId);

        success.Should().BeTrue();
        var updated = _gameService.GetGame(game.GameId)!;
        updated.Phase.Should().Be(Models.GamePhase.LoverReveal);
        updated.PhaseEndsAt.Should().NotBeNull();
        updated.Lover1Id.Should().Be(others[0].PlayerId);
        updated.Lover2Id.Should().Be(others[1].PlayerId);
    }

    [Fact]
    public void CupidAction_SameLover_ShouldFail()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Cupid" });
        _gameService.StartGame(game.GameId, game.CreatorId);
        MarkAllAliveDone(game.GameId); // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // WerewolvesMeeting → CupidTurn

        var state = _gameService.GetGame(game.GameId)!;
        var cupid = state.Players.First(p => p.Skill == Models.PlayerSkill.Cupid);

        var (success, error) = _gameService.CupidAction(game.GameId, cupid.PlayerId, cupid.PlayerId, cupid.PlayerId);

        success.Should().BeFalse();
    }

    [Fact]
    public void CupidSkip_WhenCupidDoesNotAct_SkipsLoverReveal()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Cupid" });
        _gameService.StartGame(game.GameId, game.CreatorId);
        MarkAllAliveDone(game.GameId); // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // WerewolvesMeeting → CupidTurn
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // skip CupidTurn without action

        // No lovers chosen → should skip LoverReveal and go to Discussion
        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.Discussion);
    }

    [Fact]
    public void SeerAction_ReturnsTrueRevealingTargetRoleAndSkill()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Seer" }); // 5 players, 1W, 4V (Seer + 3 plain)
        _gameService.StartGame(game.GameId, game.CreatorId);

        var state = _gameService.GetGame(game.GameId)!;
        // Get to WerewolvesTurn round 2
        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        // Eliminate a villager to reach round 2
        var wolf    = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = state.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        var seer    = state.Players.First(p => p.Skill == Models.PlayerSkill.Seer);
        var toElim  = villagers.First(p => p.Skill == Models.PlayerSkill.None && p.PlayerId != seer.PlayerId);

        foreach (var v in state.Players.Where(p => p.PlayerId != toElim.PlayerId))
            _gameService.CastVote(game.GameId, v.PlayerId, toElim.PlayerId);
        MarkAllAliveDone(game.GameId); // → DayElimination
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn round 2

        // Wolf votes
        _gameService.CastVote(game.GameId, wolf.PlayerId, villagers.First(p => p.PlayerId != toElim.PlayerId && p.PlayerId != seer.PlayerId).PlayerId);
        // CastVote auto-advances to SeerTurn when all wolves have voted

        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.SeerTurn);

        // Seer inspects the wolf
        var (success, error, result) = _gameService.SeerAction(game.GameId, seer.PlayerId, wolf.PlayerId);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.IsWerewolf.Should().BeTrue();
        result.Skill.Should().BeNull(); // wolf has no special skill
    }

    [Fact]
    public void WitchAction_SavesNightKillTarget()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Witch" });
        _gameService.StartGame(game.GameId, game.CreatorId);

        var state = _gameService.GetGame(game.GameId)!;
        var wolf  = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var witch = state.Players.First(p => p.Skill == Models.PlayerSkill.Witch);
        var victim = state.Players.First(p => p.Role == Models.PlayerRole.Villager && p.PlayerId != witch.PlayerId);

        // Get to WerewolvesTurn round 2
        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion

        // Survive round 1 discussion without elimination: all players mark done, no winner
        MarkAllAliveDone(game.GameId); // → DayElimination (no votes)
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn round 2

        // Wolf votes for victim
        _gameService.CastVote(game.GameId, wolf.PlayerId, victim.PlayerId);
        // CastVote auto-advances to WitchTurn when all wolves have voted

        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.WitchTurn);
        _gameService.GetGame(game.GameId)!.NightKillTargetId.Should().Be(victim.PlayerId);

        // Witch saves the victim
        var (success, error) = _gameService.WitchAction(game.GameId, witch.PlayerId, "save", null);

        success.Should().BeTrue();
        var updated = _gameService.GetGame(game.GameId)!;
        updated.Phase.Should().Be(Models.GamePhase.NightElimination);
        updated.NightDeaths.Should().BeEmpty("witch saved the victim");
        updated.WitchHealUsed.Should().BeTrue();

        // Victim should NOT be eliminated
        updated.Players.First(p => p.PlayerId == victim.PlayerId).IsEliminated.Should().BeFalse();
    }

    [Fact]
    public void WitchAction_PoisonKillsTarget()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Witch" });
        _gameService.StartGame(game.GameId, game.CreatorId);

        var state = _gameService.GetGame(game.GameId)!;
        var wolf  = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var witch = state.Players.First(p => p.Skill == Models.PlayerSkill.Witch);
        var villagers = state.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        var poisonTarget = villagers.First(p => p.PlayerId != witch.PlayerId);

        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion
        MarkAllAliveDone(game.GameId); // → DayElimination (no kill)
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn round 2

        // Wolf votes anyone else
        var wolfVictim = villagers.First(p => p.PlayerId != witch.PlayerId && p.PlayerId != poisonTarget.PlayerId);
        _gameService.CastVote(game.GameId, wolf.PlayerId, wolfVictim.PlayerId);
        // CastVote auto-advances to WitchTurn when all wolves have voted

        // Witch poisons a different target
        var (success, _) = _gameService.WitchAction(game.GameId, witch.PlayerId, "poison", poisonTarget.PlayerId);

        success.Should().BeTrue();
        var updated = _gameService.GetGame(game.GameId)!;
        updated.NightDeaths.Should().Contain(e => e.PlayerId == poisonTarget.PlayerId &&
            e.Cause == Models.EliminationCause.WitchPoison);
        updated.Players.First(p => p.PlayerId == poisonTarget.PlayerId).IsEliminated.Should().BeTrue();
    }

    [Fact]
    public void HunterAction_ShootsTargetWhenEliminated()
    {
        var game = CreateReadyGameWithSkills(4, new List<string> { "Hunter" });
        _gameService.StartGame(game.GameId, game.CreatorId);

        var state  = _gameService.GetGame(game.GameId)!;
        var wolf   = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var hunter = state.Players.First(p => p.Skill == Models.PlayerSkill.Hunter);
        var bystander = state.Players.First(p => p.Role == Models.PlayerRole.Villager && p.Skill == Models.PlayerSkill.None);

        // Get to WerewolvesTurn round 2
        MarkAllAliveDone(game.GameId); // → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // → Discussion
        MarkAllAliveDone(game.GameId); // → DayElimination (no kill)
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn round 2

        // Wolf kills the Hunter
        _gameService.CastVote(game.GameId, wolf.PlayerId, hunter.PlayerId);
        // CastVote auto-advances to NightElimination when all wolves have voted

        var afterNight = _gameService.GetGame(game.GameId)!;
        afterNight.Phase.Should().Be(Models.GamePhase.NightElimination);
        afterNight.HunterMustShoot.Should().BeTrue();

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → HunterTurn

        var inHunterTurn = _gameService.GetGame(game.GameId)!;
        inHunterTurn.Phase.Should().Be(Models.GamePhase.HunterTurn);

        // Hunter shoots the bystander
        var (success, error) = _gameService.HunterAction(game.GameId, hunter.PlayerId, bystander.PlayerId);

        success.Should().BeTrue();
        var final = _gameService.GetGame(game.GameId)!;
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
        _gameService.StartGame(game.GameId, game.CreatorId);

        var state = _gameService.GetGame(game.GameId)!;
        var wolf  = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var cupid = state.Players.First(p => p.Skill == Models.PlayerSkill.Cupid);
        var others = state.Players.Where(p => p.PlayerId != cupid.PlayerId).ToList();

        MarkAllAliveDone(game.GameId); // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // WerewolvesMeeting → CupidTurn

        // Cupid links wolf and one villager as lovers
        var lover1 = wolf;
        var lover2 = others.First(p => p.Role == Models.PlayerRole.Villager);
        _gameService.CupidAction(game.GameId, cupid.PlayerId, lover1.PlayerId, lover2.PlayerId);

        // → LoverReveal → Discussion
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // LoverReveal → Discussion

        // Eliminate everyone except the 2 lovers
        var toElims = state.Players.Where(p => p.PlayerId != lover1.PlayerId && p.PlayerId != lover2.PlayerId).ToList();
        foreach (var target in toElims)
        {
            // vote out each non-lover one by one (force advance after each round)
            var alive = _gameService.GetGame(game.GameId)!.Players.Where(p => !p.IsEliminated && p.ParticipationStatus == Models.ParticipationStatus.Participating).ToList();
            if (alive.Count <= 2) break;
            foreach (var voter in alive.Where(p => p.PlayerId != target.PlayerId))
                _gameService.CastVote(game.GameId, voter.PlayerId, target.PlayerId);
            MarkAllAliveDone(game.GameId); // → DayElimination
            _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn or Discussion
            // If we hit WerewolvesTurn, advance past it
            var g2 = _gameService.GetGame(game.GameId)!;
            if (g2.Phase == Models.GamePhase.WerewolvesTurn)
            {
                _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → NightElimination (no kill)
                _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Discussion
            }
        }

        var finalState = _gameService.GetGame(game.GameId)!;
        finalState.Winner.Should().Be("Lovers");
    }

    [Fact]
    public void LoverCascade_KillingOneLoverAlsoKillsOther()
    {
        var game = CreateReadyGameWithSkills(3, new List<string> { "Cupid" }); // 4 players: 1W + 3V
        _gameService.StartGame(game.GameId, game.CreatorId);

        var state = _gameService.GetGame(game.GameId)!;
        var wolf  = state.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var cupid = state.Players.First(p => p.Skill == Models.PlayerSkill.Cupid);
        var villagers = state.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        var loverVillager1 = villagers[0];
        var loverVillager2 = villagers[1];

        MarkAllAliveDone(game.GameId); // RoleReveal → WerewolvesMeeting
        MarkAliveWolvesDone(game.GameId); // WerewolvesMeeting → CupidTurn
        // Link two villagers as lovers
        _gameService.CupidAction(game.GameId, cupid.PlayerId, loverVillager1.PlayerId, loverVillager2.PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // LoverReveal → Discussion
        MarkAllAliveDone(game.GameId); // → DayElimination (no kill)
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → WerewolvesTurn round 2

        // Wolf kills loverVillager1 — loverVillager2 should die in cascade
        _gameService.CastVote(game.GameId, wolf.PlayerId, loverVillager1.PlayerId);
        // CastVote auto-advances to NightElimination when all wolves have voted

        var final = _gameService.GetGame(game.GameId)!;
        final.NightDeaths.Should().Contain(e => e.PlayerId == loverVillager1.PlayerId);
        final.NightDeaths.Should().Contain(e => e.PlayerId == loverVillager2.PlayerId &&
            e.Cause == Models.EliminationCause.LoverDeath);
        final.Players.First(p => p.PlayerId == loverVillager2.PlayerId).IsEliminated.Should().BeTrue();
    }
}

