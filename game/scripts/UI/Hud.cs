using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OCT7.Game.Input;
using OCT7.Game.Match;
using OCT7.Game.Views;
using OCT7.Sim;
using OCT7.Sim.Data;
using OCT7.Sim.Production;
using OCT7.Sim.Units;

namespace OCT7.Game.UI
{
    /// <summary>
    /// In-match HUD: top bar (resources + income, pop, tickets, VPs, clock, menu), message feed,
    /// bottom bar (minimap, selection panel with production queue, command card with grid hotkeys).
    /// </summary>
    public partial class Hud : Control
    {
        private static readonly Key[] SlotKeys = { Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M, Key.G };

        private readonly List<(Button button, Key key, Action action)> _cardButtons = new List<(Button, Key, Action)>();
        private readonly List<(Label label, float time)> _messages = new List<(Label, float)>();
        private MatchController _match;
        private PlayerController _player;
        private Label _mp, _mu, _fu, _pop, _clock, _myTicketsLabel, _enemyTicketsLabel;
        private ProgressBar _myTickets, _enemyTickets;
        private VBoxContainer _messageBox;
        private VBoxContainer _selection;
        private GridContainer _card;
        private int _shownVersion = -1;
        private string _cardSignature = "";
        private bool _buildMenu;
        private float _refresh;

        public Minimap Minimap { get; private set; }

        private Simulation Sim => _match.Sim;
        private int Me => _match.LocalPlayerId;

        public void Initialize(MatchController match, PlayerController player)
        {
            _match = match;
            _player = player;
            Theme = UiTheme.Create();
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            BuildTopBar();
            BuildBottomBar();
            _messageBox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            _messageBox.SetAnchorsPreset(LayoutPreset.CenterTop);
            _messageBox.Position = new Vector2(-260f, 58f);
            _messageBox.CustomMinimumSize = new Vector2(520f, 0f);
            AddChild(_messageBox);
        }

        // ------------------------------------------------------------------ layout

        private void BuildTopBar()
        {
            var bar = new PanelContainer();
            bar.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            bar.OffsetBottom = 44f;
            AddChild(bar);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 18);
            bar.AddChild(row);

            _mp = Resource(row, "MP");
            _mu = Resource(row, "MU");
            _fu = Resource(row, "FU");
            _pop = Resource(row, "POP");

            row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
            var tickets = new HBoxContainer();
            tickets.AddThemeConstantOverride("separation", 8);
            row.AddChild(tickets);
            _myTicketsLabel = UiTheme.Label("", 14, TeamColors.For(Me).Lightened(0.3f));
            _myTickets = UiTheme.Bar(TeamColors.For(Me), 170f, 14f);
            _enemyTickets = UiTheme.Bar(TeamColors.For(1 - Me), 170f, 14f);
            _enemyTicketsLabel = UiTheme.Label("", 14, TeamColors.For(1 - Me).Lightened(0.3f));
            _myTickets.FillMode = (int)ProgressBar.FillModeEnum.EndToBegin;
            tickets.AddChild(_myTicketsLabel);
            tickets.AddChild(Center(_myTickets));
            tickets.AddChild(UiTheme.Label("VS", 12, UiTheme.Dim));
            tickets.AddChild(Center(_enemyTickets));
            tickets.AddChild(_enemyTicketsLabel);
            row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

            _clock = UiTheme.Label("00:00", 16, UiTheme.Accent);
            row.AddChild(_clock);
            var menu = new Button { Text = "Menu", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(70f, 0f) };
            menu.Pressed += () => _match.TogglePause();
            row.AddChild(menu);
        }

        private static Control Center(Control c)
        {
            var box = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
            box.AddChild(c);
            return box;
        }

        private static Label Resource(HBoxContainer row, string name)
        {
            var box = new HBoxContainer();
            box.AddThemeConstantOverride("separation", 4);
            box.AddChild(UiTheme.Label(name, 12, UiTheme.Accent));
            var value = UiTheme.Label("0", 16);
            box.AddChild(value);
            row.AddChild(box);
            return value;
        }

        private void BuildBottomBar()
        {
            var bar = new PanelContainer();
            bar.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            bar.OffsetTop = -206f;
            AddChild(bar);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            bar.AddChild(row);

            Minimap = new Minimap { CustomMinimumSize = new Vector2(190f, 190f) };
            row.AddChild(Minimap);

            var selectionPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            selectionPanel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.1f, 0.11f, 0.09f, 0.9f), 3, 10));
            row.AddChild(selectionPanel);
            _selection = new VBoxContainer();
            selectionPanel.AddChild(_selection);

            var cardPanel = new PanelContainer { CustomMinimumSize = new Vector2(352f, 0f) };
            cardPanel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.1f, 0.11f, 0.09f, 0.9f), 3, 8));
            row.AddChild(cardPanel);
            _card = new GridContainer { Columns = 4 };
            _card.AddThemeConstantOverride("h_separation", 5);
            _card.AddThemeConstantOverride("v_separation", 5);
            cardPanel.AddChild(_card);
        }

        // ------------------------------------------------------------------ messages

        public void ShowMessage(string text, Color? color = null)
        {
            var label = UiTheme.Label(text, 16, color ?? UiTheme.Text);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            _messageBox.AddChild(label);
            _messages.Add((label, 4.5f));
            while (_messages.Count > 5)
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
                _refresh = 0.2f;
                _shownVersion = _player.Version;
                RefreshSelection();
                RefreshCard();
            }
        }

        private void RefreshTopBar()
        {
            var p = Sim.GetPlayer(Me);
            var enemy = Sim.GetPlayer(1 - Me);
            _mp.Text = $"{(int)p.Manpower} (+{(int)p.ManpowerIncome})";
            _mu.Text = $"{(int)p.Munitions} (+{(int)p.MunitionsIncome})";
            _fu.Text = $"{(int)p.Fuel} (+{(int)p.FuelIncome})";
            int pop = Sim.World.PopulationOf(Me) + Sim.World.QueuedPopulationOf(Me);
            _pop.Text = $"{pop}/{Sim.Data.Economy.PopCap}";
            float start = Sim.Data.Economy.StartingTickets;
            _myTickets.Value = p.Tickets / start;
            _enemyTickets.Value = enemy.Tickets / start;
            _myTicketsLabel.Text = $"{Sim.Data.GetFaction(p.FactionId).DisplayName}  {(int)p.Tickets}  ◆{Sim.Territory.VictoryPointsOf(Me)}";
            _enemyTicketsLabel.Text = $"◆{Sim.Territory.VictoryPointsOf(enemy.Id)}  {(int)enemy.Tickets}  {Sim.Data.GetFaction(enemy.FactionId).DisplayName}";
            int seconds = (int)Sim.ElapsedSeconds;
            _clock.Text = $"{seconds / 60:00}:{seconds % 60:00}";
        }

        private void RefreshSelection()
        {
            foreach (Node child in _selection.GetChildren())
            {
                child.QueueFree();
            }

            var squads = _player.SelectedSquadObjects();
            var structure = _player.SelectedStructureObject();
            if (squads.Count == 1)
            {
                SquadDetails(squads[0]);
            }
            else if (squads.Count > 1)
            {
                _selection.AddChild(UiTheme.Label($"{squads.Count} squads selected", 16, UiTheme.Accent));
                foreach (var group in squads.GroupBy(s => s.Def.Name))
                {
                    float health = group.Average(s => s.HealthFraction);
                    _selection.AddChild(UiTheme.Label($"{group.Count()} × {group.Key}   {(int)(health * 100)}%", 13));
                }
            }
            else if (structure != null)
            {
                StructureDetails(structure);
            }
            else
            {
                _selection.AddChild(UiTheme.Label("Nothing selected", 15, UiTheme.Dim));
                _selection.AddChild(UiTheme.Label("Left-click or drag to select · Right-click to move / attack\nWASD/arrows/edge: pan · Wheel: zoom · Q/E: rotate\nR retreat · T reinforce · H stop · Ctrl+1-9 groups · Esc menu", 12, UiTheme.Dim));
            }
        }

        private void SquadDetails(Squad s)
        {
            _selection.AddChild(UiTheme.Label(s.Def.Name, 17, UiTheme.Accent));
            var bar = UiTheme.Bar(TeamColors.For(Me), 300f, 12f);
            bar.Value = s.HealthFraction;
            _selection.AddChild(bar);
            string models = s.Def.IsVehicle ? $"Hull {(int)s.Health}/{(int)s.MaxHealth}" : $"Soldiers {s.Models}/{s.Def.SquadSize}";
            string status = s.IsRetreating ? "Retreating" : s.SuppressionState == SuppressionState.Pinned ? "PINNED" : s.SuppressionState == SuppressionState.Suppressed ? "Suppressed" :
                s.BuildTargetId != 0 ? "Building" : s.ReinforcePending > 0 ? "Reinforcing" : s.TargetId != 0 ? "Engaging" : s.IsMoving ? "Moving" : "Ready";
            _selection.AddChild(UiTheme.Label($"{models}    Status: {status}    Kills: {s.Kills}", 13));
            var cover = Sim.Cover.GetCover(Sim.Map.Grid.WorldToCell(s.Position));
            _selection.AddChild(UiTheme.Label($"Cover: {cover}    Weapons: {string.Join(", ", s.Weapons.Select(w => w.Name))}", 12, UiTheme.Dim));
        }

        private void StructureDetails(Structure st)
        {
            _selection.AddChild(UiTheme.Label(st.Def.Name, 17, UiTheme.Accent));
            var bar = UiTheme.Bar(TeamColors.For(Me), 300f, 12f);
            bar.Value = st.HealthFraction;
            _selection.AddChild(bar);
            if (!st.IsComplete)
            {
                _selection.AddChild(UiTheme.Label($"Under construction: {(int)(st.BuildProgress * 100)}%  (right-click it with engineers to help)", 13));
                return;
            }

            if (st.Def.Produces.Count == 0)
            {
                return;
            }

            _selection.AddChild(UiTheme.Label(st.Queue.Count == 0 ? "Queue empty · right-click ground to set a rally point" : "Production queue (click to cancel):", 12, UiTheme.Dim));
            var queue = new HBoxContainer();
            for (int i = 0; i < st.Queue.Count; i++)
            {
                int index = i;
                var item = st.Queue[i];
                var b = new Button
                {
                    Text = i == 0 ? $"{Short(item.Unit.Name)}\n{(int)(item.Progress * 100)}%" : Short(item.Unit.Name),
                    CustomMinimumSize = new Vector2(86f, 46f),
                    FocusMode = FocusModeEnum.None,
                    TooltipText = "Cancel (full refund)",
                };
                b.Pressed += () => _player.CancelQueue(index);
                queue.AddChild(b);
            }

            _selection.AddChild(queue);
        }

        private static string Short(string name) => name.Length > 12 ? name.Substring(0, 11) + "…" : name;

        private void RefreshCard()
        {
            var squads = _player.SelectedSquadObjects();
            var st = _player.SelectedStructureObject();
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
                    AddCardButton($"{Short(d.Name)}\n{Cost(d.Manpower, d.Munitions, d.Fuel)}", SlotKeys[slot++ % SlotKeys.Length], () => _player.BeginPlacement(d), StructureTooltip(d), d.Id);
                }

                AddCardButton("Back", Key.Escape, () => { _buildMenu = false; _cardSignature = ""; }, "Back to orders");
                return;
            }

            if (squads.Count > 0)
            {
                AddCardButton("Retreat\n[R]", Key.R, _player.CommandRetreat, "Fall back to HQ: faster, harder to hit, cannot fire");
                AddCardButton("Reinforce\n[T]", Key.T, _player.CommandReinforce, "Refill lost soldiers near HQ or a production building (manpower per soldier)");
                AddCardButton("Stop\n[H]", Key.H, _player.CommandStop, "Stop and hold position");
                if (engineers)
                {
                    AddCardButton("Build\n[B]", Key.B, () => { _buildMenu = true; _cardSignature = ""; }, "Construct buildings and sandbags");
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
                    AddCardButton($"{Short(unit.Name)}\n{Cost(unit.Manpower, unit.Munitions, unit.Fuel)}", key, () => _player.Produce(id), UnitTooltip(unit, key), id);
                }
            }
        }

        private void UpdateCardStates(Structure st)
        {
            var player = Sim.GetPlayer(Me);
            foreach (var (button, _, _) in _cardButtons)
            {
                var id = button.GetMeta("def", "").AsString();
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (Sim.Data.HasStructure(id))
                {
                    var d = Sim.Data.GetStructure(id);
                    bool unlocked = d.Requires.All(r => Sim.World.OwnsComplete(Me, r));
                    bool affordable = player.Manpower >= d.Manpower && player.Munitions >= d.Munitions && player.Fuel >= d.Fuel;
                    button.Disabled = !unlocked || !affordable;
                }
                else if (st != null)
                {
                    var reason = Sim.Production.CanProduce(Me, st, id);
                    button.Disabled = reason != RejectReason.None;
                }
            }
        }

        private void AddCardButton(string text, Key key, Action action, string tooltip, string defId = null)
        {
            string keyName = key == Key.Escape ? "Esc" : OS.GetKeycodeString(key);
            var b = new Button
            {
                Text = text.Contains("[") || key == Key.Escape ? text : $"{text}\n[{keyName}]",
                CustomMinimumSize = new Vector2(82f, 56f),
                FocusMode = FocusModeEnum.None,
                TooltipText = tooltip,
                ClipText = true,
            };
            b.AddThemeFontSizeOverride("font_size", 11);
            if (defId != null)
            {
                b.SetMeta("def", defId);
            }

            b.Pressed += action;
            _card.AddChild(b);
            _cardButtons.Add((b, key, action));
        }

        private static string Cost(float mp, float mu, float fu)
        {
            var parts = new List<string> { $"{(int)mp}MP" };
            if (mu > 0) parts.Add($"{(int)mu}MU");
            if (fu > 0) parts.Add($"{(int)fu}FU");
            return string.Join(" ", parts);
        }

        private string StructureTooltip(StructureDef d)
        {
            var lines = new List<string> { d.Name, $"Cost: {Cost(d.Manpower, d.Munitions, d.Fuel)} · Build time {d.BuildTime:0}s" };
            if (d.Produces.Count > 0)
            {
                lines.Add("Unlocks: " + string.Join(", ", d.Produces.Select(p => Sim.Data.GetUnit(p).Name)));
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
            return $"{u.Name}  [{OS.GetKeycodeString(key)}]\nCost: {Cost(u.Manpower, u.Munitions, u.Fuel)} · Pop {u.Pop} · {u.BuildTime:0}s\n" +
                   $"{u.SquadSize} × {u.HealthPerModel:0} HP · Speed {u.MoveSpeed:0.#} m/s · Sight {u.SightRange:0} m\nWeapons: {weapons}";
        }
    }
}
