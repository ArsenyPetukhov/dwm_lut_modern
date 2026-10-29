using System.Collections.Generic;

namespace DwmLutModern.Services
{
    /// <summary>A selectable UI language.</summary>
    public sealed class LanguageOption
    {
        public LanguageOption(string code, string name) { Code = code; Name = name; }
        public string Code { get; }
        public string Name { get; }
    }

    /// <summary>
    /// In-app localization. Strings are keyed messageKey -> (language -> text) and resolved
    /// against the current language, falling back to English so nothing is ever blank. Short
    /// chrome (nav, buttons, labels, section headers) is translated into all ten languages;
    /// longer help paragraphs are English + Russian with English fallback for the rest.
    /// </summary>
    public static class LocalizationService
    {
        public static IReadOnlyList<LanguageOption> Languages { get; } = new[]
        {
            new LanguageOption("en", "English"),
            new LanguageOption("ru", "Русский"),
            new LanguageOption("es", "Español"),
            new LanguageOption("fr", "Français"),
            new LanguageOption("de", "Deutsch"),
            new LanguageOption("pt", "Português"),
            new LanguageOption("it", "Italiano"),
            new LanguageOption("zh", "中文"),
            new LanguageOption("ja", "日本語"),
            new LanguageOption("ko", "한국어"),
        };

        private static string _current = "en";
        public static string Current => _current;

        public static void SetLanguage(string? code)
        {
            _current = string.IsNullOrWhiteSpace(code) ? "en" : code!.Split('-')[0].ToLowerInvariant();
        }

        public static string T(string key)
        {
            // Table is keyed messageKey -> (language -> text).
            if (Table.TryGetValue(key, out var byLang))
            {
                if (byLang.TryGetValue(_current, out var value) && !string.IsNullOrEmpty(value)) return value;
                if (byLang.TryGetValue("en", out var en)) return en;
            }
            return key;
        }

        private static readonly Dictionary<string, Dictionary<string, string>> Table = Build();

        private static Dictionary<string, Dictionary<string, string>> Build()
        {
            var t = new Dictionary<string, Dictionary<string, string>>();

            // Full ten-language entry (short chrome).
            void Add(string key, string en, string ru, string es, string fr, string de, string pt, string it, string zh, string ja, string ko)
            {
                t[key] = new Dictionary<string, string>
                {
                    ["en"] = en, ["ru"] = ru, ["es"] = es, ["fr"] = fr, ["de"] = de,
                    ["pt"] = pt, ["it"] = it, ["zh"] = zh, ["ja"] = ja, ["ko"] = ko
                };
            }

            // English + Russian entry; other languages fall back to English (for long help text).
            void Er(string key, string en, string ru)
            {
                t[key] = new Dictionary<string, string> { ["en"] = en, ["ru"] = ru };
            }

            // ---- Navigation ----
            Add("Nav.Home", "Home", "Главная", "Inicio", "Accueil", "Start", "Início", "Home", "主页", "ホーム", "홈");
            Add("Nav.Luts", "LUT library", "Библиотека LUT", "Biblioteca de LUT", "Bibliothèque LUT", "LUT-Bibliothek", "Biblioteca de LUT", "Libreria LUT", "LUT 库", "LUT ライブラリ", "LUT 라이브러리");
            Add("Nav.Settings", "Settings", "Настройки", "Ajustes", "Paramètres", "Einstellungen", "Configurações", "Impostazioni", "设置", "設定", "설정");
            Add("Nav.About", "About", "О программе", "Acerca de", "À propos", "Über", "Sobre", "Info", "关于", "情報", "정보");

            // ---- Actions ----
            Add("Action.Apply", "Apply", "Применить", "Aplicar", "Appliquer", "Anwenden", "Aplicar", "Applica", "应用", "適用", "적용");
            Add("Action.Disable", "Disable", "Отключить", "Desactivar", "Désactiver", "Deaktivieren", "Desativar", "Disattiva", "禁用", "無効化", "비활성화");
            Add("Action.Browse", "Browse", "Обзор", "Examinar", "Parcourir", "Durchsuchen", "Procurar", "Sfoglia", "浏览", "参照", "찾아보기");
            Add("Action.Clear", "Clear", "Очистить", "Borrar", "Effacer", "Löschen", "Limpar", "Cancella", "清除", "クリア", "지우기");
            Add("Action.Set", "Set", "Задать", "Definir", "Définir", "Festlegen", "Definir", "Imposta", "设置", "設定", "설정");
            Add("Action.Import", "Import LUTs…", "Импорт LUT…", "Importar LUT…", "Importer des LUT…", "LUTs importieren…", "Importar LUTs…", "Importa LUT…", "导入 LUT…", "LUT を読み込む…", "LUT 가져오기…");
            Add("Action.Identify", "Identify", "Определить", "Identificar", "Identifier", "Identifizieren", "Identificar", "Identifica", "识别", "識別", "식별");
            Add("Action.Recheck", "Recheck", "Проверить", "Recomprobar", "Revérifier", "Erneut prüfen", "Verificar", "Ricontrolla", "重新检测", "再確認", "다시 확인");

            // ---- SDR dithering ----
            Add("Dither.Section", "SDR dithering", "Дизеринг SDR", "Tramado SDR", "Tramage SDR", "SDR-Dithering", "Pontilhamento SDR", "Dithering SDR", "SDR 抖动", "SDR ディザリング", "SDR 디더링");
            Add("Dither.Mode", "Dithering mode", "Режим дизеринга", "Modo de tramado", "Mode de tramage", "Dithering-Modus", "Modo de pontilhamento", "Modalità dithering", "抖动模式", "ディザリング方式", "디더링 모드");
            Add("Dither.BlueNoise", "Blue noise (default)", "Синий шум (по умолчанию)", "Ruido azul (predeterminado)", "Bruit bleu (par défaut)", "Blaues Rauschen (Standard)", "Ruído azul (padrão)", "Rumore blu (predefinito)", "蓝噪声（默认）", "ブルーノイズ（既定）", "블루 노이즈 (기본값)");
            Add("Dither.ErrorDiffusion", "Error diffusion (experimental)", "Диффузия ошибки (экспериментально)", "Difusión de error (experimental)", "Diffusion d’erreur (expérimental)", "Fehlerdiffusion (experimentell)", "Difusão de erro (experimental)", "Diffusione dell’errore (sperimentale)", "误差扩散（实验性）", "誤差拡散（試験的）", "오차 확산 (실험적)");
            Add("Dither.Limits", "4K, HDR and fullscreen", "4K, HDR и полный экран", "4K, HDR y pantalla completa", "4K, HDR et plein écran", "4K, HDR und Vollbild", "4K, HDR e tela cheia", "4K, HDR e schermo intero", "4K、HDR 与全屏", "4K・HDR・全画面", "4K, HDR 및 전체 화면");
            Er("Dither.Precision", "SDR rounding follows the screen buffer: 8-bit or 10-bit. This does not change your display's output bit depth.",
                "Точность SDR соответствует экранному буферу: 8 или 10 бит. Эта настройка не меняет разрядность вывода дисплея.");
            Er("Dither.BlueNoiseDetail", "Default. Low GPU cost and a stable noise pattern. Recommended for normal desktop and video playback.",
                "По умолчанию. Низкая нагрузка на GPU и стабильный рисунок шума. Рекомендуется для рабочего стола и видео.");
            Er("Dither.ErrorDiffusionDetail", "Tiled Sierra Lite for SDR. Uses more GPU time and may show block edges or shimmer during motion and partial redraws. Uses blue noise if unavailable.",
                "Плиточная диффузия Sierra Lite для SDR. Повышает нагрузку на GPU; при движении и частичной перерисовке возможны мерцание и границы блоков. Если режим недоступен, используется синий шум.");
            Er("Dither.Reapply", "Changing modes reapplies active LUTs and may briefly flicker.",
                "Смена режима повторно применяет активные LUT; возможно краткое мигание.");
            Er("Dither.FourK", "Designed for 4K buffers, including portrait displays. Error diffusion uses fixed 32 × 32 blocks, extra GPU memory and more processing time. High refresh rates may stutter; return to blue noise if they do.",
                "Оба режима рассчитаны на 4K, включая портретную ориентацию. Диффузия ошибки использует блоки 32 × 32; плавность при высокой частоте зависит от GPU. Если видео подтормаживает, выберите синий шум.");
            Er("Dither.Hdr", "These modes affect SDR only. HDR keeps its floating-point LUT path and needs a matching HDR LUT; no extra HDR dithering is added.",
                "Эти режимы относятся только к SDR. HDR сохраняет обработку LUT с плавающей точкой и требует отдельного HDR-LUT; дополнительный дизеринг HDR не добавляется.");
            Er("Dither.DirectX", "DirectX 11 and DirectX 12 apps can be corrected while Windows composites them. Direct scanout can bypass the LUT in either mode. For MPC-HC SDR playback, MPC Video Renderer with Swap effect set to Discard is a confirmed workaround.",
                "Коррекция DirectX 11 и DirectX 12 возможна, пока Windows выполняет композицию. Прямой вывод может обходить LUT в обоих режимах. Для SDR в MPC-HC подтверждён обходной путь: MPC Video Renderer с Swap effect = Discard.");

            // ---- Home ----
            Add("Home.Displays", "Displays", "Дисплеи", "Pantallas", "Écrans", "Anzeigen", "Monitores", "Schermi", "显示器", "ディスプレイ", "디스플레이");
            Add("Home.Active", "LUT active", "LUT активен", "LUT activo", "LUT actif", "LUT aktiv", "LUT ativo", "LUT attivo", "LUT 已启用", "LUT 有効", "LUT 활성");
            Add("Home.Disabled", "LUT disabled", "LUT отключён", "LUT desactivado", "LUT désactivé", "LUT deaktiviert", "LUT desativado", "LUT disattivato", "LUT 已禁用", "LUT 無効", "LUT 비활성");
            Add("Home.Applies", "Apply:", "Режим:", "Aplicar:", "Appliquer :", "Anwenden:", "Aplicar:", "Applica:", "应用：", "適用：", "적용:");
            Er("Home.DragHint", "Drag a LUT here from the LUT library, or use Browse. HDR LUTs apply only while Windows HDR is on.",
                "Перетащите LUT из библиотеки или нажмите «Обзор». HDR-LUT применяются только при включённом HDR в Windows.");
            Er("Home.Unavailable", "Status unavailable", "Статус недоступен");
            Er("Home.RunAsAdmin", "Run DWM LUT Modern as Administrator to control the LUT.",
                "Запустите DWM LUT Modern от имени администратора, чтобы управлять LUT.");
            Er("Home.ActiveDetail", "Your calibrated color profile is applied to the desktop.",
                "Ваш калиброванный цветовой профиль применён к рабочему столу.");
            Er("Home.AssignHint", "Assign a LUT to a display, then click Apply.",
                "Назначьте LUT дисплею и нажмите «Применить».");
            Er("Home.Applying", "LUT enabled (applying…)", "LUT включён (применяется…)");
            Er("Home.WillApply", "Enabled; the profile will apply when DWM is ready.",
                "Включено; профиль применится, когда DWM будет готов.");

            // ---- Mode (per-display domain policy) ----
            Add("Mode.Auto", "Auto (SDR + HDR)", "Авто (SDR + HDR)", "Auto (SDR + HDR)", "Auto (SDR + HDR)", "Auto (SDR + HDR)", "Auto (SDR + HDR)", "Auto (SDR + HDR)", "自动 (SDR + HDR)", "自動 (SDR + HDR)", "자동 (SDR + HDR)");
            Add("Mode.SdrOnly", "SDR only", "Только SDR", "Solo SDR", "SDR uniquement", "Nur SDR", "Apenas SDR", "Solo SDR", "仅 SDR", "SDR のみ", "SDR 전용");
            Add("Mode.HdrOnly", "HDR only", "Только HDR", "Solo HDR", "HDR uniquement", "Nur HDR", "Apenas HDR", "Solo HDR", "仅 HDR", "HDR のみ", "HDR 전용");

            // ---- Settings ----
            Add("Settings.Appearance", "Appearance", "Оформление", "Apariencia", "Apparence", "Darstellung", "Aparência", "Aspetto", "外观", "外観", "모양");
            Add("Settings.Theme", "Theme", "Тема", "Tema", "Thème", "Design", "Tema", "Tema", "主题", "テーマ", "테마");
            Add("Settings.Language", "Language", "Язык", "Idioma", "Langue", "Sprache", "Idioma", "Lingua", "语言", "言語", "언어");
            Add("Settings.Startup", "Startup", "Автозапуск", "Inicio", "Démarrage", "Autostart", "Inicialização", "Avvio", "启动", "起動", "시작");
            Add("Settings.Hotkeys", "Hotkeys", "Горячие клавиши", "Atajos", "Raccourcis", "Tastenkürzel", "Atalhos", "Scorciatoie", "快捷键", "ホットキー", "단축키");
            Add("Settings.Advanced", "Advanced", "Дополнительно", "Avanzado", "Avancé", "Erweitert", "Avançado", "Avanzate", "高级", "詳細", "고급");
            Add("Settings.StartWithWindows", "Start with Windows", "Запускать с Windows", "Iniciar con Windows", "Démarrer avec Windows", "Mit Windows starten", "Iniciar com o Windows", "Avvia con Windows", "随 Windows 启动", "Windows と同時に起動", "Windows 시작 시 실행");
            Add("Settings.StartMinimized", "Start minimized to tray", "Запускать свёрнутым в трей", "Iniciar minimizado en la bandeja", "Démarrer réduit dans la barre", "Minimiert im Infobereich starten", "Iniciar minimizado na bandeja", "Avvia ridotto nella barra", "启动时最小化到托盘", "トレイに最小化して起動", "트레이에 최소화하여 시작");
            Add("Settings.ThemeName", "Theme", "Тема", "Tema", "Thème", "Design", "Tema", "Tema", "主题", "テーマ", "테마");
            Add("Settings.DwmSupport", "DWM support", "Поддержка DWM", "Compatibilidad con DWM", "Prise en charge DWM", "DWM-Unterstützung", "Suporte a DWM", "Supporto DWM", "DWM 支持", "DWM サポート", "DWM 지원");
            Add("Settings.ToggleHotkey", "Toggle LUT on/off", "Вкл/выкл LUT", "Activar/desactivar LUT", "Activer/désactiver le LUT", "LUT ein/aus", "Ativar/desativar LUT", "Attiva/disattiva LUT", "开关 LUT", "LUT のオン/オフ", "LUT 켜기/끄기");
            Add("Settings.CycleHotkey", "Cycle saved LUTs", "Переключать сохранённые LUT", "Alternar LUT guardados", "Faire défiler les LUT", "Gespeicherte LUTs durchschalten", "Alternar LUTs salvos", "Scorri i LUT salvati", "循环切换已保存 LUT", "保存した LUT を切替", "저장된 LUT 순환");

            Er("Settings.ThemeDesc", "Follow Windows, or force light or dark.", "Следовать Windows или задать светлую/тёмную.");
            Er("Settings.LanguageDesc", "Interface language. Restart the app to fully apply.", "Язык интерфейса. Перезапустите приложение для полного применения.");
            Er("Settings.StartWithWindowsDesc", "Creates a logon task that restores your last choice. If you disabled the LUT, it stays disabled after restart.",
                "Создаёт задачу при входе, восстанавливающую ваш выбор. Если вы отключили LUT, он останется отключённым после перезагрузки.");
            Er("Settings.StartMinimizedDesc", "Launch hidden in the notification area. Requires 'Start with Windows'.",
                "Запуск скрытым в области уведомлений. Требуется «Запускать с Windows».");
            Er("Settings.ToggleHotkeyDesc", "A global shortcut to turn the LUT on or off.", "Глобальная комбинация для включения/выключения LUT.");
            Er("Settings.CycleHotkeyDesc", "Rotate the selected display's SDR LUT through its saved list.", "Переключает SDR-LUT выбранного дисплея по его сохранённому списку.");
            Er("Settings.CompatHeader", "Compatibility mode (only change if a Windows update breaks the LUT)",
                "Режим совместимости (меняйте, только если обновление Windows сломало LUT)");
            Er("Settings.Footer", "DWM LUT Modern · system-wide 3D LUT color correction. Requires administrator to inject into the Desktop Window Manager. Config is stored in %AppData%\\DwmLutModern.",
                "DWM LUT Modern · системная 3D-LUT цветокоррекция. Требуются права администратора для внедрения в Desktop Window Manager. Настройки хранятся в %AppData%\\DwmLutModern.");

            // ---- LUT library ----
            Add("Lut.Title", "LUT library", "Библиотека LUT", "Biblioteca de LUT", "Bibliothèque LUT", "LUT-Bibliothek", "Biblioteca de LUT", "Libreria LUT", "LUT 库", "LUT ライブラリ", "LUT 라이브러리");
            Add("Lut.AssignTo", "Assign to display:", "Назначить дисплею:", "Asignar a pantalla:", "Attribuer à l'écran :", "Anzeige zuweisen:", "Atribuir ao monitor:", "Assegna allo schermo:", "分配到显示器：", "ディスプレイに割当：", "디스플레이에 지정:");
            Add("Lut.ToSdr", "→ SDR", "→ SDR", "→ SDR", "→ SDR", "→ SDR", "→ SDR", "→ SDR", "→ SDR", "→ SDR", "→ SDR");
            Add("Lut.ToHdr", "→ HDR", "→ HDR", "→ HDR", "→ HDR", "→ HDR", "→ HDR", "→ HDR", "→ HDR", "→ HDR", "→ HDR");
            Er("Lut.Hint", "Import your calibrator's .cube (or DisplayCAL .txt) files, then use → SDR / → HDR to assign one to the selected display. You can also drag a LUT onto a display on the Home page.",
                "Импортируйте .cube (или DisplayCAL .txt) вашего калибратора, затем кнопками → SDR / → HDR назначьте LUT выбранному дисплею. Также можно перетащить LUT на дисплей на «Главной».");
            Er("Lut.Empty", "No LUTs imported yet. Click Import LUTs to add your .cube files.",
                "LUT ещё не импортированы. Нажмите «Импорт LUT», чтобы добавить ваши .cube.");

            // ---- About ----
            Add("About.Developer", "Developer", "Разработчик", "Desarrollador", "Développeur", "Entwickler", "Desenvolvedor", "Sviluppatore", "开发者", "開発者", "개발자");
            Add("About.Project", "Project", "Проект", "Proyecto", "Projet", "Projekt", "Projeto", "Progetto", "项目", "プロジェクト", "프로젝트");
            Add("About.Credits", "Credits", "Благодарности", "Créditos", "Crédits", "Danksagung", "Créditos", "Crediti", "致谢", "クレジット", "크레딧");
            Add("About.CheckUpdates", "Check for updates", "Проверить обновления", "Buscar actualizaciones", "Rechercher des mises à jour", "Nach Updates suchen", "Procurar atualizações", "Controlla aggiornamenti", "检查更新", "更新を確認", "업데이트 확인");
            Add("About.Open", "Open", "Открыть", "Abrir", "Ouvrir", "Öffnen", "Abrir", "Apri", "打开", "開く", "열기");
            Er("About.ProjectDesc", "System-wide 3D LUT color correction for the Windows desktop, in SDR and HDR, by injecting a shader into the Desktop Window Manager.",
                "Системная 3D-LUT цветокоррекция рабочего стола Windows в SDR и HDR путём внедрения шейдера в Desktop Window Manager.");

            // ---- Onboarding ----
            Add("Onb.GetStarted", "Get started", "Начать", "Empezar", "Commencer", "Los geht's", "Começar", "Inizia", "开始", "開始", "시작하기");
            Add("Onb.Next", "Next", "Далее", "Siguiente", "Suivant", "Weiter", "Próximo", "Avanti", "下一步", "次へ", "다음");
            Add("Onb.Back", "Back", "Назад", "Atrás", "Retour", "Zurück", "Voltar", "Indietro", "上一步", "戻る", "뒤로");
            Add("Onb.Finish", "Finish", "Готово", "Finalizar", "Terminer", "Fertig", "Concluir", "Fine", "完成", "完了", "완료");

            return t;
        }
    }
}
