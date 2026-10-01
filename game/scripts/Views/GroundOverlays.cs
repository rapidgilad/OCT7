using Godot;
using OCT7.Sim;
using OCT7.Sim.World;

namespace OCT7.Game.Views
{
    /// <summary>
    /// Two transparent planes just above the ground: territory (thin anti-aliased sector borders in the owner's
    /// color, 4 texels per cell) and fog of war for the local player (unexplored = dark, explored but not visible =
    /// dimmed, visible = clear; 1 texel per cell, blurred so the line-of-sight edge is soft).
    /// </summary>
    public partial class GroundOverlays : Node3D
    {
        private const int TerritoryTexelsPerCell = 4;
        private const float BorderHalfWidth = 0.55f;

        private Image _fogImage;
        private ImageTexture _fogTexture;
        private byte[] _fogBytes;
        private float[] _fogRaw;
        private float[] _fogTmp;
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
            _territoryBytes = new byte[grid.Width * TerritoryTexelsPerCell * grid.Height * TerritoryTexelsPerCell * 4];
            _territoryImage = Image.CreateFromData(grid.Width * TerritoryTexelsPerCell, grid.Height * TerritoryTexelsPerCell, false, Image.Format.Rgba8, _territoryBytes);
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
            int w = grid.Width, h = grid.Height;
            if (_fogRaw == null)
            {
                _fogRaw = new float[w * h];
                _fogTmp = new float[w * h];
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = new GridPos(x, y);
                    _fogRaw[y * w + x] = sim.Vision.IsVisible(LocalPlayerId, c) ? 0f : sim.Vision.IsExplored(LocalPlayerId, c) ? 110f : 185f;
                }
            }

            // Separable box blur (radius 2 cells) so the line-of-sight edge reads as a soft fog bank, not stair steps.
            BoxBlur(_fogRaw, _fogTmp, w, h, 1, 0, 2);
            BoxBlur(_fogTmp, _fogRaw, w, h, 0, 1, 2);
            for (int i = 0; i < w * h; i++)
            {
                int b = i * 4;
                _fogBytes[b] = 10;
                _fogBytes[b + 1] = 12;
                _fogBytes[b + 2] = 18;
                _fogBytes[b + 3] = (byte)Mathf.Clamp(_fogRaw[i], 0f, 255f);
            }

            _fogImage.SetData(grid.Width, grid.Height, false, Image.Format.Rgba8, _fogBytes);
            _fogTexture.Update(_fogImage);
        }

        private static void BoxBlur(float[] src, float[] dst, int w, int h, int dx, int dy, int radius)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sx = Mathf.Clamp(x + k * dx, 0, w - 1);
                        int sy = Mathf.Clamp(y + k * dy, 0, h - 1);
                        sum += src[sy * w + sx];
                    }

                    dst[y * w + x] = sum / (2 * radius + 1);
                }
            }
        }

        private void UpdateTerritory(Simulation sim)
        {
            var sectors = sim.Territory.Sectors;
            var key = new System.Text.StringBuilder();
            foreach (var s in sectors)
            {
                key.Append(s.OwnerId).Append(',');
            }

            if (key.ToString() == _territoryKey || sectors.Count == 0)
            {
                return;
            }

            _territoryKey = key.ToString();

            // Sectors are the Euclidean Voronoi partition of the capture points (see TerritorySystem), so each border
            // can be drawn analytically: distance to the bisector between the nearest and second-nearest point.
            var grid = sim.Map.Grid;
            int w = grid.Width * TerritoryTexelsPerCell, h = grid.Height * TerritoryTexelsPerCell;
            float texel = grid.CellSize / TerritoryTexelsPerCell;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vec2((x + 0.5f) * texel, (y + 0.5f) * texel);
                    int first = -1, second = -1;
                    float d1 = float.MaxValue, d2 = float.MaxValue;
                    for (int i = 0; i < sectors.Count; i++)
                    {
                        float d = Vec2.DistanceSquared(p, sectors[i].Position);
                        if (d < d1)
                        {
                            d2 = d1;
                            second = first;
                            d1 = d;
                            first = i;
                        }
                        else if (d < d2)
                        {
                            d2 = d;
                            second = i;
                        }
                    }

                    float edge = second >= 0 ? (d2 - d1) / (2f * Vec2.Distance(sectors[first].Position, sectors[second].Position)) : 99f;
                    int owner = sectors[first].OwnerId;
                    var color = owner >= 0 ? TeamColors.For(owner).Lightened(0.15f) : new Color(0.95f, 0.93f, 0.85f);
                    float strength = owner >= 0 ? 0.75f : 0.35f;
                    float a = strength * (1f - Mathf.SmoothStep(BorderHalfWidth - 0.35f, BorderHalfWidth + 0.35f, edge));
                    int b = (y * w + x) * 4;
                    _territoryBytes[b] = (byte)(color.R * 255f);
                    _territoryBytes[b + 1] = (byte)(color.G * 255f);
                    _territoryBytes[b + 2] = (byte)(color.B * 255f);
                    _territoryBytes[b + 3] = (byte)(a * 255f);
                }
            }

            _territoryImage.SetData(w, h, false, Image.Format.Rgba8, _territoryBytes);
            _territoryTexture.Update(_territoryImage);
        }
    }
}
