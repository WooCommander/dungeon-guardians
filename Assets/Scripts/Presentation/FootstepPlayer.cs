using System.Collections.Generic;
using DungeonGuardians.Core;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Heavy stone footsteps of the guardians (Resources/Audio/guardian_step_*, cut by tools/cut_footsteps.py): one
    // stomp per cell a guardian moves. They are loud when a guardian is close to the explorer and fade to a faint
    // rumble across the level, and sound from the guardian's side, so the player hears one coming before seeing it.
    // Scaled by the "Sounds" setting.
    public sealed class FootstepPlayer : MonoBehaviour
    {
        private const string StepPath = "Audio/guardian_step_";
        private const int StepVariants = 3;
        // Distance in cells: full volume up to Near, quietest from Far on.
        private const float Near = 2f;
        private const float Far = 16f;
        private const float FarVolume = 0.1f;
        private const float LadderVolume = 0.6f;
        // Cells of horizontal offset for the furthest pan.
        private const float PanDistance = 12f;
        private const float MaxPan = 0.6f;
        private const float MinPitch = 0.92f;
        private const float MaxPitch = 1.06f;

        private readonly List<AudioClip> steps = new List<AudioClip>();
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private readonly List<GridPoint> lastPositions = new List<GridPoint>();
        private int lastVariant = -1;

        private void Awake()
        {
            for (int i = 1; i <= StepVariants; i++)
            {
                var clip = Resources.Load<AudioClip>(StepPath + i);
                if (clip != null)
                {
                    steps.Add(clip);
                }
            }
        }

        // A new level: positions start over, so a guardian's first placement is not heard as a step.
        public void BeginLevel()
        {
            lastPositions.Clear();
        }

        public void Track(DungeonSimulation simulation)
        {
            if (simulation == null || steps.Count == 0)
            {
                return;
            }

            RuntimeLevelState state = simulation.State;
            for (int i = 0; i < state.Guardians.Count; i++)
            {
                GridPoint position = state.Guardians[i].Position;
                if (i >= lastPositions.Count)
                {
                    lastPositions.Add(position);
                    continue;
                }

                GridPoint last = lastPositions[i];
                lastPositions[i] = position;
                int moved = Mathf.Abs(position.x - last.x) + Mathf.Abs(position.y - last.y);
                // A respawn jumps across the map; a guardian still falling has no ground to stomp on.
                if (moved != 1 || !simulation.HasSupport(position))
                {
                    continue;
                }

                Play(i, position, state.PlayerPosition, state.Tiles[position.x, position.y] == TileType.Ladder);
            }
        }

        private void Play(int guardian, GridPoint position, GridPoint player, bool onLadder)
        {
            float distance = Vector2.Distance(new Vector2(position.x, position.y), new Vector2(player.x, player.y));
            float closeness = 1f - Mathf.InverseLerp(Near, Far, distance);
            float volume = Mathf.Lerp(FarVolume, 1f, closeness * closeness) * GameSettings.Sound;
            if (onLadder)
            {
                volume *= LadderVolume;
            }

            AudioSource source = SourceFor(guardian);
            source.panStereo = Mathf.Clamp((position.x - player.x) / PanDistance, -MaxPan, MaxPan);
            source.pitch = Random.Range(MinPitch, MaxPitch);
            source.PlayOneShot(NextStep(), volume);
        }

        // Never the same stomp twice in a row.
        private AudioClip NextStep()
        {
            int variant = Random.Range(0, steps.Count);
            if (steps.Count > 1 && variant == lastVariant)
            {
                variant = (variant + 1) % steps.Count;
            }

            lastVariant = variant;
            return steps[variant];
        }

        // One source per guardian, so each keeps its own side in the stereo picture.
        private AudioSource SourceFor(int guardian)
        {
            while (sources.Count <= guardian)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                sources.Add(source);
            }

            return sources[guardian];
        }
    }
}
