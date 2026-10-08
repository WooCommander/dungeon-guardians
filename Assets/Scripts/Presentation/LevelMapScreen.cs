using System;
using System.Collections;
using System.Collections.Generic;
using DungeonGuardians.Core;
using DungeonGuardians.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    public sealed class LevelMapScreen : MonoBehaviour
    {
        private static readonly Vector2 CanvasReference = new Vector2(1672f, 941f);
        private static readonly Color GoldColor = new Color(1f, 0.84f, 0.42f);
        private static readonly Color CyanGlow = new Color(0.35f, 0.95f, 1f);
        private static readonly Color LockedColor = new Color(0.25f, 0.25f, 0.28f, 0.85f);
        private static readonly Color CompletedColor = new Color(0.95f, 0.78f, 0.32f);
        private static readonly Color PathActiveColor = new Color(1f, 0.82f, 0.35f, 0.75f);
        private static readonly Color PathLockedColor = new Color(0.2f, 0.22f, 0.26f, 0.45f);

        // Map layout nodes (X, Y in 1672x941 space)
        private static readonly Vector2[] NodePositions =
        {
            new Vector2(410f, 265f),  // Level 1
            new Vector2(660f, 290f),  // Level 2
            new Vector2(900f, 310f),  // Level 3
            new Vector2(980f, 430f),  // Level 4
            new Vector2(740f, 460f),  // Level 5
            new Vector2(490f, 440f),  // Level 6
            new Vector2(420f, 600f),  // Level 7
            new Vector2(690f, 630f),  // Level 8
            new Vector2(950f, 640f),  // Level 9
            new Vector2(930f, 760f),  // Level 10
            new Vector2(700f, 780f),  // Level 11
            new Vector2(450f, 760f),  // Level 12
            new Vector2(510f, 870f),  // Level 13
            new Vector2(710f, 880f),  // Level 14
            new Vector2(920f, 875f),  // Level 15
            new Vector2(1060f, 875f), // Level 16
            new Vector2(1180f, 875f), // Level 17
            new Vector2(1300f, 875f), // Level 18
        };

        private Transform picture;
        private PlayerProgress progress;
        private LevelCatalog catalog;
        private int selectedIndex;
        private int lastSeenUnlockedIndex = -1;
        private readonly List<Image> nodeImages = new List<Image>();
        private readonly List<Text> nodeLabels = new List<Text>();
        private readonly List<Text> nodeStarLabels = new List<Text>();
        private readonly List<Image> pathLines = new List<Image>();

        private Image sparkImage;
        private float sparkProgress;
        private const float SparkSpeed = 0.55f;

        // Unlock visual effects
        private Image burstRing;
        private readonly List<Image> burstSparks = new List<Image>();
        private static AudioClip unlockSfxClip;

        private Text progressText;
        private Image progressBarFill;
        private Text percentText;
        private Text totalStarsText;

        // Right side info card
        private Text cardChapterText;
        private Text cardLevelTitle;
        private Text cardBestTimeText;
        private Text cardStarsText;
        private Text cardStatusText;

        // Bottom action button
        private Text playButtonText;

        public event Action<int> LevelSelected;
        public event Action BackRequested;

        public static LevelMapScreen Create(Transform parent)
        {
            var root = new GameObject("LevelMapScreen", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            MenuStyle.Stretch((RectTransform)root.transform);
            var screen = root.AddComponent<LevelMapScreen>();
            screen.Build();
            return screen;
        }

        public void Open(PlayerProgress currentProgress, LevelCatalog levelCatalog)
        {
            progress = currentProgress;
            catalog = levelCatalog;

            int highestUnlocked = Mathf.Clamp(progress.highestUnlockedIndex, 0, catalog.Levels.Count - 1);
            selectedIndex = Mathf.Clamp(progress.lastSelectedLevelIndex, 0, highestUnlocked);
            sparkProgress = 0f;

            Refresh();
            gameObject.SetActive(true);

            // Trigger unlock ceremony if newly unlocked
            if (lastSeenUnlockedIndex >= 0 && highestUnlocked > lastSeenUnlockedIndex)
            {
                StartCoroutine(AnimateUnlockSequence(highestUnlocked));
            }
            lastSeenUnlockedIndex = highestUnlocked;
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void Build()
        {
            // Background painted map
            Image backing = MenuStyle.CreatePicture(transform, "Backgrounds/map", AspectRatioFitter.AspectMode.EnvelopeParent);
            backing.raycastTarget = true;
            picture = MenuStyle.CreatePicture(transform, "Backgrounds/map", AspectRatioFitter.AspectMode.FitInParent).transform;

            BuildPathLines();
            BuildSpark();
            BuildUnlockVfx();
            BuildLevelNodes();
            BuildTopBar();
            BuildInfoCard();
            BuildBottomPlayButton();
        }

        private void BuildPathLines()
        {
            for (int i = 0; i < NodePositions.Length - 1; i++)
            {
                Vector2 start = NodePositions[i];
                Vector2 end = NodePositions[i + 1];
                Vector2 mid = (start + end) * 0.5f;
                float dist = Vector2.Distance(start, end);
                float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;

                var lineObj = new GameObject($"PathLine_{i + 1}_{i + 2}").AddComponent<Image>();
                lineObj.transform.SetParent(picture, false);
                lineObj.color = PathLockedColor;
                lineObj.raycastTarget = false;

                RectTransform rect = lineObj.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(mid.x / CanvasReference.x, 1f - mid.y / CanvasReference.y);
                rect.sizeDelta = new Vector2(dist, 6f);
                rect.localEulerAngles = new Vector3(0f, 0f, -angle);

                pathLines.Add(lineObj);
            }
        }

        private void BuildSpark()
        {
            sparkImage = new GameObject("PathSpark").AddComponent<Image>();
            sparkImage.transform.SetParent(picture, false);
            sparkImage.sprite = ExitGlow.GetHaloSprite();
            sparkImage.color = new Color(1f, 0.95f, 0.55f, 0.95f);
            sparkImage.raycastTarget = false;
            sparkImage.rectTransform.sizeDelta = new Vector2(40f, 40f);
        }

        private void BuildUnlockVfx()
        {
            burstRing = new GameObject("BurstRing").AddComponent<Image>();
            burstRing.transform.SetParent(picture, false);
            burstRing.sprite = ExitGlow.GetHaloSprite();
            burstRing.color = Color.clear;
            burstRing.raycastTarget = false;
            burstRing.rectTransform.sizeDelta = new Vector2(90f, 90f);

            for (int i = 0; i < 12; i++)
            {
                var spark = new GameObject($"BurstSpark_{i}").AddComponent<Image>();
                spark.transform.SetParent(picture, false);
                spark.sprite = ExitGlow.GetHaloSprite();
                spark.color = Color.clear;
                spark.raycastTarget = false;
                spark.rectTransform.sizeDelta = new Vector2(24f, 24f);
                burstSparks.Add(spark);
            }
        }

        private void BuildTopBar()
        {
            // Back button in top-left
            var backBtn = MenuStyle.AddPictureButton(picture, "settings_back", new Rect(40f, 35f, 80f, 65f), "<", () => BackRequested?.Invoke());
            var backLabel = backBtn.GetComponentInChildren<Text>();
            if (backLabel != null)
            {
                backLabel.text = "←";
                backLabel.fontSize = 44;
            }

            // Title: ПУТЬ ИСКАТЕЛЯ
            var title = MenuStyle.AddLabel(picture, "ПУТЬ ИСКАТЕЛЯ", 44);
            title.fontStyle = FontStyle.Bold;
            title.color = GoldColor;
            title.alignment = TextAnchor.MiddleCenter;
            MenuStyle.PlaceOnPicture(title.rectTransform, new Rect(400f, 25f, 600f, 55f));

            // Progress bar and text
            progressText = MenuStyle.AddLabel(picture, "Пройдено 0 из 18", 22);
            progressText.color = new Color(0.95f, 0.95f, 0.95f);
            progressText.alignment = TextAnchor.MiddleRight;
            MenuStyle.PlaceOnPicture(progressText.rectTransform, new Rect(360f, 85f, 240f, 30f));

            // Progress bar track
            var barTrack = new GameObject("ProgressTrack").AddComponent<Image>();
            barTrack.transform.SetParent(picture, false);
            barTrack.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);
            MenuStyle.PlaceOnPicture(barTrack.rectTransform, new Rect(615f, 92f, 260f, 16f));

            var fillObj = new GameObject("Fill").AddComponent<Image>();
            fillObj.transform.SetParent(barTrack.transform, false);
            fillObj.color = GoldColor;
            fillObj.type = Image.Type.Filled;
            fillObj.fillMethod = Image.FillMethod.Horizontal;
            fillObj.fillOrigin = (int)Image.OriginHorizontal.Left;
            MenuStyle.Stretch(fillObj.rectTransform);
            progressBarFill = fillObj;

            percentText = MenuStyle.AddLabel(picture, "0%", 20);
            percentText.color = GoldColor;
            percentText.fontStyle = FontStyle.Bold;
            percentText.alignment = TextAnchor.MiddleLeft;
            MenuStyle.PlaceOnPicture(percentText.rectTransform, new Rect(885f, 85f, 90f, 30f));

            totalStarsText = MenuStyle.AddLabel(picture, "★ 0", 24);
            totalStarsText.color = GoldColor;
            totalStarsText.fontStyle = FontStyle.Bold;
            totalStarsText.alignment = TextAnchor.MiddleRight;
            MenuStyle.PlaceOnPicture(totalStarsText.rectTransform, new Rect(1000f, 85f, 120f, 30f));
        }

        private void BuildLevelNodes()
        {
            for (int i = 0; i < NodePositions.Length; i++)
            {
                int index = i;
                Vector2 pos = NodePositions[i];
                float size = 68f;

                var nodeObj = new GameObject($"Node_{i + 1}").AddComponent<Image>();
                nodeObj.transform.SetParent(picture, false);
                nodeObj.color = new Color(0.16f, 0.18f, 0.22f, 0.95f);
                MenuStyle.PlaceOnPicture(nodeObj.rectTransform, new Rect(pos.x - size / 2f, pos.y - size / 2f, size, size));

                var numText = MenuStyle.AddLabel(nodeObj.transform, $"{i + 1}", 26);
                numText.fontStyle = FontStyle.Bold;
                numText.alignment = TextAnchor.MiddleCenter;
                MenuStyle.Stretch(numText.rectTransform);

                var starText = MenuStyle.AddLabel(nodeObj.transform, string.Empty, 14);
                starText.color = GoldColor;
                starText.alignment = TextAnchor.LowerCenter;
                MenuStyle.Stretch(starText.rectTransform);

                MenuStyle.MakeButton(nodeObj, () => SelectLevel(index));

                nodeImages.Add(nodeObj);
                nodeLabels.Add(numText);
                nodeStarLabels.Add(starText);
            }
        }

        private void BuildInfoCard()
        {
            var card = new GameObject("InfoCard").AddComponent<Image>();
            card.transform.SetParent(picture, false);
            card.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);
            MenuStyle.PlaceOnPicture(card.rectTransform, new Rect(1240f, 120f, 380f, 680f));

            cardChapterText = MenuStyle.AddLabel(card.transform, "ГЛАВА I", 26);
            cardChapterText.color = GoldColor;
            cardChapterText.fontStyle = FontStyle.Bold;
            cardChapterText.alignment = TextAnchor.UpperCenter;
            MenuStyle.PlaceOnPicture(cardChapterText.rectTransform, new Rect(20f, 30f, 340f, 40f));

            cardLevelTitle = MenuStyle.AddLabel(card.transform, "Первые залы", 32);
            cardLevelTitle.color = Color.white;
            cardLevelTitle.fontStyle = FontStyle.Bold;
            cardLevelTitle.alignment = TextAnchor.UpperCenter;
            MenuStyle.PlaceOnPicture(cardLevelTitle.rectTransform, new Rect(20f, 80f, 340f, 60f));

            // Illustration preview panel
            var previewPanel = new GameObject("Preview").AddComponent<Image>();
            previewPanel.transform.SetParent(card.transform, false);
            previewPanel.color = new Color(0.04f, 0.06f, 0.09f, 0.95f);
            MenuStyle.PlaceOnPicture(previewPanel.rectTransform, new Rect(30f, 150f, 320f, 260f));

            var previewText = MenuStyle.AddLabel(previewPanel.transform, "✦", 64);
            previewText.color = CyanGlow;
            previewText.alignment = TextAnchor.MiddleCenter;
            MenuStyle.Stretch(previewText.rectTransform);

            // Stats
            var statsHeader = MenuStyle.AddLabel(card.transform, "СТАТИСТИКА", 22);
            statsHeader.color = GoldColor;
            statsHeader.fontStyle = FontStyle.Bold;
            statsHeader.alignment = TextAnchor.MiddleCenter;
            MenuStyle.PlaceOnPicture(statsHeader.rectTransform, new Rect(20f, 430f, 340f, 30f));

            cardBestTimeText = MenuStyle.AddLabel(card.transform, "Лучшее время: —:—", 20);
            cardBestTimeText.color = new Color(0.9f, 0.9f, 0.95f);
            cardBestTimeText.alignment = TextAnchor.MiddleLeft;
            MenuStyle.PlaceOnPicture(cardBestTimeText.rectTransform, new Rect(40f, 475f, 300f, 30f));

            cardStarsText = MenuStyle.AddLabel(card.transform, "Звёзды: ☆☆☆", 20);
            cardStarsText.color = GoldColor;
            cardStarsText.alignment = TextAnchor.MiddleLeft;
            MenuStyle.PlaceOnPicture(cardStarsText.rectTransform, new Rect(40f, 515f, 300f, 30f));

            cardStatusText = MenuStyle.AddLabel(card.transform, "Статус: ДОСТУПЕН", 20);
            cardStatusText.color = CyanGlow;
            cardStatusText.fontStyle = FontStyle.Bold;
            cardStatusText.alignment = TextAnchor.MiddleLeft;
            MenuStyle.PlaceOnPicture(cardStatusText.rectTransform, new Rect(40f, 555f, 300f, 30f));
        }

        private void BuildBottomPlayButton()
        {
            var btnImage = new GameObject("PlayLevelButton").AddComponent<Image>();
            btnImage.transform.SetParent(picture, false);
            btnImage.color = new Color(0.95f, 0.72f, 0.22f, 0.95f);
            MenuStyle.PlaceOnPicture(btnImage.rectTransform, new Rect(450f, 845f, 620f, 75f));

            playButtonText = MenuStyle.AddLabel(btnImage.transform, "ПРОДОЛЖИТЬ • УРОВЕНЬ 1", 32);
            playButtonText.fontStyle = FontStyle.Bold;
            playButtonText.color = new Color(0.12f, 0.08f, 0.04f);
            playButtonText.alignment = TextAnchor.MiddleCenter;
            MenuStyle.Stretch(playButtonText.rectTransform);

            MenuStyle.MakeButton(btnImage, LaunchSelectedLevel);
        }

        private void SelectLevel(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.Levels.Count)
            {
                return;
            }

            if (!progress.IsUnlocked(index))
            {
                return;
            }

            selectedIndex = index;
            progress.lastSelectedLevelIndex = selectedIndex;
            Refresh();
        }

        private void LaunchSelectedLevel()
        {
            if (catalog == null || selectedIndex < 0 || selectedIndex >= catalog.Levels.Count)
            {
                return;
            }

            if (progress.IsUnlocked(selectedIndex))
            {
                LevelSelected?.Invoke(selectedIndex);
            }
        }

        private IEnumerator AnimateUnlockSequence(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= NodePositions.Length)
            {
                yield break;
            }

            Vector2 targetPos = NodePositions[nodeIndex];
            PlayUnlockSound();

            // Setup burst ring
            burstRing.rectTransform.anchorMin = burstRing.rectTransform.anchorMax =
                new Vector2(targetPos.x / CanvasReference.x, 1f - targetPos.y / CanvasReference.y);
            burstRing.rectTransform.anchoredPosition = Vector2.zero;

            // Setup radial sparks
            for (int i = 0; i < burstSparks.Count; i++)
            {
                burstSparks[i].rectTransform.anchorMin = burstSparks[i].rectTransform.anchorMax =
                    new Vector2(targetPos.x / CanvasReference.x, 1f - targetPos.y / CanvasReference.y);
                burstSparks[i].rectTransform.anchoredPosition = Vector2.zero;
            }

            float duration = 0.85f;
            float elapsed = 0f;
            Transform nodeTransform = nodeImages[nodeIndex].transform;
            Vector3 originalNodePos = nodeTransform.localPosition;
            Vector3 originalPicPos = picture.localPosition;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Expand ring with fading alpha
                float ringScale = Mathf.Lerp(0.2f, 3.2f, Mathf.Sqrt(t));
                float ringAlpha = Mathf.Sin(t * Mathf.PI);
                burstRing.color = new Color(1f, 0.88f, 0.45f, ringAlpha * 0.95f);
                burstRing.transform.localScale = Vector3.one * ringScale;

                // Move 12 radial sparks outwards with varied speeds
                for (int i = 0; i < burstSparks.Count; i++)
                {
                    float angle = (i * (360f / burstSparks.Count) + Mathf.Sin(i * 1.5f) * 15f) * Mathf.Deg2Rad;
                    float speedMultiplier = 0.8f + 0.4f * Mathf.Sin(i * 2.3f);
                    float sparkDist = Mathf.Lerp(0f, 110f * speedMultiplier, 1f - Mathf.Pow(1f - t, 2.5f));
                    Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * sparkDist;
                    burstSparks[i].rectTransform.anchoredPosition = offset;
                    burstSparks[i].color = new Color(1f, 0.96f, 0.65f, (1f - t) * 0.95f);
                    burstSparks[i].transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.1f, t);
                }

                // Node bounce & ancient mechanism shake
                float bounce = 1f + 0.4f * Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t);
                nodeTransform.localScale = Vector3.one * bounce;

                // Lock rattle / screen micro-shake in the first 0.28 seconds
                if (elapsed < 0.28f)
                {
                    float shakeIntensity = (1f - elapsed / 0.28f) * 6f;
                    float shakeX = Mathf.Sin(elapsed * 90f) * shakeIntensity;
                    float shakeY = Mathf.Cos(elapsed * 75f) * shakeIntensity * 0.7f;
                    nodeTransform.localPosition = originalNodePos + new Vector3(shakeX, shakeY, 0f);
                    picture.localPosition = originalPicPos + new Vector3(shakeX * 0.35f, shakeY * 0.35f, 0f);
                }
                else
                {
                    nodeTransform.localPosition = originalNodePos;
                    picture.localPosition = originalPicPos;
                }

                yield return null;
            }

            burstRing.color = Color.clear;
            for (int i = 0; i < burstSparks.Count; i++)
            {
                burstSparks[i].color = Color.clear;
            }
            nodeTransform.localScale = Vector3.one;
            nodeTransform.localPosition = originalNodePos;
            picture.localPosition = originalPicPos;
        }

        private static void PlayUnlockSound()
        {
            if (unlockSfxClip == null)
            {
                int sampleRate = 44100;
                float duration = 0.55f;
                int samplesCount = (int)(sampleRate * duration);
                float[] samples = new float[samplesCount];

                for (int i = 0; i < samplesCount; i++)
                {
                    float t = (float)i / sampleRate;

                    // 1. Initial sharp metal pick / tumbler click (1950Hz + 2800Hz)
                    float click1 = Mathf.Sin(2f * Mathf.PI * 1950f * t) * Mathf.Exp(-t * 110f);
                    click1 += Mathf.Sin(2f * Mathf.PI * 2800f * t) * Mathf.Exp(-t * 130f) * 0.6f;

                    // 2. Heavy stone/bronze latch release latch-spring thud at t = 0.055s (180Hz - 320Hz)
                    float latchThud = 0f;
                    if (t > 0.055f)
                    {
                        float dt = t - 0.055f;
                        latchThud = Mathf.Sin(2f * Mathf.PI * 220f * dt) * Mathf.Exp(-dt * 45f) * 0.75f;
                        latchThud += Mathf.Sin(2f * Mathf.PI * 960f * dt) * Mathf.Exp(-dt * 70f) * 0.5f;
                    }

                    // 3. Resonant crystal / golden chime chord (1568Hz G6 + 2349Hz D7 + 3136Hz G7)
                    float chime = 0f;
                    if (t > 0.02f)
                    {
                        float dt = t - 0.02f;
                        chime += Mathf.Sin(2f * Mathf.PI * 1568f * dt) * Mathf.Exp(-dt * 7.5f) * 0.45f;
                        chime += Mathf.Sin(2f * Mathf.PI * 2349f * dt) * Mathf.Exp(-dt * 11.0f) * 0.25f;
                        chime += Mathf.Sin(2f * Mathf.PI * 3136f * dt) * Mathf.Exp(-dt * 14.0f) * 0.15f;
                    }

                    samples[i] = Mathf.Clamp(click1 * 0.6f + latchThud + chime, -1f, 1f);
                }

                unlockSfxClip = AudioClip.Create("UnlockSFX", samplesCount, 1, sampleRate, false);
                unlockSfxClip.SetData(samples, 0);
            }

            if (GameSettings.Sound > 0.01f)
            {
                var audioObj = new GameObject("UnlockSoundTemp");
                var src = audioObj.AddComponent<AudioSource>();
                src.clip = unlockSfxClip;
                src.volume = GameSettings.Sound * 0.95f;
                src.pitch = UnityEngine.Random.Range(0.98f, 1.03f);
                src.Play();
                UnityEngine.Object.Destroy(audioObj, 0.7f);
            }
        }

        private void Update()
        {
            if (catalog == null || progress == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            float time = Time.unscaledTime;

            // Pulse currently selected node
            float pulse = 1f + 0.08f * Mathf.Sin(time * 5f);
            if (selectedIndex >= 0 && selectedIndex < nodeImages.Count)
            {
                nodeImages[selectedIndex].transform.localScale = Vector3.one * pulse;
            }

            // Animate spark running along the unlocked path
            int maxStep = Mathf.Clamp(progress.highestUnlockedIndex, 0, NodePositions.Length - 1);
            if (maxStep > 0 && sparkImage != null)
            {
                sparkProgress += Time.unscaledDeltaTime * SparkSpeed;
                if (sparkProgress > maxStep)
                {
                    sparkProgress = 0f;
                }

                int segIndex = Mathf.FloorToInt(sparkProgress);
                float segT = sparkProgress - segIndex;
                if (segIndex >= maxStep)
                {
                    segIndex = maxStep - 1;
                    segT = 1f;
                }

                Vector2 start = NodePositions[segIndex];
                Vector2 end = NodePositions[segIndex + 1];
                Vector2 currentPos = Vector2.Lerp(start, end, segT);

                sparkImage.gameObject.SetActive(true);
                sparkImage.rectTransform.anchorMin = sparkImage.rectTransform.anchorMax =
                    new Vector2(currentPos.x / CanvasReference.x, 1f - currentPos.y / CanvasReference.y);
                sparkImage.rectTransform.anchoredPosition = Vector2.zero;

                float sparkScale = 1f + 0.3f * Mathf.Sin(time * 12f);
                sparkImage.transform.localScale = Vector3.one * sparkScale;
            }
            else if (sparkImage != null)
            {
                sparkImage.gameObject.SetActive(false);
            }
        }

        private void Refresh()
        {
            if (catalog == null || progress == null)
            {
                return;
            }

            int totalLevels = catalog.Levels.Count;
            int completed = progress.GetCompletedCount();
            int totalStars = progress.GetTotalStars();
            float ratio = totalLevels > 0 ? (float)completed / totalLevels : 0f;

            progressText.text = $"Пройдено {completed} из {totalLevels}";
            progressBarFill.fillAmount = ratio;
            percentText.text = $"{Mathf.RoundToInt(ratio * 100f)}%";
            totalStarsText.text = $"★ {totalStars}";

            // Update path lines
            for (int i = 0; i < pathLines.Count; i++)
            {
                bool lineActive = progress.IsUnlocked(i + 1);
                pathLines[i].color = lineActive ? PathActiveColor : PathLockedColor;
            }

            // Update nodes
            for (int i = 0; i < nodeImages.Count; i++)
            {
                if (i >= totalLevels)
                {
                    nodeImages[i].gameObject.SetActive(false);
                    continue;
                }

                nodeImages[i].gameObject.SetActive(true);
                bool unlocked = progress.IsUnlocked(i);
                LevelRecord record = progress.GetOrCreateRecord(i, catalog.Levels[i].id);

                if (!unlocked)
                {
                    nodeImages[i].color = LockedColor;
                    nodeLabels[i].text = "🔒";
                    nodeLabels[i].color = new Color(0.6f, 0.6f, 0.6f);
                    nodeStarLabels[i].text = string.Empty;
                }
                else if (record.completed)
                {
                    nodeImages[i].color = i == selectedIndex ? CyanGlow : CompletedColor;
                    nodeLabels[i].text = $"{i + 1} ✓";
                    nodeLabels[i].color = new Color(0.12f, 0.08f, 0.04f);
                    nodeStarLabels[i].text = GetStarsString(record.stars);
                }
                else
                {
                    nodeImages[i].color = i == selectedIndex ? CyanGlow : new Color(0.25f, 0.55f, 0.65f);
                    nodeLabels[i].text = $"{i + 1}";
                    nodeLabels[i].color = Color.white;
                    nodeStarLabels[i].text = string.Empty;
                }
            }

            // Update Card
            LevelDefinition def = catalog.Levels[selectedIndex];
            LevelRecord selRecord = progress.GetOrCreateRecord(selectedIndex, def.id);

            cardChapterText.text = GetChapterName(selectedIndex);
            cardLevelTitle.text = $"{selectedIndex + 1:00}. {def.title}";

            if (selRecord.completed && selRecord.bestTimeSeconds > 0.01f)
            {
                int mins = (int)(selRecord.bestTimeSeconds / 60f);
                int secs = (int)(selRecord.bestTimeSeconds % 60f);
                cardBestTimeText.text = $"Лучшее время: <color=#FFE7B0>{mins:00}:{secs:00}</color>";
            }
            else
            {
                cardBestTimeText.text = "Лучшее время: —:—";
            }

            cardStarsText.text = $"Звёзды: {GetStarsString(selRecord.stars)}";

            if (selRecord.completed)
            {
                cardStatusText.text = "Статус: <color=#FFE7B0>ПРОЙДЕН</color>";
            }
            else if (progress.IsUnlocked(selectedIndex))
            {
                cardStatusText.text = "Статус: <color=#5AFFDF>ДОСТУПЕН</color>";
            }
            else
            {
                cardStatusText.text = "Статус: <color=#999999>ЗАКРЫТ</color>";
            }

            playButtonText.text = $"ИГРАТЬ • УРОВЕНЬ {selectedIndex + 1}";
        }

        private static string GetStarsString(int stars)
        {
            switch (stars)
            {
                case 3: return "★★★";
                case 2: return "★★☆";
                case 1: return "★☆☆";
                default: return "☆☆☆";
            }
        }

        private static string GetChapterName(int levelIndex)
        {
            if (levelIndex < 3) return "ГЛАВА I • ДРЕВНИЕ СВОДЫ";
            if (levelIndex < 6) return "ГЛАВА II • ПЕСЧАНЫЕ ШАХТЫ";
            if (levelIndex < 9) return "ГЛАВА III • ЗАТОПЛЕННЫЕ СВОДЫ";
            if (levelIndex < 12) return "ГЛАВА IV • ЗАЛЫ ТЬМЫ";
            if (levelIndex < 15) return "ГЛАВА V • ПЕЧАТЬ СТРАЖЕЙ";
            return "ГЛАВА VI • СЕРДЦЕ ПОДЗЕМЕЛЬЯ";
        }
    }
}
