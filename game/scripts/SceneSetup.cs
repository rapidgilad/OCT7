using Godot;

namespace OCT7.Game
{
    /// <summary>
    /// Lighting and environment, built in code so the scene file stays minimal.
    /// Forward+ (desktop default) gets HDR values with Filmic tonemapping; the Compatibility renderer
    /// (used for cloud screenshots via Xvfb) has no HDR headroom, so it gets lower energies and Linear tonemapping.
    /// </summary>
    public static class SceneSetup
    {
        private static bool IsCompatibilityRenderer =>
            RenderingServer.GetCurrentRenderingMethod() == "gl_compatibility";

        public static WorldEnvironment CreateEnvironment()
        {
            bool compat = IsCompatibilityRenderer;
            var sky = new ProceduralSkyMaterial
            {
                SkyTopColor = new Color(0.36f, 0.52f, 0.72f),
                SkyHorizonColor = new Color(0.78f, 0.80f, 0.78f),
                GroundHorizonColor = new Color(0.62f, 0.58f, 0.50f),
                GroundBottomColor = new Color(0.30f, 0.28f, 0.24f),
            };

            var environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = sky },
                AmbientLightSource = Godot.Environment.AmbientSource.Sky,
                AmbientLightEnergy = compat ? 0.30f : 0.50f,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
                TonemapMode = compat ? Godot.Environment.ToneMapper.Linear : Godot.Environment.ToneMapper.Filmic,
                FogEnabled = !compat,
                FogLightColor = new Color(0.80f, 0.78f, 0.72f),
                FogDensity = 0.0006f,
            };

            return new WorldEnvironment { Name = "Environment", Environment = environment };
        }

        public static DirectionalLight3D CreateSun()
        {
            return new DirectionalLight3D
            {
                Name = "Sun",
                RotationDegrees = new Vector3(-52f, -35f, 0f),
                LightEnergy = IsCompatibilityRenderer ? 0.55f : 1.0f,
                LightColor = new Color(1f, 0.96f, 0.88f),
                ShadowEnabled = true,
                DirectionalShadowMaxDistance = 400f,
            };
        }
    }
}
