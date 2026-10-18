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
    private string _gameSystemPrompt = @"You are a diverse logic puzzle generator. Generate a short logic puzzle. Mix it up randomly between: number patterns, spatial reasoning described in text, letter sequences, and classic logic riddles. Output ONLY a valid JSON object without markdown. Schema: { ""puzzle"": ""the puzzle text"", ""answer"": ""the exact brief answer"" }. Make it very concise to save tokens.";

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

    private void ChatMode_Click(object sender, RoutedEventArgs e)
    {
        _activeMode = "Chat";
        HideAllPanels();
        MainScroll.Visibility = Visibility.Visible;
        ChatInputBar.Visibility = Visibility.Visible;
        ChatInput.Focus();
    }

    private async void GameToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == "Logic")
        {
            ChatMode_Click(sender, e);
            return;
        }

        _activeMode = "Logic";
        HideAllPanels();
        GamePanel.Visibility = Visibility.Visible;
        GameInputBar.Visibility = Visibility.Visible;

        var settings = SettingsManager.LoadSettings();
        StreakText.Text = $"Streak: {settings.DailyLogicStreak}";
        BestStreakText.Text = $"Best: {settings.HighestLogicStreak}";

        if (settings.LastPuzzleSolvedDate.Date == DateTime.Today)
        {
            GameText.Text = "You've already played today's puzzle! Come back tomorrow for a new challenge.";
            GameInput.Visibility = Visibility.Collapsed;
            GameSubmitButton.Visibility = Visibility.Collapsed;
            TriesIndicator.Visibility = Visibility.Collapsed;
        }
        else if (settings.DailyPuzzleDate.Date == DateTime.Today)
        {
            GameText.Text = settings.DailyPuzzleText;
            TriesIndicator.Text = $"{settings.DailyPuzzleAttemptsLeft} Tries Left";
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

        _activeMode = "Sudoku";
        HideAllPanels();
        SudokuPanel.Visibility = Visibility.Visible;
        SudokuInputBar.Visibility = Visibility.Visible;

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
        _sudokuCurrent = (int[,])_sudokuInitial.Clone();
        var settings = SettingsManager.LoadSettings();
        SaveSudokuProgress(settings);
        RenderSudokuBoard();
        SudokuStatusText.Text = "Board reset. Fill each row, column, and 2x3 box with 1-6.";
    }

    // ==================== POMODORO TIMER MODE ====================
    private void PomodoroMode_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == "Pomodoro")
        {
            ChatMode_Click(sender, e);
            return;
        }

        _activeMode = "Pomodoro";
        HideAllPanels();
        PomodoroPanel.Visibility = Visibility.Visible;
        PomodoroInputBar.Visibility = Visibility.Visible;
        SyncPomodoroUI();
    }

    public void SyncPomodoroUI()
    {
        if (PomodoroTimeText == null) return;

        if (App.IsPomodoroRunning && App.PomodoroEndTime.HasValue)
        {
            var remaining = App.PomodoroEndTime.Value - DateTime.Now;
            if (remaining.TotalSeconds < 0) remaining = TimeSpan.Zero;
            int mins = (int)remaining.TotalMinutes;
            int secs = remaining.Seconds;
            PomodoroTimeText.Text = $"{mins:D2}:{secs:D2}";
            PomodoroStatusLabel.Text = "Focusing...";
            PomodoroStartPauseButton.Content = "Pause";
        }
        else
        {
            int mins = App.PomodoroRemainingPausedSeconds / 60;
            int secs = App.PomodoroRemainingPausedSeconds % 60;
            PomodoroTimeText.Text = $"{mins:D2}:{secs:D2}";
            PomodoroStatusLabel.Text = "Ready";
            PomodoroStartPauseButton.Content = "Start";
        }
    }

    private void SetPomodoroMinutes(int minutes)
    {
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

        _activeMode = "Calendar";
        HideAllPanels();
        CalendarPanel.Visibility = Visibility.Visible;
        CalendarInputBar.Visibility = Visibility.Visible;

        if (ReminderTimeInput.Text == "18:00")
        {
            ReminderTimeInput.Text = DateTime.Now.AddMinutes(5).ToString("HH:mm");
        }

        RenderCalendarMonth();
        RefreshCalendarRemindersList();
    }

    private void CalendarPrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _displayedMonth = _displayedMonth.AddMonths(-1);
        RenderCalendarMonth();
    }

    private void CalendarNextMonth_Click(object sender, RoutedEventArgs e)
    {
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

            string userPrompt = $"Generate a brand new, unique daily logic puzzle for {DateTime.Today:yyyy-MM-dd}. Today's required theme is: {todayCategory}. Avoid cliché riddles (like footsteps, echo, shadow, clock, etc.).{historyContext}";

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
            }

            settings.DailyPuzzleDate = DateTime.Today;
            settings.DailyPuzzleAttemptsLeft = 3;
            SettingsManager.SaveSettings(settings);

            GameText.Text = settings.DailyPuzzleText;
            TriesIndicator.Text = "3 Tries Left";
        }
        catch (Exception ex)
        {
            GameText.Text = "Failed to generate puzzle. " + ex.Message;
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
        GameToggle_Click(this, new RoutedEventArgs());
    }

    private async void GameSubmitButton_Click(object sender, RoutedEventArgs e)
    {
        string answer = GameInput.Text.Trim();
        if (string.IsNullOrEmpty(answer)) return;

        var settings = SettingsManager.LoadSettings();

        GameInput.Text = "";
        GameButtonsPanel.Visibility = Visibility.Collapsed;
        GameLoadingIndicator.Visibility = Visibility.Visible;
        GameInput.IsEnabled = false;

        try
        {
            var aiClient = AiProviderFactory.CreateClient();
            string prompt = $"The puzzle was:\n{settings.DailyPuzzleText}\n\nThe correct answer is: {settings.DailyPuzzleAnswer}\n\nThe user guessed: '{answer}'. Evaluate their guess. If they are completely correct or conceptually correct, reply EXACTLY with 'CORRECT'. If they are wrong, reply EXACTLY with 'WRONG'. Do not include markdown.";
            AiResponse response = await aiClient.ChatAsync(prompt, "You are a puzzle master evaluating a user's guess.");
            
            string reply = response.Text.Trim().ToUpper();
            if (reply.StartsWith("CORRECT"))
            {
                settings.DailyLogicStreak++;
                if (settings.DailyLogicStreak > settings.HighestLogicStreak) 
                    settings.HighestLogicStreak = settings.DailyLogicStreak;
                settings.LastPuzzleSolvedDate = DateTime.Today;
                SettingsManager.SaveSettings(settings);

                StreakText.Text = $"Streak: {settings.DailyLogicStreak}";
                BestStreakText.Text = $"Best: {settings.HighestLogicStreak}";
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
                    GameText.Text = $"Out of tries! The correct answer was: {settings.DailyPuzzleAnswer}\n\nCome back tomorrow for the next puzzle.\n\n{settings.DailyPuzzleText}";
                    
                    GameInput.Visibility = Visibility.Collapsed;
                    GameSubmitButton.Visibility = Visibility.Collapsed;
                    TriesIndicator.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SettingsManager.SaveSettings(settings);
                    TriesIndicator.Text = $"{settings.DailyPuzzleAttemptsLeft} Tries Left";
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
        const double d = 6;
        var canvas = new System.Windows.Controls.Canvas
        {
            Width = 22,
            Height = 20,
            Margin = new Thickness(16, 8, 0, 12),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };

        var points = new[] { new System.Windows.Point(8, 0), new System.Windows.Point(1, 11), new System.Windows.Point(15, 11) };
        var flatGrey = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC8, 0xC8, 0xCC));
        flatGrey.Freeze();

        for (int i = 0; i < 3; i++)
        {
            var scale = new System.Windows.Media.ScaleTransform(0.7, 0.7);
            var ball = new System.Windows.Shapes.Ellipse
            {
                Width = d,
                Height = d,
                Fill = flatGrey,
                Opacity = 0.5,
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = scale
            };
            System.Windows.Controls.Canvas.SetLeft(ball, points[i].X);
            System.Windows.Controls.Canvas.SetTop(ball, points[i].Y);
            canvas.Children.Add(ball);

            var begin = TimeSpan.FromMilliseconds(i * 250);
            var total = TimeSpan.FromMilliseconds(1000);

            System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames Pulse(double idle, double peak)
            {
                var kf = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
                {
                    BeginTime = begin,
                    Duration = new Duration(total),
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };
                kf.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(peak, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250)),
                    new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }));
                kf.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(idle, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600)),
                    new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }));
                kf.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(idle, System.Windows.Media.Animation.KeyTime.FromTimeSpan(total)));
                return kf;
            }

            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, Pulse(0.7, 1.3));
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, Pulse(0.7, 1.3));
            ball.BeginAnimation(System.Windows.UIElement.OpacityProperty, Pulse(0.5, 1.0));
        }

        ChatMessages.Children.Add(canvas);
        MainScroll.ScrollToBottom();
        return canvas;
    }

    private Border AddChatBubble(string text, bool isUser)
    {
        var userColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#050508"); // Pure black
        var aiColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1C1C1E");

        var bubble = new Border
        {
            Background = new SolidColorBrush(isUser ? userColor : aiColor),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(15, 10, 15, 10),
            Margin = new Thickness(isUser ? 50 : 0, 0, isUser ? 0 : 50, 15),
            HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left
        };

        System.Windows.Controls.TextBox MakeTextBox(string t) => new System.Windows.Controls.TextBox
        {
            Text = t,
            Foreground = System.Windows.Media.Brushes.White,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            Cursor = System.Windows.Input.Cursors.IBeam
        };

        var segments = isUser ? null : CodeBlockRenderer.Parse(text);
        if (segments == null || !segments.Exists(s => s is CodeBlockRenderer.CodeSegment))
        {
            bubble.Child = MakeTextBox(text);
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








