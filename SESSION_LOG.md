# L2Bot 2.0 — Log Session

## Session Info
- **Date Started:** 2026-06-12
- **Thread:** Combat Zone, Shots, Player Filter fixes

---

## Done

### 1. Restore CombatZone Filter
- **File:** `Client\Domain\AI\Combat\Helper.cs`
- **What:** Restored `config.Combat.Zone.IsInside(x.Transform.Position)` check inside `GetMobsToAttackByConfig()`.
- **Status:** Implemented, deployed, user confirmed working (green circle shows on map).

### 2. Fix Stale Build Problem
- **Root Cause:** `client_only.bat` runs `publish\Client.exe`, but we were only copying builds into `publish\Client\bin\`.
- **Fix:** Now always copy `Client.dll` and `Client.exe` into **both** `publish\Client\bin\` AND `publish\` root after every build.

### 3. Implement "Don't Attack Players" Filter
- **Files:**
  - `Client\Domain\AI\Config.cs` — added `DontAttackPlayers` (default `true`)
  - `Client\Domain\AI\State\AttackState.cs` — skip attack if target type is not NPC
  - `Client\Domain\AI\State\MoveToTargetState.cs` — skip movement if target type is not NPC
  - `Client\Application\Views\AIConfig.xaml` — added CheckBox "Don't attack players"
  - `Client\Application\ViewModels\AIConfigViewModel.cs` — added property, Load, Save binding
- **Status:** Implemented, deployed.

### 4. Fix Shots/Soulshots Disabling
- **Initial Diagnosis:** `DoOnEnter` blindly toggled shots, turning OFF manually enabled ones.
- **Attempt 1:** Added `HashSet` tracking in `DoOnEntry`/`DoOnLeave`. Result: still OFF.
- **Attempt 2:** Removed `DoOnLeave` entirely (shots stay ON). Result: still OFF.
- **Attempt 3:** Removed `DoOnEntry` entirely. Result: still OFF.
- **Real Root Cause:** Stale build! `publish\Client.exe` was old and ignored all changes.
- **Fix:** Update `publish\Client.exe` from `publish\Client\bin\`.
- **Result:** Both Soulshots and Spiritshots now stay active during bot combat.

### 5. Reduce Attack Packet Spam
- **File:** `Client\Domain\AI\State\AttackState.cs`
- **What:** `RequestAttackOrFollow` sent every tick (~500 ms). Now sent **only once per target change** (`lastAttackTargetId`).
- **Status:** Implemented, deployed.

---

## Remaining / Open

### Shots (AutoUseShots checkbox)
- **Current behavior:** Bot never touches shots. Player must enable them manually via in-game panel before combat.
- **Note:** `IsAutoused` property is dead (cached at login, never updated). Reliable auto-toggle impossible without state-read API.

### Map — Combat Zone Visibility
- Green circle appears when Radius > 0 and zone is centered. User confirmed working.

### Sound Alerts
- Not implemented. Evaluate later if needed.

### Adrenaline / L2Walker Source Review
- L2Walker Java sources available (`source-archive\l2walker\`).
- Adrenaline has only binaries, no sources found.

---

## Important Notes

- **Build Deploy Rule:** Always copy `Client.dll` + `Client.exe` to `publish\` root after every build.
- **Test Environment:** `E:\AI\L2RebornBot\L2Bot 2.0 Interlude\publish\`
- **Launch Script:** `E:\AI\L2RebornBot\client_only.bat`

