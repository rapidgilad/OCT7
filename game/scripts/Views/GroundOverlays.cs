using Godot;
using OCT7.Sim;
using OCT7.Sim.World;

namespace OCT7.Game.Views
{
    /// <summary>
    /// Two transparent planes just above the ground: territory (owner tint + sector borders) and fog of war
    /// (unexplored = dark, explored but not visible = dimmed, visible = clear) for the local player.
    /// Both are 1 texel per map cell with linear filtering for soft edges.
    /// </summary>
    public partial class GroundOverlays : Node3D
    {
        private Image _fogImage;
        private ImageTexture _fogTexture;
        private byte[] _fogBytes;
        private Image _territoryImage;
        private ImageTexture _territoryTexture;
        private byte[] _territoryBytes;
        private MeshInstance3D _fogPlane;
        private int _lastFogTick = -1;
        private string _territoryKey = "";

        public int LocalPlayerId { get; set; }
        public bool FogEnabled { get; set; } = true;

        public void Build(Simulation sim)
        {
            var grid = sim.Map.Grid;
            _territoryBytes = new byte[grid.Width * grid.Height * 4];
            _territoryImage = Image.CreateFromData(grid.Width, grid.Height, false, Image.Format.Rgba8, _territoryBytes);
            _territoryTexture = ImageTexture.CreateFromImage(_territoryImage);
            AddChild(Plane(grid, _territoryTexture, 0.07f, "Territory"));

            _fogBytes = new byte[grid.Width * grid.Height * 4];
            _fogImage = Image.CreateFromData(grid.Width, grid.Height, false, Image.Format.Rgba8, _fogBytes);
            _fogTexture = ImageTexture.CreateFromImage(_fogImage);
            _fogPlane = Plane(grid, _fogTexture, 0.09f, "FogOfWar");
            AddChild(_fogPlane);
            _fogPlane.Visible = FogEnabled;
        }

        private static MeshInstance3D Plane(MapGrid grid, Texture2D texture, float y, string name) => new MeshInstance3D
        {
            Name = name,
            Mesh = new PlaneMesh { Size = new Vector2(grid.WorldWidth, grid.WorldHeight) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = texture,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            },
            Position = new Vector3(grid.WorldWidth * 0.5f, y, grid.WorldHeight * 0.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        public void UpdateOverlays(Simulation sim)
        {
            UpdateTerritory(sim);
            if (FogEnabled && sim.Tick != _lastFogTick)
            {
                _lastFogTick = sim.Tick;
                UpdateFog(sim);
            }
        }

        private void UpdateFog(Simulation sim)
        {
            var grid = sim.Map.Grid;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var c = new GridPos(x, y);
                    byte alpha = sim.Vision.IsVisible(LocalPlayerId, c) ? (byte)0 : sim.Vision.IsExplored(LocalPlayerId, c) ? (byte)95 : (byte)165;
                    int i = (y * grid.Width + x) * 4;
                    _fogBytes[i] = 8;
                    _fogBytes[i + 1] = 10;
                    _fogBytes[i + 2] = 14;
                    _fogBytes[i + 3] = alpha;
                }
            }

            _fogImage.SetData(grid.Width, grid.Height, false, Image.Format.Rgba8, _fogBytes);
            _fogTexture.Update(_fogImage);
        }

        private void UpdateTerritory(Simulation sim)
        {
            var sectors = sim.Territory.Sectors;
            var key = new System.Text.StringBuilder();
            foreach (var s in sectors)
            {
                key.Append(s.OwnerId).Append(',');
            }

            if (key.ToString() == _territoryKey)
            {
                return;
            }

            _territoryKey = key.ToString();
            var grid = sim.Map.Grid;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int sector = sim.Territory.SectorIndexAt(new GridPos(x, y));
                    int owner = sector >= 0 ? sectors[sector].OwnerId : -1;
                    bool border = sector >= 0 && (sim.Territory.SectorIndexAt(new GridPos(x + 1, y)) is int r && r >= 0 && r != sector
                                                  || sim.Territory.SectorIndexAt(new GridPos(x, y + 1)) is int u && u >= 0 && u != sector);
                    var color = owner >= 0 ? TeamColors.For(owner) : new Color(1f, 1f, 1f);
                    float a = border ? 0.32f : owner >= 0 ? 0.09f : 0f;
                    int i = (y * grid.Width + x) * 4;
                    _territoryBytes[i] = (byte)(color.R * 255f);
                    _territoryBytes[i + 1] = (byte)(color.G * 255f);
                    _territoryBytes[i + 2] = (byte)(color.B * 255f);
                    _territoryBytes[i + 3] = (byte)(a * 255f);
                }
            }

            _territoryImage.SetData(grid.Width, grid.Height, false, Image.Format.Rgba8, _territoryBytes);
            _territoryTexture.Update(_territoryImage);
        }
    }
}
