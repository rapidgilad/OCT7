using System.Collections.Generic;
using Godot;
using OCT7.Sim;

namespace OCT7.Game.Views
{
    /// <summary>Creates, updates and removes squad views to mirror the simulation every frame.</summary>
    public partial class ViewRegistry : Node3D
    {
        private readonly Dictionary<int, SquadView> _views = new Dictionary<int, SquadView>();
        private readonly HashSet<int> _alive = new HashSet<int>();
        private readonly List<int> _dead = new List<int>();

        public bool TryGetView(int squadId, out SquadView view) => _views.TryGetValue(squadId, out view);

        /// <param name="alpha">0..1 progress from the previous tick to the current one.</param>
        public void Sync(Simulation sim, float alpha, ICollection<int> selected)
        {
            _alive.Clear();
            foreach (var squad in sim.World.Squads)
            {
                _alive.Add(squad.Id);
                if (!_views.TryGetValue(squad.Id, out var view))
                {
                    view = new SquadView();
                    AddChild(view);
                    view.Build(squad, TeamColors.For(squad.OwnerId));
                    view.Rotation = new Vector3(0f, Mathf.Atan2(squad.Facing.X, squad.Facing.Y), 0f);
                    _views.Add(squad.Id, view);
                }

                var p = Vec2.Lerp(squad.PrevPosition, squad.Position, alpha);
                view.Position = new Vector3(p.X, 0f, p.Y);

                float targetYaw = Mathf.Atan2(squad.Facing.X, squad.Facing.Y);
                view.Rotation = new Vector3(0f, Mathf.LerpAngle(view.Rotation.Y, targetYaw, 0.15f), 0f);
                view.SetSelected(selected.Contains(squad.Id));
                view.SetAliveModels(squad.Models);
            }

            _dead.Clear();
            foreach (var id in _views.Keys)
            {
                if (!_alive.Contains(id))
                {
                    _dead.Add(id);
                }
            }

            foreach (var id in _dead)
            {
                _views[id].QueueFree();
                _views.Remove(id);
            }
        }
    }
}
