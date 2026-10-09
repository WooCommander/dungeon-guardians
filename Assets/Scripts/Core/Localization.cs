using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGuardians.Core
{
    public enum AppLanguage
    {
        Auto = 0,
        Russian = 1,
        English = 2
    }

    // Centralized localization manager.
    // Automatically detects device system language (Russian / Belarusian / Ukrainian / Kazakh -> Russian, otherwise English),
    // and supports user preference override.
    public static class Localization
    {
        private const string LanguagePrefKey = "App_Language_Preference";
        private static AppLanguage currentLanguage;
        private static bool initialized;

        public static event Action LanguageChanged;

        public static AppLanguage CurrentLanguage
        {
            get
            {
                if (!initialized)
                {
                    Initialize();
                }
                return currentLanguage;
            }
        }

        public static bool IsRussian => CurrentLanguage == AppLanguage.Russian;

        public static void Initialize()
        {
            initialized = true;
            int saved = PlayerPrefs.GetInt(LanguagePrefKey, (int)AppLanguage.Auto);
            if (saved == (int)AppLanguage.Auto)
            {
                currentLanguage = DetectDeviceLanguage();
            }
            else
            {
                currentLanguage = (AppLanguage)saved;
            }
        }

        public static void SetLanguage(AppLanguage language)
        {
            PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
            PlayerPrefs.Save();
            currentLanguage = language == AppLanguage.Auto ? DetectDeviceLanguage() : language;
            LanguageChanged?.Invoke();
        }

        private static AppLanguage DetectDeviceLanguage()
        {
            SystemLanguage lang = Application.systemLanguage;
            if (lang == SystemLanguage.Russian || lang == SystemLanguage.Belarusian || lang == SystemLanguage.Ukrainian)
            {
                return AppLanguage.Russian;
            }
            return AppLanguage.English;
        }

        public static string T(string key, params object[] args)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (!strings.TryGetValue(key, out string[] translations))
            {
                return args.Length > 0 ? string.Format(key, args) : key;
            }

            int index = IsRussian ? 0 : 1;
            string text = index < translations.Length ? translations[index] : translations[0];
            return args.Length > 0 ? string.Format(text, args) : text;
        }

        public static string GetLevelTitle(string levelId, string defaultTitle)
        {
            if (levelTitles.TryGetValue(levelId ?? string.Empty, out string[] titles))
            {
                return IsRussian ? titles[0] : titles[1];
            }
            return defaultTitle;
        }

        public static string GetChapterTitle(int chapterIndex)
        {
            if (chapterTitles.TryGetValue(chapterIndex, out string[] titles))
            {
                return IsRussian ? titles[0] : titles[1];
            }
            return $"Chapter {chapterIndex + 1}";
        }

        public static string[] GetStoryParagraphs()
        {
            return IsRussian ? storyParagraphsRu : storyParagraphsEn;
        }

        private static readonly Dictionary<string, string[]> strings = new Dictionary<string, string[]>
        {
            // Main Menu
            { "menu_play", new[] { "ИГРАТЬ", "PLAY" } },
            { "menu_settings", new[] { "НАСТРОЙКИ", "SETTINGS" } },
            { "menu_exit", new[] { "ВЫХОД", "EXIT" } },

            // Settings
            { "settings_music", new[] { "Музыка", "Music" } },
            { "settings_sound", new[] { "Звуки", "Sound FX" } },
            { "settings_vibration", new[] { "Вибрация", "Vibration" } },
            { "settings_size", new[] { "Размер кнопок", "Controls Size" } },
            { "settings_opacity", new[] { "Прозрачность", "Opacity" } },
            { "settings_camera", new[] { "Масштаб камеры", "Camera Zoom" } },
            { "settings_phone", new[] { "Телефон · 7 рядов", "Phone · 7 rows" } },
            { "settings_tablet", new[] { "Планшет · 11 рядов", "Tablet · 11 rows" } },
            { "settings_cancel", new[] { "ОТМЕНА", "CANCEL" } },
            { "settings_done", new[] { "ГОТОВО", "DONE" } },

            // Story Screen
            { "story_hint", new[] { "нажми, чтобы продолжить", "tap to continue" } },
            { "story_skip", new[] { "ПРОПУСТИТЬ", "SKIP" } },
            { "story_go", new[] { "В ПУТЬ", "BEGIN" } },

            // Level Map
            { "map_header", new[] { "Путь искателя", "Explorer's Path" } },
            { "map_chapter", new[] { "Глава {0}", "Chapter {0}" } },
            { "map_progress", new[] { "Пройдено {0} из {1}", "Completed {0} of {1}" } },
            { "map_total_time", new[] { "Общее время {0}", "Total Time {0}" } },
            { "map_record", new[] { "рекорд {0}", "best {0}" } },
            { "map_not_passed", new[] { "ещё не пройден", "not completed" } },
            { "map_action_play", new[] { "ИГРАТЬ", "PLAY" } },
            { "map_action_start", new[] { "НАЧАТЬ", "START" } },
            { "map_action_continue", new[] { "ПРОДОЛЖИТЬ", "CONTINUE" } },
            { "map_reset", new[] { "НАЧАТЬ ЗАНОВО", "RESET PROGRESS" } },
            { "map_back", new[] { "НАЗАД", "BACK" } },
            { "map_toast_locked", new[] { "Сначала пройдите уровень {0}", "Complete level {0} first" } },
            { "map_toast_reset", new[] { "Прогресс сброшен", "Progress reset" } },
            { "map_modal_title", new[] { "Начать игру заново?", "Reset game progress?" } },
            { "map_modal_body", new[] { "Все пройденные уровни, рекорды времени и звёзды будут удалены безвозвратно.", "All completed levels, best times and stars will be permanently erased." } },
            { "map_modal_cancel", new[] { "ОТМЕНА", "CANCEL" } },
            { "map_modal_confirm", new[] { "СБРОСИТЬ", "RESET" } },

            // In-Game HUD & Victory
            { "hud_level", new[] { "Уровень {0}", "Level {0}" } },
            { "hud_victory_title", new[] { "Уровень пройден", "Level Completed" } },
            { "hud_victory_subtitle", new[] { "«{0}» — печать снята", "«{0}» — seal broken" } },
            { "hud_all_won_title", new[] { "Все залы пройдены", "All Halls Conquered" } },
            { "hud_all_won_subtitle", new[] { "Тёмное сердце запечатано навеки", "The Dark Heart is sealed forever" } },
            { "hud_defeat_caught", new[] { "Хранитель остановил тебя", "A Guardian Caught You" } },
            { "hud_defeat_buried", new[] { "Тебя замуровало в камне", "Buried In Stone" } },
            { "hud_last_life", new[] { "Осталась последняя жизнь", "Last life remaining!" } },
            { "hud_lives_left", new[] { "Осталось жизней: {0}", "Lives remaining: {0}" } },
            { "hud_btn_next", new[] { "СЛЕДУЮЩИЙ", "NEXT LEVEL" } },
            { "hud_btn_retry", new[] { "ЗАНОВО", "RETRY" } },
            { "hud_btn_map", new[] { "КАРТА", "MAP" } },
            { "hud_btn_menu", new[] { "МЕНЮ", "MENU" } },
            { "hud_star_time", new[] { "Время: {0}с (цель {1}с)", "Time: {0}s (par {1}s)" } },
            { "hud_star_lives", new[] { "Без потери жизней ({0}/{1})", "No deaths ({0}/{1})" } },
            { "hud_star_gold", new[] { "Всё золото собрано", "All gold collected" } },
            { "hud_new_best", new[] { "НОВЫЙ РЕКОРД!", "NEW RECORD!" } },
        };

        private static readonly Dictionary<string, string[]> levelTitles = new Dictionary<string, string[]>
        {
            { "level_01", new[] { "Первые залы", "The First Halls" } },
            { "level_02", new[] { "Каменные своды", "Stone Vaults" } },
            { "level_03", new[] { "Тайник в глубине", "Deep Stash" } },
            { "level_04", new[] { "Затерянный ход", "Lost Passage" } },
            { "level_05", new[] { "Двойная стража", "Double Guard" } },
            { "level_06", new[] { "Печать тишины", "Seal of Silence" } },
            { "level_07", new[] { "Затопленный зал", "Flooded Hall" } },
            { "level_08", new[] { "Огненные копи", "Fiery Mines" } },
            { "level_09", new[] { "Великая галерея", "Great Gallery" } },
            { "level_10", new[] { "Бездна стражей", "Abyss of Guardians" } },
            { "level_11", new[] { "Храм теней", "Temple of Shadows" } },
            { "level_12", new[] { "Лабиринт механизмов", "Labyrinth of Gears" } },
            { "level_13", new[] { "Сердце горы", "Heart of the Mountain" } },
            { "level_14", new[] { "Древние врата", "Ancient Gates" } },
            { "level_15", new[] { "Палата испытаний", "Chamber of Trials" } },
            { "level_16", new[] { "Тайный склеп", "Secret Crypt" } },
            { "level_17", new[] { "Владения титана", "Titan's Domain" } },
            { "level_18", new[] { "Финальная печать", "The Final Seal" } },
        };

        private static readonly Dictionary<int, string[]> chapterTitles = new Dictionary<int, string[]>
        {
            { 0, new[] { "Заброшенные шахты", "Abandoned Mines" } },
            { 1, new[] { "Подземный город", "Sunken City" } },
            { 2, new[] { "Затопленные своды", "Flooded Vaults" } },
            { 3, new[] { "Древние механизмы", "Ancient Mechanisms" } },
            { 4, new[] { "Огненная бездна", "Fiery Abyss" } },
        };

        private static readonly string[] storyParagraphsRu = new[]
        {
            "Много веков назад под горой построили город. Его жители обнаружили необычный минерал — чёрное сердце, " +
            "которое выделяло тепло и освещало подземные залы. Благодаря ему город процветал.",

            "Но постепенно из глубины стали доноситься голоса. Людям снились чужие воспоминания, рабочие исчезали, " +
            "а тоннели возникали там, где ещё вчера была сплошная скала.",

            "Тогда мастера создали каменных хранителей и запечатали нижние своды. Для печатей использовали золото: " +
            "оно сдерживало влияние того, что проснулось под городом. Жители ушли, а хранители остались охранять пустые залы.",

            "Со временем настоящее назначение города забылось. Осталась легенда о сокровищах."
        };

        private static readonly string[] storyParagraphsEn = new[]
        {
            "Many centuries ago, a magnificent city was carved beneath the mountain. Its dwellers uncovered a wondrous mineral — the Black Heart, " +
            "radiating warmth and light into the subterranean halls. The city flourished.",

            "Yet gradually, strange whispers began echoing from the depths. Citizens were plagued by alien dreams, miners vanished, " +
            "and mysterious tunnels carved themselves into solid stone overnight.",

            "The masters forged stone guardians and sealed the deepest vaults with ancient gold to quell the awakened entity. " +
            "The people departed, leaving the guardians to stand eternal vigil over the empty halls.",

            "With the passing of ages, the true purpose of the deep was lost to time. Only legends of boundless treasure remained."
        };
    }
}
