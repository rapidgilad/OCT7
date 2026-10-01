using System.Collections.Generic;
using Godot;
using OCT7.Game.Input;
using OCT7.Game.Tools;
using OCT7.Game.UI;
using OCT7.Game.Views;
using OCT7.Sim;
using OCT7.Sim.AI;
using OCT7.Sim.Data;
using OCT7.Sim.Match;

namespace OCT7.Game
{
    /// <summary>
    /// Bootstrap and host loop. Owns the <see cref="Simulation"/>, steps it at a fixed 10 Hz and
    /// lets the views interpolate between ticks. Everything gameplay-related happens inside the sim.
    /// </summary>
    public partial class Main : Node3D
    {
        public const int LocalPlayerId = 0;

        private readonly List<IAiController> _ais = new List<IAiController>();
        private readonly List<Command> _commandBuffer = new List<Command>();

        private Simulation _sim;
        private LaunchOptions _options;
        private double _accumulator;
        private ViewRegistry _views;
        private RtsCamera _camera;
        private SelectionController _selection;
        private DebugHud _hud;

        public Simulation Sim => _sim;

        public override void _Ready()
        {
            _options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());

            var data = GodotDataSource.Load();
            var errors = GameDataValidator.Validate(data);
            foreach (var error in errors)
            {
                GD.PushError($"[data] {error}");
            }

            _sim = MatchSetup.CreateSandboxMatch(data, _options.Faction0, _options.Faction1, _options.Seed);
            bool localAi = _options.Demo || _options.SmokeTestTicks > 0;
            if (localAi)
            {
                _ais.Add(new AdvanceAi(LocalPlayerId, _options.Seed));
            }

            _ais.Add(new AdvanceAi(1, _options.Seed));

            if (_options.SmokeTestTicks > 0)
            {
                RunSmokeTest(errors.Count == 0);
                return;
            }

            for (int i = 0; i < _options.FastForwardTicks; i++)
            {
                SimLoop.Step(_sim, _ais, _commandBuffer);
            }

            InputSetup.EnsureActions();
            BuildWorld();
        }

        public override void _Process(double delta)
        {
            if (_views == null)
            {
                return;
            }

            // Fixed-step simulation; cap the backlog so a long frame can't spiral.
            _accumulator = Mathf.Min(_accumulator + delta, 0.25);
            while (_accumulator >= SimConfig.TickSeconds)
            {
                SimLoop.Step(_sim, _ais, _commandBuffer);
                _accumulator -= SimConfig.TickSeconds;
            }

            float alpha = (float)(_accumulator / SimConfig.TickSeconds);
            _views.Sync(_sim, alpha, _selection.Selected);
            _hud.Refresh(_sim, _selection.Selected.Count);
        }

        /// <summary>Local player's move order (right-click). Goes through the command queue like AI orders.</summary>
        public void IssueMove(IReadOnlyList<int> squadIds, Vec2 target)
        {
            if (squadIds.Count == 0)
            {
                return;
            }

            _sim.Enqueue(new MoveSquadsCommand(LocalPlayerId, squadIds, _sim.Map.Grid.ClampToWorld(target)));
        }

        private void BuildWorld()
        {
            AddChild(SceneSetup.CreateEnvironment());
            AddChild(SceneSetup.CreateSun());

            var mapView = new MapView { Name = "Map" };
            AddChild(mapView);
            mapView.Build(_sim.Map, _sim.Players);

            _views = new ViewRegistry { Name = "Squads" };
            AddChild(_views);

            _camera = new RtsCamera { Name = "CameraRig", EdgePanEnabled = string.IsNullOrEmpty(_options.ScreenshotPath) };
            AddChild(_camera);
            _camera.SetBounds(_sim.Map.Grid.WorldWidth, _sim.Map.Grid.WorldHeight);
            if (_options.Overview)
            {
                _camera.SetView(new Vector3(_sim.Map.Grid.WorldWidth * 0.5f, 0f, _sim.Map.Grid.WorldHeight * 0.5f), 230f, 225f);
            }
            else if (_options.FocusArmy)
            {
                var c = ArmyCentroid(LocalPlayerId);
                _camera.SetView(new Vector3(c.X, 0f, c.Y), 70f, 225f);
            }
            else
            {
                var hq = _sim.GetPlayer(LocalPlayerId).HqPosition;
                _camera.SetView(new Vector3(hq.X + 30f, 0f, hq.Y + 30f), 75f, 225f);
            }

            var hudLayer = new CanvasLayer { Name = "HUD" };
            AddChild(hudLayer);
            _hud = new DebugHud { Name = "DebugHud" };
            hudLayer.AddChild(_hud);

            _selection = new SelectionController { Name = "Selection" };
            AddChild(_selection);
            _selection.Initialize(this, _camera, _hud);

            if (_options.SelectAllOnStart)
            {
                _selection.SelectAllOwn();
            }

            if (!string.IsNullOrEmpty(_options.ScreenshotPath))
            {
                AddChild(new ScreenshotTool(_options.ScreenshotPath, _options.ScreenshotAfterFrames) { Name = "Screenshot" });
            }

            _views.Sync(_sim, 1f, _selection.Selected);
        }

        private Vec2 ArmyCentroid(int playerId)
        {
            var sum = Vec2.Zero;
            int count = 0;
            foreach (var s in _sim.World.Squads)
            {
                if (s.OwnerId == playerId)
                {
                    sum += s.Position;
                    count++;
                }
            }

            return count > 0 ? sum / count : _sim.GetPlayer(playerId).HqPosition;
        }

        private void RunSmokeTest(bool dataValid)
        {
            for (int i = 0; i < _options.SmokeTestTicks; i++)
            {
                SimLoop.Step(_sim, _ais, _commandBuffer);
            }

            GD.Print($"[smoke-test] ticks={_sim.Tick} squads={_sim.World.Squads.Count} commands={_sim.ExecutedCommandCount} " +
                     $"hash={_sim.ComputeStateHash():x16} data={(dataValid ? "ok" : "INVALID")}");
            GetTree().Quit(dataValid ? 0 : 1);
        }
    }
}
