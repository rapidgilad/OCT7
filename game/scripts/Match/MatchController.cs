using System.Collections.Generic;
using Godot;
using OCT7.Game.Input;
using OCT7.Game.Tools;
using OCT7.Game.UI;
using OCT7.Game.Vfx;
using OCT7.Game.Views;
using OCT7.Sim;
using OCT7.Sim.AI;
using OCT7.Sim.Data;
using OCT7.Sim.Match;

namespace OCT7.Game.Match
{
    /// <summary>
    /// Host of one skirmish: owns the <see cref="Simulation"/> and the AI, steps both at a fixed 10 Hz via
    /// <see cref="SimLoop"/>, routes sim events to views / VFX / HUD, and builds the 3D world and UI in code.
    /// The local player only ever changes the match through <see cref="Enqueue"/>.
    /// </summary>
    public partial class MatchController : Node3D
    {
        private readonly List<IAiController> _ais = new List<IAiController>();
        private readonly List<Command> _commandBuffer = new List<Command>();

        private LaunchOptions _options;
        private bool _dataValid = true;
        private double _accumulator;
        private bool _revealAll;
        private MapView _mapView;
        private SectorView _sectorView;
        private GroundOverlays _overlays;
        private VfxManager _vfx;
        private PauseMenu _pauseMenu;
        private EndScreen _endScreen;
        private bool _endShown;

        // End-screen statistics, counted from sim events.
        private int _fielded;
        private int _lost;
        private int _destroyed;

        public Simulation Sim { get; private set; }
        public int LocalPlayerId => 0;
        public bool Paused { get; private set; }
        public Hud Hud { get; private set; }
        public ViewRegistry Views { get; private set; }
        public RtsCamera CameraRig { get; private set; }
        public PlayerController Player { get; private set; }

        private bool AiControlsLocalPlayer => MatchSettings.Demo || _options.SmokeTestTicks > 0;

        public override void _Ready()
        {
            if (!MatchSettings.LaunchHandled)
            {
                // Scene run directly (e.g. F6 in the editor): honour the command line here.
                MatchSettings.LaunchHandled = true;
                var parsed = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
                parsed.ApplyTo();
                MatchSettings.Launch = parsed;
            }

            _options = MatchSettings.Launch ?? new LaunchOptions();
            MatchSettings.Launch = null;

            var data = GodotDataSource.Load();
            foreach (var error in GameDataValidator.Validate(data))
            {
                _dataValid = false;
                GD.PushError($"[data] {error}");
            }

            Sim = MatchSetup.CreateSkirmish(data, MatchSettings.PlayerFaction, MatchSettings.EnemyFaction, MatchSettings.Seed, MatchSettings.MapId);
            if (AiControlsLocalPlayer)
            {
                _ais.Add(SkirmishAi.Create(Sim, LocalPlayerId, MatchSettings.Difficulty));
            }

            _ais.Add(SkirmishAi.Create(Sim, 1, MatchSettings.Difficulty));

            if (_options.SmokeTestTicks > 0)
            {
                RunSmokeTest();
                return;
            }

            int fastForward = _options.FullMatch ? int.MaxValue : _options.FastForwardTicks;
            const int fastForwardCap = 60 * 60 * 10; // one simulated hour
            for (int i = 0; i < fastForward && i < fastForwardCap && !Sim.IsOver; i++)
            {
                SimLoop.Step(Sim, _ais, _commandBuffer);
                CountStats();
            }

            _revealAll = MatchSettings.Demo && !_options.Fog;
            InputSetup.EnsureActions();
            BuildWorld();
        }

        // ------------------------------------------------------------------ player API

        /// <summary>Local player's order. Goes through the command queue exactly like AI orders.</summary>
        public void Enqueue(Command command)
        {
            if (command == null || Sim.IsOver || MatchSettings.Demo)
            {
                return;
            }

            command.PlayerId = LocalPlayerId;
            Sim.Enqueue(command);
        }

        public void TogglePause()
        {
            if (Sim.IsOver)
            {
                return;
            }

            Paused = !Paused;
            _pauseMenu.Visible = Paused;
        }

        public void Surrender()
        {
            if (Sim.IsOver)
            {
                return;
            }

            Sim.Enqueue(new SurrenderCommand(LocalPlayerId));
            Paused = false;
            _pauseMenu.Visible = false;
        }

        // ------------------------------------------------------------------ loop

        public override void _Process(double delta)
        {
            if (Views == null)
            {
                return;
            }

            if (!Paused && !Sim.IsOver)
            {
                // Fixed-step simulation; cap the backlog so a long frame can't spiral.
                _accumulator = Mathf.Min(_accumulator + delta, 0.25);
                while (_accumulator >= SimConfig.TickSeconds && !Sim.IsOver)
                {
                    SimLoop.Step(Sim, _ais, _commandBuffer);
                    _accumulator -= SimConfig.TickSeconds;
                    DispatchEvents();
                }
            }

            float alpha = Sim.IsOver || Paused ? 1f : (float)(_accumulator / SimConfig.TickSeconds);
            Views.Sync(Sim, alpha, Player.SelectedSquads, Player.SelectedStructure, Paused ? 0f : (float)delta);
            _sectorView.UpdateVisual();
            _overlays.UpdateOverlays(Sim);

            if (Sim.IsOver && !_endShown)
            {
                ShowEndScreen();
            }
        }

        private void DispatchEvents()
        {
            foreach (var e in Sim.Events)
            {
                Views.OnEvent(Sim, e);
                _vfx.OnEvent(e);
                Hud.OnEvent(e);
            }

            CountStats();
        }

        private void CountStats()
        {
            foreach (var e in Sim.Events)
            {
                if (e.Type == SimEventType.UnitProduced && e.PlayerId == LocalPlayerId)
                {
                    _fielded++;
                }
                else if (e.Type == SimEventType.SquadDestroyed)
                {
                    if (e.PlayerId == LocalPlayerId)
                    {
                        _lost++;
                    }
                    else
                    {
                        _destroyed++;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ world

        private void BuildWorld()
        {
            AddChild(SceneSetup.CreateEnvironment());
            AddChild(SceneSetup.CreateSun());

            _mapView = new MapView { Name = "Map" };
            AddChild(_mapView);
            _mapView.Build(Sim.Map);

            _overlays = new GroundOverlays { Name = "Overlays", LocalPlayerId = LocalPlayerId, FogEnabled = !_revealAll };
            AddChild(_overlays);
            _overlays.Build(Sim);

            _sectorView = new SectorView { Name = "Sectors" };
            AddChild(_sectorView);
            _sectorView.Build(Sim);

            Views = new ViewRegistry { Name = "Units", LocalPlayerId = LocalPlayerId, RevealAll = _revealAll };
            AddChild(Views);

            _vfx = new VfxManager { Name = "Vfx", LocalPlayerId = LocalPlayerId, RevealAll = _revealAll };
            AddChild(_vfx);
            _vfx.Initialize(Sim, Views);

            CameraRig = new RtsCamera { Name = "CameraRig", EdgePanEnabled = string.IsNullOrEmpty(_options.ScreenshotPath) };
            AddChild(CameraRig);
            CameraRig.SetBounds(Sim.Map.Grid.WorldWidth, Sim.Map.Grid.WorldHeight);
            PlaceCamera();

            Player = new PlayerController { Name = "Player" };
            AddChild(Player);
            Player.Initialize(this, CameraRig);

            var ui = new CanvasLayer { Name = "UI" };
            AddChild(ui);
            var overlay = new OverlayLayer { Name = "Overlay" };
            ui.AddChild(overlay);
            overlay.Initialize(this);
            Hud = new Hud { Name = "Hud" };
            ui.AddChild(Hud);
            Hud.Initialize(this, Player);
            Hud.Minimap.Initialize(this, _mapView.GroundTexture);
            _pauseMenu = new PauseMenu { Name = "PauseMenu" };
            ui.AddChild(_pauseMenu);
            _pauseMenu.Initialize(this);
            _endScreen = new EndScreen { Name = "EndScreen" };
            ui.AddChild(_endScreen);

            if (_options.SelectAllOnStart)
            {
                Player.SelectAllOwn();
            }

            if (!string.IsNullOrEmpty(_options.ScreenshotPath))
            {
                AddChild(new ScreenshotTool(_options.ScreenshotPath, _options.ScreenshotAfterFrames) { Name = "Screenshot" });
            }

            if (MatchSettings.Demo)
            {
                Hud.ShowMessage("Demo: AI plays both sides", UiTheme.Accent);
            }

            Views.Sync(Sim, 1f, Player.SelectedSquads, Player.SelectedStructure, 0f);
        }

        private void PlaceCamera()
        {
            var grid = Sim.Map.Grid;
            var center = new Vec2(grid.WorldWidth * 0.5f, grid.WorldHeight * 0.5f);
            if (_options.Overview)
            {
                CameraRig.SetView(new Vector3(center.X, 0f, center.Y), 230f, YawToward(Sim.GetPlayer(LocalPlayerId).HqPosition, center));
                return;
            }

            var hq = Sim.GetPlayer(LocalPlayerId).HqPosition;
            var focus = _options.FocusArmy ? ArmyCentroid(LocalPlayerId) : hq + (center - hq) * 0.12f;
            CameraRig.SetView(new Vector3(focus.X, 0f, focus.Y), 75f, YawToward(hq, center));
        }

        /// <summary>Camera yaw (degrees, snapped to 45°) that looks from <paramref name="from"/> toward <paramref name="to"/>.</summary>
        private static float YawToward(Vec2 from, Vec2 to)
        {
            var d = to - from;
            if (d.X * d.X + d.Y * d.Y < 1e-4f)
            {
                return 225f;
            }

            float yaw = Mathf.RadToDeg(Mathf.Atan2(-d.X, -d.Y));
            return Mathf.PosMod(Mathf.Round(yaw / 45f) * 45f, 360f);
        }

        private Vec2 ArmyCentroid(int playerId)
        {
            var sum = Vec2.Zero;
            int count = 0;
            foreach (var s in Sim.World.Squads)
            {
                if (s.OwnerId == playerId)
                {
                    sum += s.Position;
                    count++;
                }
            }

            return count > 0 ? sum / count : Sim.GetPlayer(playerId).HqPosition;
        }

        // ------------------------------------------------------------------ end of match

        private void ShowEndScreen()
        {
            _endShown = true;
            Paused = false;
            _pauseMenu.Visible = false;
            Player.CancelPlacement();
            bool victory = Sim.WinnerId == LocalPlayerId;
            int loserId = Sim.WinnerId < 0 ? LocalPlayerId : (victory ? 1 : LocalPlayerId);
            string reason = Sim.WinnerId < 0 ? "Both sides were defeated at once" : DescribeDefeat(loserId, victory);

            int held = 0;
            foreach (var s in Sim.Territory.Sectors)
            {
                if (s.OwnerId == LocalPlayerId)
                {
                    held++;
                }
            }

            int seconds = (int)Sim.ElapsedSeconds;
            string stats = $"Match time {seconds / 60:00}:{seconds % 60:00}\n" +
                           $"Squads fielded {_fielded} · lost {_lost} · enemy squads destroyed {_destroyed}\n" +
                           $"Sectors held {held} / {Sim.Territory.Sectors.Count}";
            _endScreen.Show(victory, reason, stats);
        }

        private string DescribeDefeat(int loserId, bool victory)
        {
            var loser = Sim.GetPlayer(loserId);
            if (loser.Tickets <= 0)
            {
                return victory ? "Enemy victory tickets exhausted" : "Our victory tickets ran out";
            }

            if (loser.HadHq && Sim.World.FindHq(loserId) == null)
            {
                return victory ? "Enemy headquarters destroyed" : "Our headquarters was destroyed";
            }

            return victory ? "The enemy surrendered" : "We surrendered";
        }

        // ------------------------------------------------------------------ headless

        private void RunSmokeTest()
        {
            for (int i = 0; i < _options.SmokeTestTicks; i++)
            {
                SimLoop.Step(Sim, _ais, _commandBuffer);
                if (_options.FullMatch && Sim.IsOver)
                {
                    break;
                }
            }

            bool decided = Sim.IsOver && Sim.WinnerId >= 0;
            bool ok = _dataValid && (!_options.FullMatch || decided);
            GD.Print($"[smoke-test] {MatchSettings.PlayerFaction} vs {MatchSettings.EnemyFaction} ({MatchSettings.Difficulty}) seed={MatchSettings.Seed} " +
                     $"ticks={Sim.Tick} squads={Sim.World.Squads.Count} structures={Sim.World.Structures.Count} commands={Sim.ExecutedCommandCount} " +
                     $"over={Sim.IsOver} winner={Sim.WinnerId} hash={Sim.ComputeStateHash():x16} data={(_dataValid ? "ok" : "INVALID")} result={(ok ? "PASS" : "FAIL")}");
            GetTree().Quit(ok ? 0 : 1);
        }
    }
}
