> **Status:** Done
>
> **Outcome:** Implemented in `574a776`. CI exports Windows and Linux after every green push. The default branch updates the rolling `dev-latest` release (`OCT7-windows.zip`).
>
> **Note:** Stays active until Unity builds replace it (Unity port milestone U4).
>
> The plan below is the approved text, unchanged. Its file paths and numbers describe the project as it was at the time.

# Plan: automatic Windows builds after every push

## Context
You want to play the latest version on your Windows PC without installing Godot, .NET or Git ("q1 of course you should").

Today, CI on `claude/coh-rts-game-design-06vm8t` only builds and tests. Nothing playable comes out of it.

**Goal:** every push that passes CI publishes a ready-to-run `OCT7-windows.zip` at a fixed download link. You download it, unzip it and double-click `OCT7.exe`. The game menu shows which commit the build came from.

What's already in place:
- `game/export_presets.cfg` has a "Windows Desktop" preset (export path `../builds/windows/OCT7.exe`) and a "Linux" preset. Both already include `data/*.json` and `data/maps/*.json`.
- `.gitignore` excludes `builds/`.
- The `godot` CI job already installs Godot 4.5.1 mono on Linux.

## Changes

### 1. `.github/workflows/ci.yml`: a new `package` job after `godot`
- **Triggers:** add `workflow_dispatch` (manual runs). The job runs only on `push` and manual runs, not on PRs.
- **Permissions and concurrency:** the job gets `permissions: contents: write`, plus a concurrency group per ref so a newer push cancels an older build.
- **Steps:**
  1. Checkout, `setup-dotnet` 8.0.x, install Godot 4.5.1 mono (same commands as the `godot` job).
  2. **Export templates**, cached with `actions/cache` by Godot version so the large download happens only once:
     - Download `Godot_v4.5.1-stable_mono_export_templates.tpz`.
     - Unzip only `windows_release_x86_64.exe`, `windows_debug_x86_64.exe`, `linux_release.x86_64`, `linux_debug.x86_64` and `version.txt`.
     - Install them into `~/.local/share/godot/export_templates/4.5.1.stable.mono/`.
  3. **Build stamp:** write `game/build_info.json` (`{commit, short, date, branch, message}`) from the workflow's built-in variables.
  4. Run `dotnet build game/OCT7.Game.csproj`, then the headless `--import`.
  5. Export both presets:
     - `godot --headless --path game --export-release "Windows Desktop" ../builds/windows/OCT7.exe`
     - `godot --headless --path game --export-release "Linux" ../builds/linux/OCT7.x86_64`
  6. **Test the exported game itself:** run the exported Linux binary headless with `--smoke-test 600`. It must print `result=PASS`. Windows and Linux are built from the same data and code, so this proves the export includes the data files and the C# assemblies.
  7. **Package `OCT7-windows.zip`:**
     - The export: `OCT7.exe`, `OCT7.pck` and the `data_OCT7.Game_windows_x86_64/` folder.
     - Launcher `.bat` files:
       - `AI battle.bat` (`-- --demo --fast-forward 6000 --focus-army`)
       - `Gallery.bat` (`-- --showcase all`)
       - `Effects test.bat` (`-- --showcase idf --vfx-test`)
       - `Play (safe graphics).bat` (`--rendering-method gl_compatibility`)
     - A `README.txt`: how to run it, the SmartScreen "More info → Run anyway" step (the exe is unsigned), a controls summary, and the build stamp.
  8. **Publish:**
     - Upload `OCT7-windows.zip` (and `OCT7-linux.zip`) as workflow artifacts, kept 30 days.
     - On pushes to the repo's default branch (currently `claude/coh-rts-game-design-06vm8t`), update a rolling release, `dev-latest`, using the `gh` CLI with `GITHUB_TOKEN`:
       - Create the release if it's missing.
       - Move the tag to this commit.
       - Replace the zip assets (`gh release upload --clobber`).
       - Set the notes to the commit, date and message.
     - It's a normal release, not a pre-release, so the fixed link `https://github.com/rapidgilad/OCT7/releases/latest/download/OCT7-windows.zip` always gets the newest build. You must be logged in to GitHub, since the repo is private.

### 2. Game: show the build in the menu
- `game/scripts/UI/MainMenu.cs`: if `res://build_info.json` exists, the version line reads `v0.1 · build <short> · <date>`; otherwise it reads `dev build`.
- `game/export_presets.cfg`, both presets:
  - Add `build_info.json` to `include_filter`.
  - Set `application/modify_resources=false` for Windows. The resource-editing tool (rcedit) isn't available on Linux runners; this avoids an export error or warning.
- `.gitignore`: add `game/build_info.json` (it's generated).

### 3. Docs
- **README:** a "Download and play" section at the top (latest-build link, unzip, run, SmartScreen note, the launcher `.bat` files). The existing "build it yourself" section stays for development.
- **CLAUDE.md:** one line on the `package` job, the `dev-latest` release, and how to test an export locally.
- **docs/09 §8:** note that each push publishes a playable Windows build.

## Verification
1. **Local export first**, in this container, before pushing:
   - Download the templates and extract only the needed files. If the container's disk space is too tight for the roughly 1 GB template file, skip to CI.
   - Run both exports. Check the Windows folder has `OCT7.exe`, `OCT7.pck` and the `data_*` folder.
   - Run the exported Linux binary with `--smoke-test 600` and `--smoke-test 30000 --full-match`. Both must exit 0, and their hashes must match the editor runs (`abc254d66a095c02` and `d2b762761181a436`).
2. Run the usual checks: `dotnet format`, the build and the tests (the sim doesn't change).
3. **Push, then watch the workflow** with the GitHub tools (list runs, read job logs). Fix and re-push until all jobs pass. Confirm the `dev-latest` release exists with `OCT7-windows.zip`, check the asset sizes, and read the published README.
4. **Hand-off:** give you the download link and short steps:
   1. Download the zip.
   2. Unzip it to e.g. `C:\Games\OCT7`.
   3. Run `OCT7.exe`; on the SmartScreen warning click More info → Run anyway.
   4. Check the build stamp in the menu.
   5. Try the launcher `.bat` files.
