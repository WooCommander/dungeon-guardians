using System.Collections.Generic;
using DungeonGuardians.Core;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Generates and plays procedural sound effects (SFX) for gameplay actions:
    // pickaxe strike, gold collection chime, hole restoration, pressure plates, gate sliding, and victory fanfare.
    // Audio volume is scaled by GameSettings.Sound.
    public sealed class SoundEffectPlayer : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const int SourcePoolSize = 8;
        private const float PanDistance = 14f;
        private const float MaxPan = 0.65f;

        private readonly List<AudioSource> sourcePool = new List<AudioSource>();
        private int nextSourceIndex;

        private AudioClip digClip;
        private AudioClip goldClip;
        private AudioClip exitOpenClip;
        private AudioClip holeRestoreClip;
        private AudioClip platePressClip;
        private AudioClip plateReleaseClip;
        private AudioClip gateSlideClip;
        private AudioClip fragileFloorClip;
        private AudioClip victoryClip;

        private DungeonSimulation currentSimulation;

        private void Awake()
        {
            for (int i = 0; i < SourcePoolSize; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                sourcePool.Add(source);
            }

            digClip = GenerateDigClip();
            goldClip = GenerateGoldClip();
            exitOpenClip = GenerateExitOpenClip();
            holeRestoreClip = GenerateHoleRestoreClip();
            platePressClip = GeneratePlateClip(true);
            plateReleaseClip = GeneratePlateClip(false);
            gateSlideClip = GenerateGateSlideClip();
            fragileFloorClip = GenerateFragileFloorClip();
            victoryClip = GenerateVictoryClip();
        }

        public void BindSimulation(DungeonSimulation simulation)
        {
            if (currentSimulation != null)
            {
                currentSimulation.DigStarted -= OnDig;
                currentSimulation.GoldCollected -= OnGold;
                currentSimulation.ExitOpened -= OnExitOpened;
                currentSimulation.HoleRestored -= OnHoleRestored;
                currentSimulation.PlateStateChanged -= OnPlateChanged;
                currentSimulation.GatesStateChanged -= OnGatesChanged;
                currentSimulation.FragileFloorBroken -= OnFragileFloorBroken;
                currentSimulation.LevelWon -= OnLevelWon;
            }

            currentSimulation = simulation;
            if (currentSimulation != null)
            {
                currentSimulation.DigStarted += OnDig;
                currentSimulation.GoldCollected += OnGold;
                currentSimulation.ExitOpened += OnExitOpened;
                currentSimulation.HoleRestored += OnHoleRestored;
                currentSimulation.PlateStateChanged += OnPlateChanged;
                currentSimulation.GatesStateChanged += OnGatesChanged;
                currentSimulation.FragileFloorBroken += OnFragileFloorBroken;
                currentSimulation.LevelWon += OnLevelWon;
            }
        }

        private void OnDestroy()
        {
            BindSimulation(null);
        }

        private void OnDig(GridPoint point, int direction)
        {
            float pan = CalculatePan(point);
            float pitch = Random.Range(0.93f, 1.07f);
            PlayClip(digClip, 0.9f, pan, pitch);
        }

        private void OnGold(GridPoint point)
        {
            float pan = CalculatePan(point) * 0.5f;
            float pitch = Random.Range(0.97f, 1.03f);
            PlayClip(goldClip, 1.0f, pan, pitch);
        }

        private void OnExitOpened()
        {
            PlayClip(exitOpenClip, 0.95f, 0f, 1f);
        }

        private void OnHoleRestored(GridPoint point)
        {
            float pan = CalculatePan(point);
            PlayClip(holeRestoreClip, 0.75f, pan, Random.Range(0.95f, 1.05f));
        }

        private void OnPlateChanged(GridPoint point, bool pressed)
        {
            float pan = CalculatePan(point);
            PlayClip(pressed ? platePressClip : plateReleaseClip, 0.85f, pan, 1f);
        }

        private void OnGatesChanged(bool open)
        {
            PlayClip(gateSlideClip, 0.8f, 0f, open ? 1.0f : 0.9f);
        }

        private void OnFragileFloorBroken(GridPoint point)
        {
            float pan = CalculatePan(point);
            PlayClip(fragileFloorClip, 0.9f, pan, Random.Range(0.94f, 1.06f));
        }

        private void OnLevelWon()
        {
            PlayClip(victoryClip, 1.0f, 0f, 1f);
        }

        private float CalculatePan(GridPoint point)
        {
            if (currentSimulation == null)
            {
                return 0f;
            }

            float offset = point.x - currentSimulation.State.PlayerPosition.x;
            return Mathf.Clamp(offset / PanDistance, -MaxPan, MaxPan);
        }

        private void PlayClip(AudioClip clip, float volumeScale, float pan, float pitch)
        {
            if (clip == null || GameSettings.Sound <= 0.001f)
            {
                return;
            }

            AudioSource source = sourcePool[nextSourceIndex];
            nextSourceIndex = (nextSourceIndex + 1) % sourcePool.Count;

            source.panStereo = pan;
            source.pitch = pitch;
            source.PlayOneShot(clip, volumeScale * GameSettings.Sound);
        }

        // --- Procedural DSP Audio Synthesis ---

        private static AudioClip GenerateDigClip()
        {
            float duration = 0.16f;
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // 1. Pickaxe metallic strike: frequency sweeps down from 1400Hz to 500Hz
                float freq = Mathf.Lerp(1400f, 500f, t / duration);
                float strike = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 60f);

                // 2. Gravel and sand crunch burst
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 30f);

                // 3. Low stone impact thump
                float thump = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 40f);

                samples[i] = Mathf.Clamp(strike * 0.45f + noise * 0.35f + thump * 0.4f, -1f, 1f);
            }

            var clip = AudioClip.Create("SFX_Dig", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateGoldClip()
        {
            float duration = 0.42f;
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];

            float f1 = 1975.5f; // B6 bell chime
            float f2 = 2960.0f; // F#7 overtone
            float f3 = 3951.0f; // B7 sparkle

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                float shimmer = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * 20f * t);
                float tone1 = Mathf.Sin(2f * Mathf.PI * f1 * t) * Mathf.Exp(-t * 7.5f);
                float tone2 = Mathf.Sin(2f * Mathf.PI * f2 * t) * Mathf.Exp(-t * 9.5f);
                float tone3 = Mathf.Sin(2f * Mathf.PI * f3 * t) * Mathf.Exp(-t * 14f);

                samples[i] = Mathf.Clamp((tone1 * 0.55f + tone2 * 0.32f + tone3 * 0.2f) * shimmer, -1f, 1f);
            }

            var clip = AudioClip.Create("SFX_Gold", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateExitOpenClip()
        {
            float duration = 0.65f;
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];

            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6 arpeggio
            float noteDelay = 0.08f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sum = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteT = t - n * noteDelay;
                    if (noteT > 0f)
                    {
                        float env = Mathf.Exp(-noteT * 7f);
                        float wave = Mathf.Sin(2f * Mathf.PI * notes[n] * noteT)
                                   + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * noteT);
                        sum += wave * env * 0.35f;
                    }
                }

                samples[i] = Mathf.Clamp(sum, -1f, 1f);
            }

            var clip = AudioClip.Create("SFX_ExitOpen", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateHoleRestoreClip()
        {
            float duration = 0.26f;
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // Sand and stone sliding back together
                float slideFreq = Mathf.Lerp(380f, 90f, t / duration);
                float slide = Mathf.Sin(2f * Mathf.PI * slideFreq * t) * (1f - t / duration);
                float crunch = (Random.value * 2f - 1f) * Mathf.Sin(t / duration * Mathf.PI) * 0.4f;
                float lockThump = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 22f);

                samples[i] = Mathf.Clamp(slide * 0.35f + crunch * 0.35f + lockThump * 0.45f, -1f, 1f);
            }

            var clip = AudioClip.Create("SFX_HoleRestore", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GeneratePlateClip(bool press)
        {
            float duration = 0.12f;
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];

            float pitch = press ? 850f : 680f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float click = Mathf.Sin(2f * Mathf.PI * pitch * t) * Mathf.Exp(-t * 70f);
                float clack = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 35f);
                samples[i] = Mathf.Clamp(click * 0.6f + clack * 0.45f, -1f, 1f);
            }

            var clip = AudioClip.Create(press ? "SFX_PlatePress" : "SFX_PlateRelease", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateGateSlideClip()
        {
            float duration = 0.32f;
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float rumble = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.4f
                             + Mathf.Sin(2f * Mathf.PI * 175f * t) * 0.3f
                             + (Random.value * 2f - 1f) * 0.3f;
                samples[i] = Mathf.Clamp(rumble * env * 0.7f, -1f, 1f);
            }

            var clip = AudioClip.Create("SFX_GateSlide", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateFragileFloorClip()
        {
            float duration = 0.20f;
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float woodResonance = Mathf.Sin(2f * Mathf.PI * 520f * t) * Mathf.Exp(-t * 35f);
                float splinter = (Random.value * 2f - 1f) * Mathf.Exp(-t * 25f);
                samples[i] = Mathf.Clamp(woodResonance * 0.45f + splinter * 0.55f, -1f, 1f);
            }

            var clip = AudioClip.Create("SFX_FragileFloor", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateVictoryClip()
        {
            float duration = 1.05f;
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];

            // Harmonic fanfare chords: Root C5, Major 3rd E5, 5th G5, Octave C6
            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Mathf.Clamp01(t / 0.06f);
                float decay = Mathf.Exp(-t * 2.8f);
                float env = attack * decay;

                float chord = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float vibrato = 1f + 0.008f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                    chord += Mathf.Sin(2f * Mathf.PI * (freqs[f] * vibrato) * t) * (0.35f / (f + 1));
                    // Add subtle warm octave overtone
                    chord += Mathf.Sin(2f * Mathf.PI * (freqs[f] * 2f) * t) * 0.08f;
                }

                samples[i] = Mathf.Clamp(chord * env * 1.2f, -1f, 1f);
            }

            var clip = AudioClip.Create("SFX_Victory", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
