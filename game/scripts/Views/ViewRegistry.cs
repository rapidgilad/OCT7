using System.Collections.Generic;
using Godot;
using OCT7.Sim;
using OCT7.Sim.Units;

namespace OCT7.Game.Views
{
    /// <summary>
    /// Mirrors the simulation into views every frame: creates/removes squad and structure views, interpolates
    /// positions, applies fog of war for the local player (enemy units hidden unless visible; enemy structures
    /// shown once seen), and routes per-model events (deaths, reinforcements) to the right view.
    /// </summary>
    public partial class ViewRegistry : Node3D
    {
        private readonly Dictionary<int, SquadView> _squads = new Dictionary<int, SquadView>();
        private readonly Dictionary<int, StructureView> _structures = new Dictionary<int, StructureView>();
        private readonly HashSet<int> _alive = new HashSet<int>();
        private readonly List<int> _dead = new List<int>();
        private Node3D _corpses;

        public int LocalPlayerId { get; set; }

        /// <summary>When true (spectator / demo), everything is visible.</summary>
        public bool RevealAll { get; set; }

        public override void _Ready()
        {
            _corpses = new Node3D { Name = "Corpses" };
            AddChild(_corpses);
        }

        public bool TryGetSquadView(int id, out SquadView view) => _squads.TryGetValue(id, out view);
        public bool TryGetStructureView(int id, out StructureView view) => _structures.TryGetValue(id, out view);

        public bool IsShown(Simulation sim, Squad s) =>
            RevealAll || s.OwnerId == LocalPlayerId || sim.Vision.IsVisible(LocalPlayerId, s.Position);

        public void OnEvent(Simulation sim, SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.ModelKilled:
                    if (_squads.TryGetValue(e.TargetId, out var v))
                    {
                        v.OnModelKilled(e.Value);
                    }

                    break;
                case SimEventType.ModelReinforced:
                    if (_squads.TryGetValue(e.TargetId, out var r))
                    {
                        r.OnModelReinforced(e.Value);
                    }

                    break;
                case SimEventType.SquadDestroyed:
                    if (_squads.TryGetValue(e.TargetId, out var dead) && dead.IsVehicle)
                    {
                        dead.DetachWreck();
                    }

                    break;
            }
        }

        public void Sync(Simulation sim, float alpha, IReadOnlyCollection<int> selected, int selectedStructure, float delta)
        {
            _alive.Clear();
            foreach (var squad in sim.World.Squads)
            {
                _alive.Add(squad.Id);
                if (!_squads.TryGetValue(squad.Id, out var view))
                {
                    view = new SquadView();
                    AddChild(view);
                    view.Build(squad, TeamColors.For(squad.OwnerId), _corpses);
                    view.Position = new Vector3(squad.Position.X, 0f, squad.Position.Y);
                    view.Rotation = new Vector3(0f, Mathf.Atan2(squad.Facing.X, squad.Facing.Y), 0f);
                    _squads.Add(squad.Id, view);
                }

                view.Visible = IsShown(sim, squad);
                var p = Vec2.Lerp(squad.PrevPosition, squad.Position, alpha);
                Vector3? aim = null;
                if (squad.TargetId != 0 && sim.TryGetEntityPosition(squad.TargetId, out var t))
                {
                    aim = new Vector3(t.X, 0f, t.Y);
                }

                view.UpdateVisual(squad, new Vector3(p.X, 0f, p.Y), Mathf.Atan2(squad.Facing.X, squad.Facing.Y), delta, sim.Tick, aim);
                view.SetSelected(System.Linq.Enumerable.Contains(selected, squad.Id));
            }

            RemoveMissing(_squads, _alive);

            _alive.Clear();
            foreach (var st in sim.World.Structures)
            {
                _alive.Add(st.Id);
                if (!_structures.TryGetValue(st.Id, out var view))
                {
                    view = new StructureView();
                    AddChild(view);
                    view.Build(st, TeamColors.For(st.OwnerId));
                    _structures.Add(st.Id, view);
                }

                if (RevealAll || st.OwnerId == LocalPlayerId || sim.Vision.IsVisible(LocalPlayerId, st.Center))
                {
                    view.EverSeen = true;
                }

                view.Visible = view.EverSeen;
                view.UpdateVisual(st);
                view.SetSelected(st.Id == selectedStructure);
            }

            RemoveMissing(_structures, _alive);
        }

        private void RemoveMissing<T>(Dictionary<int, T> views, HashSet<int> alive) where T : Node3D
        {
            _dead.Clear();
            foreach (var id in views.Keys)
            {
                if (!alive.Contains(id))
                {
                    _dead.Add(id);
                }
            }

            foreach (var id in _dead)
            {
                views[id].QueueFree();
                views.Remove(id);
            }
        }
    }
}
