# Authorization

The app has two distinct concepts on `PlayerState` that must not be conflated: **creator** and **moderator**.

---

## IsCreator — informational, permanent

`IsCreator` marks the player who originally created the tournament session. It is set once at game creation and never changes.

**This flag grants no permissions.** Its only purpose is display: the lobby shows a "HOST" badge next to the creator's name.

---

## IsModerator — the action gate

`IsModerator` governs all privileged actions:

| Action | Guard |
|---|---|
| Start the game | `IsModerator` |
| Update settings (player counts, durations, skills) | `IsModerator` |
| Remove a player from the lobby | `IsModerator` |
| Force-advance the current phase | `IsModerator` |

When a game is created, the creator is automatically given `IsModerator = true`. But the two flags are independent — in principle a moderator could be someone who did not create the session.

---

## Design rule for AI assistants

**When adding or modifying a privileged action, always gate it on `IsModerator`, never on `game.CreatorId`.** Checking `game.CreatorId` directly is wrong because it hard-wires the permission to the creator and makes the moderator concept meaningless.

### Backend pattern

```csharp
// Correct
if (!game.Players.Any(p => p.PlayerId == moderatorId && p.IsModerator))
    return (false, "Only a moderator can do this");

// Wrong — do not use
if (game.CreatorId != creatorId)
    return (false, "Only the creator can do this");
```

### Frontend pattern

```html
<!-- Correct: show controls only to moderators -->
<p-button *ngIf="isModerator" ...></p-button>

<!-- Wrong — do not use -->
<p-button *ngIf="isCreator" ...></p-button>
```

Both `lobby.ts` and `session.ts` expose an `isModerator` getter (reads `currentPlayer?.isModerator`) for use in templates. `isCreator` remains available only for the HOST badge.
