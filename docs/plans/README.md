# Plans

Every approved plan is committed here **before** implementation starts, so any session (cloud or desktop) can see what was decided, why, and what happened.

## Convention
- **File name:** `YYYY-MM-DD-NN-short-name.md`, where `NN` is a running number in approval order.
- **Status header:** each file starts with one:
  - `Status`: Proposed, In progress, Done, or Not implemented (superseded).
  - `Outcome`: commits and results.
  - `Note`: optional context.
- **Approved text:** the plan body stays as approved. Later changes go in the status header or a "Decision log" section, not in rewrites.
- **Mechanic changes:** a plan that changes a mechanic also updates [`../10-implementation-reference.md`](../10-implementation-reference.md) in the same commit.

## Index

| # | Date | Plan | Status |
|---|---|---|---|
| 01 | 2026-10-01 | [V1 game design (the GDD)](2026-10-01-01-v1-game-design.md) | Done; docs 01–09 |
| 02 | 2026-10-01 | [Unity 6 project skeleton](2026-10-01-02-unity-skeleton.md) | Superseded by 03 (Godot chosen for cloud builds); input to 08 |
| 03 | 2026-10-01 | [Godot 4 .NET project skeleton](2026-10-01-03-godot-skeleton.md) | Done |
| 04 | 2026-10-01 | [v0.1 first playable skirmish](2026-10-01-04-v0.1-first-playable.md) | Done (phases 1–5; 6–8 under 05) |
| 05 | 2026-10-01 | [Finish v0.1 (phases 6–8)](2026-10-01-05-v0.1-finish.md) | Done, plus sounds and the first visual pass |
| 06 | 2026-10-02 | [Automatic Windows builds](2026-10-02-06-windows-builds.md) | Done; CI `dev-latest` release |
| 07 | 2026-10-02 | [Desktop handoff](2026-10-02-07-desktop-handoff.md) | Done |
| 08 | 2026-10-02 | [**Unity port**](2026-10-02-08-unity-port.md) | **Proposed**; owned by the desktop session |

## Reading order for a new session
1. [`../../CLAUDE.md`](../../CLAUDE.md): rules and commands.
2. [`../10-implementation-reference.md`](../10-implementation-reference.md): the game as built.
3. The newest plan in this folder whose status is not Done. Today that is [08, the Unity port](2026-10-02-08-unity-port.md).
4. The design docs (`../01`–`../09`) for intent and future scope.
