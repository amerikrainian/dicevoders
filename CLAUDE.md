# DiceVoders — Accessibility Mod for DiceVaders

Screen-reader accessibility mod for blind players. Port of the starvaders-access architecture
(`C:\Users\CRNTJR3\cs\starvaders-access`, itself a port of `C:\Users\CRNTJR3\cs\guildrun_access`):
host/module hot reload, the engine-free key-graph Core, Prism speech, the dev HTTP server, buffers,
key help. DiceVaders is the same studio's (Pengonauts) follow-up to StarVaders and shares its UI
framework, so most of starvaders-access's Module patterns apply here directly; look there before
writing a screen from scratch. Speech is the sole interface: a silent failure is invisible to the
player, so every catch logs, and nothing caches game state.

## Game facts
- **Engine**: Unity **6000.3.10f1**, **IL2CPP** x64, metadata **v39**, unobfuscated. Steam app
  **3917700**, build 25892943 (2026-10-16). Install:
  `C:\Program Files (x86)\Steam\steamapps\common\DiceVaders` (`Directory.Build.props`: registry,
  `DICEVADERS_DIR`, `-p:GameDir=`, or a gitignored `Directory.Build.local.props`). Steam and GOG
  Galaxy SDKs ship; audio is FMOD.
- **Game code**: `ScriptAssembly.dll` (namespace `StarVaders`, the working title, plus the global
  namespace), small `Assembly-CSharp` and `Assembly-CSharp-firstpass`. No DI container: one
  `StarVaders.SceneController` subclass per scene (`BootupController`, `MainMenuController`,
  `CharacterSelectionController`, `EncounterController`, `CompendiumController`,
  `SettingsSceneController`, `KeyBindingsSceneController`, `ConfirmPromptController`,
  `CreditsController`, `StrategyGuideController`), additive scenes for overlays, `SceneLoader`.
  The pause menu is not a scene controller: `StarVaders.PauseMenu.PauseMenuController` lives in the
  encounter.
- **Widgets**: the StarVaders family: `AViewItem` MonoBehaviours (`ButtonView`, `OptionView`,
  `OptionToggle`, `OptionSlider`, `EnterGuideButton`...) with explicit `LeftNav/RightNav/UpNav/DownNav`;
  clickable ones implement `IClickable.OnClick`. Icon buttons carry their name only in their hover
  tooltip (`ButtonView.Tooltip`, a hidden TMP). Object names repeat across a scene's panels
  (`DiscordButton`, `ExitButton` in both the menu and its popup): never look an item up by name
  across a whole scene where a panel holds a duplicate.
- **Input**: new Input System only: `UnityEngine.Input` THROWS (checked live). Per-scene
  `InputHandler` (hover state, `OnHoverViewItemByKey`) and `*KeyInputController`s.
- **Localization**: Unity Localization, table `Everything` (`LocalizationUtils.LocalizeSynchronous`).
  Locales: `en fr zh-Hans ja ko es es-MX de pt-BR ru zh-TW tr` (plus a pseudo-locale coded `en`);
  the picker offers all but `tr`. `lang/<code>.txt` per locale, `LangFilesTests` checks them; `es-MX`
  started as a copy of `es`. Inline icons in game strings (`UI/GameText`): an icon inside
  `<nobr>` before a word is a keyword's decoration (`<nobr><Chrono> Chrono</nobr>`), dropped;
  elsewhere it reads as its word unless its own word follows (Charge's title). ShopToken reads Cash
  (`ShopToken_TooltipTitle`); the scoring line's Target reads "When scored" (quoted in the guide's
  `Guide_Scoring_1`), with a colon when it opens a line; Arrow (trigger condition, then effect:
  `Guide_Triggers_2`) and React keep their names, as the game's explanations quote them. A run of
  one icon is a count ("Spend 2 Charge").
- **BepInEx**: the vendored 6.0.0-be.788 boots the game as is (Cpp2IL reads v39; one warning,
  "Class::Init signatures have been exhausted, using a substitute!", harmless so far). StarVaders'
  preloader patcher (a Unity 2020 GetMethod hook bug) is NOT needed here and was not ported.

## Decompiled reference (gitignored `game/`; never commit it)
- `game/il2cppdump/dump.cs` (Il2CppDumper: ChanGu54's fork at `C:\tools\Il2CppDumper`, upstream
  stops at v31), `DummyDll/`, `script.json`, `il2cpp.h`, `stringliteral.json`.
- `game/analysis/types.tsv`, `version.json`, `porting-report.md` (the first investigation).
- Regenerate after a game update: `uv run python tools/python/dump_game.py --keep --check`, then
  `dump_diff.py` against the parked `game/il2cppdump.prev`.

## Tools (`tools/python/`, standard library; `uv run python tools/python/<tool>.py`)
guildrun's, retargeted in `gamelib.py` (app id, folder, `GAME_ASSEMBLY_PREFIXES`,
`DVO_DEV_URL`/`DVO_DEV_PORT`): `show.py Type`, `holders.py Type`, `census.py --report`,
`disasm.py Type.Method --calls` (with `uv run --with capstone`), `dump_game.py`, `dump_diff.py`,
`check_members.py`, and `dev.py` (`launch`, `kill`, `reload`, `nav`, `input ui.down`,
`speech --tail N`, `log --grep X`, `gui`, `eval file.cs`, `typeinfo`, `actions`, `module`,
`screenshot`). `dev.py screenshot` timed out with the game in the background. `stage.py` stages
encounter scenarios through the game's hidden `DebugController` (in the encounter scene, inactive;
`SetupDebug` then `CloseDebug` fills it without its canvas): `state`, `skip` (Skip Round: the next
target, a pilot's rewards included), `pilot NAME [--index I]` (`EncounterModel.PilotOrder`, then
`MapPanel.UpdateMap`), `offer NAME [--slot S]` (a shop choice becomes any unit, `DebugInitializeUnit`,
at 0 cash, or artifact, `Initialize(..., DraftType.Artifact, name, ...)`), `find TEXT`, `gain
money|budge|chrono|heart [N]`, `set VALUE N` (`IntValues` or `BigDoubleValues`). Achievements cannot be
blocked reliably: an earned one is recorded in the save (`LoggerDataSO.GetAchievement`) and pushed to
Steam again at boot (`SyncSteamAchievements`) and on load.

## Build & deploy
`dotnet build DiceVoders.slnx` (or `.\build.ps1`) builds all five projects and deploys host +
Contracts + Roslyn deps + Core + Module to `<game>\BepInEx\plugins\DiceVoders\`, `prism.dll`
next to `DiceVaders.exe`, `lang\*.txt` beside the plugin. A host or Contracts change needs the game
closed; Core/Module: `dotnet build src/DiceVoders.Module/DiceVoders.Module.csproj` then
`dev.py reload` (or F6 in game). First-time setup: `.\setup-bepinex.ps1`, launch once through Steam
(interop generates, about a minute), build. Launch: `dev.py launch` (Steam `-applaunch 3917700`,
waits for the dev server). Tests: `dotnet test src/DiceVoders.Tests/DiceVoders.Tests.csproj`
(Core + Contracts, no game). After adding a string: `DVO_WRITE_LANG_TEMPLATE=1` on that test run
regenerates `lang/en.txt`; every other lang file needs the key too.
The version lives in `Directory.Build.props` alone (0.0.1). Release: `build_release.ps1` (the game-folder zip
`releases\DiceVoders-v<version>.zip`: BepInEx, the four mod dlls and `lang\` under
`BepInEx\plugins\DiceVoders`, `prism.dll`, a BepInEx.cfg with the console off; no patcher),
`build-installer.ps1` (the Rust installer in `installer\`, `releases\DiceVodersInstaller.exe`;
`test-installer.ps1` runs its tests), `create-release.ps1 vX.Y.Z` (the GitHub release from a pushed
tag and its CHANGELOG section). The manual: mdbook 0.5.2, `docs_src/src/*.md`
(`mdbook build docs_src`, output `docs_src/book`, gitignored; Change Log includes `CHANGELOG.md`),
bundled by `build_release.ps1` as `DiceVodersDocs`, published to GitHub Pages by
`.github/workflows/build-docs.yml` on a push to main touching it (Pages must be set to "GitHub
Actions" in the repo settings). The repo is https://github.com/amerikrainian/dicevoders (public, `origin`; nothing pushed yet). The update check asks
`amerikrainian/dicevoders`'s latest release and logs a 404 until one exists.

## Architecture (guildrun's, see its CLAUDE.md for the long form)
- **Contracts** (netstandard2.0, permanent), **host** `DiceVoders` (net6.0, permanent: Plugin,
  Prism, `HostPump`, `ModuleLoader`, dev server), **Core** (netstandard2.0, reloadable, engine-free,
  unit-tested: key graph, navigator, buffers, input manager, screens, strings), **Module** (net6.0,
  reloadable, engine-coupled: day-to-day work), **Tests**.
- Buffers (`Module/UI/Buffers`, cycling order; `BufferKeys` lists the keys; an empty one is skipped):
  `ui` (the focused control's lines), `units` (`Encounter/EncounterBuffers`, named by the game's
  `Units_Subtitle`: each unit on the grid as its cell reads, its place after, "Migo, 10 strength, 2,
  1", top row first; "blocked" leading as on the cell), `perks` (named by `Powers_Subtitle_1` as the status's row: the
  columns' artifacts first, "column 4, Meteor, 0/10 XP: After you score...", then the starter perk,
  then the others in slot order, each with its description; a pilot's left out), `events` (`EncounterEvents.Log`: every line the narration
  said, uncapped, oldest first, opening on the newest; a `FollowLatest` buffer steps backward,
  `BufferControls`, so Ctrl+Up goes to older lines as it goes on elsewhere; cleared by a new controller: a run begun or
  resumed, a turn rewound; empty outside a run). The manual does not list the buffers (starvaders'
  does not either).
- Not ported from starvaders: its game screens, the fight (Core `Fight/`, Module `Fight/`), the
  popup reader. The encounter is this game's own (`Module/Encounter/`, Core `Encounter/`).

## Input
`Module/Input/OsKeys` reads the OS (`GetAsyncKeyState`, once per frame, only while the game window
is foreground); `KeyboardBinding` keeps its `KeyCode` identity on top; `FocusMode` disables the
Input System keyboard and the EventSystem's navigation. Host F6 uses the same OS read. The dev
driver's `/input` dispatches mod actions directly, so it does not exercise `OsKeys`: real keys need
a scratch script that brings the game forward (`AttachThreadInput` + `SetForegroundWindow`) and
sends `keybd_event`.

## Screens and game access
- `Module/Interop/GameScenes`: the live scene controllers and input handlers, rebuilt only on
  Unity's sceneLoaded/sceneUnloaded; `Controller<T>()`, `Item(controller, name)`,
  `Items(controller)` (every view item of the controller's scene in hierarchy order, gathered once
  per controller, keyed by native pointer: a proxy is a fresh object per cast, so never compare
  proxies by reference), `Components<T>()`, `InputFor(gameObject)`. No per-frame scene scans.
- `Module/UI/ViewNodes`: `Button(item, label, describeFrom)` over `AViewItem` (Enter is
  `IClickable.OnClick`, focus hovers the item through its scene's `InputHandler`), `Radio(item,
  selected)`, `Label(item)` (visible caption, else the hover tooltip's text, else a hidden caption),
  `Prose(scope, skip)` (a panel's texts outside view items, top to bottom as drawn, one per line).
- Screens so far:
  - `BootScreen` (the title, "PRESS ANY BUTTON TO PLAY": `BootupController.OnPressAnything`, up
    while `IsFinishedLoadingGame && !IsEndingScene`). Unchanged from starvaders.
  - `MainMenuScreen` (`MainMenuController`): the scene's shown clickable items outside the popup and
    the language picker, in hierarchy order; the column (Resume Game, its delete icon read "Delete
    run", Start Game, Options, Exit, Strategy Guide) then the edge icons (`ExtraButtons*`,
    `WishlistButtonFrame`: News, Join Discord, Credits, Change Language, Compendium, Wishlist).
    Resume, the delete icon, Strategy Guide, Compendium and Wishlist were hidden on this profile
    (no run, new save): untested.
  - `WelcomePopupScreen` (`PopupCanvas`, layer 5, exclusive; shown every launch): dialog named by
    the visible `Title`, one stop: its prose lines (focus lands on the first), then Join Discord and Got It; Escape is
    Got It.
  - `LanguageScreen` (`LanguageSelector`, layer 10, exclusive; up on the first launch over the
    popup, and from Change Language): dialog named by "Select Language", the `Page/Grid` languages
    as radio buttons (selected = the item's `Selected` child), Apply & Exit; Enter picks (the locale
    switches at once, and the mod's strings with it), landing picks nothing; Escape is Apply & Exit.
    Enter says "selected" at once (the radio's `StateText`); the switch then rewords every live part,
    which `LanguageSync` tells the navigator (`Navigation.LanguageChanged`: a silent re-baseline), so
    it is said once, in whichever language was in effect when the click returned.
  - `CharacterSelectScreen` (`CharacterSelectionController`, CharacterSelectionScene: the hub, where the
    unlocks show), one stop as drawn: Select Challenge (`OnPressDifficultyPanelButton`, a toggle of
    the chapter panel `DifficultyPanel`; "expanded"/"collapsed" from `IsDifficultyPanelActive`; while
    shut its label adds the chosen chapter drawn above it, `PanelBG/Image/Text (TMP)`); open, the
    chapters as a row (`DifficultyNodes` by `_index`, "CH1 - First Contact" = `<DifficultyName>_Title`
    of `DifficultyNames[_index]`, locked = `IsLocked`, completed = a shown `CompletedObjects`,
    selected = `IsSelected`, Enter = its click) and the chosen chapter's `DifficultyPanelFlavor` and
    `Chapter1TutObject` lines (its headings, repeating the name, and its arrows, the row's
    neighbours, left out), the challenges once unlocked (`ChallengeNodes`, a row: `<ChallengeName>_Title`,
    locked, completed, selected = `IsSelected`, Backspace `_Desc` and the `Mod` rules, Enter its
    click); the mission panel (`TutorialCompletePanel`, else `MissionPanel`: "Current
    Mission", "Win CH1 with any commander") as prose then its buttons ("New Chapter Unlocked!" =
    `OnPressCompleteTutorial`, writes the save); the shown `CommanderIcons` as a row by x (unlocked:
    "The Captain, New!": `<Commander>_Misc`, `_Arti_Title` being its perk's, Backspace the perk's
    name; "New!" from the `NewText` field's child text, the field being `UnlockIcon`; locked: the
    `Tooltip` title "Locked - The Invasion Begins!", Backspace "Reach Chapter 1 to unlock!";
    selected = `SelectedObject`), the row ending with `RandomIcon` (`OnPressRandom`, drawn only as its
    key prompt: the mod's "Random commander"); the chosen commander's panel (`Frame`): "The Captain, LV. 2"
    (`CommanderText`, `LevelTextObject` or `LevelTextObjectChinese`), the starting perk
    (`CommanderArtifactView`, as on the board, Backspace with keywords), the unique artifacts as
    drawn, rows by height top first, left to right (index i: `LockedArtifactItems[i]` shown = locked,
    by its `Tooltip` condition "Defeat 4 pilots with this commander to level up and unlock this!
    (2/4)" (the slot draws a bare lock), Backspace its title "Locked LV.3 Unique", Enter its click; else `UnlockedArtifactViews[i]` by name,
    "Monocle", Backspace its tooltip with the game's unlock text, as `SetArtifacts` sets it
    (`SetModel(model, true)`: "You now have a chance to find this perk during a run with this
    commander!", its dashed divider dropped): not a choice, a perk added to the run's pool); every other shown clickable in hierarchy order (Launch!, `StartButton`); Back
    (`OnExitPressed`) last, Escape too. `GameScenes.Items` can hold items destroyed since (a unique's
    lock, once unlocked): every loop skips `item == null` first, and the build is guarded so one
    failure does not leave the screen without nodes (it did: arrows went dead). Seen live: after the tutorial, after New Chapter Unlocked
    (CH1 open, three commanders unlocked), The Captain chosen. A unique ready to unlock ("You've unlocked
    this by defeating 2 pilots! Press to unlock!", its click reveals it and writes the save) says the
    revealed artifact ("Sky Arbiter"), the slot turning into another kind of node; seen live on a
    third, throwaway profile (P-3) with `LoggerDataSO.CommanderPilotsDefeated[Mothership]` set to 4 and
    `IsTutorialComplete`. The challenges and Launch! seen live too. On that fresh profile the game marks
    no commander icon selected (`IsSelected`, `SelectedObject` off on all) though `CurrentCommander` is
    set and its panel shows: read as the game draws it. Not seen: the missions' commander images.
  - `CompendiumScreen` (`CompendiumController`, CompendiumSceneAdditive over the main menu, from its
    Compendium icon, `GlossaryIcon`, which reads "Compendium, 12%": an edge icon is its tooltip's
    name with its caption; layer 10, exclusive; up while `Canvas` shows), named by `Pages/Title`.
    One stop: "Unlocked: 12%", "Completion: 0%" (a label text with its value as a child text); the
    10 `Pages` as a row of tabs named by their keys (`GameStats_Misc`, `Powers_Subtitle_1..3`,
    `<Rarity>Arti_Subtitle`, `Units_Subtitle`, `WarpedUnits_Subtitle`: a page not yet shown holds
    its prefab's subtitle), landing turns to it (`SetPage`); then the page. Stats: `General`'s
    figures ("Runs played: 1"), "0 / 19 Challenges Completed", the chapters (`DifficultyNodes`,
    `Chapter<n>_Title`, locked, completed) and the challenges (`ChallengeNodes`, `<ChallengeName>_Title`,
    Backspace `_Desc` and the `<Name>Mod_Title`/`_Desc` rules) as rows, "Won with 0 / 10
    Commanders", the commanders as a row of text (their click acts only on the hub): name or lock,
    "completed" while their `Star` shows. Other pages: each `GridLayoutGroup`'s
    children in hierarchy order, rows of its `constraintCount` (8 on the perks pages; world
    positions are 1.5 apart and useless for rows): an `ArtifactView` by name (Backspace its tooltip
    and keywords), an `InvaderCompendiumEntry` by name (Backspace its `EntityTooltip`), a
    `Locked*` placeholder "locked" (a bare picture with prefab text). Constellations (`CompendiumButton`'s
    `ConstellationButton`, `OnConstellationPressed`, once unlocked), Exit (`Pages/BG/Exit/ExitButton`,
    `OnExitPressed`), Escape too. The constellations panel (`IsConstellationPanelActive`), under the
    context "Constellations": `ConstellationsPageNumber` ("1-3 / 30"), the shown `Constellations`
    as the encounter's scene reads them (`ConstellationScreen.Node`: "Aegis, You can find these unique
    SHIELD related artifacts during this run.", Backspace the set's artifacts and keywords), the
    `LeftArrow`/`RightArrow` (no caption: the credits' "Previous page"/"Next page"; a turn lands on
    the page line), Exit (`ConstellationExitButton`, its caption drawn beside it: `Exit_Misc`),
    Escape too. Seen live with Unlock All on: 30 constellations, pages turned both ways.
  - `GuideScreen` (`StrategyGuideController`, StrategyGuideSceneAdditive, from the main menu or the game
    menu; layer 36, exclusive; up while its `Pages`' canvas shows), "Strategy Guide"
    (`StrategyGuide_Misc`). One stop: the `GuideButtons` as a row of tabs, landing turns
    (`SetPage`); a label is its caption and the icons pictured around it (separate Images:
    `T_RB_budgeicon`, `T_RB_rerollicon`, `T_RB_minidiceicon`, `Arrow`, `Target`) left to right as their
    words, "Budge + Chrono", "Mult & Mini-Dice", "When scored & Repeats"; locked, New! (its `NewText`).
    Then the page group by group (the page's children: a paragraph with its example beside it, top
    to bottom): the paragraph (a text that is the group or its direct child) as one line, its line
    breaks being layout; then the example's texts by height (a text with texts drawn in it as one
    line, left to right: "11 / 11 pages discovered!"; one-letter key prompts dropped) and its
    `ArtifactView`s as a row (Backspace their tooltips: page 5's quiz); the page's own buttons (the
    last page's Join Discord); Exit (`Pages/BG/Exit/ExitButton`), Escape too. Seen live from the main
    menu: pages 0, 1, 3, 5, 10.
  - `SettingsScreen` (`SettingsSceneController`, SettingsMenuScene, layer 35, exclusive; named by
    its `BackgroundPanel/TitleText`, "Options & Extras"): three Tab-stops. The tab strip
    (`CategoryButtons/*/Button`, captions beside them) is a row entered at `_currentTab`; landing on
    a tab clicks it (the page switches). The page (`Panels[_currentTab]`) in hierarchy order: rows
    are the view items in no other view item (a chooser's arrows and the "?" are parts of it), texts
    outside rows are lines ("Music by plasterbrain..."), except the key bindings' column headers
    (`Labels`) and a captionless button's caption beside it (Delete Current Profile). Then Reset to
    Default and Apply & Exit (`InnerBG/<name>/Button`); Escape is Apply & Exit. Rows
    (`UI/OptionNodes`, starvaders'): toggles (Enter = `OnClick`), sliders (Left/Right = the row's
    steps; 0..1 as percent), choosers (Resolution, Screen Type) as combo boxes whose Enter opens a
    `ChoiceSubmenuScreen` landing on the current choice (pick = `_currentOptionIndex` +
    `UpdateText`, applied with the settings), the screen type's own Apply after it, key bindings
    ("Select, W"; Enter pushes `KeyCaptureScreen`, the game's rebind with the keyboard lent),
    profile slots as radio buttons ("P-1" from `Existed` over "Create New Profile", selected =
    `Selected` child). A row's tooltip is read only while its `TooltipTriggerView` shows: every row
    has one, and the hidden ones hold prefab placeholder text. `SaveAndExit` applies, then unloads
    the scene in a run or reloads the main menu from it (a fresh menu: focus on its first item).
    Verified live: every page's readout, tab switching and wrap, a slider step, a toggle, both
    choosers' lists (opened and left with Escape), Escape out. Not exercised: picking a different
    resolution or screen type, a rebind (needs real keys), Reset to Default (resets at once, no
    prompt), the profile slots and Delete Current Profile (they change the save), options in a run.
  - `CreditsScreen` (`CreditsController`, CreditsScene): six pages, one shown
    (`PageList[currentPageIndex]`). One stop: the page's lines under a "Page 2/6" context, then
    Previous page (`UpButton`), Next page (`DownButton`), Exit (`ExitButton`, last page only), each
    while shown; a turn (`OnPressNext/PreviousPageButton`, ignored by the game while
    `IsTransitioning`; the index moves at once) focuses the new page's first line. Lines: a heading
    with a `Name` child pairs as "heading: name" (a `Name` with a nested `Name` is two names of a
    team row, read apart); " | " in a text is a separator; the localization page's legend
    ("<sprite Budge> : Translator") is not read but names the icons in its entries ("Español (es),
    Translator, LQA: ..."); a heading with no `Name` but a `*-logo` image under it names it by the
    sprite ("Music By: plasterbrain"). Art Direction's name is drawn as `"Text Settings"`,
    quotes and all. Escape is `OnPressExitButton`, from any page, as the game's cancel.
  - `EncounterScreen` (`EncounterController`, `Module/Encounter/`; its name said once per push, not
    again as an overlay closes (result, tip, pause); named by the score panel's
    `TargetText`, "Defeat the P.O.G. (Pluto Outer Guards)"; in a pilot fight, `IsPilotTribute`, the
    `PilotPanel` covers it and `TargetText` holds a missing key: the target reads "Roxy,
    Trailblazer", `GetCurrentPilot` and the panel's `AbilityTitleText`): two stops, starvaders' fight layout.
    A pilot is also an artifact (`ArtifactType.Pilot`, named for them, rarity `None`: its tooltip's
    rarity line is a missing key, skipped); pilot names exist only as images. `GameText.Of` and
    `GameLoc.Get` drop Unity's "No translation found for '...'" (the game builds keys that do not
    exist), logging each once. The final boss's `FinalBossPanel` is not read yet.
    The board: a header row ("column 3, Dice Power 2[, honey][, artifact]": power from
    `DicePanel.GetAllDicePower`, keyed by the 1-based column; the column artifact is
    `ArtifactSlots[x]`, the first 6 slots being the columns', `GetColumnArtifacts`), the 6x3 grid
    top row first ("2, 1, Migo, 10 strength": tiles "x, y" 1-based, row 1 at the bottom; the
    model's y 0 IS the bottom, each `GridColumn`'s layout being reversed), then a strict strip:
    in `Phase.Player` the dice in `DiceVisualOrder` ("3, frozen") and Launch
    (`DicePanel/ProceedKnob/ProceedLine/LaunchPanel/LaunchButton`). Focus lands on the first die.
    Backspace on a unit runs the game's `EntityTooltip.UpdateTooltip` on the tile's own tooltip
    (a hidden one holds prefab placeholders: "Mutantenschwamm", Legendary) and reads it; an
    artifact's runs `Tooltip.SetArtifactText` and leads with its title ("Meteor [9/10 XP]"). An
    artifact reads "Meteor, 9/10 XP" (", LV. 2" past level 1: `Level`, `CurrentXP`,
    `ArtifactModel.GetLevelXPRequirement`; a starter perk's XP is -1 and reads none). The status: an action tip while one shows, target
    and act, "0/100 attack" (`TotalScore` over `TargetTributeScore`: the score panel's two), turns, hearts, Budge, Chrono, Swap, Mini-Dice, Cash, Mult, a pilot's artifact alone (drawn on the pilot panel), the
    starter perk alone (`ArtifactType.Starter`, drawn in the commander's frame,
    `BossPanel/NormalFrame/CommanderArtifactView`, above the perks), then the perks (the other
    non-column slots, `LegendaryRelics/RelicHolder (n)`) as a row under "Perks" (the compendium's
    heading `Powers_Subtitle_1`, "Perks (1-24)", its range dropped: the game has no other plural), in
    slot order, as drawn left to right. The driver's `/input` readout includes a row's context on
    every move; speech says it only on entering.
    A tip waiting for an action ("Press the LAUNCH button") is said once on arrival
    (`OnUpdate`). `ModelItemDict` and `ArtifactViewDict` are keyed by the boxed `ID`: artifacts
    are matched by `ArtifactID.Number` instead. Keys (`EncounterKeys`, Game category): C
    coordinates, Ctrl+S the game menu's stats, Home/End row ends, PgUp/PgDn grid top and bottom; in the
    Run category (`UI/RunKeys`: live while `Encounter.Controller` exists, declared by the encounter and
    the overlays over it: pause, result, victory, constellations, tips, guide, settings, confirm)
    Ctrl+A attack ("0/100 attack"), Ctrl+T turns and hearts, Ctrl+R tokens, Ctrl+D dice, Ctrl+O
    target, Ctrl+M Mult. `/input` fires an action whether live or not: check liveness with
    `InputManager.IsLive` through `/eval` (by reflection: the module's assemblies are not referenced,
    and each reload loads a generation; take the last). The game's encounter keys
    (defaults, `EncounterInputHandler`'s PlayerInput): W/Space Click, Q Cancel, E Proceed
    (launch, leave the shop), R Reroll (Chrono), A Decrement and S Increment (budge), F Freeze,
    T ShowGuide, D NavigateNext, 1-3 SelectCard, Tab UINavigation. Ours for S, A, F, R, E, Q
    (`GameKeyBinding`, rebinds carry over) call `EncounterKeyInputController`'s own handlers
    (`OnIncrement`...), which read nothing of the context, with the focused die hovered first:
    they return at once while `TipManager.IsShowingTip()` (any tip, the action tips included).
    Pass `new InputAction.CallbackContext()`: the interop type is a reference wrapper, `default` is
    null and the call throws. A budge or freeze says the die after ("4, Budge 2"); a refusal
    says "can't budge/freeze/reroll"; off a die they do nothing. Narration (`EncounterEvents`,
    starvaders' FightEvents: settled snapshots, Core `EncounterDiff`, tested). The scoring never
    settles (Launch to the shop is one busy stretch, and Mult raised in it is back to 1 by the shop),
    so it is said column by column, as starvaders snapshots each invader's action: `ScoreDiceTask`
    scores the columns holding dice in ascending order, each a `ScoreColumnTask` with all that
    column's dice (`ArgKey.Amount`), the mini-dice through a second `ScoreDiceTask` (`isMini`). A
    Harmony prefix and postfix on its coroutine (`ScoreColumnTask._Execute_d__1.MoveNext`: state 0
    is the start, `false` the end; `_i_5__2` the 1-based column, `_isMini_5__3`), live engine only,
    snapshot as each column starts (what came before is said) and ends (`EncounterDiff.Step`: "column
    4: 80", then what it set off: "4D Printer level 2", "new tile at 5, 1", "Mult 2"; the gain is
    the column units' `AttackGainedThisTurn` summed, end less start; with several dice the label
    the game draws over the column joins it, "column 2: 140, Pair" (`GridColumn.PairTextObjects[dice -
    2]`, its `LocalizeStringEvent`'s string: hidden, the text is the prefab's); a base die's column that changed
    nothing is silent, a mini-die's always said, "Mini-Dice, column 5: 0", "3 Mini-Dice, column 1:
    0"; the Mini-Dice count spent in the scoring unsaid). Then the scoring board under the grid
    (`EncounterView.CurrentScoreBoard`, `StellarVisScoreBoard`: the turn's attack counting up, "X 2.0"
    beside it once there is Mult, then `CombineAndScore` writes CurrentScore times GlobalMult to
    `CenterTotalScoreText` and shows it), polled each frame of the scoring and said as the total
    shows, only with a Mult: "turn: 210, Mult 2.5, 525". The shop's settled snapshot then says the
    end of the turn, "1,220/500 attack", "Shop". Mult reads as the board prints it
    (`BigNumber.FormatBigDouble(value, "F1", null)`, its zero fraction dropped: `Encounter.Mult`,
    `WholeMult`): `BigDouble.ToString()` rounds to a whole number, so 2.5 read "3". No barks: the
    typing sound after a score is the score panel's flip digits (`ScorePanel.AnimateText`,
    `SoundName.P_Typing`, its one caller); the floating popups (`FloatingUpText` Level Up/attack up,
    `FloatingScoreText`, `FloatingIcon`) are covered by the steps; `P_ChaosDialogue` is never played. The old one-line summary ("column 3: 20, column 5:
    10, 30/100 attack", `AttackGainedLastTurn` once the shop opens) stays for a turn no step said.
    The result screen settles too: the last turn's total is said before it. Also said: target
    reached, a heart lost, "shop", a new turn ("3 Turns, rolled 2, 4, 6"), a reroll; and every other
    settled change: a new target (its round's resets unsaid), attack, units destroyed/added/moved
    (keyed `EntityName#Number`), strength ("all units +5 strength" when alike), states and charge,
    artifacts added/removed/moved/levelled/XP (keyed `ArtifactID.Number`; a pilot's artifact left
    out: its arrival is the new target's line, "Noel, Hyperfreeze, Act 2-3"), new tiles, clouds, honey,
    tokens ("Chrono 4"), the shop's rarity ("Shop rarity, +5 %": `EncounterValue.ShopRarity`, titled
    by its hover, whose key reads "% Shop Rarity"), Mult rising, the shop's offer rerolled (a purchase only removes), turns
    left changing within a phase. The raw turn figures are logged beside what is said. Verified
    live: the refusals with a tip up, launches (Shooter's Repeat, a levelled artifact building a
    tile, mini-dice, the last turn into the result), a budge ("6, cracked, Budge 5"). Not exercised:
    a real freeze, reroll, cancel.
    Tiles: only `GridModel.BuiltCoords` exist (the tutorial starts with the bottom row less column
    4, plus 2,2); others read "no tile"; `GetBuildableCoords` is where Build Tile can add one. The
    shop (`Phase.Draft`) strip: `DraftPanel.DraftChoices` still `IsActive` ("Meteor, Artifact, 2
    cash[, Uncommon][, cursed][, frozen][, can't afford]": `tempRarity`, `isCursed`, `IsLocked`,
    `CantBuyCostIcon`), then Proceed (`BG/ProceedButton`) and Skip remaining turns
    (`SkipTurns/ProceedLine/SkipButton`, when shown; after a target taken in the first turn it adds the
    drawn "One-Turn KO!, +50% SHOP RARITY", `ProceedLine/OTKO`; no skip cash is drawn). A choice's text is in `EntityTooltip` for a unit
    (title in `TitleLongerText`), `Tooltip` otherwise (the other holds prefab placeholders); hidden
    tooltips, so a text counts only by its own `activeSelf` (spells have no scaling line). Enter
    buys (the game's click); F freezes. Selections (`SelectionController.IsSelecting`): cells whose
    `TileView` or unit `EntityView`, and headers whose slot view (`ArtifactViewObjects[x]`, the
    first 6 the columns'), pass `IsSatisfied` read "target" first and take Enter (a targeted column slot says instead what
    placing does, the game's mark under its `Selectionhighlight`: `LevelUpObject` "Level Up!" for the same
    artifact, else `SwapObject`'s `ReplaceObject` caption "Replace", drawn only on the hovered slot but
    read on all; an empty slot draws an arrow and says nothing; one artifact per column); focus goes to the
    first on the selection's start; Escape is the game's Cancel. Landings: the shop's first choice
    as it opens, the first die (silent) at a new turn; the strip's nodes `QuietVanish`. Readouts
    checked by reflection over the module (`Invoke` on `EncounterScreen.ChoiceText`...) with a tip
    up; buying, placing, Proceed not exercised.
  - Keywords (`UI/Keywords`, starvaders' `*_InfoLine` more-info lines' counterpart): the game draws a
    tooltip's keywords as side boxes (`NestedTooltipController`), one per `NestedTooltipName`, from
    `<Name>_TooltipTitle`/`_TooltipDesc`; which ones is the list the fill keeps (`_nestedTooltips` on
    `Tooltip` after `SetArtifactText`/`SetTextKey`, on `EntityTooltip` after `UpdateTooltip`, from the
    definition's `NestedTooltips`), then the unit it creates (the definition's `NestedEntity`, whose
    whole tooltip the game draws beside it while `NestedEntityTooltipObject` is on, on `Tooltip` and
    `EntityTooltip` alike: Weaver's "Web: When scored: ...", "Unit - Token", "10 strength"). Every
    artifact, unit, shop choice and constellation set's details
    (Backspace and the ui buffer) end with them ("Mini-Dice: Rolled and scored automatically each
    turn."); a column header adds Dice Power's while it has some. The status stop's details: Mult,
    Budge, Chrono, Mini-Dice, Cash = their keyword entries (Swap has none); hearts, attack and target =
    the board's `TooltipTriggerView` hovers (`HeartTooltipTrigger` "Commander Hearts",
    `ScoreTooltipTrigger` "Objective", `PilotPanelTooltipTrigger` in a pilot fight else
    `MapTooltipTrigger`, both "VICTORY CONDITION"); the shop strip has "Shop rarity, +0 %"
    (`DraftPanel/BG/RarityObject`, its `RarityNumber`) with its hover, whose chances the game tells apart
    by colour alone: a figure coloured as a rarity (`ContentGetter.GetRarityTextColor`, matched for
    Common/Uncommon/Rare/Legendary) reads with its name, bars as commas ("Current chances: Common 84%,
    Uncommon 13%, Rare 6%, Legendary 0%"). Seen live in the shop, with the choices' keywords (Comet:
    Base Dice, Mult; Shooter: Repeat; the spells carry none).
  - The Oculus (and Warped Oculus) score "in the direction its looking at", drawn only as the
    sprite's facing: a unit with an `OculusTask`/`WarpedOculusTask` in `OnScoreTaskList` reads its
    `OculusDirection` last, "looking right" (`EncounterScreen.Facing`; N is a row up, E a column
    right, per `GridModel.GetCoordsInDirection`). Selection previews (`PreviewController.RunPreview`)
    draw nothing in this game: the whole scene diffed with a target hovered for Give Shield,
    Sacrifice Unit, Swap or Move (both picks), placing a unit; no unit has a `PreviewTaskList`.
  - Blocked (Xenn's "Sol & Luna": each turn 2 random artifacts with an Arrow trigger, drawn as a sun
    or moon, `BlockedBySol`/`BlockedByLuna`; Tippy's `BlockedByTippy`; `IsBlocked`, on units too):
    leads the readout as "target" does, "blocked, column 4, Dice Power 1, 4D Printer, ..." (status
    artifacts, cells alike), and is an event, "4D Printer: blocked", "Locked Slot: blocked lost"
    (Xenn re-picks as the turn ends, before "Shop"). Seen live with Xenn staged as act 2's pilot.
  - A pilot's own die (Kaia's: its face is the trapped column; the game's "Some pilots will use DICE
    against you!") is drawn on the pilot panel (`PilotPanel/AbilityPanel/DangerDice`, `IsDangerDice`),
    not in the tray: `Encounter.PilotDie`, the dice row's last die ("4, pilot die, cracked"), Budge's
    target as the tray's are, Ctrl+D, the roll ("rolled 3, 3, 5, pilot die 3"; "pilot die 4" when it
    changes in the turn). A column with `GridColumn.TrapObject` shown leads with "trapped" (Kaia's
    "When trapped column is scored..."). Seen live with Kaia, the trap following a budged die.
  - Garu's Turret strikes the bottom tile of the column his die names (the pilot die, budgeable) as
    the turn ends; the tile shows a danger zone (`TileView._dangerZoneView._isActive`, its
    `DangerZoneMask` faded in): the cell leads with "danger" (`Encounter.Danger`). Seen live, the zone
    following a budged die.
  - Tippy ("Call Tippy!": artifacts there cannot trigger, units cannot score) covers one of five
    areas, moved as each turn ends (`EncounterModel.TippyArea`, the `TippyController`'s shown
    `TippyArea`: 0 left artifact slots, 1 right, 2 left tiles, 3 right, 4 the dice buttons, where the
    game itself refuses Budge): the status stop's "Tippy: left artifacts" after the target, said as
    it moves; tiles in a covered half lead with "blocked", empty ones too; its units and artifacts
    are the game's blocked ones. Every pilot panel holds a `DangerDice`; only a pilot that rolls it
    has it `IsActive` (Tippy's is face 0). Seen live, the areas forced through `TippyArea` and
    `SetTippyAreaVisual`.
  - Blood Beast (starter perk `Hemophage`): Rage (`EncounterValue.Rage`, drawn only as a second bar
    on the hearts, `HeartsPanel/RageBar`; its hover is "Hearts / Rage") joins the status's hearts,
    "Hearts 5, Rage 2", and Beast Mode its turns left (`BeastTurn`, counted down as turns start):
    "..., Beast Mode, 3 Turns" (the game's `TurnsLeft_Misc`); said as they change ("Rage 5", "Beast
    Mode", "Beast Mode lost"). "Rage" and "Beast Mode" are the mod's (no key of their own). Seen live:
    Rage set to 4, a Sacrifice entering the game's Beast Mode.
  - The final boss (True Ending runs, CH6 "The End?": `RunModName.TrueEnding`, `EncounterModel.IsFinalBoss`,
    pilot Star, act 4 round 3): its draft (`Phase.FinalBossDraft`, `EncounterController.FinalBossDraft`:
    "Choose One" of three star cards, handicaps Star takes on) is the row under the board, each card
    its `CardTooltip` title, Backspace the tooltip, Enter its click; landed on as it shows. The
    status stop adds "HP 3" (`FinalBossHP`, drawn only as `FinalBossPanel.PanicBarImage`; "HP" is the
    mod's) and the held cards (`FinalBossPanel.StarCardViews`, filled ones); a taken card and HP
    changes are said. Its target reads "Star" (the pilot panel keeps the last pilot's ability).
    Seen live: reached with `stage.py skip` from act 1, Overclock taken.
  - The abilities knob under the dice and the shop (`BottomPanel/AbilitiesKnob`, found from
    `EncounterController.BudgeIconImage`) is a row below theirs: "Reroll Button, Chrono 2" (Enter =
    R's reroll), "Budge Button, Budge 6" ("undo" while `CanUndoBudge`, Enter = Q's undo; else "nothing
    to undo": budging is A/S), "Mini-Dice, 5" (`MiniDicePanel`); their tooltips are localization-only
    (`Keywords.LocalizedTitle`, `Tooltip(.., localized: true)`). A, S, F on the row act on the die or
    shop item last focused (recorded as each takes the focus). A shop reroll empties the offer and
    refills it: the snapshot's `LastOffer` makes it "Shop: ...", and focus that fell off a choice as it
    emptied returns to it (not one left on purpose). Q holds the narration (`EncounterEvents.Hold`)
    while it says the undo itself.
  - A new encounter met mid-turn (a run begun, Rewind Turn, Resume Game) says where it stands as its
    first settled frame is taken, "3 Turns, rolled 3, 6, 6" (`EncounterDiff.TurnLine`), and its turn
    lands on the first die as a new turn's does (the screen notices the controller changing). Seen
    live: Rewind Turn, Save and Exit then Resume Game, Options (its Sandbox tab holds "Disable
    Constellations System"), the Strategy Guide from the game menu, the skip simulation ("Mini-Dice
    3", "Shop rarity, +25 %", then the success screen), Game Over ("PILOTS DEFEATED : 0", Exit), the
    Overseer's level-up ("Choose One" of the perks to level, Backspace each one's next level), the
    hub's challenges (selecting one unselects the chapter).
  - Context highlighting: hovering a unit, artifact or shop choice lights the tiles and artifact
    slots its `ContextHighlight` tag names (`EncounterView.HighlightContextViews`: Book of Life's
    Migo units), each view's `ContextHighlightObject` switched on; focus hovers, so the details
    (Backspace, the ui buffer) of column headers, cells, the status's artifacts and shop choices add
    what is lit after the description: "affects: Migo at 2, 1, Migo at 4, 1" (`EncounterScreen.Highlighted`;
    tiles top row first, empty ones by place; slots in order, an empty column slot by its column).
  - `PauseScreen` (`EncounterController.PauseMenuController`, layer 33, exclusive; up while
    `PausePanel` is active and `IsPaused`): "Game Menu". Escape on the encounter opens it: the
    navigator's `BackFallback` is the game's Menu key handler (`EncounterKeyInputController.OnMenu`:
    ignored while a tip shows, else `OnPressPauseButton`, a toggle), and Escape in it resumes the same
    way. One stop, as drawn: Resume (landing: `IsSelected`, else the selected speed would take it),
    Options, Abandon Run, Save and Exit, Rewind Turn, the speeds as a row of radio buttons ("speed
    1".."speed 4": the arrows draw no caption; selected = `SelectedSpeedObjects[i].activeSelf`),
    Strategy Guide. Verified live: open, resume, both by Escape. A second stop, the stats
    panel (the board overlay drawn while paused, `IsDetailsVisible`; the game's Tab is `OnPressTab`):
    its `DetailType` views as a row of radio buttons named by `<DetailType>_Misc` ("ALL unit STR",
    "ATTACK & MULT scored last turn", "TRIGGERS and SCORED last turn"), entered at
    `CurrentDetailType`, Enter = `SetCurrentDetail` (after `OnPressTab` if hidden); then the overlay's
    figures, bare numbers whose meaning is the title: tiles top row first ("3, 2, Migo, 810": the
    `TileView` `AttackDetailText`, `MultDetailText` with "Mult", `ScoredDetailText`), then artifacts
    by slot (`ArtifactView` `MultDetailText` "Trail Pot, Mult 9.5", `TriggerDetailText`). Verified
    live, every view. Ctrl+S on the encounter (`enc.stats`) opens the menu (the Menu key) and jumps to
    this stop once built, queued after "Game Menu". Not exercised: any button but Resume.
  - `ResultScreen` (`EncounterController.ResultPanel`, layer 30, exclusive): the end screen shown
    (`GreenScreen` "INVASION SUCCESSFUL" with "NEXT TARGET : 500", `RedScreen` "SIMULATION FAILED"
    / "LOSE 1 HEART : EXTRA TURN", `OTKScreen` "SUCCESS!" / "ONE TURN KO!", `GameOverScreen`), a
    dialog named by its `Title`: its prose, then its buttons (no caption: the game's "Proceed").
    Up while `ABottomPanel.IsActive`: a dismissed panel slides off with its end screen still on.
    Seen live: the success screen after Skip remaining turns (phase `Simulation`). The panel sits
    under a view item, so `ViewNodes.Prose` skips only texts of items inside its scope. Not
    exercised: Proceed, the other three screens, `SimScreen` (the skipped turns played out).
  - `VictoryScreen` (`EncounterController.VictoryPanel`, layer 31, exclusive; up while `isShowing`):
    the run won, a dialog named by the shown ending's title (`NormalEndingObject`/`TrueEndingObject`/
    `UltimateEndingObject` are the title's variants: "True End...?"), one stop: "Pilots Defeated: Bud,
    Tippy" (drawn only as portraits: `EncounterModel.DefeatedPilots`, left out when none), "Best Turn:
    40, Mult 1" (`BestTurnScoreText`, `BestTurnMultText` "X 1"), then Endless Mode, Exit to Menu, Hide.
    Reached through the result panel's `VictoryScreen` ("VICTORY!", its Proceed =
    `OnPressShowVictoryPanel`, or E), which also brings it back after Hide; `ResultScreen` reads it.
    Seen by `ShowPanel()` over the final boss (it writes only `HasWonEndless`, past act 8); Exit to Menu
    records the win in the save (victory stats): never pressed on a staged panel.
  - `ConstellationScreen` (`EncounterController.ConstellationController`, layer 32, exclusive; up while
    its object is active; holds arrival until its `Canvas` is opaque, 3 s at most): "Constellations"
    (`Constellations_Misc`), the run's random modifiers, shown at a run's start once unlocked and from
    the encounter's Constellations button (`ConstellationButtonObject`, in the status stop after the
    artifacts; it draws only initials). One stop: the `Constellations` left to right by x, each one node
    of its shown texts (`TitleText`, `LockedText`, `PanelDescription` while `PanelTransform` shows, the
    `Parallaxer/RevealObject` caption "Reveal!" while shown, Enter = that button's click, its
    `OnPressReveal`); Backspace = the set (`ArtifactSetObject`'s views through `ArtifactTooltip(artifact,
    view)`, or `UnitSetObject`'s `InvaderCompendiumEntry`s), then Proceed (`ProceedButton/Mover/Button`,
    `OnContinuePressed`). The first-time notice (`UnlockConstellationObject`, "CONSTELLATIONS
    UNLOCKED!...") is the only node while shown; Enter is its `TipBox` click
    (`OnPressUnlockConstellation`, which writes the save). This save has none: tested by switching the
    controller on over the encounter (placeholder texts, one view given a board artifact), nothing
    pressed, then restored.
  - A pilot's rewards (`Screens/SpecialRewardStrip`, `EncounterController.specialRewardPanel`, while
    `IsActive` and one of its screens shows; `Phase.LegendaryDraft`, after a pilot's success screen):
    the encounter's strip, as the game draws it where the dice and shop go with the board above (it
    was an exclusive dialog, which hid the board), a row under the context of `TitleText` ("Level
    Up!", "You Receive", "Choose One"), each screen's first item landed on as it shows; the shown
    screen's view items in hierarchy order: `LevelUpScreen` (the starter perk, `LevelUpArtifact`, then Proceed =
    `OnPressLevelUpProceed`), `PilotDraftScreen` ("You Receive": Budge/Chrono/Heart holders read as text by
    their tooltips' titles, "Get +3 Budge", then Proceed = `ContinueObject`'s `OnPressReceiveAllButton`,
    which gives all three; `OnPressReceive*` return at once unless `HasRunMod(Scarcity)`, when each is
    a button receiving that one alone; seen live; their tooltips are filled only by
    `LocalizeStringEvent`s, which a hidden one leaves on the prefab's placeholder, "4D-принтер", so
    they read from the events' `GetLocalizedString`: `Keywords.Tooltip(.., localized: true)`), `LegendaryDraftScreen` (three artifacts =
    `OnPressArtifactSelect`, `OnPressSkipForMoney`, `OnContinuePressed`), `OverseerLevelupScreen`. An
    artifact reads as on the board, Backspace its tooltip (`ArtifactDetails(model, view)`); a button
    by its hover tooltip's title (`ButtonView.Tooltip`, `Keywords.Tooltip`), else caption, else
    "Proceed". A legendary pick (`OnPressArtifactSelect`) takes the artifact at once. Seen live: the
    level up ("The Tutorial - LV.2: ... +3 XP"), the loot, the legendary draft ("Choose One":
    Universe, Last Train, Shimmer, Skip For Income); not the overseer's.
    Before it, the encounter read on, so the game sat waiting with nothing said.
  - `TipScreen` (`TipManager.CurrentTip`, layer 40, exclusive; `UI/Tips`): a tutorial tip that
    waits for a click (its `ClickToContinue` line shows) as one text node; Enter is
    `TipManager.ClickTipBox` (ignored by the game until `TipBox._isFinished`, set when the box has
    popped in). Tip01 ("Welcome to DiceVaders!...") shows over the first encounter after Start Game
    on a new save; Tips 1 to 8 were clicked through with it. Tips without the line (waiting for an
    action) are the encounter's to read.
  - `ConfirmPromptScreen` (`ConfirmPromptController`, layer 37): starvaders', compiled against this
    game's controller (focus start from `GetFirstActiveViewItemToStartAt`), not yet seen live.
  - `ModMenuScreen`, `HelpScreen`, `KeyCaptureScreen`: the mod's own, unchanged.

## Dev driver (Debug only; `DVO_NO_DEV=1` disables, `DVO_DEV_PORT` sets the port, default 8773)
Guildrun's server unchanged: `/input`, `/nav`, `/gui`, `/speech`, `/log`, `/eval` (Roslyn, every
interop proxy referenced), `/wait`, `/typeinfo`, `/screenshot`, `/reload`, `/module`, `/health`.
`DVO_NO_SPEECH=1` skips Prism; `DVO_UPDATE_URL` replaces the update check's URL. Ports: guildrun
8771, starvaders 8772, dicevoders 8773, so the three can run side by side. **Never press a control
that starts a tutorial or a run, or changes the save, through the driver without asking**: verify
screens by reading them.

## Keys
F1 key help, arrows, Tab/Shift+Tab, Enter, Backspace (secondary), Escape, Home/End (within the
current stop), Alt+Up/Down sections, type-ahead letters, Ctrl+arrows buffers, Ctrl+Shift+A focus
mode, Ctrl+Shift+M mod menu.
