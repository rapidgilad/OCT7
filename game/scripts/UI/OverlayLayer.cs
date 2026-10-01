using Godot;
using OCT7.Game.Match;
using OCT7.Game.Views;
using OCT7.Sim;
using OCT7.Sim.Data;
using OCT7.Sim.Units;
using OCT7.Sim.World;

namespace OCT7.Game.UI
{
    /// <summary>
    /// 2D overlay drawn over the 3D view: health bars (with soldier pips), suppression / pinned / retreat markers,
    /// construction and capture progress, the cover preview at the cursor, the drag-selection box and placement hints.
    /// </summary>
    public partial class OverlayLayer : Control
    {
        private MatchController _match;

        public void Initialize(MatchController match)
        {
            _match = match;
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Process(double delta) => QueueRedraw();

        private bool Project(Vector3 world, out Vector2 screen)
        {
            var cam = _match.CameraRig.Camera;
            screen = default;
            if (cam.IsPositionBehind(world))
            {
                return false;
            }

            screen = cam.UnprojectPosition(world);
            return GetViewportRect().HasPoint(screen);
        }

        public override void _Draw()
        {
            if (_match?.Sim == null)
            {
                return;
            }

            var sim = _match.Sim;
            var font = ThemeDB.FallbackFont;
            var selected = _match.Player.SelectedSquads;

            foreach (var s in sim.World.Squads)
            {
                if (!_match.Views.IsShown(sim, s))
                {
                    continue;
                }

                float height = s.Def.IsVehicle ? 4.2f : 2.9f;
                if (!Project(new Vector3(s.Position.X, height, s.Position.Y), out var p))
                {
                    continue;
                }

                bool mine = s.OwnerId == _match.LocalPlayerId;
                bool isSelected = mine && System.Linq.Enumerable.Contains(selected, s.Id);
                if (!isSelected && s.HealthFraction > 0.995f && s.SuppressionState == SuppressionState.None && !s.IsRetreating && s.Models == s.Def.SquadSize)
                {
                    continue; // keep the screen clean: only damaged, suppressed or selected squads show bars
                }

                float w = s.Def.IsVehicle ? 46f : 36f;
                var team = TeamColors.For(s.OwnerId);
                DrawRect(new Rect2(p.X - w * 0.5f - 1f, p.Y - 1f, w + 2f, 7f), new Color(0f, 0f, 0f, 0.7f));
                DrawRect(new Rect2(p.X - w * 0.5f, p.Y, w * s.HealthFraction, 5f), team.Lightened(0.15f));
                if (!s.Def.IsVehicle)
                {
                    float pip = w / s.Def.SquadSize;
                    for (int i = 0; i < s.Def.SquadSize; i++)
                    {
                        var c = i < s.Models ? new Color(0.95f, 0.95f, 0.9f) : new Color(0.3f, 0.3f, 0.3f);
                        DrawRect(new Rect2(p.X - w * 0.5f + i * pip + 1f, p.Y + 7f, pip - 2f, 3f), c);
                    }
                }

                string tag = s.IsRetreating ? "RETREAT" : s.SuppressionState == SuppressionState.Pinned ? "PINNED" : s.SuppressionState == SuppressionState.Suppressed ? "SUPPRESSED" : null;
                if (tag != null)
                {
                    var color = s.IsRetreating ? new Color(0.85f, 0.9f, 1f) : s.SuppressionState == SuppressionState.Pinned ? new Color(1f, 0.4f, 0.3f) : new Color(1f, 0.8f, 0.3f);
                    DrawString(font, new Vector2(p.X - w * 0.5f, p.Y - 4f), tag, HorizontalAlignment.Left, -1, 11, color);
                }
            }

            foreach (var st in sim.World.Structures)
            {
                bool mine = st.OwnerId == _match.LocalPlayerId;
                bool seen = mine || (_match.Views.TryGetStructureView(st.Id, out var v) && v.EverSeen);
                if (!seen || (st.IsComplete && st.HealthFraction > 0.99f && st.Id != _match.Player.SelectedStructure) || st.Def.Kind == StructureKind.Sandbags && st.IsComplete)
                {
                    continue;
                }

                if (!Project(new Vector3(st.Center.X, st.Def.Height + 2.5f, st.Center.Y), out var p))
                {
                    continue;
                }

                float w = 60f;
                DrawRect(new Rect2(p.X - w * 0.5f - 1f, p.Y - 1f, w + 2f, 8f), new Color(0f, 0f, 0f, 0.7f));
                if (!st.IsComplete)
                {
                    DrawRect(new Rect2(p.X - w * 0.5f, p.Y, w * st.BuildProgress, 6f), new Color(0.9f, 0.8f, 0.4f));
                    DrawString(font, new Vector2(p.X - w * 0.5f, p.Y - 4f), $"Building {(int)(st.BuildProgress * 100)}%", HorizontalAlignment.Left, -1, 11, UiTheme.Accent);
                }
                else
                {
                    DrawRect(new Rect2(p.X - w * 0.5f, p.Y, w * st.HealthFraction, 6f), TeamColors.For(st.OwnerId).Lightened(0.15f));
                }
            }

            foreach (var sector in sim.Territory.Sectors)
            {
                bool capturing = sector.OwnerId < 0 && sector.Progress > 0f || sector.OwnerId >= 0 && sector.Progress < 1f;
                if (!capturing || !Project(new Vector3(sector.Position.X, 7.2f, sector.Position.Y), out var p))
                {
                    continue;
                }

                int who = sector.OwnerId >= 0 ? sector.OwnerId : sector.CapturingPlayerId;
                var color = who >= 0 ? TeamColors.For(who).Lightened(0.2f) : Colors.White;
                DrawArc(p, 11f, 0f, Mathf.Tau, 32, new Color(0f, 0f, 0f, 0.6f), 5f);
                DrawArc(p, 11f, -Mathf.Pi * 0.5f, -Mathf.Pi * 0.5f + Mathf.Tau * sector.Progress, 32, color, 3.5f);
            }

            DrawCoverPreview(sim, font);

            if (_match.Player.DragRect.HasValue)
            {
                var r = _match.Player.DragRect.Value;
                DrawRect(r, new Color(0.55f, 1f, 0.55f, 0.12f));
                DrawRect(r, new Color(0.55f, 1f, 0.55f, 0.9f), false, 1.5f);
            }

            if (_match.Player.Placing != null)
            {
                var mouse = GetViewport().GetMousePosition();
                string text = _match.Player.PlacingReason == RejectReason.None
                    ? $"{_match.Player.Placing.Name} · left-click to place · Space rotates · right-click cancels"
                    : Hud.Describe(_match.Player.PlacingReason, _match.Player.Placing.Id, sim);
                DrawString(font, mouse + new Vector2(18f, -12f), text, HorizontalAlignment.Left, -1, 13,
                    _match.Player.PlacingReason == RejectReason.None ? UiTheme.Good : UiTheme.Bad);
            }
        }

        private void DrawCoverPreview(Simulation sim, Font font)
        {
            var hover = _match.Player.HoverGround;
            if (!hover.HasValue || _match.Player.SelectedSquads.Count == 0 || _match.Player.Placing != null)
            {
                return;
            }

            var pos = new Vec2(hover.Value.X, hover.Value.Z);
            CoverType cover = CoverType.None;
            if (sim.Cover.TryFindCoverNear(pos, sim.Rules.CoverSnapRadius, out var coverPos))
            {
                cover = sim.Cover.GetCover(sim.Map.Grid.WorldToCell(coverPos));
                pos = coverPos;
            }

            if (!Project(new Vector3(pos.X, 0.3f, pos.Y), out var p))
            {
                return;
            }

            var color = cover == CoverType.Heavy ? new Color(0.4f, 1f, 0.4f) : cover == CoverType.Light ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.35f);
            DrawCircle(p, 6f, new Color(0f, 0f, 0f, 0.6f));
            DrawCircle(p, 4.5f, color);
            DrawString(font, p + new Vector2(9f, 4f), cover == CoverType.None ? "open" : cover == CoverType.Heavy ? "heavy cover" : "light cover", HorizontalAlignment.Left, -1, 11, color);
        }
    }
}
