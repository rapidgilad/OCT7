using System.Collections.Generic;
using Godot;
using OCT7.Sim;
using OCT7.Sim.Data;

namespace OCT7.Game.Audio
{
    /// <summary>
    /// Event-driven positional sound: gunfire per weapon kind, rocket launches, cannon fire and explosions
    /// (impacts delayed to match the VFX flight time, plus vehicle and building destruction). Uses a pool of
    /// AudioStreamPlayer3D voices heard by the current camera, limits how many sounds of one kind start per frame
    /// so big firefights don't turn into noise, and only plays what the local player can see.
    /// </summary>
    public partial class AudioManager : Node3D
    {
        private const int VoiceCount = 32;

        private struct Pending
        {
            public float At;
            public SoundId Id;
            public Vector3 Position;
            public float VolumeDb;
            public float Pitch;
        }

        private readonly List<AudioStreamPlayer3D> _voices = new List<AudioStreamPlayer3D>();
        private readonly float[] _voiceStarted = new float[VoiceCount];
        private readonly List<Pending> _pending = new List<Pending>();
        private readonly int[] _startedThisFrame = new int[SoundBank.All.Length];
        private readonly int[] _played = new int[SoundBank.All.Length];
        private readonly RandomNumberGenerator _rng = new RandomNumberGenerator { Seed = 4242 };
        private Simulation _sim;
        private float _time;

        public int LocalPlayerId { get; set; }
        public bool RevealAll { get; set; }

        /// <summary>Print a one-line summary of played sounds on exit (automated runs).</summary>
        public bool LogStats { get; set; }

        public void Initialize(Simulation sim)
        {
            _sim = sim;
            SoundBank.Warm();
            for (int i = 0; i < VoiceCount; i++)
            {
                var voice = new AudioStreamPlayer3D
                {
                    Name = $"Voice{i}",
                    AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                    UnitSize = 30f,
                    MaxDistance = 320f,
                    DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled,
                    AttenuationFilterCutoffHz = 9000f,
                    AttenuationFilterDb = -12f,
                };
                AddChild(voice);
                _voices.Add(voice);
            }
        }

        private bool Seen(Vec2 p) => RevealAll || _sim.Vision.IsVisible(LocalPlayerId, p);

        private static Vector3 V3(Vec2 p, float y) => new Vector3(p.X, y, p.Y);

        public void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.ShotFired:
                    if (Seen(e.From) || Seen(e.To))
                    {
                        Shot(e);
                    }

                    break;
                case SimEventType.SquadDestroyed:
                    if (_sim.Data.HasUnit(e.DefId) && _sim.Data.GetUnit(e.DefId).IsVehicle && Seen(e.To))
                    {
                        Play(SoundId.Explosion, V3(e.To, 1f), 3f, 0.85f);
                    }

                    break;
                case SimEventType.StructureDestroyed:
                    if (Seen(e.To))
                    {
                        Play(SoundId.Explosion, V3(e.To, 2f), 4f, 0.72f);
                    }

                    break;
            }
        }

        private void Shot(SimEvent e)
        {
            var kind = _sim.Data.HasWeapon(e.DefId) ? _sim.Data.GetWeapon(e.DefId).Kind : WeaponKind.SmallArms;
            var from = V3(e.From, 1.4f);
            var to = V3(e.To, 1f);
            switch (kind)
            {
                case WeaponKind.SmallArms:
                    Play(SoundId.Rifle, from, -4f, 1f);
                    break;
                case WeaponKind.MachineGun:
                    Play(SoundId.MachineGun, from, -3f, 1f);
                    break;
                case WeaponKind.Sniper:
                    Play(SoundId.Sniper, from, 0f, 1f);
                    break;
                case WeaponKind.Autocannon:
                    Play(SoundId.Cannon, from, -5f, 1.7f);
                    break;
                case WeaponKind.TankGun:
                    Play(SoundId.Cannon, from, 2f, 1f);
                    Play(SoundId.Explosion, to, -2f, 1.3f, 0.12f); // shell impact, timed with the VFX
                    break;
                case WeaponKind.AntiTank:
                    Play(SoundId.RocketLaunch, from, -1f, 1f);
                    Play(SoundId.Explosion, to, e.Value > 0 ? 0f : -3f, 1.15f, 0.3f); // rocket impact, timed with the VFX
                    break;
            }
        }

        /// <summary>Queues a sound; <paramref name="pitch"/> also scales its length (lower = bigger, longer).</summary>
        public void Play(SoundId id, Vector3 position, float volumeDb, float pitch, float delay = 0f)
        {
            _pending.Add(new Pending { At = _time + delay, Id = id, Position = position, VolumeDb = volumeDb, Pitch = pitch });
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;
            System.Array.Clear(_startedThisFrame, 0, _startedThisFrame.Length);
            for (int i = 0; i < _pending.Count; i++)
            {
                var p = _pending[i];
                if (p.At > _time)
                {
                    continue;
                }

                _pending.RemoveAt(i--);
                int k = (int)p.Id;
                if (_startedThisFrame[k] >= MaxPerFrame(p.Id))
                {
                    continue; // already loud enough this frame
                }

                _startedThisFrame[k]++;
                _played[k]++;
                var voice = FreeVoice();
                voice.Stream = SoundBank.Get(p.Id, _rng.RandiRange(0, 7));
                voice.Position = p.Position;
                voice.VolumeDb = p.VolumeDb + _rng.RandfRange(-1.5f, 1f);
                voice.PitchScale = p.Pitch * _rng.RandfRange(0.93f, 1.07f);
                voice.Play();
                _voiceStarted[_voices.IndexOf(voice)] = _time;
            }
        }

        private static int MaxPerFrame(SoundId id) => id == SoundId.Rifle ? 3 : 2;

        /// <summary>A voice that isn't playing, or else the one that started longest ago.</summary>
        private AudioStreamPlayer3D FreeVoice()
        {
            int oldest = 0;
            for (int i = 0; i < _voices.Count; i++)
            {
                if (!_voices[i].Playing)
                {
                    return _voices[i];
                }

                if (_voiceStarted[i] < _voiceStarted[oldest])
                {
                    oldest = i;
                }
            }

            return _voices[oldest];
        }

        public override void _ExitTree()
        {
            if (!LogStats)
            {
                return;
            }

            var parts = new List<string>();
            foreach (var id in SoundBank.All)
            {
                parts.Add($"{SoundBank.FileName(id)}={_played[(int)id]}");
            }

            GD.Print($"[audio] played {string.Join(" ", parts)} driver={AudioServer.GetDriverName()}");
        }
    }
}
