using System;
using System.Collections;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // What happens when a guardian touches the explorer, about two and a half seconds:
    //   a heavy stone thud, the helmet lamp flickers;
    //   stone climbs from the feet up while the explorer tugs at the pickaxe arm and freezes in fright (the Petrify clip);
    //   the lamp is the last thing to go out, and a little sand trickles from the stone shell;
    //   then the "try again" panel (GameHud.ShowDefeat).
    public sealed class DefeatSequence : MonoBehaviour
    {
        private const string ThudPath = "Audio/guardian_step_1";
        private const float ThudPitch = 0.72f;
        private const float LampFlickerEnd = 0.7f;
        private const float StoneStart = 0.12f;
        private const float StoneEnd = 1.55f;
        private const float LampOutStart = 1.6f;
        private const float LampOut = 1.85f;
        private const float SandAt = 1.75f;
        private const float PanelAt = 2.6f;
        // Buried: the block slams shut over the explorer, sand spills from it, and the panel follows sooner.
        private const float BuriedPanelAt = 1.4f;

        private AudioSource source;
        private AudioClip thud;
        private Coroutine running;

        public void Play(CharacterView explorer, HeadLamp lamp, bool buried, Action finished)
        {
            Stop();
            running = StartCoroutine(buried ? RunBuried(explorer, finished) : Run(explorer, lamp, finished));
        }

        public void Stop()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            thud = Resources.Load<AudioClip>(ThudPath);
        }

        private IEnumerator Run(CharacterView explorer, HeadLamp lamp, Action finished)
        {
            if (thud != null)
            {
                source.pitch = ThudPitch;
                source.PlayOneShot(thud, GameSettings.Sound);
            }

            GameSettings.Vibrate();
            explorer.SetPose(CharacterPose.Petrify);
            bool sand = false;
            float seed = UnityEngine.Random.value * 100f;
            for (float time = 0f; time < PanelAt; time += Time.deltaTime)
            {
                float stone = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StoneStart, StoneEnd, time));
                explorer.SetStoneLevel(stone);

                if (lamp != null)
                {
                    lamp.SetLevel(LampLevel(time, seed));
                }

                if (!sand && time >= SandAt)
                {
                    sand = true;
                    SandBurst.Spawn(explorer.transform.parent, explorer.transform.position, explorer.Height);
                }

                yield return null;
            }

            running = null;
            finished?.Invoke();
        }

        private IEnumerator RunBuried(CharacterView explorer, Action finished)
        {
            if (thud != null)
            {
                source.pitch = ThudPitch * 0.85f;
                source.PlayOneShot(thud, GameSettings.Sound);
            }

            GameSettings.Vibrate();
            // The explorer is inside the closed block now; only the sand shows where.
            SandBurst.Spawn(explorer.transform.parent, explorer.transform.position, explorer.Height);
            explorer.SetVisible(false);
            for (float time = 0f; time < BuriedPanelAt; time += Time.deltaTime)
            {
                yield return null;
            }

            running = null;
            finished?.Invoke();
        }

        // Flickers from the blow, burns steadily while the stone climbs, sputters and goes out last.
        private static float LampLevel(float time, float seed)
        {
            if (time < LampFlickerEnd)
            {
                return Mathf.PerlinNoise(seed, time * 22f) > 0.42f ? 1f : 0.15f;
            }

            if (time < LampOutStart)
            {
                return 1f;
            }

            if (time < LampOut)
            {
                float fade = 1f - Mathf.InverseLerp(LampOutStart, LampOut, time);
                return Mathf.PerlinNoise(seed + 7f, time * 30f) > 0.5f ? fade : fade * 0.2f;
            }

            return 0f;
        }
    }
}
