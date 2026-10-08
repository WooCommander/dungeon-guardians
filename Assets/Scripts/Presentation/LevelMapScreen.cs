using System;
using System.Collections.Generic;
using DungeonGuardians.Core;
using DungeonGuardians.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // "Путь искателя": the level map and the player's progress in one screen. The painting of the cave covers the
    // whole screen, and the circles and the path lie on it by its pixels. The interface (pieces cut by
    // tools/cut_level_map.cs: the title, the chapter card, the buttons) keeps to the screen's edges, inside the safe
    // area, so nothing goes off screen whatever its shape. On wide phones the buttons stand in a column on the left
    // and the progress goes above the card, so the circles keep the middle. Starting the game over lives here too,
    // behind a confirmation.
    public sealed class LevelMapScreen : MonoBehaviour
    {
        // Circle centres in picture pixels, in the order of play: a snake down the five tiers of the cave.
        private static readonly Vector2[] Nodes =
        {
            new Vector2(407f, 155f), new Vector2(662f, 180f), new Vector2(895f, 196f),
            new Vector2(980f, 304f), new Vector2(737f, 320f), new Vector2(480f, 307f),
            new Vector2(411f, 434f), new Vector2(545f, 442f), new Vector2(680f, 450f), new Vector2(951f, 466f),
            new Vector2(924f, 600f), new Vector2(684f, 600f), new Vector2(551f, 595f), new Vector2(419f, 585f),
            new Vector2(520f, 732f), new Vector2(721f, 744f), new Vector2(855f, 752f), new Vector2(989f, 746f),
        };

        // Where the path bends on its way from each circle to the next.
        private static readonly Vector2[][] Bends =
        {
            new[] { new Vector2(500f, 168f), new Vector2(565f, 173f) },
            new[] { new Vector2(760f, 188f), new Vector2(822f, 194f) },
            new[] { new Vector2(975f, 222f), new Vector2(1012f, 240f), new Vector2(1026f, 262f), new Vector2(1012f, 285f) },
            new[] { new Vector2(900f, 318f), new Vector2(840f, 324f) },
            new[] { new Vector2(650f, 313f), new Vector2(580f, 308f) },
            new[] { new Vector2(412f, 337f), new Vector2(350f, 366f), new Vector2(330f, 392f), new Vector2(352f, 418f) },
            new[] { new Vector2(478f, 440f) },
            new[] { new Vector2(612f, 448f) },
            new[] { new Vector2(770f, 466f), new Vector2(830f, 474f), new Vector2(892f, 471f) },
            new[] { new Vector2(1003f, 486f), new Vector2(1030f, 520f), new Vector2(1018f, 565f), new Vector2(975f, 592f) },
            new[] { new Vector2(850f, 594f), new Vector2(790f, 602f) },
            new[] { new Vector2(618f, 599f) },
            new[] { new Vector2(485f, 590f) },
            new[] { new Vector2(372f, 620f), new Vector2(382f, 680f), new Vector2(440f, 722f) },
            new[] { new Vector2(620f, 741f) },
            new[] { new Vector2(788f, 750f) },
            new[] { new Vector2(922f, 752f) },
        };

        // The chapters: first level of each, its name, and the piece of the map shown on the card (none: the card's
        // own painting, which is of chapter III).
        private static readonly int[] ChapterStarts = { 0, 3, 6, 10, 14 };
        private static readonly string[] ChapterNumbers = { "I", "II", "III", "IV", "V" };
        private static readonly string[] ChapterNames =
        {
            "Заброшенные шахты", "Подземный город", "Затопленные своды", "Древние механизмы", "Огненная бездна",
        };
        private static readonly Rect?[] ChapterViews =
        {
            new Rect(850f, 95f, 260f, 257f), new Rect(480f, 215f, 300f, 297f), null,
            new Rect(360f, 470f, 300f, 297f), new Rect(600f, 520f, 280f, 277f),
        };

        private const int StarsPerLevel = 3;
        private const float NodeHalf = 48.5f;
        // The helmet (UI/map_helmet.png, 80 x 56) sits this far from the centre of the chosen circle.
        private static readonly Vector2 HelmetOffset = new Vector2(-3f, -34f);
        private static readonly Vector2 HelmetSize = new Vector2(80f, 56f);

        // The interface is sized in painting pixels, at the scale the painting has on a 16:9 screen; positions are
        // the mock-up's. Sizes of the cut pieces:
        private static readonly Vector2 HeaderSize = new Vector2(740f, 146f);
        private static readonly Vector2 TitleSize = new Vector2(740f, 100f);
        private static readonly Vector2 CardSize = new Vector2(384f, 735f);
        private static readonly Vector2 BarSize = new Vector2(316f, 26f);
        // Inside the card: the window with the chapter's piece of the map.
        private static readonly Rect CardWindow = new Rect(48f, 239f, 297f, 294f);
        // Inside the bar: the fill starts at its left end and may reach its right end.
        private const float BarLeft = 5f;
        private const float BarRight = 298f;
        // The header's centre on the painting; the card's distance from the right edge and from the top (lower on
        // screens where the progress goes above it); the buttons' distance from the left and bottom edges.
        private const float HeaderCentre = 856f;
        private const float CardRight = 42f;
        private const float CardTop = 117f;
        private const float CardTopWide = 160f;
        private const float RowLeft = 48f;
        private const float RowBottom = 41f;
        private const float ButtonWidth = 380f;
        private const float ButtonHeight = 93f;
        private const float ButtonGap = 20f;
        // Wider than this the layout for phones is used. Narrower than NarrowAspect (tablets) the first circle would
        // cover the progress under the title, so it goes above the card as on phones.
        private const float WideAspect = 1.8f;
        private const float NarrowAspect = 1.58f;
        // On the painting: the left-most the path goes (the bend between 6 and 7), the top of the circles under the
        // title, the bottom of the lowest circles.
        private const float PathLeft = 318f;
        private const float CirclesTop = 132f;
        private const float CirclesBottom = 812f;

        private static readonly Color Cream = new Color(1f, 0.92f, 0.76f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.45f);
        private static readonly Color Shadow = new Color(0.08f, 0.04f, 0.01f, 0.9f);

        private enum NodeState { Done, Current, Locked }

        private sealed class NodeView
        {
            public Image Circle;
            public Text Number;
            public NodeState State;
        }

        // The painting, and the layer of circles on it (the same rectangle; the header lies between the two).
        private RectTransform painting;
        private RectTransform mapLayer;
        private Transform picture;
        // The interface behind the circles (header, totals) and in front of them (card, buttons).
        private RectTransform hudBack;
        private RectTransform hudFront;
        private Image header;
        private Sprite headerSprite;
        private Sprite titleSprite;
        private RectTransform totals;
        private RectTransform progressRow;
        private Image bar;
        private RectTransform card;
        private Image[] buttons;
        private Vector2 laidOutScreen;
        private Rect laidOutHud;
        private Font serif;
        private Sprite doneSprite;
        private Sprite currentSprite;
        private Sprite lockedSprite;
        private readonly List<NodeView> nodes = new List<NodeView>();
        private readonly List<List<Vector2>> segments = new List<List<Vector2>>();
        private MapPath pathGlow;
        private MapPath pathGold;
        private MapPath dashShadow;
        private MapPath dashes;
        private Image helmet;
        private Image currentGlow;

        private Text passedText;
        private Text percentText;
        private Image barFill;
        private Image barCap;
        private Text starsText;
        private Text timeText;
        private Text chapterNumber;
        private Text chapterName;
        private RawImage chapterView;
        private Text levelTitle;
        private Text levelRecord;
        private Text playText;
        private GameObject confirmModal;
        private Text toastText;

        private PlayerProgress progress;
        private LevelCatalog catalog;
        private int levelCount;
        private int selectedIndex;
        private int currentIndex = -1;
        private int lastSeenUnlocked = -1;
        private int poppingIndex = -1;
        private float popStart;

        public event Action<int> LevelSelected;
        public event Action BackRequested;
        // The player confirmed starting over: progress is to be wiped. The map redraws itself right after.
        public event Action ResetRequested;

        public static LevelMapScreen Create(Transform parent)
        {
            var root = new GameObject("LevelMapScreen", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            MenuStyle.Stretch((RectTransform)root.transform);
            var screen = root.AddComponent<LevelMapScreen>();
            screen.Build();
            root.SetActive(false);
            return screen;
        }

        public void Open(PlayerProgress currentProgress, LevelCatalog levelCatalog)
        {
            progress = currentProgress;
            catalog = levelCatalog;
            levelCount = Mathf.Min(catalog != null ? catalog.Levels.Count : 0, Nodes.Length);
            int highest = Mathf.Clamp(progress.highestUnlockedIndex, 0, Mathf.Max(levelCount - 1, 0));
            selectedIndex = Mathf.Clamp(progress.lastSelectedLevelIndex, 0, highest);

            // A circle opened since the map was last shown pops up.
            poppingIndex = lastSeenUnlocked >= 0 && highest > lastSeenUnlocked ? highest : -1;
            popStart = Time.unscaledTime;
            lastSeenUnlocked = highest;

            HideConfirmModal();
            HideToast();
            Refresh();
            gameObject.SetActive(true);
            // Laid out before the first frame is drawn.
            laidOutScreen = Vector2.zero;
            LateUpdate();
        }

        public void Close()
        {
            HideConfirmModal();
            HideToast();
            gameObject.SetActive(false);
        }

        // Android's back button: closes the confirmation if it is open. Returns whether it was.
        public bool HideDialog()
        {
            if (confirmModal == null || !confirmModal.activeSelf)
            {
                return false;
            }

            HideConfirmModal();
            return true;
        }

        private void Build()
        {
            serif = Resources.Load<Font>("Fonts/PTSerif-Bold");
            if (serif == null)
            {
                serif = MenuStyle.Font;
            }

            doneSprite = Resources.Load<Sprite>("UI/map_node_done");
            currentSprite = Resources.Load<Sprite>("UI/map_node_current");
            lockedSprite = Resources.Load<Sprite>("UI/map_node_locked");

            // Back to front: the painting, the header, the circles, the card and buttons. Layout() places them all.
            var paintingImage = new GameObject("Painting").AddComponent<Image>();
            paintingImage.transform.SetParent(transform, false);
            paintingImage.sprite = Resources.Load<Sprite>("Backgrounds/level_map");
            paintingImage.color = paintingImage.sprite != null ? Color.white : new Color(0.05f, 0.06f, 0.08f);
            // Catches every tap meant for the start screen underneath.
            paintingImage.raycastTarget = true;
            painting = paintingImage.rectTransform;

            hudBack = CreateHud("Interface Back");
            mapLayer = (RectTransform)new GameObject("Map", typeof(RectTransform)).transform;
            mapLayer.SetParent(transform, false);
            picture = mapLayer;
            hudFront = CreateHud("Interface Front");

            BuildPath();
            BuildNodes();
            BuildHeader();
            BuildCard();
            BuildButtons();
            BuildToast();
            BuildConfirmModal();
        }

        // A full-screen layer for the interface, kept inside the safe area.
        private RectTransform CreateHud(string name)
        {
            var hud = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            hud.SetParent(transform, false);
            MenuStyle.Stretch(hud);
            hud.gameObject.AddComponent<SafeArea>();
            return hud;
        }

        // ------------------------------------------------------------------------------------------------- path

        private void BuildPath()
        {
            // The whole route as one smooth curve through every circle and bend, cut into a piece per step.
            var points = new List<Vector2>();
            var owner = new List<int>();
            for (int i = 0; i < Nodes.Length; i++)
            {
                points.Add(Nodes[i]);
                owner.Add(i);
                if (i < Bends.Length)
                {
                    foreach (Vector2 bend in Bends[i])
                    {
                        points.Add(bend);
                        owner.Add(i);
                    }
                }
            }

            for (int i = 0; i < Nodes.Length - 1; i++)
            {
                segments.Add(new List<Vector2>());
            }

            for (int k = 0; k < points.Count - 1; k++)
            {
                Vector2 p0 = points[Mathf.Max(k - 1, 0)], p1 = points[k], p2 = points[k + 1], p3 = points[Mathf.Min(k + 2, points.Count - 1)];
                List<Vector2> segment = segments[owner[k]];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 4f));
                for (int s = segment.Count == 0 ? 0 : 1; s <= steps; s++)
                {
                    segment.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
                }
            }

            pathGlow = MapPath.Create(picture, "Path Glow", new Color(1f, 0.72f, 0.25f, 0.45f), 20f, 1f);
            pathGold = MapPath.Create(picture, "Path Gold", new Color(1f, 0.86f, 0.5f, 1f), 6f, 0.45f);
            dashShadow = MapPath.Create(picture, "Path Dash Shadow", new Color(0.04f, 0.04f, 0.05f, 0.85f), 9f, 0.35f, 14f, 10f);
            dashes = MapPath.Create(picture, "Path Dashes", new Color(0.88f, 0.87f, 0.83f, 1f), 5f, 0.3f, 14f, 10f);
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        // ------------------------------------------------------------------------------------------------ nodes

        private void BuildNodes()
        {
            currentGlow = new GameObject("Current Glow").AddComponent<Image>();
            currentGlow.transform.SetParent(picture, false);
            currentGlow.sprite = ExitGlow.GetHaloSprite();
            currentGlow.color = new Color(0.3f, 0.95f, 1f, 0.7f);
            currentGlow.raycastTarget = false;

            for (int i = 0; i < Nodes.Length; i++)
            {
                int index = i;
                var view = new NodeView();
                view.Circle = new GameObject($"Level {i + 1}").AddComponent<Image>();
                view.Circle.transform.SetParent(picture, false);
                PlaceAround(view.Circle.rectTransform, Nodes[i], NodeHalf);
                MenuStyle.MakeButton(view.Circle, () => OnNodeClicked(index));

                view.Number = AddText(view.Circle.transform, (i + 1).ToString(), Cream, TextAnchor.MiddleCenter, 60, serif);
                // The number fills the inner disc: a box of 40 x 34 picture pixels around the centre.
                view.Number.rectTransform.anchorMin = new Vector2(0.5f - 20f / 97f, 0.5f - 17f / 97f);
                view.Number.rectTransform.anchorMax = new Vector2(0.5f + 20f / 97f, 0.5f + 17f / 97f);
                nodes.Add(view);
            }

            helmet = new GameObject("Helmet").AddComponent<Image>();
            helmet.transform.SetParent(picture, false);
            helmet.sprite = Resources.Load<Sprite>("UI/map_helmet");
            helmet.raycastTarget = false;
        }

        private static void PlaceAround(RectTransform rect, Vector2 centre, float half)
        {
            MenuStyle.PlaceOnPicture(rect, new Rect(centre.x - half, centre.y - half, 2f * half, 2f * half));
        }

        private void OnNodeClicked(int index)
        {
            if (progress == null || index >= levelCount)
            {
                return;
            }

            if (!progress.IsUnlocked(index))
            {
                ShowToast($"Сначала пройдите уровень {index}");
                return;
            }

            selectedIndex = index;
            progress.lastSelectedLevelIndex = index;
            Refresh();
        }

        // ----------------------------------------------------------------------------------------------- header

        private void BuildHeader()
        {
            headerSprite = Resources.Load<Sprite>("UI/map_header");
            titleSprite = Resources.Load<Sprite>("UI/map_title");
            header = new GameObject("Header").AddComponent<Image>();
            header.transform.SetParent(hudBack, false);
            header.raycastTarget = false;

            // Totals over the card: stars and time, and on very wide or narrow screens the progress too.
            totals = (RectTransform)new GameObject("Totals", typeof(RectTransform)).transform;
            totals.SetParent(hudBack, false);
            starsText = AddText(totals, string.Empty, Gold, TextAnchor.MiddleCenter, 60, MenuStyle.Font);
            timeText = AddText(totals, string.Empty, Cream, TextAnchor.MiddleCenter, 60, serif);

            // "Пройдено N из M", the bar and the percentage: on the header's plate, or among the totals.
            progressRow = (RectTransform)new GameObject("Progress", typeof(RectTransform)).transform;
            progressRow.SetParent(header.transform, false);
            passedText = AddText(progressRow, string.Empty, Cream, TextAnchor.MiddleRight, 60, serif);
            bar = new GameObject("Bar").AddComponent<Image>();
            bar.transform.SetParent(progressRow, false);
            bar.sprite = Resources.Load<Sprite>("UI/map_bar");
            bar.color = bar.sprite != null ? Color.white : new Color(0.08f, 0.08f, 0.1f);
            bar.raycastTarget = false;
            barFill = new GameObject("Fill").AddComponent<Image>();
            barFill.transform.SetParent(bar.transform, false);
            barFill.sprite = Resources.Load<Sprite>("UI/map_fill");
            barFill.raycastTarget = false;
            barCap = new GameObject("Cap").AddComponent<Image>();
            barCap.transform.SetParent(bar.transform, false);
            barCap.sprite = Resources.Load<Sprite>("UI/map_fill_cap");
            barCap.raycastTarget = false;
            percentText = AddText(progressRow, string.Empty, Cream, TextAnchor.MiddleLeft, 60, serif);
        }

        // ------------------------------------------------------------------------------------------------- card

        private void BuildCard()
        {
            var frame = new GameObject("Card").AddComponent<Image>();
            frame.transform.SetParent(hudFront, false);
            frame.sprite = Resources.Load<Sprite>("UI/map_card");
            frame.color = frame.sprite != null ? Color.white : new Color(0.1f, 0.09f, 0.08f, 0.95f);
            // The card takes taps, so none fall through to a circle behind it.
            frame.raycastTarget = true;
            card = frame.rectTransform;

            chapterNumber = AddText(card, string.Empty, Gold, TextAnchor.MiddleCenter, 60, serif);
            PlaceIn(chapterNumber.rectTransform, CardSize, new Rect(54f, 63f, 286f, 38f));

            chapterName = AddText(card, string.Empty, Cream, TextAnchor.MiddleCenter, 46, serif);
            chapterName.lineSpacing = 0.9f;
            PlaceIn(chapterName.rectTransform, CardSize, new Rect(42f, 131f, 310f, 90f));

            chapterView = new GameObject("Chapter View").AddComponent<RawImage>();
            chapterView.transform.SetParent(card, false);
            Sprite paintingSprite = painting.GetComponent<Image>().sprite;
            chapterView.texture = paintingSprite != null ? paintingSprite.texture : null;
            chapterView.raycastTarget = false;
            PlaceIn(chapterView.rectTransform, CardSize, CardWindow);

            // The chosen level along the bottom of the card's picture, on a dark band.
            var band = new GameObject("Level Band").AddComponent<Image>();
            band.transform.SetParent(card, false);
            band.color = new Color(0.02f, 0.02f, 0.03f, 0.72f);
            band.raycastTarget = false;
            PlaceIn(band.rectTransform, CardSize, new Rect(CardWindow.x, CardWindow.yMax - 72f, CardWindow.width, 72f));

            levelTitle = AddText(card, string.Empty, Cream, TextAnchor.MiddleCenter, 40, serif);
            PlaceIn(levelTitle.rectTransform, CardSize, new Rect(CardWindow.x + 8f, CardWindow.yMax - 68f, CardWindow.width - 16f, 32f));
            levelRecord = AddText(card, string.Empty, Gold, TextAnchor.MiddleCenter, 40, MenuStyle.Font);
            PlaceIn(levelRecord.rectTransform, CardSize, new Rect(CardWindow.x + 8f, CardWindow.yMax - 36f, CardWindow.width - 16f, 30f));
        }

        // ---------------------------------------------------------------------------------------------- buttons

        private void BuildButtons()
        {
            // Three buttons of one kind and width: start over, back, and the golden banner to play; the other two are
            // its unlit twin (UI/map_button.png). Both stretch in the middle only. One caption size for all three.
            var box = new Rect(0f, 0f, ButtonWidth, ButtonHeight);
            Text reset = MenuStyle.AddQuietButton(hudFront, "map_button", box, "НАЧАТЬ ЗАНОВО", ShowConfirmModal);
            Text back = MenuStyle.AddQuietButton(hudFront, "map_button", box, "НАЗАД", () => BackRequested?.Invoke());
            playText = MenuStyle.AddCaptionedButton(hudFront, "map_play", box, string.Empty, MenuStyle.GoldCaption, OnPlayClicked);
            buttons = new[] { ButtonOf(reset), ButtonOf(back), ButtonOf(playText) };
            EqualFontSize.Apply(hudFront.gameObject, 60, playText, back, reset);
        }

        private static Image ButtonOf(Text caption)
        {
            return caption.transform.parent.GetComponent<Image>();
        }

        // ----------------------------------------------------------------------------------------------- layout

        private void LateUpdate()
        {
            Vector2 screen = ((RectTransform)transform).rect.size;
            if (screen != laidOutScreen || hudBack.rect != laidOutHud)
            {
                laidOutScreen = screen;
                laidOutHud = hudBack.rect;
                Layout(screen);
            }
        }

        private void Layout(Vector2 screen)
        {
            if (screen.x <= 0f || screen.y <= 0f)
            {
                return;
            }

            // Interface units per painting pixel; whether this is a wide phone; whether the progress goes above the card.
            float k = screen.y / MenuStyle.PictureSize.y;
            float aspect = screen.x / screen.y;
            bool wide = aspect > WideAspect;
            bool aside = wide || aspect < NarrowAspect;

            // The painting covers the screen. On wide screens it is cut at the top and bottom; it goes down as far
            // as keeps the circles under the title clear of it and the lowest ones on screen.
            float s = Mathf.Max(screen.x / MenuStyle.PictureSize.x, screen.y / MenuStyle.PictureSize.y);
            Vector2 map = MenuStyle.PictureSize * s;
            float mapLeft = (screen.x - map.x) / 2f;
            float mapTop = (screen.y - map.y) / 2f;
            if (wide)
            {
                mapTop = Mathf.Clamp(TitleSize.y * k - CirclesTop * s, screen.y - map.y, Mathf.Min(0f, screen.y - 8f * k - CirclesBottom * s));
            }

            PlaceTopLeft(painting, new Vector2(mapLeft, mapTop), map);
            PlaceTopLeft(mapLayer, new Vector2(mapLeft, mapTop), map);

            // The interface, in the safe area.
            Rect hud = hudBack.rect;
            float hudLeft = hudBack.anchorMin.x * screen.x;

            // The card at the right; the totals over it, the same width.
            PlaceTopRight(card, new Vector2(CardRight, aside ? CardTopWide : CardTop) * k, CardSize * k);
            float cardLeft = hud.width - (CardRight + CardSize.x) * k;
            var totalsSize = new Vector2(CardSize.x, aside ? CardTopWide - 22f : 80f);
            PlaceTopRight(totals, new Vector2(CardRight, 22f) * k, totalsSize * k);
            PlaceIn(starsText.rectTransform, totalsSize, new Rect(0f, 0f, CardSize.x, 46f));
            PlaceIn(timeText.rectTransform, totalsSize, new Rect(0f, 48f, CardSize.x, 30f));

            // The header at the top, over the middle of the painting but clear of the card: the title alone when the
            // progress goes among the totals.
            Vector2 headerSize = (aside ? TitleSize : HeaderSize) * k;
            header.sprite = aside ? titleSprite : headerSprite;
            header.color = header.sprite != null ? Color.white : Color.clear;
            float centre = screen.x / 2f - hudLeft + (HeaderCentre - MenuStyle.PictureSize.x / 2f) * k;
            centre = Mathf.Max(Mathf.Min(centre, cardLeft - 10f * k - headerSize.x / 2f), headerSize.x / 2f + 10f * k);
            header.rectTransform.anchorMin = header.rectTransform.anchorMax = new Vector2(0f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(centre, 0f);
            header.rectTransform.sizeDelta = headerSize;

            if (aside)
            {
                // Two lines among the totals: the count, then the bar and the percentage.
                progressRow.SetParent(totals, false);
                var row = new Vector2(CardSize.x, 60f);
                PlaceIn(progressRow, totalsSize, new Rect(0f, totalsSize.y - row.y, row.x, row.y));
                passedText.alignment = TextAnchor.MiddleCenter;
                PlaceIn(passedText.rectTransform, row, new Rect(0f, 0f, row.x, 30f));
                PlaceIn(bar.rectTransform, row, new Rect(6f, 34f, BarSize.x, BarSize.y));
                PlaceIn(percentText.rectTransform, row, new Rect(328f, 31f, 56f, 30f));
            }
            else
            {
                // One line on the header's plate, as on the mock-up.
                progressRow.SetParent(header.transform, false);
                var row = new Vector2(686f, 36f);
                PlaceIn(progressRow, HeaderSize, new Rect(54f, 104f, row.x, row.y));
                passedText.alignment = TextAnchor.MiddleRight;
                PlaceIn(passedText.rectTransform, row, new Rect(0f, 0f, 258f, row.y));
                PlaceIn(bar.rectTransform, row, new Rect(272f, 5f, BarSize.x, BarSize.y));
                PlaceIn(percentText.rectTransform, row, new Rect(596f, 0f, 90f, row.y));
            }

            // The buttons: a row along the floor up to the card, or on wide screens a column left of the path.
            float bh = ButtonHeight * k;
            float gap = ButtonGap * k;
            if (wide)
            {
                float left = 24f * k;
                float right = mapLeft + PathLeft * s - hudLeft - 14f * k;
                float bw = Mathf.Clamp(right - left, 200f * k, ButtonWidth * k);
                for (int i = 0; i < buttons.Length; i++)
                {
                    float y = RowBottom * k + (buttons.Length - 1 - i) * (bh + 14f * k);
                    PlaceBottomLeft(buttons[i].rectTransform, new Vector2(left, y), new Vector2(bw, bh));
                }
            }
            else
            {
                float left = RowLeft * k;
                float bw = Mathf.Min(ButtonWidth * k, (cardLeft - gap - left - 2f * gap) / 3f);
                for (int i = 0; i < buttons.Length; i++)
                {
                    PlaceBottomLeft(buttons[i].rectTransform, new Vector2(left + i * (bw + gap), RowBottom * k), new Vector2(bw, bh));
                }
            }

            // The fixed ends of the stretching buttons keep the scale of the rest.
            foreach (Image button in buttons)
            {
                button.pixelsPerUnitMultiplier = 1f / k;
            }
        }

        // Box in its parent's size (in painting pixels from the top-left corner), anchored so it scales with it.
        private static void PlaceIn(RectTransform rect, Vector2 parent, Rect box)
        {
            rect.anchorMin = new Vector2(box.xMin / parent.x, 1f - box.yMax / parent.y);
            rect.anchorMax = new Vector2(box.xMax / parent.x, 1f - box.yMin / parent.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void PlaceTopLeft(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(offset.x, -offset.y);
            rect.sizeDelta = size;
        }

        private static void PlaceTopRight(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-offset.x, -offset.y);
            rect.sizeDelta = size;
        }

        private static void PlaceBottomLeft(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private void OnPlayClicked()
        {
            if (progress != null && selectedIndex < levelCount && progress.IsUnlocked(selectedIndex))
            {
                progress.lastSelectedLevelIndex = selectedIndex;
                LevelSelected?.Invoke(selectedIndex);
            }
        }

        // ---------------------------------------------------------------------------------------------- refresh

        private void Refresh()
        {
            if (progress == null)
            {
                return;
            }

            currentIndex = -1;
            int completed = 0, stars = 0;
            float time = 0f;
            for (int i = 0; i < Nodes.Length; i++)
            {
                NodeView view = nodes[i];
                bool shown = i < levelCount;
                view.Circle.gameObject.SetActive(shown);
                if (!shown)
                {
                    continue;
                }

                LevelRecord record = FindRecord(i);
                if (record != null && record.completed)
                {
                    view.State = NodeState.Done;
                    completed++;
                    stars += record.stars;
                    time += record.bestTimeSeconds;
                }
                else if (progress.IsUnlocked(i))
                {
                    view.State = NodeState.Current;
                    if (currentIndex < 0)
                    {
                        currentIndex = i;
                    }
                }
                else
                {
                    view.State = NodeState.Locked;
                }

                view.Circle.sprite = view.State == NodeState.Done ? doneSprite : view.State == NodeState.Current ? currentSprite : lockedSprite;
                view.Number.color = view.State == NodeState.Done ? Cream : view.State == NodeState.Current ? Color.white : new Color(0.86f, 0.85f, 0.82f);
            }

            // The path is golden up to every circle that is open, dashed beyond.
            var golden = new List<Vector2[]>();
            var dashed = new List<Vector2[]>();
            for (int i = 0; i < segments.Count && i + 1 < levelCount; i++)
            {
                (progress.IsUnlocked(i + 1) ? golden : dashed).Add(segments[i].ToArray());
            }
            pathGlow.SetLines(golden);
            pathGold.SetLines(golden);
            dashShadow.SetLines(dashed);
            dashes.SetLines(dashed);

            currentGlow.gameObject.SetActive(currentIndex >= 0);
            if (currentIndex >= 0)
            {
                PlaceAround(currentGlow.rectTransform, Nodes[currentIndex], 70f);
            }

            float ratio = levelCount > 0 ? (float)completed / levelCount : 0f;
            passedText.text = $"Пройдено {completed} из {levelCount}";
            percentText.text = $"{Mathf.RoundToInt(ratio * 100f)}%";
            float end = Mathf.Lerp(BarLeft, BarRight, ratio);
            barFill.gameObject.SetActive(ratio > 0f);
            barCap.gameObject.SetActive(ratio > 0f);
            PlaceIn(barFill.rectTransform, BarSize, new Rect(BarLeft, 4f, Mathf.Max(end - BarLeft - 6f, 0.01f), 19f));
            PlaceIn(barCap.rectTransform, BarSize, new Rect(Mathf.Max(end - 6f, BarLeft), 4f, 18f, 19f));

            starsText.text = $"★ {stars} / {levelCount * StarsPerLevel}";
            timeText.text = completed > 0 ? $"Общее время {FormatTime(time)}" : string.Empty;

            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (levelCount == 0)
            {
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, levelCount - 1);
            NodeView selected = nodes[selectedIndex];
            MenuStyle.PlaceOnPicture(helmet.rectTransform,
                new Rect(Nodes[selectedIndex].x + HelmetOffset.x - HelmetSize.x / 2f, Nodes[selectedIndex].y + HelmetOffset.y - HelmetSize.y / 2f, HelmetSize.x, HelmetSize.y));

            int chapter = ChapterOf(selectedIndex);
            chapterNumber.text = "Глава " + ChapterNumbers[chapter];
            chapterName.text = ChapterNames[chapter];
            Rect? view = ChapterViews[chapter];
            chapterView.gameObject.SetActive(view.HasValue && chapterView.texture != null);
            if (view.HasValue)
            {
                Rect r = view.Value;
                Vector2 size = MenuStyle.PictureSize;
                chapterView.uvRect = new Rect(r.x / size.x, 1f - r.yMax / size.y, r.width / size.x, r.height / size.y);
            }

            LevelRecord record = FindRecord(selectedIndex);
            string title = catalog.Levels[selectedIndex].title;
            levelTitle.text = $"{selectedIndex + 1}. {title}";
            levelRecord.text = record != null && record.completed
                ? $"{StarLine(record.stars)}   рекорд {FormatTime(record.bestTimeSeconds)}"
                : "ещё не пройден";

            string verb = selected.State == NodeState.Done ? "ИГРАТЬ" : selectedIndex == 0 && FindRecord(0) == null ? "НАЧАТЬ" : "ПРОДОЛЖИТЬ";
            // The level itself is named on the card.
            playText.text = verb;
        }

        private static int ChapterOf(int index)
        {
            int chapter = 0;
            for (int i = 0; i < ChapterStarts.Length; i++)
            {
                if (index >= ChapterStarts[i])
                {
                    chapter = i;
                }
            }
            return chapter;
        }

        private LevelRecord FindRecord(int index)
        {
            foreach (LevelRecord record in progress.records)
            {
                if (record.levelIndex == index)
                {
                    return record;
                }
            }
            return null;
        }

        private static string StarLine(int stars)
        {
            var line = new System.Text.StringBuilder();
            for (int i = 0; i < StarsPerLevel; i++)
            {
                line.Append(i < stars ? '★' : '☆');
            }
            return line.ToString();
        }

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.RoundToInt(seconds);
            return whole >= 3600 ? $"{whole / 3600}:{whole / 60 % 60:00}:{whole % 60:00}" : $"{whole / 60}:{whole % 60:00}";
        }

        // Text on the picture: sized to its box, with a dark rim so it reads over the painting.
        private static Text AddText(Transform parent, string value, Color color, TextAnchor alignment, int maxSize, Font font)
        {
            Text text = MenuStyle.AddLabel(parent, value, maxSize);
            text.font = font;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 6;
            text.resizeTextMaxSize = maxSize;
            text.GetComponent<Outline>().effectColor = Shadow;
            return text;
        }

        // ----------------------------------------------------------------------------------------- animation

        private void Update()
        {
            float t = Time.unscaledTime;
            if (helmet != null && levelCount > 0)
            {
                // The helmet bobs gently over the chosen circle: its anchors place it, the offset carries the bob.
                helmet.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(t * 2.4f) * 2.5f);
            }

            if (currentGlow != null && currentGlow.gameObject.activeSelf)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
                currentGlow.color = new Color(0.3f, 0.95f, 1f, 0.45f + 0.35f * pulse);
                currentGlow.rectTransform.localScale = Vector3.one * (0.92f + 0.12f * pulse);
            }

            if (poppingIndex >= 0 && poppingIndex < nodes.Count)
            {
                float k = Mathf.Clamp01((t - popStart) / 0.6f);
                // Out of nothing, a little past full size, and back.
                float scale = k < 1f ? Mathf.Sin(k * Mathf.PI * 0.75f) / Mathf.Sin(Mathf.PI * 0.75f) : 1f;
                nodes[poppingIndex].Circle.rectTransform.localScale = Vector3.one * Mathf.Max(scale, 0.01f);
                if (k >= 1f)
                {
                    nodes[poppingIndex].Circle.rectTransform.localScale = Vector3.one;
                    poppingIndex = -1;
                }
            }
        }

        // --------------------------------------------------------------------------------------- start over

        private void ShowConfirmModal()
        {
            confirmModal.SetActive(true);
        }

        private void HideConfirmModal()
        {
            if (confirmModal != null)
            {
                confirmModal.SetActive(false);
            }
        }

        // Progress is wiped and the legend will be told again; the settings stay as they are.
        private void ExecuteStartOver()
        {
            HideConfirmModal();
            StoryScreen.Seen = false;
            ResetRequested?.Invoke();
            selectedIndex = 0;
            lastSeenUnlocked = 0;
            poppingIndex = -1;
            Refresh();
            ShowToast("Прогресс сброшен");
        }

        private void BuildConfirmModal()
        {
            confirmModal = new GameObject("Confirm Start Over", typeof(RectTransform));
            confirmModal.transform.SetParent(transform, false);
            MenuStyle.Stretch((RectTransform)confirmModal.transform);

            // The scrim catches every tap behind the dialog.
            var scrim = confirmModal.AddComponent<Image>();
            scrim.color = new Color(0.03f, 0.03f, 0.04f, 0.85f);
            scrim.raycastTarget = true;

            var box = new GameObject("Dialog").AddComponent<Image>();
            box.transform.SetParent(confirmModal.transform, false);
            box.color = new Color(0.1f, 0.09f, 0.08f, 0.98f);
            Center(box.rectTransform, Vector2.zero, new Vector2(760f, 380f));
            var border = box.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.85f, 0.6f, 0.25f, 0.95f);
            border.effectDistance = new Vector2(4f, 4f);

            Text title = AddText(box.transform, "Начать игру заново?", Gold, TextAnchor.MiddleCenter, 40, serif);
            Center(title.rectTransform, new Vector2(0f, 130f), new Vector2(700f, 60f));

            Text desc = AddText(box.transform,
                "Все пройденные уровни, рекорды времени и звёзды будут удалены безвозвратно.", Cream, TextAnchor.MiddleCenter, 28, serif);
            Center(desc.rectTransform, new Vector2(0f, 30f), new Vector2(640f, 110f));

            Button cancel = MenuStyle.CreatePlateButton(box.transform, "ОТМЕНА", new Vector2(280f, 70f), HideConfirmModal);
            Center((RectTransform)cancel.transform, new Vector2(-165f, -115f), new Vector2(280f, 70f));

            Button confirm = MenuStyle.CreatePlateButton(box.transform, "СБРОСИТЬ", new Vector2(280f, 70f), ExecuteStartOver);
            Center((RectTransform)confirm.transform, new Vector2(165f, -115f), new Vector2(280f, 70f));
            confirm.GetComponent<Image>().color = new Color(1f, 0.55f, 0.48f);

            confirmModal.SetActive(false);
        }

        private void BuildToast()
        {
            var toast = new GameObject("Toast").AddComponent<Image>();
            toast.transform.SetParent(transform, false);
            toast.color = new Color(0.08f, 0.07f, 0.06f, 0.94f);
            toast.raycastTarget = false;
            Center(toast.rectTransform, new Vector2(0f, 250f), new Vector2(620f, 64f));
            var outline = toast.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.6f, 0.25f, 0.9f);
            outline.effectDistance = new Vector2(2f, 2f);

            toastText = AddText(toast.transform, string.Empty, Cream, TextAnchor.MiddleCenter, 30, serif);
            MenuStyle.Stretch(toastText.rectTransform);
            toast.gameObject.SetActive(false);
        }

        private void ShowToast(string message)
        {
            toastText.text = message;
            toastText.transform.parent.gameObject.SetActive(true);
            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), 2.2f);
        }

        private void HideToast()
        {
            if (toastText != null)
            {
                toastText.transform.parent.gameObject.SetActive(false);
            }
        }

        // Centred on the parent at an offset, in canvas units.
        private static void Center(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
