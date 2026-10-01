using Godot;

namespace OCT7.Game.Audio
{
    /// <summary>Master volume (0..1), applied to the Master bus and remembered in user://settings.cfg.</summary>
    public static class GameAudio
    {
        private const string SettingsPath = "user://settings.cfg";
        private static float _volume = -1f;

        public static float Volume
        {
            get
            {
                if (_volume < 0f)
                {
                    var config = new ConfigFile();
                    _volume = config.Load(SettingsPath) == Error.Ok ? config.GetValue("audio", "volume", 0.8).AsSingle() : 0.8f;
                    Apply();
                }

                return _volume;
            }
            set
            {
                _volume = Mathf.Clamp(value, 0f, 1f);
                Apply();
                var config = new ConfigFile();
                config.Load(SettingsPath);
                config.SetValue("audio", "volume", _volume);
                config.Save(SettingsPath);
            }
        }

        /// <summary>Applies the saved volume (call once at startup).</summary>
        public static void Initialize() => _ = Volume;

        private static void Apply()
        {
            AudioServer.SetBusMute(0, _volume <= 0.001f);
            AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Mathf.Max(_volume, 0.001f)));
        }
    }
}
