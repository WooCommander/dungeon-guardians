using System;
using DungeonGuardians.Core;
using DungeonGuardians.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // "Прогресс" from the start screen: how far the player has come (levels, stars, time) and the only way to start
    // the game over, behind a confirmation. Drawn in code in the stone-and-gold look of the other menus.
    public sealed class ProgressScreen : MonoBehaviour
    {
        private const int StarsPerLevel = 3;
        private static readonly Color GoldColor = new Color(1f, 0.84f, 0.42f);
        private static readonly Color TextColor = new Color(0.9f, 0.88f, 0.84f);
        private static readonly Color PanelColor = new Color(0.1f, 0.09f, 0.08f, 0.97f);
        private static readonly Color DangerColor = new Color(0.62f, 0.22f, 0.16f, 1f);

        private PlayerProgress progress;
        private LevelCatalog catalog;
        private Text levelsValue;
        private Text starsValue;
        private Text timeValue;
        private Image barFill;
        private Text percentText;
        private GameObject confirmModal;
        private Text toastText;

        // The player confirmed starting over: progress is to be wiped. The screen redraws itself right after.
        public event Action ResetRequested;

        public static ProgressScreen Create(Transform parent)
        {
            var root = new GameObject("Progress", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            MenuStyle.Stretch((RectTransform)root.transform);
            var screen = root.AddComponent<ProgressScreen>();
            screen.Build();
            root.SetActive(false);
            return screen;
        }

        public void Open(PlayerProgress currentProgress, LevelCatalog levelCatalog)
        {
            progress = currentProgress;
            catalog = levelCatalog;
            HideConfirmModal();
            HideToast();
            Refresh();
            gameObject.SetActive(true);
        }

        public void Close()
        {
            HideConfirmModal();
            gameObject.SetActive(false);
        }

        // Android's back button: closes the confirmation first, then the screen.
        public void Back()
        {
            if (confirmModal.activeSelf)
            {
                HideConfirmModal();
            }
            else
            {
                Close();
            }
        }

        private void Build()
        {
            // The blurred dungeon behind, darkened, catches every tap meant for the menu underneath.
            Image backing = MenuStyle.CreatePicture(transform, "Backgrounds/settings_blur", AspectRatioFitter.AspectMode.EnvelopeParent);
            backing.raycastTarget = true;
            var shade = new GameObject("Shade").AddComponent<Image>();
            shade.transform.SetParent(transform, false);
            shade.color = new Color(0f, 0f, 0f, 0.45f);
            shade.raycastTarget = false;
            MenuStyle.Stretch(shade.rectTransform);

            RectTransform panel = AddPanel(transform, "Panel", new Vector2(860f, 600f));

            Text title = MenuStyle.AddLabel(panel, "ПРОГРЕСС", 52);
            title.color = GoldColor;
            Place(title.rectTransform, new Vector2(0f, 235f), new Vector2(800f, 80f));

            levelsValue = AddStatRow(panel, "Пройдено уровней", 130f);
            starsValue = AddStatRow(panel, "Звёзды", 65f);
            timeValue = AddStatRow(panel, "Время прохождения", 0f);

            // The share of levels passed as a golden bar.
            var track = new GameObject("Track").AddComponent<Image>();
            track.transform.SetParent(panel, false);
            track.color = new Color(0.04f, 0.04f, 0.04f, 1f);
            track.raycastTarget = false;
            Place(track.rectTransform, new Vector2(-40f, -80f), new Vector2(620f, 26f));
            var trackBorder = track.gameObject.AddComponent<Outline>();
            trackBorder.effectColor = new Color(0.55f, 0.4f, 0.2f, 0.9f);
            trackBorder.effectDistance = new Vector2(2f, 2f);

            barFill = new GameObject("Fill").AddComponent<Image>();
            barFill.transform.SetParent(track.transform, false);
            barFill.sprite = Resources.Load<Sprite>("UI/settings_fill");
            barFill.color = barFill.sprite != null ? Color.white : GoldColor;
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFill.raycastTarget = false;
            MenuStyle.Stretch(barFill.rectTransform);
            barFill.rectTransform.offsetMin = new Vector2(3f, 3f);
            barFill.rectTransform.offsetMax = new Vector2(-3f, -3f);

            percentText = MenuStyle.AddLabel(panel, "0%", 30);
            percentText.color = GoldColor;
            percentText.alignment = TextAnchor.MiddleLeft;
            Place(percentText.rectTransform, new Vector2(330f, -80f), new Vector2(120f, 40f));

            Button back = MenuStyle.CreatePlateButton(panel, "НАЗАД", new Vector2(360f, 78f), Close);
            Place((RectTransform)back.transform, new Vector2(-195f, -215f), new Vector2(360f, 78f));

            Button reset = MenuStyle.CreatePlateButton(panel, "НАЧАТЬ ЗАНОВО", new Vector2(360f, 78f), ShowConfirmModal);
            Place((RectTransform)reset.transform, new Vector2(195f, -215f), new Vector2(360f, 78f));
            reset.GetComponent<Image>().color = new Color(1f, 0.62f, 0.55f);

            BuildToast();
            BuildConfirmModal();
        }

        private void Refresh()
        {
            if (progress == null)
            {
                return;
            }

            int total = catalog != null ? catalog.Levels.Count : 0;
            int completed = progress.GetCompletedCount();
            float time = 0f;
            foreach (LevelRecord record in progress.records)
            {
                if (record.completed)
                {
                    time += record.bestTimeSeconds;
                }
            }

            float ratio = total > 0 ? Mathf.Clamp01((float)completed / total) : 0f;
            levelsValue.text = $"{completed} из {total}";
            starsValue.text = $"★ {progress.GetTotalStars()} из {total * StarsPerLevel}";
            timeValue.text = completed > 0 ? FormatTime(time) : "—";
            barFill.fillAmount = ratio;
            percentText.text = Mathf.RoundToInt(ratio * 100f) + "%";
        }

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.RoundToInt(seconds);
            return whole >= 3600
                ? $"{whole / 3600}:{whole / 60 % 60:00}:{whole % 60:00}"
                : $"{whole / 60}:{whole % 60:00}";
        }

        // A caption on the left and its value on the right, on one line of the panel.
        private static Text AddStatRow(RectTransform panel, string caption, float y)
        {
            Text label = MenuStyle.AddLabel(panel, caption, 32);
            label.color = TextColor;
            label.alignment = TextAnchor.MiddleLeft;
            Place(label.rectTransform, new Vector2(-110f, y), new Vector2(520f, 50f));

            Text value = MenuStyle.AddLabel(panel, string.Empty, 34);
            value.color = GoldColor;
            value.alignment = TextAnchor.MiddleRight;
            Place(value.rectTransform, new Vector2(230f, y), new Vector2(300f, 50f));
            return value;
        }

        private static RectTransform AddPanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(name).AddComponent<Image>();
            panel.transform.SetParent(parent, false);
            panel.color = PanelColor;
            Place(panel.rectTransform, Vector2.zero, size);
            var border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.85f, 0.6f, 0.25f, 0.95f);
            border.effectDistance = new Vector2(4f, 4f);
            return panel.rectTransform;
        }

        // Centred on the parent at an offset, in canvas units.
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

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
            Refresh();
            ShowToast("ПРОГРЕСС СБРОШЕН");
        }

        private void BuildConfirmModal()
        {
            confirmModal = new GameObject("ConfirmModal", typeof(RectTransform));
            confirmModal.transform.SetParent(transform, false);
            MenuStyle.Stretch((RectTransform)confirmModal.transform);

            var scrim = confirmModal.AddComponent<Image>();
            scrim.color = new Color(0.04f, 0.05f, 0.07f, 0.85f);
            scrim.raycastTarget = true;

            RectTransform box = AddPanel(confirmModal.transform, "DialogBox", new Vector2(760f, 380f));

            Text title = MenuStyle.AddLabel(box, "СБРОСИТЬ ВЕСЬ ПРОГРЕСС?", 34);
            title.color = GoldColor;
            Place(title.rectTransform, new Vector2(0f, 135f), new Vector2(700f, 60f));

            Text desc = MenuStyle.AddLabel(box,
                "Все пройденные уровни, рекорды времени и заработанные звёзды будут удалены безвозвратно.", 24);
            desc.color = TextColor;
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(desc.rectTransform, new Vector2(0f, 30f), new Vector2(660f, 120f));

            Button cancel = MenuStyle.CreatePlateButton(box, "ОТМЕНА", new Vector2(280f, 70f), HideConfirmModal);
            Place((RectTransform)cancel.transform, new Vector2(-165f, -115f), new Vector2(280f, 70f));

            Button confirm = MenuStyle.CreatePlateButton(box, "ДА, СБРОСИТЬ", new Vector2(280f, 70f), ExecuteStartOver);
            Place((RectTransform)confirm.transform, new Vector2(165f, -115f), new Vector2(280f, 70f));
            confirm.GetComponent<Image>().color = DangerColor;

            confirmModal.SetActive(false);
        }

        private void BuildToast()
        {
            var toast = new GameObject("Toast").AddComponent<Image>();
            toast.transform.SetParent(transform, false);
            toast.color = new Color(0.1f, 0.4f, 0.2f, 0.95f);
            toast.raycastTarget = false;
            Place(toast.rectTransform, new Vector2(0f, 360f), new Vector2(560f, 60f));
            var outline = toast.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 1f, 0.6f, 0.7f);
            outline.effectDistance = new Vector2(2f, 2f);

            toastText = MenuStyle.AddLabel(toast.transform, string.Empty, 26);
            toastText.color = Color.white;
            toast.gameObject.SetActive(false);
        }

        private void ShowToast(string message)
        {
            toastText.text = message;
            toastText.transform.parent.gameObject.SetActive(true);
            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), 2.5f);
        }

        private void HideToast()
        {
            if (toastText != null)
            {
                toastText.transform.parent.gameObject.SetActive(false);
            }
        }
    }
}
