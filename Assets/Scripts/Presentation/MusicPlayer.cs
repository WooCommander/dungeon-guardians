using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // The cavern music (Resources/Audio/music_cavern), looping from start-up. On the start screen it plays fuller;
    // during a level it sinks into the background, so it builds tension without covering the game's own sounds.
    // The "Music" setting scales both levels; changes fade in over a moment.
    public sealed class MusicPlayer : MonoBehaviour
    {
        private const string TrackPath = "Audio/music_cavern";
        private const float MenuLevel = 0.7f;
        private const float GameLevel = 0.3f;
        private const float PausedLevel = 0.18f;
        private const float FadeSpeed = 0.6f;

        public enum Mood
        {
            Menu,
            Game,
            Paused
        }

        private AudioSource source;
        private Mood mood = Mood.Menu;

        public void SetMood(Mood value)
        {
            mood = value;
        }

        private void Awake()
        {
            var clip = Resources.Load<AudioClip>(TrackPath);
            if (clip == null)
            {
                Debug.LogWarning($"Music not found at Resources/{TrackPath}.");
                enabled = false;
                return;
            }

            // Nothing is heard without a listener; a camera created by GameBootstrap has none.
            if (FindObjectOfType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }

            source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = TargetVolume();
            source.Play();
        }

        private void Update()
        {
            source.volume = Mathf.MoveTowards(source.volume, TargetVolume(), FadeSpeed * Time.unscaledDeltaTime);
        }

        private float TargetVolume()
        {
            float level = mood == Mood.Menu ? MenuLevel : mood == Mood.Game ? GameLevel : PausedLevel;
            return level * GameSettings.Music;
        }
    }
}
