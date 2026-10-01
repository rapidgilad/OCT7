using Godot;

namespace OCT7.Game.Views
{
    /// <summary>Team colors are per player slot (not per faction) so mirror matches stay readable.</summary>
    public static class TeamColors
    {
        private static readonly Color[] Colors =
        {
            new Color(0.25f, 0.52f, 0.95f), // player 0 — blue
            new Color(0.90f, 0.30f, 0.22f), // player 1 — red
        };

        public static Color For(int playerId) => Colors[Mathf.PosMod(playerId, Colors.Length)];

        public static readonly Color Selection = new Color(0.55f, 1f, 0.55f);
    }
}
