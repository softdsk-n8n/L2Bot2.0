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

---

## 2026-06-12 — UI Improvements

### Start/Stop Button (MainWindow)
- **What:** Added prominent Start/Stop button to main UI (right side of menu bar).
- **How:** Bound to `ToggleAICommand`, content bound to `AIStatusText` property.
- **Files:** `MainWindow.xaml`, `MainViewModel.cs`.

### Dark Chat Background
- **What:** Chat `ListBox` now uses dark background (`#FF1E1E1E`).
- **How:** Changed `Background` property, changed default message color from `Brushes.Black` to `Brushes.White` for readability.
- **Files:** `MainWindow.xaml`, `ChatMessageViewModel.cs`.

### System Messages Toggle & Color
- **What:** System messages (`ChatChannelEnum.Announcement`) now appear in **Yellow** and can be toggled via checkbox.
- **How:** Added `ShowSystemMessages` property + checkbox binding; filter in `Handle(ChatMessageCreatedEvent)` skips Announcement if unchecked.
- **Files:** `MainWindow.xaml`, `MainViewModel.cs`, `ChatMessageViewModel.cs`.

## 2026-06-12 — Combat Zone Refactor (Polygon + Dynamic Circle)

### Domain Model Refactor
- **What:** Replaced fixed `Center`/`Radius` with polymorphic `CombatZone` model (`Free`, `DynamicCircle`, `FixedPolygon`).
- **How:** Added `ZoneType` enum, `ObservableCollection<Vector3> Vertices`, `IsRelativeToHero`, `MaxZDelta`, `BypassObstacles`, `BypassTimeoutMs`, `StepBackMs`, `StepSideMs`. `IsInside()` uses ray-casting for polygon, distance for circle.
- **Files:** `CombatZone.cs`, `Config.cs`.

### AI Logic Updates
- **What:** 3D distance checks instead of horizontal; basic anti-jam (bypass obstacles).
- **How:** `Helper.cs` now filters by `Zone.MaxZDelta` and `Zone.IsInside`. `TransitionBuilder.cs` uses `Distance` (3D). `MoveToTargetState.cs` adds `CheckAntiJam()` — if locked for > timeout ms, performs perpendicular escape step.
- **Files:** `Helper.cs`, `TransitionBuilder.cs`, `MoveToTargetState.cs`.

### Map Visualization
- **What:** Combat zone now renders as polygon on map instead of ellipse.
- **How:** `AICombatZoneMapViewModel.cs` generates screen-space vertices (16 segments for circle). `Map.xaml` uses `<Polygon>` with `PointsConverter`.
- **Files:** `AICombatZoneMapViewModel.cs`, `Map.xaml`, `PointsConverter.cs`.

### Config UI (Combat Zone Tab)
- **What:** New "Combat Zone" tab in AIConfig with all settings.
- **How:** `AIConfigViewModel.cs` wrapper expanded with `Type`, `Vertices`, `MaxZDelta`, anti-jam fields + commands (`SetZoneFromHero`, `AddVertex`, `RemoveVertex`, `ClearVertices`). `AIConfig.xaml` new `TabItem` with ComboBox, DataGrid, TextBoxes, CheckBoxes, Buttons.
- **Files:** `AIConfigViewModel.cs`, `AIConfig.xaml`.

### Build & Deploy
- **Status:** `dotnet build` succeeded (`CS8625` nullable warning only). `dotnet publish` deployed to `publish\`.

### Draw Zone by Click (Map)
- **What:** Can now draw polygon combat zone directly on map with mouse clicks (Adrenaline-style).
- **How:** Added `IsDrawingZone` + `ToggleDrawZoneCommand` to `MapViewModel`. `OnLeftMouseClick` now appends click coordinates to `CombatZone.Vertices` when drawing mode is active. Added `Zone` property to `AICombatZoneMapViewModel`. Added `Draw Zone` button on `Map.xaml`.
- **Files:** `MapViewModel.cs`, `AICombatZoneMapViewModel.cs`, `Map.xaml`.

### Fix: Polygon Vertices Rendering on Map
- **What:** Polygon zone now updates immediately when adding vertices via click-draw; yellow vertex dots appear on map; polygon lines render.
- **How:** Added `Vertices.CollectionChanged` handler in `AICombatZoneMapViewModel`. Added `ItemsControl` with yellow `Ellipse` dots bound to `ScreenVertices` inside `Map.xaml`.
- **Files:** `AICombatZoneMapViewModel.cs`, `Map.xaml`.

### Feature: Mob Aggro Radius Toggle
- **What:** Map now shows red aggro circles around aggressive mobs when enabled.
- **How:** Added `ShowMobAggro` + `ToggleShowMobAggroCommand` to `MapViewModel`. Replaced `DataTrigger` with `MultiDataTrigger` on `CreatureAggroRadius` — visible only if both `IsAggressive=True` AND `ShowMobAggro=True`. Added `CheckBox` on map.
- **Files:** `MapViewModel.cs`, `Map.xaml`.

### Fix: Polygon not rendering + Bot not attacking
- **Root causes:**
  1. `CombatZone` setter never called `MapUpdated`, leaving `Scale=0` in `AICombatZoneMapViewModel` → all screen coords = NaN.
  2. `FixedPolygon` not selected automatically when drawing on map.
  3. Default `Radius=0` made zone empty.
- **Fixes:**
  1. `CombatZone` setter now calls `value.MapUpdated(scale, ...)` immediately.
  2. `MapViewModel.OnLeftMouseClick` auto-sets `Zone.Type = FixedPolygon` when drawing.
  3. Default `Radius` set to `1000` in `Config.cs`.
- **Files:** `MapViewModel.cs`, `Config.cs`.

