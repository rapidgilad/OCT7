using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OCT7.Game.Input;
using OCT7.Game.Match;
using OCT7.Game.Views;
using OCT7.Sim;
using OCT7.Sim.Data;
using OCT7.Sim.Economy;
using OCT7.Sim.Production;
using OCT7.Sim.Units;

namespace OCT7.Game.UI
{
    /// <summary>
    /// Compact in-match HUD built from small corner panels so the battlefield stays visible:
    /// resources (top-left), tickets and victory points (top-center), clock and menu (top-right),
    /// minimap (bottom-left), selection card (bottom-center, only when something is selected) and
    /// command card (bottom-right, only when there are orders). Messages appear under the ticket bar.
    /// </summary>
    public partial class Hud : Control
    {
        private const float Margin = 8f;
        private const float CardButtonSize = 62f;
        private static readonly Key[] SlotKeys = { Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M, Key.G };
        private static readonly Color PanelColor = new Color(0.055f, 0.062f, 0.05f, 0.84f);
        private static readonly Color PanelBorder = new Color(0.42f, 0.4f, 0.3f, 0.55f);

        private readonly List<(Button button, Key key, Action action)> _cardButtons = new List<(Button, Key, Action)>();
        private readonly List<(Label label, float time)> _messages = new List<(Label, float)>();
        private readonly List<Action> _selectionUpdaters = new List<Action>();
        private MatchController _match;
        private PlayerController _player;
        private Label _mp, _mu, _fu, _pop, _mpIncome, _muIncome, _fuIncome, _clock;
        private Label _myTickets, _enemyTickets, _myName, _enemyName;
        private DrawBox _ticketBars;
        private VBoxContainer _messageBox;
        private PanelContainer _selectionPanel;
        private VBoxContainer _selection;
        private PanelContainer _cardPanel;
        private GridContainer _card;
        private Label _hint;
        private int _shownVersion = -1;
        private string _selectionSignature = "";
        private string _cardSignature = "";
        private bool _buildMenu;
        private float _refresh;

        public Minimap Minimap { get; private set; }

        private Simulation Sim => _match.Sim;
        private int Me => _match.LocalPlayerId;
        private PlayerState Enemy => Sim.Players.FirstOrDefault(p => p.Id != Me);

        public void Initialize(MatchController match, PlayerController player)
        {
            _match = match;
            _player = player;
            Theme = UiTheme.Create();
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            BuildResources();
            BuildTickets();
            BuildClock();
            BuildMinimap();
            BuildSelection();
            BuildCommandCard();
            BuildMessages();
        }

        private static StyleBoxFlat PanelStyle(float margin = 6f) => UiTheme.Box(PanelColor, 4, margin, PanelBorder);

        private PanelContainer Corner(LayoutPreset preset, float margin = 6f)
        {
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", PanelStyle(margin));
            AddChild(panel);
            panel.SetAnchorsAndOffsetsPreset(preset, LayoutPresetMode.Minsize, (int)Margin);
            return panel;
        }

        // ------------------------------------------------------------------ top

        private void BuildResources()
        {
            var panel = Corner(LayoutPreset.TopLeft, 5f);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            panel.AddChild(row);
            (_mp, _mpIncome) = Resource(row, IconKind.Manpower, new Color(0.9f, 0.86f, 0.74f), "Manpower: units, reinforcements and buildings");
            (_mu, _muIncome) = Resource(row, IconKind.Munitions, new Color(0.95f, 0.72f, 0.36f), "Munitions: anti-tank teams and support weapons");
            (_fu, _fuIncome) = Resource(row, IconKind.Fuel, new Color(0.55f, 0.78f, 0.95f), "Fuel: vehicles and advanced buildings");
            (_pop, _) = Resource(row, IconKind.Population, UiTheme.Dim, "Population (current + queued / cap)");
        }

        private static (Label value, Label income) Resource(HBoxContainer row, IconKind icon, Color color, string tooltip)
        {
            var box = new HBoxContainer { TooltipText = tooltip, MouseFilter = MouseFilterEnum.Pass };
            box.AddThemeConstantOverride("separation", 4);
            box.AddChild(new IconView(icon, color, 16f) { SizeFlagsVertical = SizeFlags.ShrinkCenter });
            var value = UiTheme.Label("0", 15);
            box.AddChild(value);
            Label income = null;
            if (icon != IconKind.Population)
            {
                income = UiTheme.Label("", 11, new Color(0.6f, 0.82f, 0.5f));
                income.VerticalAlignment = VerticalAlignment.Center;
                box.AddChild(income);
            }

            row.AddChild(box);
            return (value, income);
        }

        private void BuildTickets()
        {
            var panel = Corner(LayoutPreset.CenterTop, 5f);
            panel.GrowHorizontal = GrowDirection.Both;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            panel.AddChild(row);
            _myName = UiTheme.Label("", 12, TeamColors.For(Me).Lightened(0.35f));
            _myTickets = UiTheme.Label("", 15);
            _enemyTickets = UiTheme.Label("", 15);
            _enemyName = UiTheme.Label("", 12, TeamColors.For(1 - Me).Lightened(0.35f));
            _ticketBars = new DrawBox { CustomMinimumSize = new Vector2(300f, 18f), SizeFlagsVertical = SizeFlags.ShrinkCenter, OnDraw = DrawTicketBars, TooltipText = "Victory tickets. Holding more victory points (◆) drains the enemy's tickets.", MouseFilter = MouseFilterEnum.Pass };
            row.AddChild(_myName);
            row.AddChild(_myTickets);
            row.AddChild(_ticketBars);
            row.AddChild(_enemyTickets);
            row.AddChild(_enemyName);
        }

        /// <summary>Two ticket bars growing outward from the center, with one diamond per victory point between them.</summary>
        private void DrawTicketBars(DrawBox box)
        {
            if (_match?.Sim == null)
            {
                return;
            }

            var size = box.Size;
            float start = Sim.Data.Economy.StartingTickets;
            var vps = Sim.Territory.Sectors.Where(s => s.Type == SectorType.Victory).ToList();
            float vpWidth = Mathf.Max(1, vps.Count) * 14f + 6f;
            float barWidth = (size.X - vpWidth) * 0.5f;
            float barY = size.Y * 0.5f - 4f;
            var bg = new Color(0f, 0f, 0f, 0.55f);
            float mine = Mathf.Clamp((float)(Sim.GetPlayer(Me).Tickets / start), 0f, 1f);
            box.DrawRect(new Rect2(0f, barY, barWidth, 8f), bg);
            box.DrawRect(new Rect2(barWidth * (1f - mine), barY, barWidth * mine, 8f), TeamColors.For(Me));
            var enemy = Enemy;
            if (enemy != null)
            {
                float theirs = Mathf.Clamp((float)(enemy.Tickets / start), 0f, 1f);
                box.DrawRect(new Rect2(size.X - barWidth, barY, barWidth, 8f), bg);
                box.DrawRect(new Rect2(size.X - barWidth, barY, barWidth * theirs, 8f), TeamColors.For(enemy.Id));
            }

            for (int i = 0; i < vps.Count; i++)
            {
                var center = new Vector2(barWidth + 10f + i * 14f, size.Y * 0.5f);
                var color = vps[i].OwnerId >= 0 ? TeamColors.For(vps[i].OwnerId).Lightened(0.2f) : new Color(0.75f, 0.74f, 0.68f);
                Icons.Draw(box, IconKind.VictoryPoint, new Rect2(center - new Vector2(6f, 6f), new Vector2(12f, 12f)), new Color(0f, 0f, 0f, 0.7f));
                Icons.Draw(box, IconKind.VictoryPoint, new Rect2(center - new Vector2(5f, 5f), new Vector2(10f, 10f)), color);
            }
        }

        private void BuildClock()
        {
            var panel = Corner(LayoutPreset.TopRight, 4f);
            panel.GrowHorizontal = GrowDirection.Begin;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            panel.AddChild(row);
            _clock = UiTheme.Label("00:00", 15, UiTheme.Accent);
            _clock.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(_clock);
            var menu = new Button { Text = "Menu", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(52f, 24f), TooltipText = "Pause menu (Esc)" };
            menu.AddThemeFontSizeOverride("font_size", 12);
            menu.Pressed += () => _match.TogglePause();
            row.AddChild(menu);
        }

        // ------------------------------------------------------------------ bottom

        private void BuildMinimap()
        {
            var panel = Corner(LayoutPreset.BottomLeft, 3f);
            panel.GrowVertical = GrowDirection.Begin;
            Minimap = new Minimap { CustomMinimumSize = new Vector2(176f, 176f) };
            panel.AddChild(Minimap);
        }

        private void BuildSelection()
        {
            _selectionPanel = Corner(LayoutPreset.CenterBottom, 8f);
            _selectionPanel.GrowHorizontal = GrowDirection.Both;
            _selectionPanel.GrowVertical = GrowDirection.Begin;
            _selectionPanel.CustomMinimumSize = new Vector2(440f, 0f);
            _selection = new VBoxContainer();
            _selection.AddThemeConstantOverride("separation", 4);
            _selectionPanel.AddChild(_selection);
            _selectionPanel.Visible = false;

            _hint = UiTheme.Label("Drag to select · Right-click to move / attack · R retreat · T reinforce · Esc menu", 12, new Color(0.85f, 0.85f, 0.8f, 0.75f));
            _hint.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(_hint);
            _hint.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, 10);
            _hint.GrowHorizontal = GrowDirection.Both;
            _hint.GrowVertical = GrowDirection.Begin;
        }

        private void BuildCommandCard()
        {
            _cardPanel = Corner(LayoutPreset.BottomRight, 6f);
            _cardPanel.GrowHorizontal = GrowDirection.Begin;
            _cardPanel.GrowVertical = GrowDirection.Begin;
            _card = new GridContainer { Columns = 4 };
            _card.AddThemeConstantOverride("h_separation", 4);
            _card.AddThemeConstantOverride("v_separation", 4);
            _cardPanel.AddChild(_card);
            _cardPanel.Visible = false;
        }

        private void BuildMessages()
        {
            _messageBox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_messageBox);
            _messageBox.SetAnchorsPreset(LayoutPreset.CenterTop);
            _messageBox.OffsetLeft = -260f;
            _messageBox.OffsetRight = 260f;
            _messageBox.OffsetTop = 46f;
            _messageBox.OffsetBottom = 46f;
            _messageBox.GrowHorizontal = GrowDirection.Both;
        }

        // ------------------------------------------------------------------ messages

        public void ShowMessage(string text, Color? color = null)
        {
            var label = UiTheme.Label(text, 14, color ?? UiTheme.Text);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            _messageBox.AddChild(label);
            _messages.Add((label, 4f));
            while (_messages.Count > 4)
            {
                _messages[0].label.QueueFree();
                _messages.RemoveAt(0);
            }
        }

        public void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.CommandRejected when e.PlayerId == Me:
                    ShowMessage(Describe((RejectReason)e.Value, e.DefId, Sim), UiTheme.Bad);
                    break;
                case SimEventType.UnitProduced when e.PlayerId == Me:
                    ShowMessage($"{Sim.Data.GetUnit(e.DefId).Name} ready", UiTheme.Good);
                    break;
                case SimEventType.StructureCompleted when e.PlayerId == Me && Sim.Data.GetStructure(e.DefId).Kind != StructureKind.Sandbags:
                    ShowMessage($"{Sim.Data.GetStructure(e.DefId).Name} complete", UiTheme.Good);
                    break;
                case SimEventType.SectorCaptured when e.PlayerId == Me:
                    ShowMessage(Sim.Territory.Sectors[e.TargetId].Type == SectorType.Victory ? "Victory point captured" : "Sector captured", UiTheme.Good);
                    break;
                case SimEventType.SectorNeutralized when e.Value == Me:
                    ShowMessage(Sim.Territory.Sectors[e.TargetId].Type == SectorType.Victory ? "We are losing a victory point!" : "Sector lost", UiTheme.Bad);
                    break;
                case SimEventType.StructureDestroyed when e.PlayerId == Me:
                    ShowMessage($"{Sim.Data.GetStructure(e.DefId).Name} destroyed", UiTheme.Bad);
                    break;
                case SimEventType.SquadDestroyed when e.PlayerId == Me:
                    ShowMessage($"{Sim.Data.GetUnit(e.DefId).Name} lost", UiTheme.Bad);
                    break;
            }
        }

        public static string Describe(RejectReason reason, string defId, Simulation sim)
        {
            switch (reason)
            {
                case RejectReason.NotEnoughResources: return "Not enough resources";
                case RejectReason.PopCap: return "Population cap reached";
                case RejectReason.QueueFull: return "Production queue is full";
                case RejectReason.NotUnlocked:
                    if (defId != null && sim.Data.HasStructure(defId))
                    {
                        var missing = sim.Data.GetStructure(defId).Requires.Select(r => sim.Data.GetStructure(r).Name);
                        return "Requires " + string.Join(", ", missing);
                    }

                    return "Not available yet";
                case RejectReason.InvalidPlacement: return "Can't build there (must be clear ground in your territory)";
                case RejectReason.NoEngineer: return "Only engineers can build";
                default: return "Can't do that";
            }
        }

        // ------------------------------------------------------------------ hotkeys

        public bool HandleHotkey(Key key)
        {
            foreach (var (button, k, action) in _cardButtons)
            {
                if (k == key && !button.Disabled)
                {
                    action();
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ refresh

        public override void _Process(double delta)
        {
            if (_match == null)
            {
                return;
            }

            float dt = (float)delta;
            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                var (label, time) = _messages[i];
                time -= dt;
                label.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time, 0f, 1f));
                if (time <= 0f)
                {
                    label.QueueFree();
                    _messages.RemoveAt(i);
                }
                else
                {
                    _messages[i] = (label, time);
                }
            }

            RefreshTopBar();
            _refresh -= dt;
            if (_refresh <= 0f || _player.Version != _shownVersion)
            {
                _refresh = 0.15f;
                _shownVersion = _player.Version;
                RefreshSelection();
                RefreshCard();
            }
        }

        private void RefreshTopBar()
        {
            var p = Sim.GetPlayer(Me);
            _mp.Text = $"{(int)p.Manpower}";
            _mu.Text = $"{(int)p.Munitions}";
            _fu.Text = $"{(int)p.Fuel}";
            _mpIncome.Text = $"+{(int)p.ManpowerIncome}";
            _muIncome.Text = $"+{(int)p.MunitionsIncome}";
            _fuIncome.Text = $"+{(int)p.FuelIncome}";
            int pop = Sim.World.PopulationOf(Me) + Sim.World.QueuedPopulationOf(Me);
            _pop.Text = $"{pop}/{Sim.Data.Economy.PopCap}";
            _myName.Text = Sim.Data.GetFaction(p.FactionId).DisplayName.ToUpperInvariant();
            _myTickets.Text = $"{(int)p.Tickets}";
            var enemy = Enemy;
            _enemyTickets.Visible = _enemyName.Visible = enemy != null;
            if (enemy != null)
            {
                _enemyTickets.Text = $"{(int)enemy.Tickets}";
                _enemyName.Text = Sim.Data.GetFaction(enemy.FactionId).DisplayName.ToUpperInvariant();
            }

            int seconds = (int)Sim.ElapsedSeconds;
            _clock.Text = $"{seconds / 60:00}:{seconds % 60:00}";
        }

        // ------------------------------------------------------------------ selection card

        private void RefreshSelection()
        {
            var squads = _player.SelectedSquadObjects();
            var structure = squads.Count == 0 ? _player.SelectedStructureObject() : null;
            string signature = squads.Count > 0
                ? "s" + string.Join(",", squads.Select(s => s.Id))
                : structure != null ? $"b{structure.Id}|{structure.IsComplete}|{string.Join(",", structure.Queue.Select(q => q.Unit.Id))}" : "";
            if (signature != _selectionSignature)
            {
                _selectionSignature = signature;
                foreach (Node child in _selection.GetChildren())
                {
                    child.QueueFree();
                }

                _selectionUpdaters.Clear();
                if (squads.Count == 1)
                {
                    SquadCard(squads[0]);
                }
                else if (squads.Count > 1)
                {
                    SquadChips(squads);
                }
                else if (structure != null)
                {
                    StructureCard(structure);
                }
            }

            bool any = signature.Length > 0;
            _selectionPanel.Visible = any;
            _hint.Visible = !any && _player.Placing == null && Sim.ElapsedSeconds < 240f;
            foreach (var update in _selectionUpdaters)
            {
                update();
            }
        }

        private static Control Portrait(IconKind icon, Color team, float size)
        {
            var frame = new PanelContainer { CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore };
            frame.AddThemeStyleboxOverride("panel", UiTheme.Box(team.Darkened(0.55f), 4, 6, team.Lightened(0.1f)));
            frame.AddChild(new IconView(icon, new Color(0.95f, 0.94f, 0.88f), size - 12f));
            return frame;
        }

        private static WeaponKind Primary(Squad s) => s.Weapons.Count > 0 ? s.Weapons[0].Kind : WeaponKind.SmallArms;

        private void SquadCard(Squad s)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            _selection.AddChild(row);
            row.AddChild(Portrait(Icons.ForUnit(s.Def, Primary(s)), TeamColors.For(Me), 58f));
            var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            info.AddThemeConstantOverride("separation", 2);
            row.AddChild(info);
            info.AddChild(UiTheme.Label(s.Def.Name, 15, UiTheme.Accent));
            var health = new DrawBox { CustomMinimumSize = new Vector2(320f, 12f) };
            int id = s.Id;
            health.OnDraw = box => DrawSquadHealth(box, Sim.World.GetSquad(id));
            info.AddChild(health);
            var status = UiTheme.Label("", 12);
            var detail = UiTheme.Label("", 11, UiTheme.Dim);
            info.AddChild(status);
            info.AddChild(detail);
            _selectionUpdaters.Add(() =>
            {
                var squad = Sim.World.GetSquad(id);
                if (squad == null)
                {
                    return;
                }

                string models = squad.Def.IsVehicle ? $"Hull {(int)squad.Health}/{(int)squad.MaxHealth}" : $"{squad.Models}/{squad.Def.SquadSize} soldiers";
                status.Text = $"{models}  ·  {Status(squad)}  ·  {squad.Kills} kills";
                var cover = Sim.Cover.GetCover(Sim.Map.Grid.WorldToCell(squad.Position));
                detail.Text = $"Cover: {cover}  ·  {string.Join(", ", squad.Weapons.Select(w => w.Name))}";
            });
        }

        private static string Status(Squad s) =>
            s.IsRetreating ? "Retreating" : s.SuppressionState == SuppressionState.Pinned ? "PINNED" : s.SuppressionState == SuppressionState.Suppressed ? "Suppressed" :
            s.BuildTargetId != 0 ? "Building" : s.ReinforcePending > 0 ? "Reinforcing" : s.TargetId != 0 ? "Engaging" : s.IsMoving ? "Moving" : "Ready";

        /// <summary>Health bar; infantry get one segment per soldier (dead soldiers dark).</summary>
        private void DrawSquadHealth(DrawBox box, Squad s)
        {
            if (s == null)
            {
                return;
            }

            var size = box.Size;
            var team = TeamColors.For(s.OwnerId).Lightened(0.15f);
            box.DrawRect(new Rect2(Vector2.Zero, size), new Color(0f, 0f, 0f, 0.6f));
            if (s.Def.IsVehicle)
            {
                box.DrawRect(new Rect2(1f, 1f, (size.X - 2f) * s.HealthFraction, size.Y - 2f), team);
                return;
            }

            int n = s.Def.SquadSize;
            float seg = size.X / n;
            for (int i = 0; i < n; i++)
            {
                float hp = s.Def.HealthPerModel > 0f ? s.GetModelHealth(i) / s.Def.HealthPerModel : 0f;
                var rect = new Rect2(i * seg + 1f, 1f, seg - 2f, size.Y - 2f);
                box.DrawRect(rect, new Color(0.18f, 0.18f, 0.17f));
                if (hp > 0f)
                {
                    box.DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X * Mathf.Clamp(hp, 0f, 1f), rect.Size.Y)), team);
                }
            }
        }

        private void SquadChips(List<Squad> squads)
        {
            var header = UiTheme.Label($"{squads.Count} squads", 13, UiTheme.Accent);
            _selection.AddChild(header);
            var grid = new GridContainer { Columns = 8 };
            grid.AddThemeConstantOverride("h_separation", 4);
            grid.AddThemeConstantOverride("v_separation", 4);
            _selection.AddChild(grid);
            foreach (var s in squads.Take(16))
            {
                int id = s.Id;
                var chip = new Button { CustomMinimumSize = new Vector2(50f, 46f), FocusMode = FocusModeEnum.None, TooltipText = $"{s.Def.Name}\nClick to select only this squad" };
                chip.Pressed += () => _player.SelectSquads(new[] { id });
                var icon = new IconView(Icons.ForUnit(s.Def, Primary(s)), new Color(0.92f, 0.91f, 0.85f), 24f) { Position = new Vector2(13f, 5f), Size = new Vector2(24f, 24f) };
                chip.AddChild(icon);
                var bar = new DrawBox { Position = new Vector2(5f, 35f), Size = new Vector2(40f, 6f) };
                bar.OnDraw = box => DrawSquadHealth(box, Sim.World.GetSquad(id));
                chip.AddChild(bar);
                grid.AddChild(chip);
            }

            if (squads.Count > 16)
            {
                grid.AddChild(UiTheme.Label($"+{squads.Count - 16}", 13, UiTheme.Dim));
            }
        }

        private void StructureCard(Structure st)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            _selection.AddChild(row);
            row.AddChild(Portrait(Icons.ForStructure(st.Def), TeamColors.For(Me), 58f));
            var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            info.AddThemeConstantOverride("separation", 3);
            row.AddChild(info);
            info.AddChild(UiTheme.Label(st.Def.Name, 15, UiTheme.Accent));
            int id = st.Id;
            var health = new DrawBox { CustomMinimumSize = new Vector2(320f, 10f) };
            health.OnDraw = box =>
            {
                var s = Sim.World.GetStructure(id);
                if (s == null)
                {
                    return;
                }

                box.DrawRect(new Rect2(Vector2.Zero, box.Size), new Color(0f, 0f, 0f, 0.6f));
                float f = s.IsComplete ? s.HealthFraction : s.BuildProgress;
                box.DrawRect(new Rect2(1f, 1f, (box.Size.X - 2f) * f, box.Size.Y - 2f), s.IsComplete ? TeamColors.For(Me).Lightened(0.15f) : new Color(0.9f, 0.78f, 0.4f));
            };
            info.AddChild(health);
            var status = UiTheme.Label("", 12, UiTheme.Dim);
            info.AddChild(status);
            _selectionUpdaters.Add(() =>
            {
                var s = Sim.World.GetStructure(id);
                if (s == null)
                {
                    return;
                }

                status.Text = !s.IsComplete ? $"Under construction {(int)(s.BuildProgress * 100)}% · right-click with engineers to help"
                    : s.Def.Produces.Count == 0 ? $"{(int)s.Health}/{(int)s.MaxHealth} HP"
                    : s.Queue.Count == 0 ? "Queue empty · right-click ground to set the rally point" : "Producing · click a queued unit to cancel (full refund)";
            });

            if (!st.IsComplete || st.Queue.Count == 0)
            {
                return;
            }

            var queue = new HBoxContainer();
            queue.AddThemeConstantOverride("separation", 4);
            info.AddChild(queue);
            for (int i = 0; i < st.Queue.Count; i++)
            {
                int index = i;
                var item = st.Queue[i];
                var chip = new Button { CustomMinimumSize = new Vector2(40f, 36f), FocusMode = FocusModeEnum.None, TooltipText = $"{item.Unit.Name}\nClick to cancel (full refund)" };
                chip.Pressed += () => _player.CancelQueue(index);
                var weapon = item.Unit.Weapons.Count > 0 && Sim.Data.HasWeapon(item.Unit.Weapons[0].Weapon) ? Sim.Data.GetWeapon(item.Unit.Weapons[0].Weapon).Kind : WeaponKind.SmallArms;
                var icon = new IconView(Icons.ForUnit(item.Unit, weapon), new Color(0.92f, 0.91f, 0.85f), 22f) { Position = new Vector2(9f, 4f), Size = new Vector2(22f, 22f) };
                chip.AddChild(icon);
                if (i == 0)
                {
                    var progress = new DrawBox { Position = new Vector2(3f, 29f), Size = new Vector2(34f, 4f) };
                    progress.OnDraw = box =>
                    {
                        var s = Sim.World.GetStructure(id);
                        float f = s != null && s.Queue.Count > 0 ? s.Queue[0].Progress : 0f;
                        box.DrawRect(new Rect2(Vector2.Zero, box.Size), new Color(0f, 0f, 0f, 0.6f));
                        box.DrawRect(new Rect2(Vector2.Zero, new Vector2(box.Size.X * f, box.Size.Y)), UiTheme.Accent);
                    };
                    chip.AddChild(progress);
                }

                queue.AddChild(chip);
            }
        }

        // ------------------------------------------------------------------ command card

        private void RefreshCard()
        {
            var squads = _player.SelectedSquadObjects();
            var st = squads.Count == 0 ? _player.SelectedStructureObject() : null;
            bool engineers = squads.Any(s => s.Def.Engineer);
            if (!engineers)
            {
                _buildMenu = false;
            }

            string signature = $"{string.Join(",", squads.Select(s => s.Id))}|{st?.Id}|{st?.IsComplete}|{_buildMenu}";
            if (signature != _cardSignature)
            {
                _cardSignature = signature;
                RebuildCard(squads, st, engineers);
            }

            _cardPanel.Visible = _cardButtons.Count > 0;
            UpdateCardStates(st);
        }

        private void RebuildCard(List<Squad> squads, Structure st, bool engineers)
        {
            foreach (Node child in _card.GetChildren())
            {
                child.QueueFree();
            }

            _cardButtons.Clear();
            if (squads.Count > 0 && _buildMenu)
            {
                int slot = 0;
                foreach (var def in Sim.Data.BuildableStructures(Sim.GetPlayer(Me).FactionId))
                {
                    var d = def;
                    AddCardButton(Icons.ForStructure(d), ShortName(d.Name), Cost(d.Manpower, d.Munitions, d.Fuel), SlotKeys[slot++ % SlotKeys.Length], () => _player.BeginPlacement(d), StructureTooltip(d), d.Id);
                }

                AddCardButton(IconKind.Back, "Back", "", Key.Escape, () => { _buildMenu = false; _cardSignature = ""; }, "Back to orders (Esc)");
                return;
            }

            if (squads.Count > 0)
            {
                AddCardButton(IconKind.Retreat, "Retreat", "", Key.R, _player.CommandRetreat, "Retreat [R]\nFall back to HQ: faster, harder to hit, cannot fire");
                AddCardButton(IconKind.Reinforce, "Reinforce", "", Key.T, _player.CommandReinforce, "Reinforce [T]\nRefill lost soldiers near HQ or a production building (manpower per soldier)");
                AddCardButton(IconKind.Stop, "Stop", "", Key.H, _player.CommandStop, "Stop [H]\nStop and hold position");
                if (engineers)
                {
                    AddCardButton(IconKind.Build, "Build", "", Key.B, () => { _buildMenu = true; _cardSignature = ""; }, "Build [B]\nConstruct buildings and sandbags");
                }

                return;
            }

            if (st != null && st.IsComplete)
            {
                int slot = 0;
                foreach (var unitId in st.Def.Produces)
                {
                    var unit = Sim.Data.GetUnit(unitId);
                    if (!unit.Enabled)
                    {
                        continue;
                    }

                    string id = unitId;
                    var key = SlotKeys[slot++ % SlotKeys.Length];
                    var weapon = unit.Weapons.Count > 0 && Sim.Data.HasWeapon(unit.Weapons[0].Weapon) ? Sim.Data.GetWeapon(unit.Weapons[0].Weapon).Kind : WeaponKind.SmallArms;
                    AddCardButton(Icons.ForUnit(unit, weapon), ShortName(unit.Name), Cost(unit.Manpower, unit.Munitions, unit.Fuel), key, () => _player.Produce(id), UnitTooltip(unit, key), id);
                }
            }
        }

        private void UpdateCardStates(Structure st)
        {
            var player = Sim.GetPlayer(Me);
            foreach (var (button, _, _) in _cardButtons)
            {
                var id = button.GetMeta("def", "").AsString();
                if (!string.IsNullOrEmpty(id))
                {
                    if (Sim.Data.HasStructure(id))
                    {
                        var d = Sim.Data.GetStructure(id);
                        bool unlocked = d.Requires.All(r => Sim.World.OwnsComplete(Me, r));
                        bool affordable = player.Manpower >= d.Manpower && player.Munitions >= d.Munitions && player.Fuel >= d.Fuel;
                        button.Disabled = !unlocked || !affordable;
                    }
                    else if (st != null)
                    {
                        button.Disabled = Sim.Production.CanProduce(Me, st, id) != RejectReason.None;
                    }
                }

                if (button.GetChildCount() > 0 && button.GetChild(0) is Control content)
                {
                    content.Modulate = button.Disabled ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
                }
            }
        }

        /// <summary>Square button: hotkey (top-left), icon, short name and cost; full details in the tooltip.</summary>
        private void AddCardButton(IconKind icon, string name, string cost, Key key, Action action, string tooltip, string defId = null)
        {
            var b = new Button { CustomMinimumSize = new Vector2(CardButtonSize, CardButtonSize), FocusMode = FocusModeEnum.None, TooltipText = tooltip };
            var content = new Control { MouseFilter = MouseFilterEnum.Ignore };
            content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            b.AddChild(content);

            var iconView = new IconView(icon, new Color(0.94f, 0.92f, 0.84f), 24f) { Position = new Vector2((CardButtonSize - 24f) * 0.5f, 7f), Size = new Vector2(24f, 24f) };
            content.AddChild(iconView);
            var hotkey = UiTheme.Label(key == Key.Escape ? "Esc" : OS.GetKeycodeString(key), 10, UiTheme.Accent);
            hotkey.Position = new Vector2(4f, 1f);
            content.AddChild(hotkey);
            var label = UiTheme.Label(name, 10);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.ClipText = true;
            label.Position = new Vector2(2f, 33f);
            label.Size = new Vector2(CardButtonSize - 4f, 13f);
            content.AddChild(label);
            if (!string.IsNullOrEmpty(cost))
            {
                var costLabel = UiTheme.Label(cost, 9, UiTheme.Dim);
                costLabel.HorizontalAlignment = HorizontalAlignment.Center;
                costLabel.ClipText = true;
                costLabel.Position = new Vector2(1f, 46f);
                costLabel.Size = new Vector2(CardButtonSize - 2f, 12f);
                content.AddChild(costLabel);
            }

            if (defId != null)
            {
                b.SetMeta("def", defId);
            }

            b.Pressed += action;
            _card.AddChild(b);
            _cardButtons.Add((b, key, action));
        }

        /// <summary>Card label: the most distinctive word(s) of a name ("Golani Rifle Squad" → "Golani").</summary>
        private static string ShortName(string name)
        {
            var words = name.Split(' ');
            string first = words[0];
            if (first.Length <= 4 && words.Length > 1)
            {
                first = $"{first} {words[1]}";
            }

            return first.Length > 11 ? first.Substring(0, 10) + "…" : first;
        }

        private static string Cost(float mp, float mu, float fu)
        {
            var parts = new List<string> { $"{(int)mp}" };
            if (mu > 0) parts.Add($"{(int)mu}mu");
            if (fu > 0) parts.Add($"{(int)fu}fu");
            return string.Join(" ", parts);
        }

        private static string FullCost(float mp, float mu, float fu)
        {
            var parts = new List<string> { $"{(int)mp} manpower" };
            if (mu > 0) parts.Add($"{(int)mu} munitions");
            if (fu > 0) parts.Add($"{(int)fu} fuel");
            return string.Join(", ", parts);
        }

        private string StructureTooltip(StructureDef d)
        {
            var lines = new List<string> { d.Name, $"Cost: {FullCost(d.Manpower, d.Munitions, d.Fuel)} · Build time {d.BuildTime:0}s" };
            if (d.Produces.Count > 0)
            {
                lines.Add("Unlocks: " + string.Join(", ", d.Produces.Where(p => Sim.Data.GetUnit(p).Enabled).Select(p => Sim.Data.GetUnit(p).Name)));
            }

            if (d.Requires.Count > 0)
            {
                lines.Add("Requires: " + string.Join(", ", d.Requires.Select(r => Sim.Data.GetStructure(r).Name)));
            }

            if (d.Kind == StructureKind.Sandbags)
            {
                lines.Add("Heavy cover for infantry behind it. Space rotates.");
            }

            lines.Add("Place in your territory · Shift-click to place several");
            return string.Join("\n", lines);
        }

        private string UnitTooltip(UnitDef u, Key key)
        {
            var weapons = string.Join(", ", u.Weapons.Where(w => Sim.Data.HasWeapon(w.Weapon)).Select(w => Sim.Data.GetWeapon(w.Weapon).Name));
            return $"{u.Name}  [{OS.GetKeycodeString(key)}]\nCost: {FullCost(u.Manpower, u.Munitions, u.Fuel)} · Pop {u.Pop} · {u.BuildTime:0}s\n" +
                   $"{u.SquadSize} × {u.HealthPerModel:0} HP · Speed {u.MoveSpeed:0.#} m/s · Sight {u.SightRange:0} m\nWeapons: {weapons}";
        }
    }
}
