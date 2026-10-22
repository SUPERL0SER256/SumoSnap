using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SumoSnap;

public partial class PostCaptureWindow : Window
{
    private string _activeMode = "Chat"; // "Chat", "Logic", "Sudoku", "Pomodoro", "Calendar"
    private string _gameSystemPrompt = @"You are an analytical logic puzzle designer. Generate a concise, rigorous deduction puzzle with a single unambiguous answer. Strictly avoid elementary single-step sequences (like +2, x2, primes, Fibonacci) and avoid cliché word riddles (like shadow, echo, footsteps, clock). Output ONLY a valid JSON object without markdown. Schema: { ""puzzle"": ""the puzzle text"", ""answer"": ""the exact brief answer"" }. Keep the text concise.";

    private bool _suppressKeyClick = false;
    private static readonly SolidColorBrush _ledLitBrush = CreateFrozenBrush(0xFF, 0xFF, 0xFF);
    private static readonly SolidColorBrush _ledUnlitBrush = CreateFrozenBrush(0x22, 0x22, 0x28);

    private void UpdateActiveModeLights()
    {
        if (ChatLed == null) return;

        void SetLed(System.Windows.Shapes.Ellipse led, bool active)
        {
            led.Fill = active ? _ledLitBrush : _ledUnlitBrush;
            led.Effect = active
                ? new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.White,
                    BlurRadius = 5,
                    ShadowDepth = 0,
                    Opacity = 0.9
                }
                : null;
        }

        SetLed(CalendarLed, _activeMode == "Calendar");
        SetLed(PomodoroLed, _activeMode == "Pomodoro");
        SetLed(SudokuLed, _activeMode == "Sudoku");
        SetLed(LogicLed, _activeMode == "Logic");
        SetLed(ChatLed, _activeMode == "Chat");
    }

    private static void PlayPanelTransition(FrameworkElement panel)
    {
        panel.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        var scale = new ScaleTransform(1.0, 0.965);
        panel.RenderTransform = scale;

        var dur = new Duration(TimeSpan.FromMilliseconds(85));
        var ease = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };

        var opAnim = new System.Windows.Media.Animation.DoubleAnimation(0.18, 1.0, dur) { EasingFunction = ease };
        var syAnim = new System.Windows.Media.Animation.DoubleAnimation(0.965, 1.0, dur) { EasingFunction = ease };

        panel.BeginAnimation(UIElement.OpacityProperty, opAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, syAnim);
    }

    private void PlayModeSound()
    {
        if (_suppressKeyClick) return;
        HardwareAudio.PlayKeyClick();
    }

    private void HideAllPanels()
    {
        MainScroll.Visibility = Visibility.Collapsed;
        ChatInputBar.Visibility = Visibility.Collapsed;
        GamePanel.Visibility = Visibility.Collapsed;
        GameInputBar.Visibility = Visibility.Collapsed;
        SudokuPanel.Visibility = Visibility.Collapsed;
        SudokuInputBar.Visibility = Visibility.Collapsed;
        PomodoroPanel.Visibility = Visibility.Collapsed;
        PomodoroInputBar.Visibility = Visibility.Collapsed;
        CalendarPanel.Visibility = Visibility.Collapsed;
        CalendarInputBar.Visibility = Visibility.Collapsed;
    }

    // ==================== TOP ROTARY KNURLED ZOOM WHEEL ====================
    private bool _isDraggingZoomWheel = false;
    private double _lastWheelMouseX = 0;
    private double _wheelDragAccumulator = 0;
    private double _viewportZoom = 1.0;

    private void TopZoomWheel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            SetViewportZoom(1.0, zoomInSound: true);
            e.Handled = true;
            return;
        }

        _isDraggingZoomWheel = true;
        _lastWheelMouseX = e.GetPosition(this).X;
        _wheelDragAccumulator = 0;
        TopZoomWheel.CaptureMouse();
        e.Handled = true;
    }

    private void TopZoomWheel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingZoomWheel)
        {
            _isDraggingZoomWheel = false;
            TopZoomWheel.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void TopZoomWheel_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDraggingZoomWheel) return;

        double currentX = e.GetPosition(this).X;
        double dx = currentX - _lastWheelMouseX;
        _lastWheelMouseX = currentX;

        if (Math.Abs(dx) < 0.01) return;

        // Visually roll the knurled vertical ribs along with the horizontal drag
        if (WheelRibTransform != null)
        {
            double nextOffset = (WheelRibTransform.X + dx * 0.75) % 6.0;
            if (nextOffset < 0) nextOffset += 6.0;
            WheelRibTransform.X = nextOffset;
        }

        _wheelDragAccumulator += dx;
        const double pixelsPerDetent = 10.0;

        while (_wheelDragAccumulator >= pixelsPerDetent)
        {
            _wheelDragAccumulator -= pixelsPerDetent;
            StepViewportZoom(+0.05);
        }

        while (_wheelDragAccumulator <= -pixelsPerDetent)
        {
            _wheelDragAccumulator += pixelsPerDetent;
            StepViewportZoom(-0.05);
        }
    }

    private void TopZoomWheel_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0) return;

        bool zoomIn = e.Delta > 0;
        if (WheelRibTransform != null)
        {
            double nextOffset = (WheelRibTransform.X + (zoomIn ? 3.0 : -3.0)) % 6.0;
            if (nextOffset < 0) nextOffset += 6.0;
            WheelRibTransform.X = nextOffset;
        }

        StepViewportZoom(zoomIn ? +0.05 : -0.05);
        e.Handled = true;
    }

    private void StepViewportZoom(double delta)
    {
        double target = Math.Clamp(Math.Round((_viewportZoom + delta) * 20.0) / 20.0, 0.70, 1.45);
        SetViewportZoom(target, zoomInSound: delta > 0);
    }

    private void SetViewportZoom(double targetZoom, bool zoomInSound)
    {
        _viewportZoom = targetZoom;
        HardwareAudio.PlayWheelTick(zoomInSound);

        if (ViewportScaleTransform != null)
        {
            ViewportScaleTransform.ScaleX = _viewportZoom;
            ViewportScaleTransform.ScaleY = _viewportZoom;
        }

        if (ZoomHudBadge != null && ZoomHudText != null)
        {
            ZoomHudText.Text = $"ZOOM // {(int)Math.Round(_viewportZoom * 100)}%";
            var fade = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            fade.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteDoubleKeyFrame(1.0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fade.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteDoubleKeyFrame(1.0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650))));
            fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0.0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(950))));
            ZoomHudBadge.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }

    private void SideSwitch_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlaySideSwitch();
        _suppressKeyClick = true;
        try
        {
            switch (_activeMode)
            {
                case "Chat":
                    GameToggle_Click(sender, e);
                    break;
                case "Logic":
                    SudokuMode_Click(sender, e);
                    break;
                case "Sudoku":
                    PomodoroMode_Click(sender, e);
                    break;
                case "Pomodoro":
                    CalendarMode_Click(sender, e);
                    break;
                default:
                    ChatMode_Click(sender, e);
                    break;
            }
        }
        finally
        {
            _suppressKeyClick = false;
        }
    }

    private void ChatMode_Click(object sender, RoutedEventArgs e)
    {
        PlayModeSound();
        _activeMode = "Chat";
        UpdateActiveModeLights();
        HideAllPanels();
        MainScroll.Visibility = Visibility.Visible;
        ChatInputBar.Visibility = Visibility.Visible;
        PlayPanelTransition(MainScroll);
        ChatInput.Focus();
    }

    private static string FormatTriesPips(int triesLeft) => triesLeft switch
    {
        >= 3 => "● ● ●",
        2 => "● ● ○",
        1 => "● ○ ○",
        _ => "○ ○ ○"
    };

    private void UpdateGameLevelHeader(int streak)
    {
        if (GameModuleHeaderText == null) return;
        GameModuleHeaderText.Text = streak >= 6
            ? $"[ MODULE // DAILY_LOGIC // LEVEL: ADVANCED +{streak - 5} ]"
            : "[ MODULE // DAILY_LOGIC // LEVEL: STANDARD ]";
    }

    private async void GameToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == "Logic")
        {
            ChatMode_Click(sender, e);
            return;
        }

        PlayModeSound();
        _activeMode = "Logic";
        UpdateActiveModeLights();
        HideAllPanels();
        GamePanel.Visibility = Visibility.Visible;
        GameInputBar.Visibility = Visibility.Visible;
        PlayPanelTransition(GamePanel);

        var settings = SettingsManager.LoadSettings();
        StreakText.Text = $"Streak: {settings.DailyLogicStreak}";
        BestStreakText.Text = $"Best: {settings.HighestLogicStreak}";
        UpdateGameLevelHeader(settings.DailyLogicStreak);

        if (settings.LastPuzzleSolvedDate.Date == DateTime.Today)
        {
            GameText.Text = "You've already played today's puzzle! Come back tomorrow for a new challenge.";
            GameInput.Visibility = Visibility.Collapsed;
            GameSubmitButton.Visibility = Visibility.Collapsed;
            TriesIndicator.Visibility = Visibility.Collapsed;
        }
        else if (settings.DailyPuzzleDate.Date == DateTime.Today && !string.IsNullOrWhiteSpace(settings.DailyPuzzleText))
        {
            GameText.Text = settings.DailyPuzzleText;
            TriesIndicator.Text = FormatTriesPips(settings.DailyPuzzleAttemptsLeft);
            GameInput.Visibility = Visibility.Visible;
            GameSubmitButton.Visibility = Visibility.Visible;
            TriesIndicator.Visibility = Visibility.Visible;
            GameInput.IsEnabled = true;
            GameInput.Focus();
        }
        else
        {
            await StartNewGameAsync();
        }
    }

    // ==================== 6x6 MINI SUDOKU (DAILY ALGORITHMIC / 0 TOKENS) ====================
    private int[,] _sudokuInitial = new int[6, 6];
    private int[,] _sudokuCurrent = new int[6, 6];
    private System.Windows.Controls.Button[,] _sudokuButtons = new System.Windows.Controls.Button[6, 6];
    private bool _sudokuWon = false;

    private void SudokuMode_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == "Sudoku")
        {
            ChatMode_Click(sender, e);
            return;
        }

        PlayModeSound();
        _activeMode = "Sudoku";
        UpdateActiveModeLights();
        HideAllPanels();
        SudokuPanel.Visibility = Visibility.Visible;
        SudokuInputBar.Visibility = Visibility.Visible;
        PlayPanelTransition(SudokuPanel);

        LoadOrGenerateDailySudoku();
    }

    private void LoadOrGenerateDailySudoku()
    {
        var settings = SettingsManager.LoadSettings();

        // Reset streak if the user missed yesterday
        if (settings.LastSudokuSolvedDate != DateTime.MinValue &&
            settings.LastSudokuSolvedDate.Date < DateTime.Today.AddDays(-1))
        {
            settings.SudokuSolvedCount = 0;
            SettingsManager.SaveSettings(settings);
        }

        SudokuSolvedText.Text = $"Streak: {settings.SudokuSolvedCount}";
        SudokuBestText.Text = $"Best: {settings.BestSudokuStreak}";

        if (settings.DailySudokuDate.Date == DateTime.Today &&
            settings.DailySudokuInitial != null && settings.DailySudokuInitial.Length == 36 &&
            settings.DailySudokuCurrent != null && settings.DailySudokuCurrent.Length == 36)
        {
            for (int i = 0; i < 36; i++)
            {
                _sudokuInitial[i / 6, i % 6] = settings.DailySudokuInitial[i];
                _sudokuCurrent[i / 6, i % 6] = settings.DailySudokuCurrent[i];
            }

            _sudokuWon = settings.LastSudokuSolvedDate.Date == DateTime.Today;
            RenderSudokuBoard();

            if (_sudokuWon)
            {
                SudokuStatusText.Text = "You've solved today's Mini Sudoku! Come back tomorrow for a new grid.";
                SudokuClearButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                SudokuStatusText.Text = "Fill each row, column, and 2x3 box with 1-6. Click a cell to cycle.";
                SudokuClearButton.Visibility = Visibility.Visible;
            }
        }
        else
        {
            GenerateNewDailySudoku6x6(settings);
        }
    }

    private void SaveSudokuProgress(AppSettings settings)
    {
        settings.DailySudokuInitial = new int[36];
        settings.DailySudokuCurrent = new int[36];
        for (int i = 0; i < 36; i++)
        {
            settings.DailySudokuInitial[i] = _sudokuInitial[i / 6, i % 6];
            settings.DailySudokuCurrent[i] = _sudokuCurrent[i / 6, i % 6];
        }
        SettingsManager.SaveSettings(settings);
    }

    private void GenerateNewDailySudoku6x6(AppSettings settings)
    {
        _sudokuWon = false;
        SudokuClearButton.Visibility = Visibility.Visible;
        SudokuStatusText.Text = "Fill each row, column, and 2x3 box with 1-6. Click a cell to cycle.";

        int[,] baseGrid = new int[6, 6]
        {
            { 1, 2, 3, 4, 5, 6 },
            { 4, 5, 6, 1, 2, 3 },
            { 2, 3, 1, 5, 6, 4 },
            { 5, 6, 4, 2, 3, 1 },
            { 3, 1, 2, 6, 4, 5 },
            { 6, 4, 5, 3, 1, 2 }
        };

        // Deterministic daily seed so the board is uniquely tied to today's date
        int dateSeed = DateTime.Today.Year * 10000 + DateTime.Today.Month * 100 + DateTime.Today.Day;
        var rng = new Random(dateSeed);

        // Permute digits 1..6
        int[] map = Enumerable.Range(1, 6).OrderBy(_ => rng.Next()).ToArray();
        for (int r = 0; r < 6; r++)
            for (int c = 0; c < 6; c++)
                baseGrid[r, c] = map[baseGrid[r, c] - 1];

        // Shuffle rows within each 2-row band (bands: 0..1, 2..3, 4..5)
        for (int band = 0; band < 3; band++)
        {
            if (rng.Next(2) == 1)
            {
                int r1 = band * 2, r2 = band * 2 + 1;
                for (int c = 0; c < 6; c++)
                    (baseGrid[r1, c], baseGrid[r2, c]) = (baseGrid[r2, c], baseGrid[r1, c]);
            }
        }

        // Shuffle the 3 row bands
        int[] bandOrder = new[] { 0, 1, 2 }.OrderBy(_ => rng.Next()).ToArray();
        int[,] tempBandGrid = new int[6, 6];
        for (int b = 0; b < 3; b++)
        {
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 6; c++)
                    tempBandGrid[b * 2 + r, c] = baseGrid[bandOrder[b] * 2 + r, c];
        }
        baseGrid = tempBandGrid;

        // Shuffle columns within each 3-col stack (stacks: 0..2, 3..5)
        for (int stack = 0; stack < 2; stack++)
        {
            int[] colOrder = new[] { 0, 1, 2 }.OrderBy(_ => rng.Next()).ToArray();
            int[,] tempColGrid = (int[,])baseGrid.Clone();
            for (int c = 0; c < 3; c++)
                for (int r = 0; r < 6; r++)
                    baseGrid[r, stack * 3 + c] = tempColGrid[r, stack * 3 + colOrder[c]];
        }

        _sudokuInitial = (int[,])baseGrid.Clone();

        var allCells = Enumerable.Range(0, 36).OrderBy(_ => rng.Next()).Take(18).ToList();
        foreach (int idx in allCells)
        {
            _sudokuInitial[idx / 6, idx % 6] = 0;
        }

        _sudokuCurrent = (int[,])_sudokuInitial.Clone();
        settings.DailySudokuDate = DateTime.Today;
        SaveSudokuProgress(settings);
        RenderSudokuBoard();
    }

    private void RenderSudokuBoard()
    {
        SudokuGrid.Children.Clear();
        for (int r = 0; r < 6; r++)
        {
            for (int c = 0; c < 6; c++)
            {
                int row = r, col = c;
                bool isFixed = _sudokuInitial[r, c] != 0;
                int val = _sudokuCurrent[r, c];

                // Thicker borders between 2x3 sub-boxes
                double rightThick = (c == 2) ? 2.5 : 0.5;
                double bottomThick = (r == 1 || r == 3) ? 2.5 : 0.5;

                var cellBorder = new Border
                {
                    BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333338")),
                    BorderThickness = new Thickness(0.5, 0.5, rightThick, bottomThick),
                    Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isFixed ? "#1A1A1F" : "#0E0E11"))
                };

                var btn = new System.Windows.Controls.Button
                {
                    Content = val == 0 ? "" : val.ToString(),
                    FontSize = 15,
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isFixed ? "#888888" : "#FFFFFF")),
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = (isFixed || _sudokuWon) ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand
                };

                if (!isFixed && !_sudokuWon)
                {
                    btn.Click += (s, e) => OnSudokuCellClicked(row, col);
                }

                _sudokuButtons[r, c] = btn;
                cellBorder.Child = btn;
                SudokuGrid.Children.Add(cellBorder);
            }
        }
    }

    private void OnSudokuCellClicked(int r, int c)
    {
        if (_sudokuWon || _sudokuInitial[r, c] != 0) return;

        HardwareAudio.PlayKeyClick();
        _sudokuCurrent[r, c] = (_sudokuCurrent[r, c] + 1) % 7;
        _sudokuButtons[r, c].Content = _sudokuCurrent[r, c] == 0 ? "" : _sudokuCurrent[r, c].ToString();

        var settings = SettingsManager.LoadSettings();
        SaveSudokuProgress(settings);

        CheckSudokuBoardState(settings);
    }

    private void CheckSudokuBoardState(AppSettings settings)
    {
        bool allFilled = true;
        bool hasConflict = false;

        for (int r = 0; r < 6; r++)
        {
            for (int c = 0; c < 6; c++)
            {
                int v = _sudokuCurrent[r, c];
                if (v == 0)
                {
                    allFilled = false;
                    continue;
                }

                // Check row & col conflicts
                for (int k = 0; k < 6; k++)
                {
                    if (k != c && _sudokuCurrent[r, k] == v) hasConflict = true;
                    if (k != r && _sudokuCurrent[k, c] == v) hasConflict = true;
                }

                // Check 2x3 box conflict
                int br = (r / 2) * 2;
                int bc = (c / 3) * 3;
                for (int dr = 0; dr < 2; dr++)
                {
                    for (int dc = 0; dc < 3; dc++)
                    {
                        int nr = br + dr, nc = bc + dc;
                        if ((nr != r || nc != c) && _sudokuCurrent[nr, nc] == v)
                            hasConflict = true;
                    }
                }
            }
        }

        if (allFilled && !hasConflict)
        {
            _sudokuWon = true;
            HardwareAudio.PlayChime();
            settings.SudokuSolvedCount++;
            if (settings.SudokuSolvedCount > settings.BestSudokuStreak)
                settings.BestSudokuStreak = settings.SudokuSolvedCount;
            settings.LastSudokuSolvedDate = DateTime.Today;
            SaveSudokuProgress(settings);

            SudokuSolvedText.Text = $"Streak: {settings.SudokuSolvedCount}";
            SudokuBestText.Text = $"Best: {settings.BestSudokuStreak}";
            SudokuStatusText.Text = "You've solved today's Mini Sudoku! Come back tomorrow for a new grid.";
            SudokuClearButton.Visibility = Visibility.Collapsed;
            RenderSudokuBoard();
        }
        else if (allFilled && hasConflict)
        {
            SudokuStatusText.Text = "Some numbers conflict in a row, column, or 2x3 box.";
        }
        else
        {
            SudokuStatusText.Text = "Fill each row, column, and 2x3 box with 1-6. Click a cell to cycle.";
        }
    }

    private void SudokuClear_Click(object sender, RoutedEventArgs e)
    {
        if (_sudokuWon) return;
        HardwareAudio.PlayKeyClick();
        _sudokuCurrent = (int[,])_sudokuInitial.Clone();
        var settings = SettingsManager.LoadSettings();
        SaveSudokuProgress(settings);
        RenderSudokuBoard();
        SudokuStatusText.Text = "Board reset. Fill each row, column, and 2x3 box with 1-6.";
    }

    // ==================== POMODORO TIMER MODE ====================
    private System.Windows.Shapes.Ellipse[,]? _pomodoroDots;
    private static readonly SolidColorBrush _dotLitBrush = CreateFrozenBrush(0xE8, 0xE8, 0xEC);
    private static readonly SolidColorBrush _dotUnlitBrush = CreateFrozenBrush(0x1A, 0x1A, 0x20);

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static readonly Dictionary<char, string[]> _font5x7 = new()
    {
        ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
        ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
        ['2'] = new[] { "01110", "10001", "00001", "00110", "01000", "10000", "11111" },
        ['3'] = new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
        ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
        ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
        ['6'] = new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
        ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
        ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
        ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" }
    };

    private void EnsurePomodoroMatrixInitialized()
    {
        if (_pomodoroDots != null || PomodoroMatrixGrid == null) return;
        _pomodoroDots = new System.Windows.Shapes.Ellipse[7, 27];
        PomodoroMatrixGrid.Children.Clear();
        for (int r = 0; r < 7; r++)
        {
            for (int c = 0; c < 27; c++)
            {
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 5.5,
                    Height = 5.5,
                    Fill = _dotUnlitBrush,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                _pomodoroDots[r, c] = dot;
                PomodoroMatrixGrid.Children.Add(dot);
            }
        }
    }

    private void RenderPomodoroMatrix(string timeStr)
    {
        EnsurePomodoroMatrixInitialized();
        if (_pomodoroDots == null || timeStr.Length < 5) return;

        // Clear all dots first
        for (int r = 0; r < 7; r++)
            for (int c = 0; c < 27; c++)
                _pomodoroDots[r, c].Fill = _dotUnlitBrush;

        void DrawDigit(char ch, int startCol)
        {
            if (!_font5x7.TryGetValue(ch, out var rows)) return;
            for (int r = 0; r < 7; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    if (rows[r][c] == '1')
                        _pomodoroDots[r, startCol + c].Fill = _dotLitBrush;
                }
            }
        }

        DrawDigit(timeStr[0], 0);
        DrawDigit(timeStr[1], 6);

        // Colon at column 13, rows 2 and 4
        _pomodoroDots[2, 13].Fill = _dotLitBrush;
        _pomodoroDots[4, 13].Fill = _dotLitBrush;

        DrawDigit(timeStr[3], 16);
        DrawDigit(timeStr[4], 22);
    }

    private void PomodoroMode_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == "Pomodoro")
        {
            ChatMode_Click(sender, e);
            return;
        }

        PlayModeSound();
        _activeMode = "Pomodoro";
        UpdateActiveModeLights();
        HideAllPanels();
        PomodoroPanel.Visibility = Visibility.Visible;
        PomodoroInputBar.Visibility = Visibility.Visible;
        PlayPanelTransition(PomodoroPanel);
        SyncPomodoroUI();
    }

    public void SyncPomodoroUI()
    {
        if (PomodoroTimeText == null) return;

        if (App.IsPomodoroRunning && App.PomodoroEndTime.HasValue)
        {
            var remaining = App.PomodoroEndTime.Value - DateTime.Now;
            if (remaining.TotalSeconds < 0) remaining = TimeSpan.Zero;
            int mins = Math.Min(99, (int)remaining.TotalMinutes);
            int secs = remaining.Seconds;
            string formatted = $"{mins:D2}:{secs:D2}";
            PomodoroTimeText.Text = formatted;
            RenderPomodoroMatrix(formatted);
            PomodoroStatusLabel.Text = "Focusing...";
            PomodoroStartPauseButton.Content = "Pause";
        }
        else
        {
            int mins = Math.Min(99, App.PomodoroRemainingPausedSeconds / 60);
            int secs = App.PomodoroRemainingPausedSeconds % 60;
            string formatted = $"{mins:D2}:{secs:D2}";
            PomodoroTimeText.Text = formatted;
            RenderPomodoroMatrix(formatted);
            PomodoroStatusLabel.Text = "Ready";
            PomodoroStartPauseButton.Content = "Start";
        }
    }

    private void SetPomodoroMinutes(int minutes)
    {
        HardwareAudio.PlayKeyClick();
        if (minutes < 5) minutes = 5;
        if (minutes > 120) minutes = 120;
        App.PomodoroEndTime = null;
        App.PomodoroDurationMinutes = minutes;
        App.PomodoroRemainingPausedSeconds = minutes * 60;
        SyncPomodoroUI();
    }

    private void PomodoroMinus_Click(object sender, RoutedEventArgs e) => SetPomodoroMinutes(App.PomodoroDurationMinutes - 5);
    private void PomodoroPlus_Click(object sender, RoutedEventArgs e) => SetPomodoroMinutes(App.PomodoroDurationMinutes + 5);
    private void PomodoroPreset15_Click(object sender, RoutedEventArgs e) => SetPomodoroMinutes(15);
    private void PomodoroPreset25_Click(object sender, RoutedEventArgs e) => SetPomodoroMinutes(25);
    private void PomodoroPreset45_Click(object sender, RoutedEventArgs e) => SetPomodoroMinutes(45);

    private void PomodoroStartPause_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlayKeyClick();
        if (App.IsPomodoroRunning && App.PomodoroEndTime.HasValue)
        {
            var remaining = App.PomodoroEndTime.Value - DateTime.Now;
            App.PomodoroRemainingPausedSeconds = Math.Max(1, (int)remaining.TotalSeconds);
            App.PomodoroEndTime = null;
        }
        else
        {
            if (App.PomodoroRemainingPausedSeconds <= 0)
                App.PomodoroRemainingPausedSeconds = App.PomodoroDurationMinutes * 60;
            App.PomodoroEndTime = DateTime.Now.AddSeconds(App.PomodoroRemainingPausedSeconds);
        }
        SyncPomodoroUI();
    }

    private void PomodoroReset_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlayKeyClick();
        App.PomodoroEndTime = null;
        App.PomodoroRemainingPausedSeconds = App.PomodoroDurationMinutes * 60;
        SyncPomodoroUI();
    }

    // ==================== CALENDAR & REMINDERS MODE ====================
    private DateTime _displayedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _selectedCalendarDate = DateTime.Today;

    private void CalendarMode_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == "Calendar")
        {
            ChatMode_Click(sender, e);
            return;
        }

        PlayModeSound();
        _activeMode = "Calendar";
        UpdateActiveModeLights();
        HideAllPanels();
        CalendarPanel.Visibility = Visibility.Visible;
        CalendarInputBar.Visibility = Visibility.Visible;
        PlayPanelTransition(CalendarPanel);

        if (ReminderTimeInput.Text == "18:00")
        {
            ReminderTimeInput.Text = DateTime.Now.AddMinutes(5).ToString("HH:mm");
        }

        RenderCalendarMonth();
        RefreshCalendarRemindersList();
    }

    private void CalendarPrevMonth_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlayKeyClick();
        _displayedMonth = _displayedMonth.AddMonths(-1);
        RenderCalendarMonth();
    }

    private void CalendarNextMonth_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlayKeyClick();
        _displayedMonth = _displayedMonth.AddMonths(1);
        RenderCalendarMonth();
    }

    private void RenderCalendarMonth()
    {
        CalendarMonthLabel.Text = _displayedMonth.ToString("MMMM yyyy");
        CalendarDaysGrid.Children.Clear();

        var settings = SettingsManager.LoadSettings();
        var reminders = settings.Reminders ?? new List<ReminderItem>();

        int startDayOfWeek = (int)_displayedMonth.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(_displayedMonth.Year, _displayedMonth.Month);

        for (int i = 0; i < 42; i++)
        {
            int dayNum = i - startDayOfWeek + 1;
            if (dayNum < 1 || dayNum > daysInMonth)
            {
                CalendarDaysGrid.Children.Add(new Border());
                continue;
            }

            DateTime cellDate = new DateTime(_displayedMonth.Year, _displayedMonth.Month, dayNum);
            bool isSelected = cellDate.Date == _selectedCalendarDate.Date;
            bool isToday = cellDate.Date == DateTime.Today;
            bool hasReminders = reminders.Any(r => r.DueDateTime.Date == cellDate.Date);

            string bgHex = isSelected ? "#2A2A30" : (isToday ? "#18181C" : "Transparent");
            string borderHex = isSelected ? "#666670" : (hasReminders ? "#44444A" : "#1A1A1E");

            var border = new Border
            {
                Background = bgHex == "Transparent" ? System.Windows.Media.Brushes.Transparent : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bgHex)),
                BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(borderHex)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(2)
            };

            string labelText = hasReminders ? $"{dayNum} •" : dayNum.ToString();
            var btn = new System.Windows.Controls.Button
            {
                Content = labelText,
                FontSize = 12,
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isSelected ? "#FFFFFF" : "#CCCCCC")),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            btn.Click += (s, e) =>
            {
                _selectedCalendarDate = cellDate;
                RenderCalendarMonth();
                RefreshCalendarRemindersList();
            };

            border.Child = btn;
            CalendarDaysGrid.Children.Add(border);
        }
    }

    public void RefreshCalendarRemindersList()
    {
        if (RemindersListPanel == null) return;

        SelectedDateLabel.Text = $"Reminders for {_selectedCalendarDate:MMM dd, yyyy}";
        RemindersListPanel.Children.Clear();

        var settings = SettingsManager.LoadSettings();
        var dayReminders = (settings.Reminders ?? new List<ReminderItem>())
            .Where(r => r.DueDateTime.Date == _selectedCalendarDate.Date)
            .OrderBy(r => r.DueDateTime)
            .ToList();

        if (dayReminders.Count == 0)
        {
            RemindersListPanel.Children.Add(new TextBlock
            {
                Text = "No reminders scheduled for this day.",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#555555")),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            });
            return;
        }

        foreach (var rem in dayReminders)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string statusSuffix = rem.Notified ? " (Done)" : "";
            var txt = new TextBlock
            {
                Text = $"{rem.DueDateTime:HH:mm}  —  {rem.Title}{statusSuffix}",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(rem.Notified ? "#666666" : "#E0E0E0")),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(txt, 0);

            var delBtn = new System.Windows.Controls.Button
            {
                Content = "✕",
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#777777")),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 11,
                Padding = new Thickness(6, 2, 6, 2)
            };
            string remId = rem.Id;
            delBtn.Click += (s, e) =>
            {
                var st = SettingsManager.LoadSettings();
                st.Reminders.RemoveAll(x => x.Id == remId);
                SettingsManager.SaveSettings(st);
                RenderCalendarMonth();
                RefreshCalendarRemindersList();
            };
            Grid.SetColumn(delBtn, 1);

            row.Children.Add(txt);
            row.Children.Add(delBtn);
            RemindersListPanel.Children.Add(row);
        }
    }

    private void AddReminder_Click(object sender, RoutedEventArgs e)
    {
        string title = ReminderTitleInput.Text.Trim();
        if (string.IsNullOrEmpty(title)) return;

        if (!TimeSpan.TryParse(ReminderTimeInput.Text.Trim(), out TimeSpan timeOfDay))
        {
            timeOfDay = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(1));
        }

        DateTime due = _selectedCalendarDate.Date.Add(timeOfDay);

        var settings = SettingsManager.LoadSettings();
        settings.Reminders ??= new List<ReminderItem>();
        settings.Reminders.Add(new ReminderItem
        {
            DueDateTime = due,
            Title = title,
            Notified = false
        });
        SettingsManager.SaveSettings(settings);

        ReminderTitleInput.Text = "";
        RenderCalendarMonth();
        RefreshCalendarRemindersList();
    }

    private void ReminderTitleInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddReminder_Click(sender, e);
            e.Handled = true;
        }
    }

    private void ReminderTitleInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateLiquidGlow(AddReminderButton, ReminderTitleInput, AddReminderButton.IsMouseOver);
    }

    private void AddReminderButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateLiquidGlow(AddReminderButton, ReminderTitleInput, true);
    }

    private void AddReminderButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateLiquidGlow(AddReminderButton, ReminderTitleInput, false);
    }

    private async Task StartNewGameAsync()
    {
        GameText.Text = "Generating today's logic puzzle...";
        GameButtonsPanel.Visibility = Visibility.Collapsed;
        GameSubmitButton.Visibility = Visibility.Visible;
        TriesIndicator.Visibility = Visibility.Visible;
        GameLoadingIndicator.Visibility = Visibility.Visible;
        GameInput.Visibility = Visibility.Visible;
        GameInput.IsEnabled = false;

        try
        {
            var settings = SettingsManager.LoadSettings();
            settings.PastPuzzles ??= new();

            string[] categories = new[]
            {
                "a numerical sequence or mathematical deduction puzzle",
                "a spatial or geometric reasoning puzzle described in plain text",
                "a lateral thinking or deductive logic riddle",
                "a wordplay, cipher, or letter-pattern puzzle",
                "a truth-teller / liar or Knights-and-Knaves deduction puzzle",
                "a scheduling, ordering, or constraint-satisfaction logic puzzle",
                "an algorithmic or probability brain-teaser"
            };
            string todayCategory = categories[DateTime.Today.DayOfYear % categories.Length];

            string historyContext = settings.PastPuzzles.Count > 0
                ? "\n\nCRITICAL: NEVER repeat or closely resemble any of these previously used puzzles or answers:\n- " + string.Join("\n- ", settings.PastPuzzles)
                : "";

            int streak = settings.DailyLogicStreak;
            UpdateGameLevelHeader(streak);

            string difficultySpec;
            if (streak <= 5)
            {
                difficultySpec = "Difficulty: LEVEL // STANDARD (Medium 2-Step Deduction). The puzzle must require combining at least 2 distinct logical steps, constraints, or compound rules (e.g., interleaved/two-operation sequences, 3-entity truth/liar deduction, or multi-clue elimination). Do NOT generate trivial 1-step patterns.";
            }
            else
            {
                int advTier = streak - 5; // Tier 1 at streak 6, Tier 2 at streak 7, etc.
                int requiredSteps = Math.Min(5, 2 + (advTier + 1) / 2);
                difficultySpec = $"Difficulty: LEVEL // ADVANCED +{advTier} (Incremental Streak Challenge, Streak={streak}). Make this puzzle noticeably harder than standard: require {requiredSteps}-step deductive chaining, indirect constraints, compound mathematical/cipher operations, or 1 subtle red-herring clue that must be ruled out logically while still having a single provable answer.";
            }

            string userPrompt = $"Generate a brand new, unique daily logic puzzle for {DateTime.Today:yyyy-MM-dd}. Today's required theme is: {todayCategory}. {difficultySpec} Avoid cliché riddles (like footsteps, echo, shadow, clock, etc.).{historyContext}";

            var aiClient = AiProviderFactory.CreateClient();
            AiResponse response = await aiClient.ChatAsync(userPrompt, _gameSystemPrompt);
            
            string json = response.Text.Replace("```json", "").Replace("```", "").Trim();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("puzzle", out var puzzleProp))
                settings.DailyPuzzleText = puzzleProp.GetString() ?? "";
            if (root.TryGetProperty("answer", out var ansProp))
                settings.DailyPuzzleAnswer = ansProp.GetString() ?? "";
                
            if (!string.IsNullOrWhiteSpace(settings.DailyPuzzleText))
            {
                string summary = $"{settings.DailyPuzzleText} (Answer: {settings.DailyPuzzleAnswer})";
                if (!settings.PastPuzzles.Contains(summary))
                    settings.PastPuzzles.Add(summary);

                while (settings.PastPuzzles.Count > 7)
                {
                    settings.PastPuzzles.RemoveAt(0);
                }
            }

            settings.DailyPuzzleDate = DateTime.Today;
            settings.DailyPuzzleAttemptsLeft = 3;
            SettingsManager.SaveSettings(settings);

            GameText.Text = settings.DailyPuzzleText;
            TriesIndicator.Text = FormatTriesPips(3);
        }
        catch (Exception ex)
        {
            GameText.Text = "The AI server is temporarily busy. Click RETRY below to generate today's puzzle.";
            GameSubmitButton.Visibility = Visibility.Collapsed;
            TriesIndicator.Visibility = Visibility.Collapsed;
            GameNextButton.Content = "RETRY";
            GameNextButton.Visibility = Visibility.Visible;
        }
        finally
        {
            GameButtonsPanel.Visibility = Visibility.Visible;
            GameLoadingIndicator.Visibility = Visibility.Collapsed;
            GameInput.IsEnabled = true;
            GameInput.Focus();
        }
    }

    private void GameNextButton_Click(object sender, RoutedEventArgs e)
    {
        GameNextButton.Visibility = Visibility.Collapsed;
        _ = StartNewGameAsync();
    }

    private static string NormalizeAnswerToken(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        return new string(s.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private async void GameSubmitButton_Click(object sender, RoutedEventArgs e)
    {
        string answer = GameInput.Text.Trim();
        if (string.IsNullOrEmpty(answer)) return;

        HardwareAudio.PlayKeyClick();
        var settings = SettingsManager.LoadSettings();

        GameInput.Text = "";
        GameButtonsPanel.Visibility = Visibility.Collapsed;
        GameLoadingIndicator.Visibility = Visibility.Visible;
        GameInput.IsEnabled = false;

        try
        {
            var aiClient = AiProviderFactory.CreateClient();
            string prompt = $"The puzzle was:\n{settings.DailyPuzzleText}\n\nThe correct answer is: {settings.DailyPuzzleAnswer}\n\nThe user guessed: '{answer}'. Evaluate their guess. If they are completely correct or conceptually correct, reply EXACTLY with 'CORRECT'. If they are wrong, reply EXACTLY with 'WRONG'. Do not include markdown.";
            AiResponse response = NormalizeAnswerToken(answer).Length > 0 && NormalizeAnswerToken(answer) == NormalizeAnswerToken(settings.DailyPuzzleAnswer ?? "")
                ? new AiResponse { Text = "CORRECT" }
                : await aiClient.ChatAsync(prompt, "You are a puzzle master evaluating a user's guess.");
            
            string reply = response.Text.Trim().ToUpper();
            if (reply.StartsWith("CORRECT"))
            {
                HardwareAudio.PlayChime();
                settings.DailyLogicStreak++;
                if (settings.DailyLogicStreak > settings.HighestLogicStreak) 
                    settings.HighestLogicStreak = settings.DailyLogicStreak;
                settings.LastPuzzleSolvedDate = DateTime.Today;
                SettingsManager.SaveSettings(settings);

                StreakText.Text = $"Streak: {settings.DailyLogicStreak}";
                BestStreakText.Text = $"Best: {settings.HighestLogicStreak}";
        UpdateGameLevelHeader(settings.DailyLogicStreak);
                GameText.Text = "Correct! Come back tomorrow for the next puzzle.\n\n" + settings.DailyPuzzleText;
                
                GameInput.Visibility = Visibility.Collapsed;
                GameSubmitButton.Visibility = Visibility.Collapsed;
                TriesIndicator.Visibility = Visibility.Collapsed;
            }
            else
            {
                settings.DailyPuzzleAttemptsLeft--;
                if (settings.DailyPuzzleAttemptsLeft <= 0)
                {
                    settings.DailyLogicStreak = 0;
                    settings.LastPuzzleSolvedDate = DateTime.Today;
                    SettingsManager.SaveSettings(settings);

                    StreakText.Text = "Streak: 0";
                    UpdateGameLevelHeader(0);
                    GameText.Text = $"Out of tries! The correct answer was: {settings.DailyPuzzleAnswer}\n\nCome back tomorrow for the next puzzle.\n\n{settings.DailyPuzzleText}";
                    
                    GameInput.Visibility = Visibility.Collapsed;
                    GameSubmitButton.Visibility = Visibility.Collapsed;
                    TriesIndicator.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SettingsManager.SaveSettings(settings);
                    TriesIndicator.Text = FormatTriesPips(settings.DailyPuzzleAttemptsLeft);
                    GameText.Text = $"Wrong guess!\n\n" + settings.DailyPuzzleText;
                }
            }
        }
        catch (Exception ex)
        {
            GameText.Text = "Error checking answer: " + ex.Message + "\n\n" + settings.DailyPuzzleText;
        }
        finally
        {
            GameButtonsPanel.Visibility = Visibility.Visible;
            GameLoadingIndicator.Visibility = Visibility.Collapsed;
            GameInput.IsEnabled = true;
            GameInput.Focus();
        }
    }

    private void GameInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (GameSubmitButton.Visibility == Visibility.Visible)
                GameSubmitButton_Click(sender, e);
            else if (GameNextButton.Visibility == Visibility.Visible)
                GameNextButton_Click(sender, e);
            e.Handled = true;
        }
    }
    private BitmapSource _currentImage;

    private void Minimize_Click(object sender, RoutedEventArgs e) { this.WindowState = WindowState.Minimized; }
    private void Maximize_Click(object sender, RoutedEventArgs e) { this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    private void InfoButton_Click(object sender, RoutedEventArgs e) { new InfoWindow().ShowDialog(); }
    
    // Completely destroy the session on close
    private void Close_Click(object sender, RoutedEventArgs e) { this.Close(); }

    public PostCaptureWindow(BitmapSource capturedImage)
    {
        InitializeComponent();
        ThemeManager.ApplyDarkTitleBar(this);
        _currentImage = capturedImage;
        PreviewImage.Source = _currentImage;
        
        
        
        ChatInput.Focus();
        this.PreviewKeyDown += (s, e) => 
        {
            if (e.Key == Key.Escape) 
            {
                this.WindowState = WindowState.Minimized;
                e.Handled = true;
            }
        };
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataObject = new System.Windows.DataObject();
            dataObject.SetImage(_currentImage);

            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(_currentImage));
            var ms = new MemoryStream();
            pngEncoder.Save(ms);
            dataObject.SetData("PNG", ms, false);

            System.Windows.Clipboard.SetDataObject(dataObject, true);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to copy: {ex.Message}");
        }
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG Image|*.png|JPEG Image|*.jpg",
            DefaultExt = ".png"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            BitmapEncoder encoder = saveFileDialog.FilterIndex == 2 
                ? new JpegBitmapEncoder() 
                : new PngBitmapEncoder();
                
            encoder.Frames.Add(BitmapFrame.Create(_currentImage));
            
            using (var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
            Close();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow().ShowDialog();
        
        ((App)System.Windows.Application.Current).SyncBuddyVisibility();
    }

    
    private void TriggerKeystrokePulse()
    {
        if (ChatChromeFocused != null)
        {
            var thicknessAnim = new System.Windows.Media.Animation.ThicknessAnimation();
            thicknessAnim.From = new System.Windows.Thickness(1.4);
            thicknessAnim.To = new System.Windows.Thickness(0);
            thicknessAnim.Duration = new System.Windows.Duration(TimeSpan.FromMilliseconds(300));
            thicknessAnim.EasingFunction = new System.Windows.Media.Animation.QuinticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            
            ChatChromeFocused.BeginAnimation(System.Windows.FrameworkElement.MarginProperty, thicknessAnim, System.Windows.Media.Animation.HandoffBehavior.Compose);
        }
    } 

    
        private void UpdateLiquidGlow(System.Windows.Controls.Button btn, System.Windows.Controls.TextBox input, bool isHovering)
    {
        bool hasText = input.Text.Trim().Length > 0;
        bool shouldGlow = hasText || isHovering;

        var outerRing = btn.Template.FindName("OuterRingHover", btn) as System.Windows.Controls.Border;
        var innerOverlay = btn.Template.FindName("InnerHoverOverlay", btn) as System.Windows.Controls.Border;

        if (outerRing != null && innerOverlay != null)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(shouldGlow ? 1.0 : 0.0, new System.Windows.Duration(TimeSpan.FromMilliseconds(shouldGlow ? 200 : 300)));
            outerRing.BeginAnimation(System.Windows.UIElement.OpacityProperty, anim);
            innerOverlay.BeginAnimation(System.Windows.UIElement.OpacityProperty, anim);
        }
    }

    private void ChatInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateLiquidGlow(SendButton, ChatInput, SendButton.IsMouseOver);
    }

    private void SendButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateLiquidGlow(SendButton, ChatInput, true);
    }

    private void SendButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateLiquidGlow(SendButton, ChatInput, false);
    }

    private void GameInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateLiquidGlow(GameSubmitButton, GameInput, GameSubmitButton.IsMouseOver);
    }

    private void GameSubmitButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateLiquidGlow(GameSubmitButton, GameInput, true);
    }

    private void GameSubmitButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateLiquidGlow(GameSubmitButton, GameInput, false);
    }

    private void ChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendButton_Click(sender, e);
            e.Handled = true;
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        string userMessage = ChatInput.Text.Trim();
        if (string.IsNullOrEmpty(userMessage)) return;

        HardwareAudio.PlayKeyClick();
        AddChatBubble(userMessage, isUser: true);
        ChatInput.Text = "";
        
        FrameworkElement thinkingBubble = null;
        try
        {
            SendButton.Visibility = Visibility.Collapsed;
            ChatInput.IsEnabled = false;

            thinkingBubble = CreateMetallicLoader();

            var aiClient = AiProviderFactory.CreateClient();
            AiResponse response = await aiClient.ChatWithImageAsync(_currentImage, userMessage);
            
            if (thinkingBubble != null) ChatMessages.Children.Remove(thinkingBubble);
            AddChatBubble(response.Text, isUser: false);

            
                } catch (MissingKeyException ex)
        {
            if (thinkingBubble != null) ChatMessages.Children.Remove(thinkingBubble);
            var result = System.Windows.MessageBox.Show($"Please enter your {ex.Message} API key in Settings.", "Missing API Key", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                new SettingsWindow().ShowDialog();
            }
        }
        catch (Exception ex)
        {
            if (thinkingBubble != null) ChatMessages.Children.Remove(thinkingBubble);
            
            string errorMsg = ex.Message;
            if (errorMsg.Contains("TooManyRequests") || errorMsg.Contains("429"))
            {
                AddChatBubble("Rate limit exceeded. You've sent too many requests to the AI provider. Please wait a moment and try again.", isUser: false);
            }
            else if (errorMsg.Contains("Unauthorized") || errorMsg.Contains("401") || errorMsg.Contains("Forbidden") || errorMsg.Contains("403"))
            {
                AddChatBubble("Authentication failed. Your API key might be invalid, expired, or lacking permissions. Please check your settings.", isUser: false);
            }
            else if (errorMsg.Contains("InternalServerError") || errorMsg.Contains("500") || errorMsg.Contains("502") || errorMsg.Contains("503"))
            {
                AddChatBubble("The AI provider's servers are currently down or returning errors. Please try again later.", isUser: false);
            }
            else
            {
                AddChatBubble($"An unexpected error occurred communicating with the AI. Details: {ex.Message}", isUser: false);
            }
        }
        finally
        {
            SendButton.Visibility = Visibility.Visible;
            LoadingIndicator.Visibility = Visibility.Collapsed;
            ChatInput.IsEnabled = true;
            ChatInput.Focus();
        }
    }

    private FrameworkElement CreateMetallicLoader()
    {
        const int rows = 3;
        const int cols = 8;

        var well = new Border
        {
            Background = CreateFrozenBrush(0x00, 0x00, 0x00),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 4, 0, 12),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };

        var grid = new System.Windows.Controls.Primitives.UniformGrid
        {
            Rows = rows,
            Columns = cols,
            Width = 68,
            Height = 22
        };

        var total = TimeSpan.FromMilliseconds(960);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                bool isCenterRow = (r == 1);
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 4.5,
                    Height = 4.5,
                    Fill = _dotLitBrush,
                    Opacity = 0.14,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                grid.Children.Add(dot);

                // Stagger each column like a hardware step sequencer scanning left-to-right
                var begin = TimeSpan.FromMilliseconds(c * 110 + (isCenterRow ? 0 : 45));
                double peak = isCenterRow ? 1.0 : 0.55;

                var kf = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
                {
                    BeginTime = begin,
                    Duration = new Duration(total),
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };
                kf.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteDoubleKeyFrame(peak, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
                kf.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.14, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)),
                    new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }));
                kf.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteDoubleKeyFrame(0.14, System.Windows.Media.Animation.KeyTime.FromTimeSpan(total)));

                dot.BeginAnimation(System.Windows.UIElement.OpacityProperty, kf);
            }
        }

        well.Child = grid;
        ChatMessages.Children.Add(well);
        MainScroll.ScrollToBottom();
        return well;
    }

    private Border AddChatBubble(string text, bool isUser)
    {
        var userColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1D1D22");
        var aiColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#131317");
        var borderColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isUser ? "#34343C" : "#26262C");

        var bubble = new Border
        {
            Background = new SolidColorBrush(isUser ? userColor : aiColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(isUser ? 50 : 0, 0, isUser ? 0 : 50, 12),
            HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left
        };

        System.Windows.Controls.TextBox MakeTextBox(string t) => new System.Windows.Controls.TextBox
        {
            Text = t,
            Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8E8EC")),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Segoe UI"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            Cursor = System.Windows.Input.Cursors.IBeam
        };

        var segments = isUser ? null : CodeBlockRenderer.Parse(text);
        if (segments == null || !segments.Exists(s => s is CodeBlockRenderer.CodeSegment))
        {
            bubble.Child = MakeTextBox(isUser ? text : CodeBlockRenderer.CleanMarkdown(text));
        }
        else
        {
            var stack = new StackPanel();
            foreach (var seg in segments)
            {
                if (seg is CodeBlockRenderer.CodeSegment code)
                    stack.Children.Add(CodeBlockRenderer.BuildCodeBlock(code.Language, code.Code, MainScroll));
                else if (seg is CodeBlockRenderer.TextSegment txt)
                    stack.Children.Add(MakeTextBox(txt.Text));
            }
            bubble.Child = stack;
        }

        ChatMessages.Children.Add(bubble);
        MainScroll.ScrollToEnd();
        return bubble;
    }
}








