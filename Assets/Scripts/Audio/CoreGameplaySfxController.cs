using BattleMaps.SaveStates;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.Audio
{
    /// <summary>
    /// Reconstructed from BattleMaps.Audio.CoreGameplaySfxController (18 methods) & KillComboPitch.
    /// Generates clean procedural AudioClips at startup so SFX work out-of-the-box with zero external audio files.
    /// </summary>
    public class CoreGameplaySfxController : MonoBehaviour
    {
        private AudioSource _sfxSource;
        private AudioSource _musicSource;
        private AudioClip _captureClip;
        private AudioClip _killClip;
        private AudioClip _powerClip;
        private AudioClip _winClip;
        private AudioClip _bgmClip;
        private float _lastKillTime;
        private int _killCombo;

        private void Awake()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.volume = 0.35f;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.volume = 0.14f;

            _captureClip = CreateToneClip("CaptureSfx", 520f, 780f, 0.14f);
            _killClip = CreateToneClip("KillSfx", 440f, 920f, 0.18f);
            _powerClip = CreateToneClip("PowerSfx", 280f, 880f, 0.25f);
            _winClip = CreateToneClip("WinSfx", 587f, 1174f, 0.35f);
            _bgmClip = CreateProceduralMusicLoop();
            _musicSource.clip = _bgmClip;

            EventBus.Shared.Subscribe<ZoneCapturedSignal>(OnZoneCaptured);
            EventBus.Shared.Subscribe<PlayerKilledSignal>(OnPlayerKilled);
            EventBus.Shared.Subscribe<PowerActivatedSignal>(OnPowerActivated);
            EventBus.Shared.Subscribe<MatchEndedSignal>(OnMatchEnded);
        }

        private void Update()
        {
            bool musicOn = SaveStateController.Instance == null || SaveStateController.Instance.Data.MusicEnabled;
            if (musicOn && !_musicSource.isPlaying) _musicSource.Play();
            else if (!musicOn && _musicSource.isPlaying) _musicSource.Pause();
        }

        private static AudioClip CreateProceduralMusicLoop()
        {
            const int sr = 22050;
            float[] notes = { 261.63f, 329.63f, 392.00f, 523.25f, 392.00f, 329.63f, 293.66f, 349.23f, 440.00f, 349.23f, 392.00f, 493.88f, 587.33f, 493.88f, 392.00f, 329.63f };
            float noteDur = 0.22f;
            int total = Mathf.RoundToInt(notes.Length * noteDur * sr);
            var samples = new float[total];
            int samplesPerNote = total / notes.Length;

            for (int n = 0; n < notes.Length; n++)
            {
                float f = notes[n];
                float phase = 0f;
                for (int i = 0; i < samplesPerNote; i++)
                {
                    float t = i / (float)samplesPerNote;
                    phase += 2f * Mathf.PI * f / sr;
                    float env = Mathf.Sin(t * Mathf.PI) * 0.28f;
                    float bass = Mathf.Sin(phase * 0.5f) * 0.14f;
                    samples[n * samplesPerNote + i] = (Mathf.Sin(phase) * env) + bass;
                }
            }

            var clip = AudioClip.Create("ProceduralBGM", total, 1, sr, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private bool IsSfxOn => SaveStateController.Instance == null || SaveStateController.Instance.Data.SfxEnabled;

        private void OnZoneCaptured(ZoneCapturedSignal sig)
        {
            if (!IsSfxOn || sig.OwnerId != 1) return;
            _sfxSource.pitch = Mathf.Clamp(0.95f + sig.AddedArea * 0.015f, 0.95f, 1.55f);
            _sfxSource.PlayOneShot(_captureClip);
        }

        private void OnPlayerKilled(PlayerKilledSignal sig)
        {
            if (!IsSfxOn || sig.KillerId != 1) return;
            if (Time.time - _lastKillTime < 6f) _killCombo++;
            else _killCombo = 0;
            _lastKillTime = Time.time;

            _sfxSource.pitch = Mathf.Clamp(1.0f + _killCombo * 0.12f, 1.0f, 1.75f);
            _sfxSource.PlayOneShot(_killClip);
        }

        private void OnPowerActivated(PowerActivatedSignal sig)
        {
            if (!IsSfxOn || sig.PlayerId != 1) return;
            _sfxSource.pitch = 1.0f;
            _sfxSource.PlayOneShot(_powerClip);
        }

        private void OnMatchEnded(MatchEndedSignal sig)
        {
            if (!IsSfxOn || !sig.LocalPlayerWon) return;
            _sfxSource.pitch = 1.0f;
            _sfxSource.PlayOneShot(_winClip);
        }

        private static AudioClip CreateToneClip(string name, float startHz, float endHz, float duration)
        {
            const int sampleRate = 22050;
            int count = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            var samples = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float freq = Mathf.Lerp(startHz, endHz, t);
                phase += 2f * Mathf.PI * freq / sampleRate;
                float env = Mathf.Sin(t * Mathf.PI);
                samples[i] = Mathf.Sin(phase) * env * 0.5f;
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            EventBus.Shared.Unsubscribe<ZoneCapturedSignal>(OnZoneCaptured);
            EventBus.Shared.Unsubscribe<PlayerKilledSignal>(OnPlayerKilled);
            EventBus.Shared.Unsubscribe<PowerActivatedSignal>(OnPowerActivated);
            EventBus.Shared.Unsubscribe<MatchEndedSignal>(OnMatchEnded);
        }
    }
}
