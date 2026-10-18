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
  started as a copy of `es`. Inline icons in game strings: Attack, Budge, Chrono, MiniDice,
  ShopToken (`UI/GameText`).
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
`screenshot`). `dev.py screenshot` timed out with the game in the background.

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
The version lives in `Directory.Build.props` alone (0.0.1). Not ported yet: the release scripts
(`build_release.ps1`, `create-release.ps1`), the Rust installer, the mdbook manual; starvaders-access
has all of them with constants to retarget. The update check asks
`amerikrainian/dicevoders`'s latest release and logs a 404 until one exists.

## Architecture (guildrun's, see its CLAUDE.md for the long form)
- **Contracts** (netstandard2.0, permanent), **host** `DiceVoders` (net6.0, permanent: Plugin,
  Prism, `HostPump`, `ModuleLoader`, dev server), **Core** (netstandard2.0, reloadable, engine-free,
  unit-tested: key graph, navigator, buffers, input manager, screens, strings), **Module** (net6.0,
  reloadable, engine-coupled: day-to-day work), **Tests**.
- Buffers: only `ui` (the focused control's lines) so far; `BufferKeys` lists the keys.
- Not ported from starvaders: its game screens, the fight (Core `Fight/`, Module `Fight/`), the
  popup reader, the pause fallback for Escape (`Navigation.BackFallback` is unset).

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
  - `EncounterScreen` (`EncounterController`, `Module/Encounter/`; named by the score panel's
    `TargetText`, "Defeat the P.O.G. (Pluto Outer Guards)"): two stops, starvaders' fight layout.
    The board: a header row ("column 3, Dice Power 2[, honey][, artifact]": power from
    `DicePanel.GetAllDicePower`, keyed by the 1-based column; the column artifact is
    `ArtifactSlots[x]`, the first 6 slots being the columns', `GetColumnArtifacts`), the 6x3 grid
    top row first ("2, 1, Migo, 10 strength": tiles "x, y" 1-based, row 1 at the bottom; the
    model's y 0 IS the bottom, each `GridColumn`'s layout being reversed), then a strict strip:
    in `Phase.Player` the dice in `DiceVisualOrder` ("die 3, frozen") and Launch
    (`DicePanel/ProceedKnob/ProceedLine/LaunchPanel/LaunchButton`). Focus lands on the first die.
    Backspace on a unit runs the game's `EntityTooltip.UpdateTooltip` on the tile's own tooltip
    (a hidden one holds prefab placeholders: "Mutantenschwamm", Legendary) and reads it; an
    artifact's runs `Tooltip.SetArtifactText`. The status: an action tip while one shows, target
    and act, Target Defense (`TargetTributeScore`), Total Attack (`TotalScore`: the score panel's
    two), turns, hearts, Budge, Chrono, Swap, Mini-Dice, Cash, Mult, the non-column artifacts.
    A tip waiting for an action ("Press the LAUNCH button") is said once on arrival
    (`OnUpdate`). `ModelItemDict` and `ArtifactViewDict` are keyed by the boxed `ID`: artifacts
    are matched by `ArtifactID.Number` instead. Keys (`EncounterKeys`, Game category): C
    coordinates, Ctrl+A attack and defense, Ctrl+T turns and hearts, Ctrl+R tokens, Ctrl+D dice,
    Ctrl+O target, Home/End row ends, PgUp/PgDn grid top and bottom. The game's encounter keys
    (defaults, `EncounterInputHandler`'s PlayerInput): W/Space Click, Q Cancel, E Proceed
    (launch, leave the shop), R Reroll (Chrono), A Decrement and S Increment (budge), F Freeze,
    T ShowGuide, D NavigateNext, 1-3 SelectCard, Tab UINavigation; not taken yet. Not built: the
    shop strip, budge/freeze/reroll/launch keys, scoring narration, selections (placing a unit).
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
