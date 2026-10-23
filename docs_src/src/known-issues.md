# Troubleshooting And Known Issues

## No Speech

If the mod does not speak:

1. Wait. The first launch after installing, and after every game update, takes a stretch of silence while the loader generates its assemblies.
2. Confirm `winhttp.dll`, `doorstop_config.ini`, `prism.dll`, `dotnet`, and `BepInEx` sit next to `DiceVaders.exe`, and the mod is at `BepInEx\plugins\DiceVoders`.
3. Launch through Steam.
4. Check the BepInEx log:

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\DiceVaders\BepInEx\LogOutput.log
   ```

5. Look for `DiceVoders`. Loader problems are at the top of the file.

The log is truncated on every launch, so whatever is in it is from the current session. Attach it to any bug report.

## Shift+Tab Opens Something Else

That is the Steam overlay. Turn it off; see [Installation](installation.md#turn-off-the-steam-overlay-first).

## The Keyboard Stopped Working

You pressed Ctrl+Shift+A, which hands the keyboard to the game. Press it again.

## The Game's Own Keys Do Nothing

While the mod has the keyboard, the game does not hear it. The mod runs the game's encounter actions for you (budge, freeze, reroll, proceed, cancel, the game menu; see [Controls](controls.md#the-encounter-the-games-keys)); for anything else, Ctrl+Shift+A hands the keyboard to the game and takes it back.
