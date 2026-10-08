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
        private static readonly Vector2 CanvasReference = new Vector2(1024f, 576f);
        private static readonly Color GoldColor = new Color(1f, 0.84f, 0.42f);
        private static readonly Color CyanGlow = new Color(0.35f, 0.95f, 1f);
        private static readonly Color PathActiveColor = new Color(1f, 0.84f, 0.42f, 0.95f);
        private static readonly Color PathLockedColor = new Color(0.35f, 0.38f, 0.44f, 0.45f);

        // 15 Level node positions in 1024x576 space matching reference concept art
        private static readonly Vector2[] NodePositions =
        {
            new Vector2(248f, 98f),  // Level 1 (Mine Shaft Bridge)
            new Vector2(395f, 110f), // Level 2
            new Vector2(536f, 120f), // Level 3
            new Vector2(286f, 188f), // Level 4 (Ancient Sunken City)
            new Vector2(441f, 202f), // Level 5
            new Vector2(586f, 185f), // Level 6
            new Vector2(250f, 268f), // Level 7 (Flooded Azure Waters)
            new Vector2(410f, 280f), // Level 8
            new Vector2(570f, 286f), // Level 9
            new Vector2(252f, 368f), // Level 10 (Stone Mechanisms & Wheels)
            new Vector2(410f, 372f), // Level 11
            new Vector2(556f, 368f), // Level 12
            new Vector2(310f, 450f), // Level 13 (Magma Core Abyss)
            new Vector2(430f, 454f), // Level 14
            new Vector2(595f, 456f)  // Level 15
        };

        // S-curve connection sequence between nodes matching the art
        private static readonly (int from, int to)[] PathSegments =
        {
            (0, 1),   // 1 -> 2
            (1, 2),   // 2 -> 3
            (2, 5),   // 3 -> 6 (curve down right)
            (5, 4),   // 6 -> 5
            (4, 3),   // 5 -> 4
            (3, 6),   // 4 -> 7 (curve down left)
            (6, 7),   // 7 -> 8
            (7, 8),   // 8 -> 9
            (8, 11),  // 9 -> 12 (curve down right)
            (11, 10), // 12 -> 11
            (10, 9),  // 11 -> 10
            (9, 12),  // 10 -> 13 (curve down left)
            (12, 13), // 13 -> 14
            (13, 14)  // 14 -> 15
        };

        private Transform picture;
        private PlayerProgress progress;
        private LevelCatalog catalog;
        private int selectedIndex;
        private int lastSeenUnlockedIndex = -1;
        private readonly List<Image> nodeImages = new List<Image>();
        private readonly List<Text> nodeLabels = new List<Text>();
        private readonly List<Image> nodeIconOverlays = new List<Image>();
        private readonly List<Text> nodeStarLabels = new List<Text>();
        private readonly List<Image> pathLines = new List<Image>();

        // Sprites from atlas
        private Sprite nodeGoldSprite;
        private Sprite nodeCyanSprite;
        private Sprite nodeLockedSprite;
        private Sprite iconCheckSprite;
        private Sprite iconLockSprite;
        private Sprite iconHelmetSprite;
        private Sprite cardFrameSprite;
        private Sprite playBtnSprite;
        private Sprite backBtnSprite;
        private Sprite progressTrackSprite;
        private Sprite progressFillSprite;
        private Sprite dividerSprite;

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
            screen.LoadAtlasSprites();
            screen.Build();
            return screen;
        }

        private void LoadAtlasSprites()
        {
            nodeGoldSprite = Resources.Load<Sprite>("UI/map_node_gold");
            nodeCyanSprite = Resources.Load<Sprite>("UI/map_node_cyan");
            nodeLockedSprite = Resources.Load<Sprite>("UI/map_node_locked");
            iconCheckSprite = Resources.Load<Sprite>("UI/map_icon_check");
            iconLockSprite = Resources.Load<Sprite>("UI/map_icon_lock");
            iconHelmetSprite = Resources.Load<Sprite>("UI/map_icon_helmet");
            cardFrameSprite = Resources.Load<Sprite>("UI/map_card_frame");
            playBtnSprite = Resources.Load<Sprite>("UI/map_play_btn");
            backBtnSprite = Resources.Load<Sprite>("UI/map_back_btn");
            progressTrackSprite = Resources.Load<Sprite>("UI/map_progress_track");
            progressFillSprite = Resources.Load<Sprite>("UI/map_progress_fill");
            dividerSprite = Resources.Load<Sprite>("UI/map_divider");
        }

        public void Open(PlayerProgress currentProgress, LevelCatalog levelCatalog)
        {
            progress = currentProgress;
            catalog = levelCatalog;

            int maxLevels = catalog != null && catalog.Levels.Count > 0 ? catalog.Levels.Count : NodePositions.Length;
            int highestUnlocked = Mathf.Clamp(progress.highestUnlockedIndex, 0, maxLevels - 1);
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
            // Background painted map (1024x576)
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
            for (int i = 0; i < PathSegments.Length; i++)
            {
                var (from, to) = PathSegments[i];
                Vector2 start = NodePositions[from];
                Vector2 end = NodePositions[to];
                Vector2 mid = (start + end) * 0.5f;
                float dist = Vector2.Distance(start, end);
                float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;

                var lineObj = new GameObject($"PathLine_{from + 1}_{to + 1}").AddComponent<Image>();
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
            sparkImage.rectTransform.sizeDelta = new Vector2(34f, 34f);
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
            // Back button in top-left using sprite from atlas (240x160 ratio)
            var backBtnObj = new GameObject("BackButton").AddComponent<Image>();
            backBtnObj.transform.SetParent(picture, false);
            if (backBtnSprite != null)
            {
                backBtnObj.sprite = backBtnSprite;
            }
            else
            {
                backBtnObj.color = new Color(0.2f, 0.16f, 0.12f, 0.95f);
            }
            MenuStyle.PlaceOnPicture(backBtnObj.rectTransform, new Rect(24f, 16f, 66f, 44f));
            MenuStyle.MakeButton(backBtnObj, () => BackRequested?.Invoke());

            // Title: ПУТЬ ИСКАТЕЛЯ
            var title = MenuStyle.AddLabel(picture, "ПУТЬ ИСКАТЕЛЯ", 28);
            title.fontStyle = FontStyle.Bold;
            title.color = GoldColor;
            title.alignment = TextAnchor.MiddleCenter;
            MenuStyle.PlaceOnPicture(title.rectTransform, new Rect(320f, 10f, 384f, 32f));

            // Decorative divider below title (285x60 ratio)
            if (dividerSprite != null)
            {
                var divObj = new GameObject("TitleDivider").AddComponent<Image>();
                divObj.transform.SetParent(picture, false);
                divObj.sprite = dividerSprite;
                divObj.raycastTarget = false;
                MenuStyle.PlaceOnPicture(divObj.rectTransform, new Rect(427f, 40f, 170f, 24f));
            }

            // Progress text
            progressText = MenuStyle.AddLabel(picture, "Пройдено 0 из 15", 15);
            progressText.color = new Color(0.92f, 0.92f, 0.92f);
            progressText.alignment = TextAnchor.MiddleRight;
            MenuStyle.PlaceOnPicture(progressText.rectTransform, new Rect(240f, 65f, 180f, 22f));

            // Progress bar track using atlas sprite (220x60 ratio)
            var barTrack = new GameObject("ProgressTrack").AddComponent<Image>();
            barTrack.transform.SetParent(picture, false);
            if (progressTrackSprite != null)
            {
                barTrack.sprite = progressTrackSprite;
            }
            else
            {
                barTrack.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);
            }
            MenuStyle.PlaceOnPicture(barTrack.rectTransform, new Rect(430f, 64f, 210f, 24f));

            var fillObj = new GameObject("Fill").AddComponent<Image>();
            fillObj.transform.SetParent(barTrack.transform, false);
            if (progressFillSprite != null)
            {
                fillObj.sprite = progressFillSprite;
            }
            else
            {
                fillObj.color = GoldColor;
            }
            fillObj.type = Image.Type.Filled;
            fillObj.fillMethod = Image.FillMethod.Horizontal;
            fillObj.fillOrigin = (int)Image.OriginHorizontal.Left;
            
            RectTransform fillRect = fillObj.rectTransform;
            fillRect.anchorMin = new Vector2(0.04f, 0.15f);
            fillRect.anchorMax = new Vector2(0.96f, 0.85f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            progressBarFill = fillObj;

            percentText = MenuStyle.AddLabel(picture, "0%", 15);
            percentText.color = GoldColor;
            percentText.fontStyle = FontStyle.Bold;
            percentText.alignment = TextAnchor.MiddleLeft;
            MenuStyle.PlaceOnPicture(percentText.rectTransform, new Rect(650f, 65f, 60f, 22f));

            totalStarsText = MenuStyle.AddLabel(picture, "★ 0", 18);
            totalStarsText.color = GoldColor;
            totalStarsText.fontStyle = FontStyle.Bold;
            totalStarsText.alignment = TextAnchor.MiddleRight;
            MenuStyle.PlaceOnPicture(totalStarsText.rectTransform, new Rect(750f, 18f, 100f, 30f));
        }

        private void BuildLevelNodes()
        {
            for (int i = 0; i < NodePositions.Length; i++)
            {
                int index = i;
                Vector2 pos = NodePositions[i];
                float size = 56f;

                var nodeObj = new GameObject($"Node_{i + 1}").AddComponent<Image>();
                nodeObj.transform.SetParent(picture, false);
                nodeObj.sprite = nodeLockedSprite;
                nodeObj.color = Color.white;
                MenuStyle.PlaceOnPicture(nodeObj.rectTransform, new Rect(pos.x - size / 2f, pos.y - size / 2f, size, size));

                var btn = nodeObj.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (progress != null && progress.IsUnlocked(index))
                    {
                        selectedIndex = index;
                        Refresh();
                    }
                });

                // Icon overlay (Lock / Checkmark / Helmet)
                var iconObj = new GameObject("IconOverlay").AddComponent<Image>();
                iconObj.transform.SetParent(nodeObj.transform, false);
                iconObj.raycastTarget = false;
                var iconRect = iconObj.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(28f, 28f);
                iconRect.anchoredPosition = Vector2.zero;

                nodeImages.Add(nodeObj);
                nodeIconOverlays.Add(iconObj);

                var label = MenuStyle.AddLabel(nodeObj.transform, $"{i + 1}", 20);
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
                MenuStyle.Stretch(label.rectTransform);
                nodeLabels.Add(label);

                // Stars below node
                var starsLabel = MenuStyle.AddLabel(nodeObj.transform, string.Empty, 11);
                starsLabel.color = GoldColor;
                starsLabel.fontStyle = FontStyle.Bold;
                starsLabel.alignment = TextAnchor.LowerCenter;
                MenuStyle.Stretch(starsLabel.rectTransform);
                starsLabel.rectTransform.anchoredPosition = new Vector2(0f, -16f);
                nodeStarLabels.Add(starsLabel);
            }
        }

        private void BuildInfoCard()
        {
            // Panel frame on the right side of the screen matching concept stone card
            var cardPanel = new GameObject("InfoCardPanel").AddComponent<Image>();
            cardPanel.transform.SetParent(picture, false);
            if (cardFrameSprite != null)
            {
                cardPanel.sprite = cardFrameSprite;
            }
            else
            {
                cardPanel.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);
            }
            MenuStyle.PlaceOnPicture(cardPanel.rectTransform, new Rect(750f, 75f, 235f, 410f));

            var border = cardPanel.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 0.84f, 0.42f, 0.55f);
            border.effectDistance = new Vector2(2f, 2f);

            cardChapterText = MenuStyle.AddLabel(cardPanel.transform, "ГЛАВА I", 16);
            cardChapterText.color = GoldColor;
            cardChapterText.fontStyle = FontStyle.Bold;
            cardChapterText.alignment = TextAnchor.UpperCenter;
            MenuStyle.PlaceOnPicture(cardChapterText.rectTransform, new Rect(10f, 18f, 215f, 24f));

            cardLevelTitle = MenuStyle.AddLabel(cardPanel.transform, "Уровень 1", 20);
            cardLevelTitle.color = Color.white;
            cardLevelTitle.fontStyle = FontStyle.Bold;
            cardLevelTitle.alignment = TextAnchor.UpperCenter;
            MenuStyle.PlaceOnPicture(cardLevelTitle.rectTransform, new Rect(10f, 42f, 215f, 48f));

            // Illustration preview box
            var previewBox = new GameObject("PreviewBox").AddComponent<Image>();
            previewBox.transform.SetParent(cardPanel.transform, false);
            previewBox.color = new Color(0.04f, 0.06f, 0.08f, 0.95f);
            MenuStyle.PlaceOnPicture(previewBox.rectTransform, new Rect(15f, 95f, 205f, 175f));

            var icon = MenuStyle.AddLabel(previewBox.transform, "✦", 48);
            icon.color = CyanGlow;
            icon.alignment = TextAnchor.MiddleCenter;
            MenuStyle.Stretch(icon.rectTransform);

            cardBestTimeText = MenuStyle.AddLabel(cardPanel.transform, "Лучшее время: —:—", 14);
            cardBestTimeText.color = new Color(0.85f, 0.85f, 0.88f);
            cardBestTimeText.alignment = TextAnchor.MiddleLeft;
            MenuStyle.PlaceOnPicture(cardBestTimeText.rectTransform, new Rect(15f, 280f, 205f, 24f));

            cardStarsText = MenuStyle.AddLabel(cardPanel.transform, "Звёзды: ☆☆☆", 16);
            cardStarsText.color = GoldColor;
            cardStarsText.fontStyle = FontStyle.Bold;
            cardStarsText.alignment = TextAnchor.MiddleLeft;
            MenuStyle.PlaceOnPicture(cardStarsText.rectTransform, new Rect(15f, 310f, 205f, 24f));

            cardStatusText = MenuStyle.AddLabel(cardPanel.transform, "Статус: ДОСТУПЕН", 15);
            cardStatusText.color = CyanGlow;
            cardStatusText.fontStyle = FontStyle.Bold;
            cardStatusText.alignment = TextAnchor.MiddleLeft;
            MenuStyle.PlaceOnPicture(cardStatusText.rectTransform, new Rect(15f, 345f, 205f, 24f));
        }

        private void BuildBottomPlayButton()
        {
            var btnImage = new GameObject("PlayButton").AddComponent<Image>();
            btnImage.transform.SetParent(picture, false);
            if (playBtnSprite != null)
            {
                btnImage.sprite = playBtnSprite;
            }
            else
            {
                btnImage.color = new Color(0.95f, 0.72f, 0.22f, 0.95f);
            }
            MenuStyle.PlaceOnPicture(btnImage.rectTransform, new Rect(300f, 495f, 424f, 58f));

            playButtonText = MenuStyle.AddLabel(btnImage.transform, "ПРОДОЛЖИТЬ", 22);
            playButtonText.fontStyle = FontStyle.Bold;
            playButtonText.color = new Color(0.12f, 0.08f, 0.03f);
            playButtonText.alignment = TextAnchor.MiddleCenter;
            MenuStyle.Stretch(playButtonText.rectTransform);

            MenuStyle.MakeButton(btnImage, OnPlayClicked);
        }

        private void OnPlayClicked()
        {
            if (progress != null && progress.IsUnlocked(selectedIndex))
            {
                progress.lastSelectedLevelIndex = selectedIndex;
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
            int maxStep = Mathf.Clamp(progress.highestUnlockedIndex, 0, PathSegments.Length);
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

                var (from, to) = PathSegments[segIndex];
                Vector2 start = NodePositions[from];
                Vector2 end = NodePositions[to];
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

            int totalLevels = catalog.Levels.Count > 0 ? catalog.Levels.Count : NodePositions.Length;
            int completed = progress.GetCompletedCount();
            int totalStars = progress.GetTotalStars();
            float ratio = totalLevels > 0 ? (float)completed / totalLevels : 0f;

            progressText.text = $"Пройдено {completed} из {totalLevels}";
            progressBarFill.fillAmount = ratio;
            percentText.text = $"{Mathf.RoundToInt(ratio * 100f)}%";
            totalStarsText.text = $"★ {totalStars}";

            // Update path lines
            for (int i = 0; i < pathLines.Count && i < PathSegments.Length; i++)
            {
                var (_, to) = PathSegments[i];
                bool lineActive = progress.IsUnlocked(to);
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
                string levelId = i < catalog.Levels.Count ? catalog.Levels[i].id : $"level_{i + 1}";
                LevelRecord record = progress.GetOrCreateRecord(i, levelId);

                Image overlay = nodeIconOverlays[i];

                if (!unlocked)
                {
                    nodeImages[i].sprite = nodeLockedSprite;
                    nodeLabels[i].text = string.Empty;
                    nodeStarLabels[i].text = string.Empty;

                    if (iconLockSprite != null)
                    {
                        overlay.gameObject.SetActive(true);
                        overlay.sprite = iconLockSprite;
                        overlay.color = Color.white;
                        overlay.rectTransform.sizeDelta = new Vector2(24f, 30f);
                        overlay.rectTransform.anchoredPosition = Vector2.zero;
                    }
                }
                else if (record.completed)
                {
                    nodeImages[i].sprite = nodeGoldSprite;
                    nodeLabels[i].text = $"{i + 1}";
                    nodeLabels[i].color = new Color(1f, 0.95f, 0.7f);
                    nodeStarLabels[i].text = GetStarsString(record.stars);

                    if (iconCheckSprite != null)
                    {
                        overlay.gameObject.SetActive(true);
                        overlay.sprite = iconCheckSprite;
                        overlay.color = Color.white;
                        overlay.rectTransform.sizeDelta = new Vector2(20f, 20f);
                        overlay.rectTransform.anchoredPosition = new Vector2(16f, -14f);
                    }
                    else
                    {
                        overlay.gameObject.SetActive(false);
                    }
                }
                else
                {
                    // Current active level: Cyan glow node with helmet
                    nodeImages[i].sprite = nodeCyanSprite;
                    nodeLabels[i].text = $"{i + 1}";
                    nodeLabels[i].color = Color.white;
                    nodeStarLabels[i].text = string.Empty;

                    if (iconHelmetSprite != null)
                    {
                        overlay.gameObject.SetActive(true);
                        overlay.sprite = iconHelmetSprite;
                        overlay.color = Color.white;
                        overlay.rectTransform.sizeDelta = new Vector2(46f, 34f);
                        overlay.rectTransform.anchoredPosition = new Vector2(0f, 22f);
                    }
                    else
                    {
                        overlay.gameObject.SetActive(false);
                    }
                }
            }

            // Update Card
            int validIndex = Mathf.Clamp(selectedIndex, 0, catalog.Levels.Count - 1);
            LevelDefinition def = catalog.Levels[validIndex];
            LevelRecord selRecord = progress.GetOrCreateRecord(validIndex, def.id);

            cardChapterText.text = GetChapterName(validIndex);
            cardLevelTitle.text = $"{validIndex + 1:00}. {def.title}";

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
            else if (progress.IsUnlocked(validIndex))
            {
                cardStatusText.text = "Статус: <color=#5AFFDF>ДОСТУПЕН</color>";
            }
            else
            {
                cardStatusText.text = "Статус: <color=#999999>ЗАКРЫТ</color>";
            }

            playButtonText.text = $"ПРОДОЛЖИТЬ • УРОВЕНЬ {validIndex + 1}";
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
