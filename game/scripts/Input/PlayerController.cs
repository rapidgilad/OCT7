using System.Collections.Generic;
using System.Linq;
using Godot;
using OCT7.Game.Match;
using OCT7.Game.Visual;
using OCT7.Sim;
using OCT7.Sim.Data;
using OCT7.Sim.Production;
using OCT7.Sim.Units;

namespace OCT7.Game.Input
{
    /// <summary>
    /// The local player's hands: selection (click, drag box, double-click = all of that type on screen,
    /// Shift adds, Ctrl+A all, Ctrl+1..9 / 1..9 control groups), context right-click (move with cover snap,
    /// attack, assist construction, rally point), hotkeys (R retreat, T reinforce, H stop) and structure
    /// placement with a live valid/invalid ghost (Space rotates sandbags, Esc / right-click cancels).
    /// Everything becomes a sim command, exactly like the AI's orders.
    /// </summary>
    public partial class PlayerController : Node
    {
        private const float ClickThreshold = 6f;

        private readonly HashSet<int> _squads = new HashSet<int>();
        private readonly Dictionary<int, List<int>> _groups = new Dictionary<int, List<int>>();
        private MatchController _match;
        private RtsCamera _camera;
        private bool _dragging;
        private Vector2 _dragStart;
        private Node3D _ghost;
        private MeshInstance3D _ghostBase;
        private StandardMaterial3D _ghostMaterial;
        private MeshInstance3D _moveMarker;
        private double _markerTime;

        public IReadOnlyCollection<int> SelectedSquads => _squads;
        public int SelectedStructure { get; private set; }
        public Rect2? DragRect { get; private set; }
        public StructureDef Placing { get; private set; }
        public int PlacingOrientation { get; private set; }
        public RejectReason PlacingReason { get; private set; }
        public Vector3? HoverGround { get; private set; }
        public int Version { get; private set; }

        private Simulation Sim => _match.Sim;
        private int Me => _match.LocalPlayerId;

        public void Initialize(MatchController match, RtsCamera camera)
        {
            _match = match;
            _camera = camera;
            _moveMarker = new MeshInstance3D
            {
                Mesh = MeshKit.CylMesh(1.8f, 1.8f, 0.05f, 32),
                MaterialOverride = MeshKit.Flat(new Color(0.55f, 1f, 0.55f, 0.75f)),
                Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            match.AddChild(_moveMarker);
        }

        // ------------------------------------------------------------ selection helpers

        public List<Squad> SelectedSquadObjects() =>
            _squads.Select(id => Sim.World.GetSquad(id)).Where(s => s != null).OrderBy(s => s.Id).ToList();

        public Structure SelectedStructureObject() => SelectedStructure != 0 ? Sim.World.GetStructure(SelectedStructure) : null;

        public bool HasEngineerSelected => SelectedSquadObjects().Any(s => s.Def.Engineer);

        private void Changed() => Version++;

        public void ClearSelection()
        {
            _squads.Clear();
            SelectedStructure = 0;
            Changed();
        }

        public void SelectAllOwn()
        {
            ClearSelection();
            foreach (var s in Sim.World.Squads)
            {
                if (s.OwnerId == Me)
                {
                    _squads.Add(s.Id);
                }
            }

            Changed();
        }

        public void SelectStructure(int id)
        {
            _squads.Clear();
            SelectedStructure = id;
            Changed();
        }

        // ------------------------------------------------------------ commands (also used by the HUD)

        public void CommandRetreat() => Issue(ids => new RetreatCommand(Me, ids));
        public void CommandReinforce() => Issue(ids => new ReinforceCommand(Me, ids));
        public void CommandStop() => Issue(ids => new StopCommand(Me, ids));

        public void Produce(string unitId)
        {
            if (SelectedStructure != 0)
            {
                _match.Enqueue(new ProduceCommand(Me, SelectedStructure, unitId));
            }
        }

        public void CancelQueue(int index)
        {
            if (SelectedStructure != 0)
            {
                _match.Enqueue(new CancelProductionCommand(Me, SelectedStructure, index));
            }
        }

        public void BeginPlacement(StructureDef def)
        {
            CancelPlacement();
            Placing = def;
            PlacingOrientation = 0;
            _ghost = new Node3D { Name = "PlacementGhost" };
            _ghostMaterial = MeshKit.Flat(new Color(0.4f, 1f, 0.4f, 0.35f), unique: true);
            _ghostBase = new MeshInstance3D { MaterialOverride = _ghostMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _ghost.AddChild(_ghostBase);
            _match.AddChild(_ghost);
            UpdateGhostMesh();
            Changed();
        }

        public void CancelPlacement()
        {
            if (_ghost != null)
            {
                _ghost.QueueFree();
                _ghost = null;
            }

            Placing = null;
            Changed();
        }

        private void Issue(System.Func<int[], Command> make)
        {
            var ids = SelectedSquadObjects().Select(s => s.Id).ToArray();
            if (ids.Length > 0)
            {
                _match.Enqueue(make(ids));
            }
        }

        // ------------------------------------------------------------ input

        public override void _UnhandledInput(InputEvent @event)
        {
            if (_match.Paused || Sim.IsOver)
            {
                return;
            }

            switch (@event)
            {
                case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left:
                    if (mb.Pressed)
                    {
                        if (Placing != null)
                        {
                            TryPlace(mb.ShiftPressed);
                        }
                        else
                        {
                            _dragging = true;
                            _dragStart = mb.Position;
                        }
                    }
                    else if (_dragging)
                    {
                        _dragging = false;
                        DragRect = null;
                        FinishSelection(_dragStart, mb.Position, mb.ShiftPressed, mb.DoubleClick);
                    }

                    break;

                case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Right && mb.Pressed:
                    if (Placing != null)
                    {
                        CancelPlacement();
                    }
                    else
                    {
                        ContextCommand(mb.Position);
                    }

                    break;

                case InputEventMouseMotion motion when _dragging:
                    if (motion.Position.DistanceTo(_dragStart) > ClickThreshold)
                    {
                        DragRect = new Rect2(_dragStart, motion.Position - _dragStart).Abs();
                    }

                    break;

                case InputEventKey key when key.Pressed && !key.Echo:
                    HandleKey(key);
                    break;
            }
        }

        private void HandleKey(InputEventKey key)
        {
            var code = key.PhysicalKeycode;
            if (code == Key.Escape)
            {
                if (Placing != null)
                {
                    CancelPlacement();
                }
                else if (_squads.Count > 0 || SelectedStructure != 0)
                {
                    ClearSelection();
                }
                else
                {
                    _match.TogglePause();
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.CtrlPressed && code == Key.A)
            {
                SelectAllOwn();
                return;
            }

            if (code >= Key.Key1 && code <= Key.Key9)
            {
                int group = (int)(code - Key.Key0);
                if (key.CtrlPressed)
                {
                    _groups[group] = _squads.ToList();
                }
                else if (_groups.TryGetValue(group, out var ids))
                {
                    ClearSelection();
                    foreach (var id in ids.Where(id => Sim.World.GetSquad(id) != null))
                    {
                        _squads.Add(id);
                    }

                    Changed();
                }

                return;
            }

            if (Placing != null && code == Key.Space)
            {
                PlacingOrientation = 1 - PlacingOrientation;
                UpdateGhostMesh();
                return;
            }

            if (_match.Hud != null && _match.Hud.HandleHotkey(code))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            switch (code)
            {
                case Key.R: CommandRetreat(); break;
                case Key.T: CommandReinforce(); break;
                case Key.H: CommandStop(); break;
            }
        }

        // ------------------------------------------------------------ picking

        private bool CanSee(Squad s) => s.OwnerId == Me || _match.Views.IsShown(Sim, s);

        private Squad PickSquad(Vector2 screen, bool ownOnly)
        {
            Squad best = null;
            float bestD = float.MaxValue;
            foreach (var s in Sim.World.Squads)
            {
                if ((ownOnly && s.OwnerId != Me) || !CanSee(s))
                {
                    continue;
                }

                var world = new Vector3(s.Position.X, s.Def.IsVehicle ? 1.5f : 1f, s.Position.Y);
                if (_camera.Camera.IsPositionBehind(world))
                {
                    continue;
                }

                float radius = s.Def.IsVehicle ? 46f : 30f;
                float d = _camera.Camera.UnprojectPosition(world).DistanceTo(screen);
                if (d < radius && d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }

            return best;
        }

        private Structure PickStructure(Vector2 screen)
        {
            if (!_camera.TryScreenToGround(screen, out var ground))
            {
                return null;
            }

            int id = Sim.World.StructureAt(Sim.Map.Grid.WorldToCell(new Vec2(ground.X, ground.Z)));
            var st = id != 0 ? Sim.World.GetStructure(id) : null;
            if (st == null)
            {
                return null;
            }

            return st.OwnerId == Me || (_match.Views.TryGetStructureView(id, out var v) && v.EverSeen) ? st : null;
        }

        private void FinishSelection(Vector2 start, Vector2 end, bool additive, bool doubleClick)
        {
            if (start.DistanceTo(end) <= ClickThreshold)
            {
                var squad = PickSquad(end, ownOnly: false);
                if (squad != null && squad.OwnerId == Me)
                {
                    if (!additive)
                    {
                        ClearSelection();
                    }

                    if (doubleClick)
                    {
                        foreach (var s in Sim.World.Squads.Where(s => s.OwnerId == Me && s.Def.Id == squad.Def.Id && OnScreen(s)))
                        {
                            _squads.Add(s.Id);
                        }
                    }
                    else if (additive && _squads.Contains(squad.Id))
                    {
                        _squads.Remove(squad.Id);
                    }
                    else
                    {
                        _squads.Add(squad.Id);
                    }

                    SelectedStructure = 0;
                    Changed();
                    return;
                }

                var structure = PickStructure(end);
                if (structure != null && structure.OwnerId == Me)
                {
                    SelectStructure(structure.Id);
                    return;
                }

                if (!additive)
                {
                    ClearSelection();
                }

                return;
            }

            if (!additive)
            {
                ClearSelection();
            }

            var rect = new Rect2(start, end - start).Abs();
            foreach (var s in Sim.World.Squads)
            {
                if (s.OwnerId != Me)
                {
                    continue;
                }

                var world = new Vector3(s.Position.X, 1f, s.Position.Y);
                if (!_camera.Camera.IsPositionBehind(world) && rect.HasPoint(_camera.Camera.UnprojectPosition(world)))
                {
                    _squads.Add(s.Id);
                }
            }

            SelectedStructure = 0;
            Changed();
        }

        private bool OnScreen(Squad s)
        {
            var world = new Vector3(s.Position.X, 1f, s.Position.Y);
            if (_camera.Camera.IsPositionBehind(world))
            {
                return false;
            }

            return GetViewport().GetVisibleRect().HasPoint(_camera.Camera.UnprojectPosition(world));
        }

        private void ContextCommand(Vector2 screen)
        {
            var squads = SelectedSquadObjects();
            if (squads.Count == 0)
            {
                var st = SelectedStructureObject();
                if (st != null && _camera.TryScreenToGround(screen, out var rally))
                {
                    _match.Enqueue(new SetRallyPointCommand(Me, st.Id, new Vec2(rally.X, rally.Z)));
                    ShowMarker(rally);
                }

                return;
            }

            var ids = squads.Select(s => s.Id).ToArray();
            var enemy = PickSquad(screen, ownOnly: false);
            if (enemy != null && enemy.OwnerId != Me)
            {
                _match.Enqueue(new AttackCommand(Me, ids, enemy.Id));
                return;
            }

            var structure = PickStructure(screen);
            if (structure != null)
            {
                if (structure.OwnerId != Me)
                {
                    _match.Enqueue(new AttackCommand(Me, ids, structure.Id));
                    return;
                }

                if (!structure.IsComplete && squads.Any(s => s.Def.Engineer))
                {
                    _match.Enqueue(ConstructCommand.Assist(Me, ids, structure.Id));
                    return;
                }
            }

            if (_camera.TryScreenToGround(screen, out var ground))
            {
                _match.Enqueue(new MoveSquadsCommand(Me, ids, new Vec2(ground.X, ground.Z)));
                ShowMarker(ground);
            }
        }

        private void ShowMarker(Vector3 at)
        {
            _moveMarker.Position = new Vector3(at.X, 0.06f, at.Z);
            _moveMarker.Scale = Vector3.One;
            _moveMarker.Visible = true;
            _markerTime = 0;
        }

        // ------------------------------------------------------------ placement

        private void UpdateGhostMesh()
        {
            if (Placing == null)
            {
                return;
            }

            ProductionSystem.FootprintSize(Placing, PlacingOrientation, out int sx, out int sy);
            float cs = Sim.Map.Grid.CellSize;
            _ghostBase.Mesh = new BoxMesh { Size = new Vector3(sx * cs, Placing.Kind == StructureKind.Sandbags ? 1f : 2.5f, sy * cs) };
            _ghostBase.Position = new Vector3(0f, Placing.Kind == StructureKind.Sandbags ? 0.5f : 1.25f, 0f);
        }

        private bool GhostCell(out GridPos cell, out Vector3 worldCenter)
        {
            cell = default;
            worldCenter = default;
            if (!_camera.TryScreenToGround(GetViewport().GetMousePosition(), out var ground))
            {
                return false;
            }

            cell = Sim.Map.Grid.WorldToCell(new Vec2(ground.X, ground.Z));
            ProductionSystem.FootprintSize(Placing, PlacingOrientation, out int sx, out int sy);
            var min = ProductionSystem.MinCellFor(cell, sx, sy);
            float cs = Sim.Map.Grid.CellSize;
            worldCenter = new Vector3((min.X + sx * 0.5f) * cs, 0f, (min.Y + sy * 0.5f) * cs);
            return true;
        }

        private void TryPlace(bool keepPlacing)
        {
            if (!GhostCell(out var cell, out _))
            {
                return;
            }

            var engineers = SelectedSquadObjects().Where(s => s.Def.Engineer).Select(s => s.Id).ToArray();
            if (engineers.Length == 0)
            {
                _match.Hud?.ShowMessage("Select engineers to build");
                CancelPlacement();
                return;
            }

            if (PlacingReason != RejectReason.None)
            {
                _match.Hud?.ShowMessage(UI.Hud.Describe(PlacingReason, Placing.Id, Sim));
                return;
            }

            _match.Enqueue(new ConstructCommand(Me, engineers, Placing.Id, cell, PlacingOrientation));
            if (!keepPlacing)
            {
                CancelPlacement();
            }
        }

        public override void _Process(double delta)
        {
            int before = _squads.Count;
            _squads.RemoveWhere(id => Sim.World.GetSquad(id) == null);
            if (SelectedStructure != 0 && Sim.World.GetStructure(SelectedStructure) == null)
            {
                SelectedStructure = 0;
                Changed();
            }

            if (_squads.Count != before)
            {
                Changed();
            }

            if (_moveMarker.Visible)
            {
                _markerTime += delta;
                float t = (float)(_markerTime / 0.7);
                _moveMarker.Scale = new Vector3(1f - t * 0.75f, 1f, 1f - t * 0.75f);
                _moveMarker.Visible = t < 1f;
            }

            HoverGround = _camera.TryScreenToGround(GetViewport().GetMousePosition(), out var hover) ? hover : (Vector3?)null;

            if (Placing != null && _ghost != null && GhostCell(out var cell, out var center))
            {
                _ghost.Position = center;
                PlacingReason = Sim.Production.CanPlace(Me, Placing, cell, PlacingOrientation);
                _ghostMaterial.AlbedoColor = PlacingReason == RejectReason.None ? new Color(0.4f, 1f, 0.4f, 0.35f) : new Color(1f, 0.3f, 0.25f, 0.4f);
            }
        }
    }
}
