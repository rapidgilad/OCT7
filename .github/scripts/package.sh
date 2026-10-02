#!/usr/bin/env bash
# Packages the exported builds (builds/windows, builds/linux) into OCT7-windows.zip and OCT7-linux.zip:
# adds launcher .bat files and a README.txt to the Windows build. Run from the repo root after exporting.
# Inputs (env, optional): SHORT_SHA, BUILD_DATE, BUILD_BRANCH, BUILD_MESSAGE.
set -euo pipefail

short="${SHORT_SHA:-local}"
date="${BUILD_DATE:-$(date -u +%Y-%m-%d)}"
branch="${BUILD_BRANCH:-local}"
message="${BUILD_MESSAGE:-local build}"
win="builds/windows"
linux="builds/linux"

test -f "$win/OCT7.exe" && test -f "$win/OCT7.pck"
ls "$win"/data_*/OCT7.Game.dll > /dev/null

# Windows text files need CRLF line endings.
crlf() { sed 's/$/\r/'; }

bat() {
  printf '@echo off\nrem %s\nstart "" "%%~dp0OCT7.exe" %s\n' "$2" "$3" | crlf > "$win/$1"
}

bat "AI battle.bat" "Watch the AI fight itself; starts about 10 minutes in with the camera on a firefight." "-- --demo --fast-forward 6000 --focus-army"
bat "Gallery.bat" "Every building and unit of all three factions (pan with WASD, zoom with the wheel)." "-- --showcase all"
bat "Effects test.bat" "Explosions, fire, tracers and rockets on a loop." "-- --showcase idf --vfx-test"
bat "Play (safe graphics).bat" "Compatibility renderer: use this if the game crashes or shows a black screen." "--rendering-method gl_compatibility"

crlf > "$win/README.txt" <<TXT
IRON SWORDS: FRONTLINES (OCT7) - development build
Build $short ($date), branch $branch
$message

HOW TO RUN
1. Extract this zip anywhere (for example C:\\Games\\OCT7) and keep all files together.
2. Double-click OCT7.exe.
   Windows SmartScreen may say "Windows protected your PC" because the build is not code-signed.
   Click "More info", then "Run anyway".
3. Choose your faction, the enemy faction and the AI difficulty, then click START SKIRMISH.
   The build number is shown under the title in the main menu.

EXTRA LAUNCHERS
  AI battle.bat              Watch the AI fight itself (starts about 10 minutes in, camera on a firefight).
  Gallery.bat                Every building and unit of all three factions.
  Effects test.bat           Explosions, fire, tracers and rockets on a loop.
  Play (safe graphics).bat   Use this if the game crashes or shows a black screen (older or integrated GPUs).

CONTROLS
  WASD / arrows / screen edge   Pan the camera        Mouse wheel   Zoom        Q / E   Rotate
  Left click / drag             Select (double-click: all of that type on screen, Shift: add)
  Right click                   Move (snaps to cover), attack, help build; with a building selected: rally point
  R / T / H                     Retreat / Reinforce / Stop
  Z X C V B N M G               Command card slots (build menu, production)
  Ctrl+1-9 / 1-9                Assign / recall control groups
  Space                         Rotate a structure while placing it
  Esc                           Cancel, deselect, or open the pause menu (volume, surrender)
  The dot at the cursor shows cover at the destination: green heavy, yellow light, red open.

GOAL
  Capture sectors for income, hold victory points (diamonds) to drain the enemy's tickets,
  or destroy the enemy HQ to win outright.

FEEDBACK
  Please mention the build number when reporting a problem; a screenshot helps (Win+Shift+S).
TXT

rm -f OCT7-windows.zip OCT7-linux.zip
(cd "$win" && zip -qr ../../OCT7-windows.zip .)
if [ -f "$linux/OCT7.x86_64" ]; then
  (cd "$linux" && zip -qr ../../OCT7-linux.zip . -x smoke.log)
fi

ls -la OCT7-*.zip
