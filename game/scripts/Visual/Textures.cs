using System.Collections.Generic;
using Godot;

namespace OCT7.Game.Visual
{
    public enum Tex
    {
        None,
        Concrete,
        Plaster,
        Stone,
        Corrugated,
        Camo,
        Canvas,
        Tiles,
        Metal,
        Earth,
        GroundDetail,
    }

    public enum Sprite
    {
        Soft,
        Flash,
        Scorch,
    }

    /// <summary>
    /// Small procedural detail textures (generated once, 128 px, mostly near-white so they modulate the material color).
    /// Materials apply them with world-space triplanar mapping, so any box or sphere gets texture without UVs.
    /// </summary>
    public static class Textures
    {
        private const int Size = 128;
        private static readonly Dictionary<Tex, ImageTexture> Cache = new Dictionary<Tex, ImageTexture>();

        /// <summary>Meters covered by one texture repeat.</summary>
        public static float Scale(Tex t)
        {
            switch (t)
            {
                case Tex.Corrugated: return 2f;
                case Tex.Tiles: return 2.5f;
                case Tex.Stone: return 3f;
                case Tex.Canvas: return 2f;
                case Tex.Camo: return 6f;
                case Tex.Earth: return 8f;
                case Tex.GroundDetail: return 4f;
                default: return 5f;
            }
        }

        public static ImageTexture Get(Tex t)
        {
            if (Cache.TryGetValue(t, out var cached))
            {
                return cached;
            }

            var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgb8);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    image.SetPixel(x, y, Sample(t, x, y));
                }
            }

            image.GenerateMipmaps();
            var texture = ImageTexture.CreateFromImage(image);
            Cache[t] = texture;
            return texture;
        }

        private static readonly Dictionary<Sprite, ImageTexture> Sprites = new Dictionary<Sprite, ImageTexture>();

        /// <summary>RGBA sprites for effects: a soft cloudy puff, a star-shaped muzzle flash and a ground scorch mark.</summary>
        public static ImageTexture GetSprite(Sprite kind)
        {
            if (Sprites.TryGetValue(kind, out var cached))
            {
                return cached;
            }

            const int size = 64;
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float theta = Mathf.Atan2(dy, dx);
                    float cloud = Smooth(x * 2f, y * 2f, 16, 20 + (int)kind);
                    float a;
                    var c = Colors.White;
                    switch (kind)
                    {
                        case Sprite.Flash:
                            {
                                float core = Mathf.Pow(Mathf.Max(0f, 1f - r), 2.2f);
                                float spikes = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(theta * 5f)), 10f) * Mathf.Max(0f, 1f - r * 1.05f);
                                a = Mathf.Clamp(core * 1.4f + spikes, 0f, 1f);
                                break;
                            }

                        case Sprite.Scorch:
                            {
                                float streaks = 0.75f + 0.25f * Mathf.Sin(theta * 9f + cloud * 4f);
                                a = Mathf.Clamp((1f - Mathf.SmoothStep(0.25f, 1f, r * (0.85f + cloud * 0.3f))) * streaks, 0f, 1f) * 0.85f;
                                c = new Color(0.06f, 0.05f, 0.04f);
                                break;
                            }

                        default:
                            a = Mathf.Pow(Mathf.Clamp(1f - r, 0f, 1f), 1.6f) * (0.7f + 0.3f * cloud);
                            break;
                    }

                    image.SetPixel(x, y, new Color(c.R, c.G, c.B, a));
                }
            }

            image.GenerateMipmaps();
            var texture = ImageTexture.CreateFromImage(image);
            Sprites[kind] = texture;
            return texture;
        }

        private static float N(int x, int y, int seed) => MeshKit.Hash01(x * 7919 + seed * 104729, y * 6007 + seed);

        /// <summary>Tileable value noise (period = Size / cell).</summary>
        private static float Smooth(float x, float y, int cell, int seed)
        {
            int period = Size / cell;
            float fx = x / cell, fy = y / cell;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = N(Mathf.PosMod(x0, period), Mathf.PosMod(y0, period), seed);
            float b = N(Mathf.PosMod(x0 + 1, period), Mathf.PosMod(y0, period), seed);
            float c = N(Mathf.PosMod(x0, period), Mathf.PosMod(y0 + 1, period), seed);
            float d = N(Mathf.PosMod(x0 + 1, period), Mathf.PosMod(y0 + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static Color Gray(float v) => new Color(Mathf.Clamp(v, 0f, 1f), Mathf.Clamp(v, 0f, 1f), Mathf.Clamp(v, 0f, 1f));

        private static Color Sample(Tex t, int x, int y)
        {
            float fine = N(x, y, (int)t);
            switch (t)
            {
                case Tex.Concrete:
                    {
                        float v = 0.86f + Smooth(x, y, 16, 1) * 0.1f + fine * 0.06f;
                        if (y % 32 == 0) v -= 0.08f; // formwork lines
                        if (Smooth(x, y, 32, 2) > 0.72f) v -= 0.08f; // stains
                        return Gray(v);
                    }

                case Tex.Plaster:
                    return Gray(0.88f + Smooth(x, y, 32, 3) * 0.08f + Smooth(x, y, 8, 4) * 0.04f + fine * 0.03f);
                case Tex.Stone:
                    {
                        // Irregular courses of blocks with dark mortar.
                        int row = y / 16;
                        int offset = (int)(N(row, 0, 5) * 24f);
                        int bx = (x + offset) / 24;
                        int ly = y % 16, lx = (x + offset) % 24;
                        bool mortar = ly < 2 || lx < 2;
                        float block = 0.8f + N(bx, row, 6) * 0.18f + fine * 0.05f;
                        return Gray(mortar ? 0.55f : block);
                    }

                case Tex.Corrugated:
                    {
                        float ridge = 0.8f + 0.2f * Mathf.Sin(x / 8f * Mathf.Tau);
                        float rust = Smooth(x, y, 32, 7);
                        var c = Gray(ridge * (0.95f + fine * 0.05f));
                        return rust > 0.62f ? c.Lerp(new Color(0.62f, 0.38f, 0.22f) * ridge, (rust - 0.62f) * 2.2f) : c;
                    }

                case Tex.Camo:
                    {
                        float a = Smooth(x, y, 32, 8), b = Smooth(x, y, 16, 9);
                        float v = a > 0.62f ? 0.62f : b > 0.6f ? 0.8f : 1f;
                        // Net mesh holes.
                        if ((x % 6 == 0) || (y % 6 == 0)) v *= 0.82f;
                        return Gray(v);
                    }

                case Tex.Canvas:
                    {
                        float weave = ((x % 4 < 2) ^ (y % 4 < 2)) ? 0.96f : 0.9f;
                        return Gray(weave + Smooth(x, y, 32, 10) * 0.06f - 0.03f);
                    }

                case Tex.Tiles:
                    {
                        // Rows of curved clay tiles.
                        int row = y / 16;
                        float lx = ((x + (row % 2) * 8) % 16) / 16f;
                        float curve = 0.78f + 0.22f * Mathf.Sin(lx * Mathf.Pi);
                        float edge = y % 16 > 13 ? 0.62f : 1f;
                        return Gray(curve * edge * (0.92f + N(x / 16, row, 11) * 0.08f));
                    }

                case Tex.Metal:
                    return Gray(0.85f + Smooth(x, y, 4, 12) * 0.06f + Smooth(x, y, 32, 13) * 0.08f);
                case Tex.Earth:
                    return Gray(0.8f + Smooth(x, y, 16, 14) * 0.15f + fine * 0.08f);
                case Tex.GroundDetail:
                    {
                        // Pebbles, hairline cracks and grain; near-white so it modulates the ground map.
                        float v = 0.9f + fine * 0.08f + Smooth(x, y, 8, 15) * 0.06f;
                        int cx = x / 8, cy = y / 8;
                        if (N(cx, cy, 17) > 0.72f)
                        {
                            float px = cx * 8 + 2 + N(cx, cy, 18) * 4f, py = cy * 8 + 2 + N(cx, cy, 19) * 4f;
                            float r = 1.0f + N(cx, cy, 20) * 1.8f;
                            float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                            if (d < r) v = 0.78f + (py - y) / r * 0.1f + N(cx, cy, 21) * 0.12f;
                            else if (d < r + 0.8f && y > py) v -= 0.06f; // pebble shadow
                        }

                        return Gray(v);
                    }
                default:
                    return Colors.White;
            }
        }
    }
}
