using System;
using System.Collections.Generic;
using UnityEngine;

namespace NO404.Core
{
    /// <summary>
    /// Every sound the game asks for, named by what it means rather than by a file.
    /// Adding a real recording later is dropping a clip into Resources/NO404/Audio with the
    /// matching name - no gameplay code changes.
    /// </summary>
    public enum AudioCue
    {
        None = 0,
        /// <summary>A channel has just started lying.</summary>
        AnomalyStart,
        /// <summary>Someone is at the front door.</summary>
        Interphone,
        /// <summary>GDD 2.3 hook #5 - the handset carries the office's own room tone back.</summary>
        InterphoneSelfEcho,
        PhoneRing,
        /// <summary>A resident crossing the room the player is standing in.</summary>
        Footsteps,
        /// <summary>
        /// Steps in a room the caretaker is not in (v2.1 spec 0.10.4).
        ///
        /// Separate from Footsteps on purpose: the ordinary cue means somebody is here, and
        /// this one has to mean the opposite - somebody is somewhere else, and there should
        /// not be.
        /// </summary>
        FootstepsDistant,
        DoorOpen,
        EvidenceTaken,
        ReportFiled,
        ReportRejected,
        FireAlarm,

        // The office door (GDD 15.6) and the night reserve (GDD 15.5). These are the only
        // cues in the game the player hears without having asked for anything.
        OfficeDoorKnock,
        OfficeDoorHandle,
        OfficeDoorForced,
        PowerWarning,
        BreakerReset,

        RoomToneOffice,
        RoomToneCorridor,
        RoomToneBasement,
        RoomToneOutside
    }

    /// <summary>
    /// What a cue is allowed to do to the mix (GDD 19.3). The class picks the level the cue
    /// is normalised to, which is the whole of this game's loudness policy.
    /// </summary>
    public enum AudioCueClass
    {
        /// <summary>The looping bed. Everything else is measured against it.</summary>
        Ambience,
        /// <summary>A confirmation the player asked for. Never louder than a footstep.</summary>
        Interface,
        /// <summary>Something in the building made this noise.</summary>
        Diegetic,
        /// <summary>The loudest the game is ever permitted to be.</summary>
        Scare
    }

    /// <summary>
    /// Audio front end (GDD 19).
    ///
    /// Callers name a cue; this resolves it. A real clip at Resources/NO404/Audio/&lt;cue&gt;
    /// wins if it exists, otherwise a placeholder is synthesised in code so a missing file is
    /// never a silent bug. Tools/GenerateAudio.py renders the shipped set.
    ///
    /// Loudness is not left to whoever authored the file. Every clip is measured once on load
    /// and played at a gain that lands it on the target level for its class, so GDD 19.3's
    /// "a scare peaks no more than +8 dB over normal ambience" holds for a synthesised
    /// placeholder and for a recording dropped in later, without anyone re-checking a mix.
    /// </summary>
    public sealed class AudioService
    {
        const string ClipFolder = "NO404/Audio/";
        const int SampleRate = 44100;

        // ---- the loudness policy (GDD 19.3) ---------------------------------

        /// <summary>Where the looping bed sits, in dBFS RMS. Everything else is relative.</summary>
        public const float AmbienceLevelDb = -33f;

        /// <summary>
        /// GDD 19.3: a jump scare may peak at most this far above normal ambience. This is a
        /// ceiling on the whole game, not a target - only the Scare class is allowed to use it.
        /// </summary>
        public const float ScareHeadroomDb = 8f;

        const float InterfaceHeadroomDb = 3f;
        const float DiegeticHeadroomDb = 5f;

        /// <summary>How long a room tone takes to become the other room tone.</summary>
        public const float AmbienceCrossfadeSeconds = 1.5f;

        // Positional playback. The corridor is 1.55m wide (BuildingSpec / GDD 17.4), so a cue
        // that fills the corridor has to be at full level well before the player is on top of it.
        const float MinDistance = 1.5f;
        const float MaxDistance = 26f;
        const int PositionalVoices = 8;

        readonly Dictionary<AudioCue, AudioClip> _clips = new Dictionary<AudioCue, AudioClip>();
        readonly Dictionary<AudioCue, float> _gains = new Dictionary<AudioCue, float>();

        readonly AudioSource _uiSource;
        readonly AudioSource[] _positional = new AudioSource[PositionalVoices];

        // Two beds, so a room change is a crossfade rather than a cut. A cut is audible and
        // reads as a bug in a game whose horror is carried by room tone.
        readonly AudioSource[] _ambienceSources = new AudioSource[2];
        int _activeBed;
        float _crossfade = 1f;

        // ---- the heartbeat --------------------------------------------------
        //
        // Its own voice, because it is the only thing in the mix that is inside the player
        // rather than in the building: it must not be stolen by a pooled positional voice, and
        // it must not duck for a door two rooms away.

        readonly AudioSource _heartSource;
        float _heartTarget;      // 0..1, pushed in every frame by DarknessDirector
        float _heartLevel;       // smoothed, so fear arrives and leaves at a human rate
        float _heartCountdown;

        readonly SettingsService _settings;
        int _nextVoice;

        AudioCue _ambience = AudioCue.None;

        public AudioService(Transform parent, SettingsService settings)
        {
            _settings = settings;

            var go = new GameObject("AudioService");
            go.transform.SetParent(parent, false);

            _uiSource = go.AddComponent<AudioSource>();
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;

            for (int i = 0; i < _ambienceSources.Length; i++)
            {
                var bed = go.AddComponent<AudioSource>();
                bed.playOnAwake = false;
                bed.loop = true;
                bed.spatialBlend = 0f;
                bed.volume = 0f;
                _ambienceSources[i] = bed;
            }

            for (int i = 0; i < PositionalVoices; i++)
            {
                var voice = new GameObject("Voice" + i);
                voice.transform.SetParent(go.transform, false);

                var source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = MinDistance;
                source.maxDistance = MaxDistance;
                _positional[i] = source;
            }


            var driver = go.AddComponent<AudioServiceDriver>();
            driver.Bind(this);
        }

        // ---- the policy, as data --------------------------------------------

        /// <summary>Which loudness class a cue belongs to. Exposed so a test can hold the line.</summary>
        public static AudioCueClass ClassOf(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.RoomToneOffice:
                case AudioCue.RoomToneCorridor:
                case AudioCue.RoomToneBasement:
                case AudioCue.RoomToneOutside:
                    return AudioCueClass.Ambience;

                case AudioCue.EvidenceTaken:
                case AudioCue.ReportFiled:
                case AudioCue.ReportRejected:
                    return AudioCueClass.Interface;

                // The three loudest moments the game has. Everything the player is meant to
                // flinch at is in here, and nothing else may use the scare headroom.
                case AudioCue.AnomalyStart:
                case AudioCue.OfficeDoorForced:
                case AudioCue.FireAlarm:
                    return AudioCueClass.Scare;

                default:
                    return AudioCueClass.Diegetic;
            }
        }

        /// <summary>The dBFS RMS a cue is normalised to before the volume sliders touch it.</summary>
        public static float TargetLevelDb(AudioCue cue)
        {
            switch (ClassOf(cue))
            {
                case AudioCueClass.Ambience:  return AmbienceLevelDb;
                case AudioCueClass.Interface: return AmbienceLevelDb + InterfaceHeadroomDb;
                case AudioCueClass.Diegetic:  return AmbienceLevelDb + DiegeticHeadroomDb;
                default:                      return AmbienceLevelDb + ScareHeadroomDb;
            }
        }

        // ---- playback --------------------------------------------------------

        /// <summary>A cue with no position - the player's own PC, the handset, the HUD.</summary>
        public void PlayCue(AudioCue cue)
        {
            if (cue == AudioCue.None) return;

            var clip = ClipFor(cue);
            if (clip == null) return;

            _uiSource.PlayOneShot(clip, GainFor(cue) * SfxVolume);
        }

        /// <summary>
        /// A cue that came from somewhere (GDD 19.3 - important sounds carry a 3D position).
        /// Voices are pooled and stolen round-robin; there is never a moment in this game with
        /// eight things making noise at once, and if there were, the oldest is the right one
        /// to lose.
        /// </summary>
        public void PlayCueAt(AudioCue cue, Vector3 worldPosition)
        {
            if (cue == AudioCue.None) return;

            var clip = ClipFor(cue);
            if (clip == null) return;

            var source = _positional[_nextVoice];
            _nextVoice = (_nextVoice + 1) % PositionalVoices;

            source.transform.position = worldPosition;
            source.PlayOneShot(clip, GainFor(cue) * SfxVolume);
        }

        /// <summary>Swaps the looping bed with a crossfade. Passing None fades to silence.</summary>
        public void PlayAmbience(AudioCue cue)
        {
            if (_ambience == cue) return;
            _ambience = cue;

            int incoming = 1 - _activeBed;
            var clip = cue == AudioCue.None ? null : ClipFor(cue);

            _ambienceSources[incoming].clip = clip;
            _ambienceSources[incoming].volume = 0f;
            if (clip != null) _ambienceSources[incoming].Play();

            _activeBed = incoming;
            _crossfade = 0f;
        }

        /// <summary>Driven by AudioServiceDriver on unscaled time - the bed keeps running while paused.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (_crossfade >= 1f) return;

            _crossfade = Mathf.Min(1f, _crossfade + unscaledDeltaTime / AmbienceCrossfadeSeconds);
            ApplyBedVolumes();

            if (_crossfade >= 1f)
            {
                var outgoing = _ambienceSources[1 - _activeBed];
                outgoing.Stop();
                outgoing.clip = null;
            }
        }

        public void ApplySettings()
        {
            ApplyBedVolumes();
        }

        /// <summary>The cue currently under everything. Exposed for the dev console and tests.</summary>
        public AudioCue CurrentAmbience { get { return _ambience; } }

        float SfxVolume
        {
            get { return _settings != null ? _settings.Current.sfxVolume : 1f; }
        }

        void ApplyBedVolumes()
        {
            // Equal power, so the sum of two room tones mid-swap is not louder than either.
            float incoming = Mathf.Sqrt(_crossfade);
            float outgoing = Mathf.Sqrt(1f - _crossfade);

            var active = _ambienceSources[_activeBed];
            var previous = _ambienceSources[1 - _activeBed];

            // The bed is a sound effect, not music: a player who turns music off must not
            // lose the layer this game's horror is actually carried by (GDD 19.1).
            active.volume = incoming * SfxVolume * GainFor(_ambience);
            if (previous.clip != null) previous.volume = outgoing * SfxVolume * GainFor(CueOf(previous.clip));
        }

        AudioCue CueOf(AudioClip clip)
        {
            foreach (var pair in _clips)
                if (ReferenceEquals(pair.Value, clip)) return pair.Key;
            return AudioCue.None;
        }

        // ---- resolution ------------------------------------------------------

        AudioClip ClipFor(AudioCue cue)
        {
            AudioClip clip;
            if (_clips.TryGetValue(cue, out clip)) return clip;

            clip = Resources.Load<AudioClip>(ClipFolder + cue);
            if (clip == null) clip = Synthesise(cue);

            _clips[cue] = clip;
            _gains[cue] = MeasureGain(cue, clip);
            return clip;
        }

        float GainFor(AudioCue cue)
        {
            if (cue == AudioCue.None) return 0f;

            float gain;
            if (_gains.TryGetValue(cue, out gain)) return gain;

            ClipFor(cue);
            return _gains.TryGetValue(cue, out gain) ? gain : 1f;
        }

        /// <summary>
        /// Measures a clip once and returns the playback gain that puts it on its class target.
        /// A clip that cannot be read - streaming, or compressed in memory - keeps unity gain
        /// rather than guessing, and the import settings in Editor/AudioImportSettings.cs are
        /// what keep that from happening to a shipped clip.
        /// </summary>
        static float MeasureGain(AudioCue cue, AudioClip clip)
        {
            if (clip == null) return 0f;

            float rms = MeasureRms(clip);
            if (rms <= 1e-6f) return 1f;

            float targetLinear = Mathf.Pow(10f, TargetLevelDb(cue) / 20f);

            // Clamped so a pathologically quiet file cannot be amplified into noise, and a
            // hot one is still brought down all the way.
            return Mathf.Clamp(targetLinear / rms, 0.02f, 8f);
        }

        static float MeasureRms(AudioClip clip)
        {
            if (clip.loadType == AudioClipLoadType.Streaming) return 0f;

            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0)) return 0f;

            double sum = 0.0;
            for (int i = 0; i < samples.Length; i++) sum += (double)samples[i] * samples[i];

            return Mathf.Sqrt((float)(sum / Mathf.Max(1, samples.Length)));
        }

        // ---- placeholders ----------------------------------------------------

        /// <summary>
        /// Builds a stand-in for a cue that has no file yet. Deliberately plain: a tone, a
        /// filtered noise burst or a thud. The shipped set comes from Tools/GenerateAudio.py;
        /// this only runs if that output is missing, and its job is to make the omission
        /// audible rather than silent.
        /// </summary>
        static AudioClip Synthesise(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.AnomalyStart:  return Sweep("cue_anomaly", 0.9f, 320f, 90f, 0.30f);
                case AudioCue.Interphone:    return Tone("cue_interphone", 0.7f, 660f, 0.22f, 6f);
                case AudioCue.PhoneRing:     return Tone("cue_phone", 1.1f, 480f, 0.22f, 9f);
                case AudioCue.InterphoneSelfEcho: return Noise("cue_self_echo", 1.6f, 0.10f, 0.35f);
                case AudioCue.Footsteps:     return Thud("cue_step", 0.22f, 120f, 0.16f);
                // Quieter, lower and longer than a step in the room: the same event heard
                // through a floor.
                case AudioCue.FootstepsDistant: return Thud("cue_step_far", 0.55f, 70f, 0.07f);
                case AudioCue.DoorOpen:      return Noise("cue_door", 0.45f, 0.14f, 0.9f);
                case AudioCue.EvidenceTaken: return Tone("cue_evidence", 0.16f, 880f, 0.14f, 0f);
                case AudioCue.ReportFiled:   return Tone("cue_report_ok", 0.14f, 990f, 0.13f, 0f);
                case AudioCue.ReportRejected:return Tone("cue_report_no", 0.20f, 220f, 0.15f, 0f);
                case AudioCue.FireAlarm:     return Tone("cue_fire", 1.4f, 750f, 0.26f, 4f);

                case AudioCue.OfficeDoorKnock:  return Thud("cue_knock", 0.55f, 78f, 0.34f);
                case AudioCue.OfficeDoorHandle: return Noise("cue_handle", 0.9f, 0.11f, 1.4f);
                case AudioCue.OfficeDoorForced: return Sweep("cue_forced", 1.3f, 220f, 46f, 0.38f);
                case AudioCue.PowerWarning:     return Tone("cue_power_low", 0.9f, 340f, 0.18f, 5f);
                case AudioCue.BreakerReset:     return Thud("cue_breaker", 0.35f, 160f, 0.24f);

                // Quiet on purpose. Room tone is meant to be noticed when it changes, not
                // while it plays.
                case AudioCue.RoomToneOffice:   return Noise("tone_office", 3.0f, 0.030f, 0f);
                case AudioCue.RoomToneCorridor: return Noise("tone_corridor", 3.0f, 0.022f, 0f);
                case AudioCue.RoomToneBasement: return Noise("tone_basement", 3.0f, 0.045f, 0f);
                case AudioCue.RoomToneOutside:  return Noise("tone_outside", 3.0f, 0.050f, 0f);
                default: return null;
            }
        }

        static AudioClip Build(string name, float seconds, Func<float, float, float> sample)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(sample(t, t / seconds), -1f, 1f);
            }

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A steady tone, optionally pulsing, fading out over its length.</summary>
        static AudioClip Tone(string name, float seconds, float hz, float gain, float pulseHz)
        {
            return Build(name, seconds, (t, through) =>
            {
                float envelope = (1f - through) * gain;
                if (pulseHz > 0f) envelope *= 0.5f + 0.5f * Mathf.Sin(t * pulseHz * Mathf.PI * 2f);
                return Mathf.Sin(t * hz * Mathf.PI * 2f) * envelope;
            });
        }

        /// <summary>A tone falling from one pitch to another - the anomaly sting.</summary>
        static AudioClip Sweep(string name, float seconds, float fromHz, float toHz, float gain)
        {
            float phase = 0f;
            return Build(name, seconds, (t, through) =>
            {
                float hz = Mathf.Lerp(fromHz, toHz, through);
                phase += hz / SampleRate * Mathf.PI * 2f;
                return Mathf.Sin(phase) * (1f - through) * gain;
            });
        }

        /// <summary>Filtered noise. Decay 0 loops flat, which is what room tone wants.</summary>
        static AudioClip Noise(string name, float seconds, float gain, float decay)
        {
            var random = new System.Random(name.GetHashCode());
            float smoothed = 0f;

            return Build(name, seconds, (t, through) =>
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                smoothed = Mathf.Lerp(smoothed, white, 0.08f);   // crude low pass
                float envelope = decay > 0f ? Mathf.Pow(1f - through, decay * 3f) : 1f;
                return smoothed * gain * envelope;
            });
        }

        /// <summary>A short low knock. Footsteps and doors.</summary>
        static AudioClip Thud(string name, float seconds, float hz, float gain)
        {
            return Build(name, seconds, (t, through) =>
            {
                float envelope = Mathf.Pow(1f - through, 4f) * gain;
                return Mathf.Sin(t * hz * Mathf.PI * 2f) * envelope;
            });
        }
    }

    /// <summary>
    /// Ticks the ambience crossfade. AudioService is a plain class by design - it is built
    /// before any scene loads - so it borrows one component off its own GameObject rather
    /// than becoming a MonoBehaviour and dragging Unity's lifecycle into the service layer.
    /// </summary>
    public sealed class AudioServiceDriver : MonoBehaviour
    {
        AudioService _service;

        public void Bind(AudioService service) { _service = service; }

        void Update()
        {
            // Unscaled: the bed has to keep crossfading while the game is paused, or opening
            // the menu mid-swap leaves two room tones stacked until it closes.
            if (_service != null) _service.Tick(Time.unscaledDeltaTime);
        }
    }
}
