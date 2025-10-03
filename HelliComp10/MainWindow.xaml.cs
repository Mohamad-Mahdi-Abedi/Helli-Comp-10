using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics; // برای Debug
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI;
using static System.Net.Mime.MediaTypeNames;

namespace HelliComp10
{
    // گرید موز عمودی ریسایز
    public class NewGrid : Grid
    {
        public InputCursor InputCursor
        {
            get => ProtectedCursor;
            set => ProtectedCursor = value;
        }
    }

    // Main Window!
    public sealed partial class MainWindow : Window
    {
        // variabel for split view
        private bool _isDragging = false;
        private double _initialY;
        private double _initialHeight;

        private Process? _process;
        private string _currentDirectory = "";
        private bool _isRunningPython = false;
        private readonly object _hiddenLock = new();
        private readonly Queue<string> _hiddenCommands = new();
        private readonly string[] _knownPrompts = { "Enter", "Enter :", "Enter: ", "Input:", "Name:", ">>>" }; // Extended for Python prompts

        public class FileItem
        {
            public string? Name { get; set; }
            public UIElement? Icon { get; set; }
            public string? FullPath { get; set; }
            public ObservableCollection<FileItem>? Children { get; set; }
            public bool IsFolder { get; set; }
        }

        private ObservableCollection<FileItem> _items = new ObservableCollection<FileItem>();
        private ObservableCollection<FileItem> _allItems = new ObservableCollection<FileItem>(); // For search filtering
        private string _currentFolder = "";
        private FileSystemWatcher _fileSystemWatcher;
        private string? _clipboardSourcePath; // For copy/cut operations
        private bool _isCutOperation; // Track if operation is cut or copy

        public MainWindow()
        {
            InitializeComponent();

            // تنظیمات پنجره
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

            OverlappedPresenter presenter = OverlappedPresenter.Create();
            presenter.PreferredMinimumWidth = 800;
            presenter.PreferredMinimumHeight = 600;
            //presenter.PreferredMaximumWidth = 1200;
            //presenter.PreferredMaximumHeight = 900;
            AppWindow.SetPresenter(presenter);

            // تنظیمات عنوان
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            var h = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(h);
            var win = AppWindow.GetFromWindowId(id);

            win.TitleBar.ExtendsContentIntoTitleBar = true;
            win.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            win.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            win.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            win.Title = "نیرا";

            // رویداد تغییر تم
            ((FrameworkElement)this.Content).ActualThemeChanged += Content_ActualThemeChanged;

            // اعمال تم اولیه
            ApplyThemeFixes(((FrameworkElement)this.Content).ActualTheme);

            StartProcess();

            // فایل ها
            FilesTreeView.ItemsSource = _items;

            // Initialize FileSystemWatcher
            _fileSystemWatcher = new FileSystemWatcher();
            _fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            _fileSystemWatcher.Created += FileSystemWatcher_Created;
            _fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
            _fileSystemWatcher.Renamed += FileSystemWatcher_Renamed;
            _fileSystemWatcher.IncludeSubdirectories = true;
            _fileSystemWatcher.EnableRaisingEvents = false;

            // Add event handlers
            FilesTreeView.RightTapped += FilesTreeView_RightTapped;
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;

            CreateFolder(@"C:\HelliComp\codes");

            // start in desktop
            LoadFolder("C:\\HelliComp\\codes");
            RunPythonButton_Click("cd C:\\HelliComp\\codes");
            RunPythonButton_Click("cls");
        }

        private void CreateFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    Console.WriteLine($"Folder created: {path}");
                }
                else
                {
                    Console.WriteLine($"Folder already exists: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating folder: {ex.Message}");
            }
        }

        private void Content_ActualThemeChanged(FrameworkElement sender, object args)
        {
            ApplyThemeFixes(sender.ActualTheme);

            // آپدیت TreeView برای تغییر آیکون‌ها با تم جدید
            if (!string.IsNullOrEmpty(_currentFolder))
            {
                LoadFolder(_currentFolder);
            }
        }

        private void ApplyThemeFixes(ElementTheme theme)
        {
            // set objects theme color

            if (theme == ElementTheme.Dark)
            {
                // dark
                TabColor1.Color = Color.FromArgb(255, 39, 39, 39);
                TabColor2.Color = Color.FromArgb(255, 39, 39, 39);

                splitfilecode.Fill = new SolidColorBrush(Color.FromArgb(255, 29, 29, 29));
                terminalsplitor.Fill = new SolidColorBrush(Color.FromArgb(255, 29, 29, 29));
                terminalresizer1.Fill = new SolidColorBrush(Color.FromArgb(255, 29, 29, 29));
                terminalresizer2.Fill = new SolidColorBrush(Color.FromArgb(255, 29, 29, 29));
                bottomsplitorstatusline.Fill = new SolidColorBrush(Color.FromArgb(255, 29, 29, 29));
                filessplitor.Fill = new SolidColorBrush(Color.FromArgb(255, 29, 29, 29));

                Splitter.Background = new SolidColorBrush(Color.FromArgb(255, 39, 39, 39));
                BackOfTextBox.Fill = new SolidColorBrush(Color.FromArgb(255, 39, 39, 39));
                BackOfFile.Fill = new SolidColorBrush(Color.FromArgb(255, 39, 39, 39));
                var h = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(h);
                var win = AppWindow.GetFromWindowId(id);

                win.TitleBar.ButtonForegroundColor = Colors.White;

                Microsoft.UI.Xaml.Application.Current.Resources["RequestedTheme"] = ElementTheme.Dark;
            }
            else
            {
                // light
                TabColor1.Color = Color.FromArgb(255, 249, 249, 249);
                TabColor2.Color = Color.FromArgb(255, 249, 249, 249);

                splitfilecode.Fill = new SolidColorBrush(Colors.LightGray);
                terminalsplitor.Fill = new SolidColorBrush(Colors.LightGray);
                terminalresizer1.Fill = new SolidColorBrush(Colors.LightGray);
                terminalresizer2.Fill = new SolidColorBrush(Colors.LightGray);
                bottomsplitorstatusline.Fill = new SolidColorBrush(Colors.LightGray);
                filessplitor.Fill = new SolidColorBrush(Colors.LightGray);

                BackOfTextBox.Fill = new SolidColorBrush(Color.FromArgb(255, 249, 249, 249));
                BackOfFile.Fill = new SolidColorBrush(Color.FromArgb(255, 249, 249, 249));
                Splitter.Background = new SolidColorBrush(Color.FromArgb(255, 249, 249, 249));
                var h = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(h);
                var win = AppWindow.GetFromWindowId(id);

                win.TitleBar.ButtonForegroundColor = Colors.Black;

                Microsoft.UI.Xaml.Application.Current.Resources["RequestedTheme"] = ElementTheme.Light;
            }
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) { }

        private async void MainTabView_AddTabButtonClick_r(TabView sender, object args)
        {
            // تکست باکس
            var inputBox = new TextBox
            {
                PlaceholderText = "name",
                Width = 182
            };

            // منو (ComboBox)
            var combo = new ComboBox
            {
                Width = 80,
                ItemsSource = new List<string> { "py", "cpp" },
                SelectedIndex = 0
            };

            // استک پنل
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            content.Children.Add(inputBox);
            content.Children.Add(combo);

            // دیالوگ
            var dialog = new ContentDialog
            {
                Title = "New File Name",
                Content = content,
                PrimaryButtonText = "تایید",
                CloseButtonText = "بستن",
                XamlRoot = RootGrid.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string text = inputBox.Text;
                string selected = combo.SelectedItem?.ToString() ?? "هیچی انتخاب نشده";

                Debug.WriteLine($"متن: {text}, انتخاب: {selected}");
                AddNewTab($"{text}.{selected}");
            }
        }

        private void AddNewTab(string name)
        {
            // ساخت تب جدید
            var newTab = new TabViewItem
            {
                // {MainTabView.TabItems.Count + 1}
                Header = $"{name}",
                IsClosable = true
            };

            // محتوای تب (EditorTab)
            var editorTab = new EditorTab
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            newTab.Content = editorTab;

            // ساخت منوی راست کلیک
            var flyout = new MenuFlyout();

            var newItem = new MenuFlyoutItem { Text = "New" };
            newItem.Click += MenuFlyout_New_Click;
            flyout.Items.Add(newItem);

            var openItem = new MenuFlyoutItem { Text = "Open..." };
            openItem.Click += MenuFlyout_Open_Click;
            flyout.Items.Add(openItem);

            var saveItem = new MenuFlyoutItem { Text = "Save" };
            saveItem.Click += MenuFlyout_Save_Click;
            flyout.Items.Add(saveItem);

            var exitItem = new MenuFlyoutItem { Text = "Exit" };
            exitItem.Click += MenuFlyout_Exit_Click;
            flyout.Items.Add(exitItem);

            // اتصال منو به تب
            newTab.ContextFlyout = flyout;

            // اضافه کردن تب به TabView
            MainTabView.TabItems.Add(newTab);
            MainTabView.SelectedItem = newTab;
        }

        private async void MenuFlyout_New_Click(object sender, RoutedEventArgs e)
        {// تکست باکس
            var inputBox = new TextBox
            {
                PlaceholderText = "name",
                Width = 182
            };

            // منو (ComboBox)
            var combo = new ComboBox
            {
                Width = 80,
                ItemsSource = new List<string> { "py", "cpp" },
                SelectedIndex = 0
            };

            // استک پنل
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            content.Children.Add(inputBox);
            content.Children.Add(combo);

            // دیالوگ
            var dialog = new ContentDialog
            {
                Title = "New File Name",
                Content = content,
                PrimaryButtonText = "تایید",
                CloseButtonText = "بستن",
                XamlRoot = RootGrid.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string text = inputBox.Text;
                string selected = combo.SelectedItem?.ToString() ?? "هیچی انتخاب نشده";

                Debug.WriteLine($"متن: {text}, انتخاب: {selected}");
                AddNewTab($"{text}.{selected}");
            }
        }

        private void MenuFlyout_Open_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Open Clicked");
        }

        private void MenuFlyout_Save_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Save Clicked");
        }

        private void MenuFlyout_Exit_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabView.SelectedItem is TabViewItem selectedTab)
            {
                MainTabView.TabItems.Remove(selectedTab);
            }
        }

        private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Text == "New")
            {// تکست باکس
                var inputBox = new TextBox
                {
                    PlaceholderText = "name",
                    Width = 182
                };

                // منو (ComboBox)
                var combo = new ComboBox
                {
                    Width = 80,
                    ItemsSource = new List<string> { "py", "cpp" },
                    SelectedIndex = 0
                };

                // استک پنل
                var content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };

                content.Children.Add(inputBox);
                content.Children.Add(combo);

                // دیالوگ
                var dialog = new ContentDialog
                {
                    Title = "New File Name",
                    Content = content,
                    PrimaryButtonText = "تایید",
                    CloseButtonText = "بستن",
                    XamlRoot = RootGrid.XamlRoot,
                    DefaultButton = ContentDialogButton.Primary
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    string text = inputBox.Text;
                    string selected = combo.SelectedItem?.ToString() ?? "هیچی انتخاب نشده";

                    Debug.WriteLine($"متن: {text}, انتخاب: {selected}");
                    AddNewTab($"{text}.{selected}");
                }
            }
        }

        //private void MainTabView_AddTabButtonClick(TabView sender, object args) => AddNewTab();

        private async void MainTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            var dialog = new ContentDialog
            {
                Title = "بستن تب",
                Content = $"آیا می‌خواهید تب «{args.Tab.Header}» بسته شود؟",
                PrimaryButtonText = "بله",
                CloseButtonText = "خیر",
                XamlRoot = RootGrid.XamlRoot, // این خیلی مهمه
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                sender.TabItems.Remove(args.Tab);
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            // kodom tabim?
            if (MainTabView.SelectedItem is TabViewItem selectedTab && selectedTab.Content is EditorTab editorTab)
            {
                // run by tab
                editorTab.RunButton_Click(sender, e);
            }
            else
            {
                // error
                var dialog = new ContentDialog
                {
                    Title = "خطا",
                    Content = "هیچ تب ویرایشگری انتخاب نشده است!",
                    CloseButtonText = "باشه",
                    XamlRoot = this.Content.XamlRoot
                };
                _ = dialog.ShowAsync();
            }
        }

        // Part Split View

        private void Splitter_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = true;
            _initialY = e.GetCurrentPoint(editorandterminal).Position.Y;
            _initialHeight = editorandterminal.RowDefinitions[0].ActualHeight;
            (sender as Border)?.CapturePointer(e.Pointer);
        }

        private void Splitter_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                var currentY = e.GetCurrentPoint(editorandterminal).Position.Y;
                var deltaY = currentY - _initialY;
                var newHeight = _initialHeight + deltaY;

                if (newHeight > 96 && newHeight < editorandterminal.ActualHeight - 96)
                {
                    editorandterminal.RowDefinitions[0].Height = new GridLength(newHeight);
                }
            }
        }

        private void Splitter_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
            (sender as Border)?.ReleasePointerCapture(e.Pointer);
        }


        // Part Resize Vertical Moz!

        private InputCursor? OriginalCursor { get; set; }

        // وقتی مکان‌نما وارد NewGrid می‌شود
        private void NewGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is NewGrid grid)
            {
                OriginalCursor = grid.InputCursor ?? InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                grid.InputCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth); // تغییر به نشانگر عمودی
            }
        }

        // وقتی مکان‌نما از NewGrid خارج می‌شود
        private void NewGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is NewGrid grid && OriginalCursor != null)
            {
                grid.InputCursor = OriginalCursor; // بازگرداندن نشانگر اصلی
            }
        }

        private void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {

        }

        private void StartProcess()
        {
            // Safely close the old process
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(true);
                        _process.WaitForExit();
                    }
                }
                catch { }
                _process = null;
            }

            // Reset Python state
            _isRunningPython = false;

            // Use Desktop or UserProfile as fallback
            //string startDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string startDir = "C:\\HelliComp\\codes";
            if (string.IsNullOrWhiteSpace(startDir) || !Directory.Exists(startDir))
                startDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            _currentDirectory = startDir;
            UpdateCurrentDirText();
            Debug.WriteLine($"StartProcess: Initial directory = {_currentDirectory}");

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = startDir,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    Arguments = "/Q" // Disable echo to reduce extra output
                },
                EnableRaisingEvents = true
            };

            _process.Exited += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    int? exitCode = null;
                    try
                    {
                        if (_process != null && _process.HasExited)
                            exitCode = _process.ExitCode;
                    }
                    catch { }

                    AppendOutput($"[cmd.exe exited{(exitCode.HasValue ? $" with code {exitCode}" : "")}]");
                    _isRunningPython = false;
                });
            };

            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                string line = e.Data;

                // Detect end of Python script (interactive prompt or exit)
                if (_isRunningPython && (line.Trim().StartsWith(">>>") || line.Contains("Python") && line.Contains("exited")))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _isRunningPython = false;
                        AppendOutput("[Python session ended]");
                    });
                    return;
                }

                if (line.StartsWith("__CWD__:"))
                {
                    string path = line.Substring("__CWD__:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(path))
                        _currentDirectory = path;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateCurrentDirText();
                        Debug.WriteLine($"OutputDataReceived: _currentDirectory updated to {_currentDirectory}");
                    });
                    return;
                }

                bool isHidden = false;
                lock (_hiddenLock)
                {
                    if (_hiddenCommands.Count > 0)
                    {
                        string[] arr = _hiddenCommands.ToArray();
                        int foundIndex = -1;
                        for (int i = 0; i < arr.Length; i++)
                        {
                            if (line.Trim().Contains(arr[i], StringComparison.OrdinalIgnoreCase))
                            {
                                foundIndex = i;
                                break;
                            }
                        }

                        if (foundIndex >= 0)
                        {
                            Queue<string> newQ = new Queue<string>();
                            for (int i = 0; i < arr.Length; i++)
                                if (i != foundIndex) newQ.Enqueue(arr[i]);
                            _hiddenCommands.Clear();
                            foreach (var it in newQ) _hiddenCommands.Enqueue(it);
                            isHidden = true;
                        }
                    }
                }

                if (!isHidden)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        AppendOutput(line);
                        OutputScroll.ChangeView(null, OutputScroll.ScrollableHeight, null, false);
                    });
                }
            };

            _process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                DispatcherQueue.TryEnqueue(() =>
                {
                    AppendOutput(e.Data);
                    OutputScroll.ChangeView(null, OutputScroll.ScrollableHeight, null, false);
                });
            };

            try
            {
                // Force UTF-8 برای Python (برای همه child processها مثل python.exe) - قبل از Start()
                _process.StartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
                // Force UTF-8 برای input هم (تا ورودی فارسی درست برسه به Python)
                _process.StartInfo.StandardInputEncoding = Encoding.UTF8;

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                // تغییر encoding کنسول به UTF-8 (برای cmd) - بعد از Start()
                WriteHiddenCommandToCmd("chcp 65001 >nul");

                // Send an echo to determine the current working directory
                WriteHiddenCommandToCmd($"echo __CWD__:%cd%");
                Debug.WriteLine("cmd.exe started.");
            }
            catch (Exception ex)
            {
                AppendOutput($"ERROR: Failed to start cmd.exe -> {ex.Message}");
                Debug.WriteLine($"Error in StartProcess: {ex.Message}");
            }
        }

        private void AppendOutput(string text, bool isUserInput = false)
        {
            // Ignore empty lines, current directory, initial cmd.exe messages, invalid command errors, or known prompts
            if (string.IsNullOrWhiteSpace(text) ||
                text.Contains("__CWD__:") ||
                text.Contains("Microsoft Windows [Version") ||
                text.Contains("(c) Microsoft Corporation") ||
                text.Contains("is not recognized as an internal or external command") ||
                _knownPrompts.Any(p => text.Trim().Equals(p, StringComparison.OrdinalIgnoreCase)))
                return;

            // Check if the line includes the current directory prefix
            string currentDirPrefix = $"{_currentDirectory}>";
            if (text.StartsWith(currentDirPrefix))
            {
                text = text.Substring(currentDirPrefix.Length).Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return;
            }

            if (OutputText.Blocks.Count == 0)
                OutputText.Blocks.Add(new Paragraph());

            if (OutputText.Blocks[0] is Paragraph paragraph)
            {
                paragraph.Inlines.Add(new Run { Text = Environment.NewLine });
                var run = new Run { Text = text + Environment.NewLine };
                if (isUserInput)
                {
                    run.Foreground = new SolidColorBrush(Colors.Blue);
                }
                paragraph.Inlines.Add(run);
            }
        }

        private void UpdateCurrentDirText()
        {
            string cleanDir = _currentDirectory.TrimEnd('\\');
            CurrentDirText.Text = cleanDir + ">";
        }

        private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;

            string command = InputText.Text?.Trim() ?? "";
            InputText.Text = string.Empty;
            e.Handled = true;

            if (string.IsNullOrWhiteSpace(command)) return;

            // Display user input
            if (!_isRunningPython)
                AppendOutput($">> {command}", isUserInput: true);
            else
                AppendOutput(command, isUserInput: true);

            if (_process == null || _process.HasExited)
            {
                AppendOutput("[cmd not running — restarting]");
                StartProcess();
                return;
            }

            // همیشه cls رو handle کن (حتی در Python mode) - بدون فرستادن به process
            if (command.Equals("cls", StringComparison.OrdinalIgnoreCase))
            {
                OutputText.Blocks.Clear();
                OutputText.Blocks.Add(new Paragraph());
                InputText.Focus(FocusState.Programmatic);
                return;  // مهم: زود return کن تا به process نرسه
            }

            if (_isRunningPython)
            {
                // Send raw input to Python
                try
                {
                    Debug.WriteLine($"Sending raw to Python: '{command}'");
                    _process.StandardInput.WriteLine(command);
                    _process.StandardInput.Flush();
                }
                catch (Exception ex)
                {
                    AppendOutput($"ERROR: Unable to send input to python -> {ex.Message}");
                    _isRunningPython = false;
                }
            }
            else if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                try { _process.StandardInput.WriteLine("exit"); } catch { }
            }
            else if (command.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
            {
                HandleCdCommand(command);
            }
            else if (command.StartsWith("python ", StringComparison.OrdinalIgnoreCase))
            {
                _isRunningPython = true;
                try
                {
                    Debug.WriteLine($"Starting Python: '{command}'");
                    _process.StandardInput.WriteLine(command);
                    _process.StandardInput.Flush();
                }
                catch (Exception ex)
                {
                    AppendOutput($"ERROR: Unable to start python -> {ex.Message}");
                    _isRunningPython = false;
                }
            }
            else
            {
                try
                {
                    Debug.WriteLine($"Sending command: '{command}'");
                    _process.StandardInput.WriteLine(command);
                    _process.StandardInput.Flush();
                }
                catch (Exception ex)
                {
                    AppendOutput($"ERROR: Unable to send command -> {ex.Message}");
                }
            }

            InputText.Focus(FocusState.Programmatic);
        }

        private void HandleCdCommand(string command)
        {
            try
            {
                string arg = command.Length > 2 ? command.Substring(2).Trim() : "";
                if (!string.IsNullOrWhiteSpace(arg))
                {
                    if (arg.StartsWith("\"") && arg.EndsWith("\"") && arg.Length >= 2)
                        arg = arg.Substring(1, arg.Length - 2);

                    if (arg.StartsWith("/d ", StringComparison.OrdinalIgnoreCase))
                        arg = arg.Substring(3).Trim();

                    string potential = Path.IsPathRooted(arg) ? arg : Path.GetFullPath(Path.Combine(_currentDirectory, arg));
                    if (Directory.Exists(potential))
                    {
                        _currentDirectory = potential;
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            UpdateCurrentDirText();
                            Debug.WriteLine($"HandleCdCommand: Directory changed to {_currentDirectory}");
                        });

                        WriteHiddenCommandToCmd(command);
                        WriteHiddenCommandToCmd($"echo __CWD__:%cd%");
                        return;
                    }
                    else
                    {
                        AppendOutput($"ERROR: Directory '{potential}' does not exist.");
                        Debug.WriteLine($"HandleCdCommand: Directory '{potential}' does not exist.");
                    }
                }
                else
                {
                    AppendOutput("ERROR: Invalid cd command.");
                    Debug.WriteLine("HandleCdCommand: Empty or invalid cd command.");
                }

                WriteHiddenCommandToCmd(command);
                WriteHiddenCommandToCmd($"echo __CWD__:%cd%");
            }
            catch (Exception ex)
            {
                AppendOutput($"ERROR: cd failed -> {ex.Message}");
                Debug.WriteLine($"Error in HandleCdCommand: {ex.Message}");
            }
        }

        private void WriteHiddenCommandToCmd(string command)
        {
            if (_process == null || _process.HasExited) return;

            lock (_hiddenLock)
            {
                _hiddenCommands.Enqueue(command);
            }

            try
            {
                _process.StandardInput.WriteLine(command);
                _process.StandardInput.Flush();
            }
            catch (Exception ex)
            {
                AppendOutput($"ERROR: Failed to write hidden cmd -> {ex.Message}");
                lock (_hiddenLock)
                {
                    if (_hiddenCommands.Count > 0)
                    {
                        var arr = _hiddenCommands.ToArray();
                        _hiddenCommands.Clear();
                        for (int i = 0; i < arr.Length - 1; i++) _hiddenCommands.Enqueue(arr[i]);
                    }
                }
            }
        }

        async void resetterminal()
        {
            // Clear output
            OutputText.Blocks.Clear();
            OutputText.Blocks.Add(new Paragraph());

            // Safely reset cmd.exe
            StartProcess();

            // Refocus on input
            InputText.Focus(FocusState.Programmatic);
        }

        private void OnResetClicked(object sender, RoutedEventArgs e)
        {
            // Clear output
            OutputText.Blocks.Clear();
            OutputText.Blocks.Add(new Paragraph());

            // Safely reset cmd.exe
            StartProcess();

            // Refocus on input
            InputText.Focus(FocusState.Programmatic);
        }


        private void FilesTreeView_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            FrameworkElement? element = e.OriginalSource as FrameworkElement;
            TreeViewItem? treeViewItem = null;
            while (element != null && treeViewItem == null)
            {
                treeViewItem = element as TreeViewItem;
                element = element.Parent as FrameworkElement;
            }

            FileItem? fileItem = treeViewItem?.DataContext as FileItem;
            MenuFlyout contextMenu = new MenuFlyout();

            if (fileItem != null)
            {
                if (fileItem.IsFolder)
                {
                    // Context menu for folders
                    //var openItem = new MenuFlyoutItem { Text = "Open" };
                    //openItem.Click += (s, args) => LoadFolder(fileItem.FullPath);

                    var newFileItem = new MenuFlyoutItem { Text = "New File" };
                    newFileItem.Click += (s, args) => CreateNewFile(fileItem.FullPath);

                    var collapseItem = new MenuFlyoutItem { Text = "Collapse" };
                    collapseItem.Click += (s, args) => CollapseFolder(treeViewItem);

                    var expandItem = new MenuFlyoutItem { Text = "Expand" };
                    expandItem.Click += (s, args) => ExpandFolder(treeViewItem);

                    var cutItem = new MenuFlyoutItem { Text = "Cut" };
                    cutItem.Click += (s, args) => CutItem(fileItem);

                    var copyItem = new MenuFlyoutItem { Text = "Copy" };
                    copyItem.Click += (s, args) => CopyItem(fileItem);

                    var pasteItem = new MenuFlyoutItem { Text = "Paste" };
                    pasteItem.Click += (s, args) => PasteItem(fileItem.FullPath);
                    pasteItem.IsEnabled = _clipboardSourcePath != null;

                    var copyPathItem = new MenuFlyoutItem { Text = "Copy Path" };
                    copyPathItem.Click += (s, args) => CopyPath(fileItem.FullPath);

                    var renameItem = new MenuFlyoutItem { Text = "Rename" };
                    renameItem.Click += (s, args) => RenameItem(fileItem);

                    var deleteItem = new MenuFlyoutItem { Text = "Delete" };
                    deleteItem.Click += (s, args) => DeleteItem(fileItem);

                    var duplicateItem = new MenuFlyoutItem { Text = "Duplicate" };
                    duplicateItem.Click += (s, args) => DuplicateItem(fileItem);

                    //contextMenu.Items.Add(openItem);
                    //contextMenu.Items.Add(new MenuFlyoutSeparator());
                    contextMenu.Items.Add(newFileItem);
                    contextMenu.Items.Add(collapseItem);
                    contextMenu.Items.Add(expandItem);
                    contextMenu.Items.Add(new MenuFlyoutSeparator());
                    contextMenu.Items.Add(cutItem);
                    contextMenu.Items.Add(copyItem);
                    contextMenu.Items.Add(pasteItem);
                    contextMenu.Items.Add(copyPathItem);
                    contextMenu.Items.Add(renameItem);
                    contextMenu.Items.Add(deleteItem);
                    contextMenu.Items.Add(duplicateItem);
                }
                else
                {
                    // Context menu for files
                    // ست تکست 
                    //var openItem = new MenuFlyoutItem { Text = "Open" };
                    //openItem.Click += (s, args) => OpenFile(fileItem.FullPath);

                    var copyItem = new MenuFlyoutItem { Text = "Copy" };
                    copyItem.Click += (s, args) => CopyItem(fileItem);

                    var cutItem = new MenuFlyoutItem { Text = "Cut" };
                    cutItem.Click += (s, args) => CutItem(fileItem);

                    var deleteItem = new MenuFlyoutItem { Text = "Delete" };
                    deleteItem.Click += (s, args) => DeleteItem(fileItem);

                    var copyPathItem = new MenuFlyoutItem { Text = "Copy Path" };
                    copyPathItem.Click += (s, args) => CopyPath(fileItem.FullPath);

                    var renameItem = new MenuFlyoutItem { Text = "Rename" };
                    renameItem.Click += (s, args) => RenameItem(fileItem);

                    var duplicateItem = new MenuFlyoutItem { Text = "Duplicate" };
                    duplicateItem.Click += (s, args) => DuplicateItem(fileItem);

                    //contextMenu.Items.Add(openItem);
                    contextMenu.Items.Add(copyItem);
                    contextMenu.Items.Add(cutItem);
                    contextMenu.Items.Add(deleteItem);
                    contextMenu.Items.Add(copyPathItem);
                    contextMenu.Items.Add(renameItem);
                    contextMenu.Items.Add(duplicateItem);

                    // Add "Write" option for Python and C++ files
                    string extension = Path.GetExtension(fileItem.FullPath).ToLower();
                    if (extension == ".py" || extension == ".cpp" || extension == ".h" || extension == ".hpp")
                    {
                        var writeItem = new MenuFlyoutItem { Text = "Quick Write" };
                        writeItem.Click += (s, args) => WriteFile(fileItem);
                        contextMenu.Items.Add(new MenuFlyoutSeparator());
                        contextMenu.Items.Add(writeItem);
                    }
                }
            }
            else
            {
                // Context menu for background
                var newFileItem = new MenuFlyoutItem { Text = "New File" };
                newFileItem.Click += (s, args) => CreateNewFile(_currentFolder);

                var newFolderItem = new MenuFlyoutItem { Text = "New Folder" };
                newFolderItem.Click += (s, args) => CreateNewFolder(_currentFolder);

                var collapseAllItem = new MenuFlyoutItem { Text = "Collapse All" };
                collapseAllItem.Click += (s, args) => CollapseAllFolders();

                var expandAllItem = new MenuFlyoutItem { Text = "Expand All" };
                expandAllItem.Click += (s, args) => ExpandAllFolders();

                var pasteItem = new MenuFlyoutItem { Text = "Paste" };
                pasteItem.Click += (s, args) => PasteItem(_currentFolder);
                pasteItem.IsEnabled = _clipboardSourcePath != null;

                contextMenu.Items.Add(newFileItem);
                contextMenu.Items.Add(newFolderItem);
                contextMenu.Items.Add(collapseAllItem);
                contextMenu.Items.Add(expandAllItem);
                contextMenu.Items.Add(pasteItem);
            }

            UIElement target = (UIElement)treeViewItem ?? FilesTreeView;
            contextMenu.ShowAt(target, e.GetPosition(target));
        }

        private async void WriteFile(FileItem fileItem)
        {
            if (FilesTreeView.XamlRoot == null)
            {
                await ShowErrorDialog("Cannot edit file: UI is not initialized.");
                return;
            }

            try
            {
                string fileContent = File.ReadAllText(fileItem.FullPath);
                System.Diagnostics.Debug.Write(fileContent);
                var textBox = new TextBox
                {
                    Text = fileContent,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    Height = 300,
                    Width = 400
                };

                var dialog = new ContentDialog
                {
                    Title = $"Edit {fileItem.Name}",
                    Content = textBox,
                    PrimaryButtonText = "Save",
                    CloseButtonText = "Cancel",
                    XamlRoot = FilesTreeView.XamlRoot
                };

                textBox.Text = fileContent;

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    File.WriteAllText(fileItem.FullPath, textBox.Text);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to edit file: {ex.Message}");
            }
        }

        private async void CreateNewFolder(string parentPath)
        {
            try
            {
                string newFolderPath = Path.Combine(parentPath, "New Folder");
                int counter = 1;
                while (Directory.Exists(newFolderPath))
                {
                    newFolderPath = Path.Combine(parentPath, $"New Folder ({counter})");
                    counter++;
                }
                Directory.CreateDirectory(newFolderPath);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to create folder: {ex.Message}");
            }
        }

        private async void CreateNewFile(string parentPath)
        {
            if (FilesTreeView.XamlRoot == null)
            {
                await ShowErrorDialog("Cannot create file: UI is not initialized.");
                return;
            }

            try
            {
                var textBox = new TextBox
                {
                    PlaceholderText = "Enter file name (e.g., newfile.txt)",
                    Width = 300
                };

                var dialog = new ContentDialog
                {
                    Title = "New File",
                    Content = textBox,
                    PrimaryButtonText = "Create",
                    CloseButtonText = "Cancel",
                    XamlRoot = FilesTreeView.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(textBox.Text))
                {
                    string newFilePath = Path.Combine(parentPath, textBox.Text);
                    int counter = 1;
                    while (File.Exists(newFilePath))
                    {
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(textBox.Text);
                        string extension = Path.GetExtension(textBox.Text);
                        newFilePath = Path.Combine(parentPath, $"{fileNameWithoutExtension} ({counter}){extension}");
                        counter++;
                    }
                    File.Create(newFilePath).Dispose();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to create file: {ex.Message}");
            }
        }

        private async void OpenFile(string filePath)
        {
            try
            {
                await Windows.System.Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(filePath));
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to open file: {ex.Message}");
            }
        }

        private async void RenameItem(FileItem item)
        {
            if (FilesTreeView.XamlRoot == null)
            {
                await ShowErrorDialog("Cannot rename: UI is not initialized.");
                return;
            }

            try
            {
                var dialog = new ContentDialog
                {
                    Title = "Rename",
                    Content = new TextBox { Text = item.Name },
                    PrimaryButtonText = "OK",
                    CloseButtonText = "Cancel",
                    XamlRoot = FilesTreeView.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var textBox = dialog.Content as TextBox;
                    string newName = textBox.Text;
                    if (!string.IsNullOrEmpty(newName))
                    {
                        string newPath = Path.Combine(Path.GetDirectoryName(item.FullPath), newName);
                        if (item.IsFolder)
                            Directory.Move(item.FullPath, newPath);
                        else
                            File.Move(item.FullPath, newPath);
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to rename: {ex.Message}");
            }
        }

        private async void DeleteItem(FileItem item)
        {
            try
            {
                if (item.IsFolder)
                    Directory.Delete(item.FullPath, true);
                else
                    File.Delete(item.FullPath);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to delete: {ex.Message}");
            }
        }

        private async void CopyItem(FileItem item)
        {
            try
            {
                _clipboardSourcePath = item.FullPath;
                _isCutOperation = false;
                var dataPackage = new DataPackage();
                dataPackage.SetText(item.FullPath);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to copy: {ex.Message}");
            }
        }

        private async void CutItem(FileItem item)
        {
            try
            {
                _clipboardSourcePath = item.FullPath;
                _isCutOperation = true;
                var dataPackage = new DataPackage();
                dataPackage.SetText(item.FullPath);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to cut: {ex.Message}");
            }
        }

        private async void PasteItem(string destinationPath)
        {
            if (string.IsNullOrEmpty(_clipboardSourcePath))
                return;

            try
            {
                string destPath = Path.Combine(destinationPath, Path.GetFileName(_clipboardSourcePath));
                int counter = 1;
                while (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_clipboardSourcePath);
                    string extension = Path.GetExtension(_clipboardSourcePath);
                    destPath = Path.Combine(destinationPath, $"{fileNameWithoutExtension} ({counter}){extension}");
                    counter++;
                }

                if (File.Exists(_clipboardSourcePath))
                {
                    File.Copy(_clipboardSourcePath, destPath);
                    if (_isCutOperation)
                        File.Delete(_clipboardSourcePath);
                }
                else if (Directory.Exists(_clipboardSourcePath))
                {
                    CopyDirectory(_clipboardSourcePath, destPath);
                    if (_isCutOperation)
                        Directory.Delete(_clipboardSourcePath, true);
                }

                if (_isCutOperation)
                    _clipboardSourcePath = null;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to paste: {ex.Message}");
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        private async void CopyPath(string path)
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(path);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to copy path: {ex.Message}");
            }
        }

        private async void DuplicateItem(FileItem item)
        {
            try
            {
                string destPath = Path.Combine(Path.GetDirectoryName(item.FullPath), $"Copy of {item.Name}");
                int counter = 1;
                while (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.Name);
                    string extension = Path.GetExtension(item.Name);
                    destPath = Path.Combine(Path.GetDirectoryName(item.FullPath), $"Copy of {fileNameWithoutExtension} ({counter}){extension}");
                    counter++;
                }

                if (item.IsFolder)
                    CopyDirectory(item.FullPath, destPath);
                else
                    File.Copy(item.FullPath, destPath);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to duplicate: {ex.Message}");
            }
        }

        private void CollapseFolder(TreeViewItem treeViewItem)
        {
            if (treeViewItem != null)
                treeViewItem.IsExpanded = false;
        }

        private void ExpandFolder(TreeViewItem treeViewItem)
        {
            if (treeViewItem != null)
                treeViewItem.IsExpanded = true;
        }

        private void CollapseAllFolders()
        {
            foreach (var node in FilesTreeView.RootNodes)
                CollapseTreeViewNode(node);
        }

        private void ExpandAllFolders()
        {
            foreach (var node in FilesTreeView.RootNodes)
                ExpandTreeViewNode(node);
        }

        private void CollapseTreeViewNode(TreeViewNode node)
        {
            node.IsExpanded = false;
            foreach (var child in node.Children)
                CollapseTreeViewNode(child);
        }

        private void ExpandTreeViewNode(TreeViewNode node)
        {
            node.IsExpanded = true;
            foreach (var child in node.Children)
                ExpandTreeViewNode(child);
        }

        private async System.Threading.Tasks.Task ShowErrorDialog(string message)
        {
            if (FilesTreeView.XamlRoot == null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = FilesTreeView.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();
            _items.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                // وقتی تکست‌فیلد خالی است، کل ساختار درختی از صفر بازسازی می‌شود
                if (!string.IsNullOrEmpty(_currentFolder))
                {
                    LoadFolder(_currentFolder); // بازسازی کامل از مسیر فعلی
                }
                return;
            }

            foreach (var item in _allItems)
            {
                var filtered = FilterItem(item, searchText);
                if (filtered != null)
                {
                    _items.Add(filtered);
                }
            }

            // بستن همه فولدرها بعد از فیلتر برای جلوگیری از باز شدن خودکار
            //CollapseAllFolders();
            //ExpandAllFolders();
        }

        private FileItem? FilterItem(FileItem item, string searchText)
        {
            if (item.Name.ToLower().Contains(searchText))
            {
                // If the item matches, return a copy with all children (recursively filtered)
                var filteredItem = CreateFilteredItem(item);
                filteredItem.Children = new ObservableCollection<FileItem>();
                if (item.Children != null)
                {
                    foreach (var child in item.Children)
                    {
                        var filteredChild = FilterItem(child, searchText);
                        if (filteredChild != null)
                        {
                            filteredItem.Children.Add(filteredChild);
                        }
                    }
                }
                return filteredItem;
            }
            else if (item.IsFolder && item.Children != null)
            {
                // If the item doesn't match but has matching descendants, return a copy with filtered children
                var filteredChildren = new ObservableCollection<FileItem>();
                foreach (var child in item.Children)
                {
                    var filteredChild = FilterItem(child, searchText);
                    if (filteredChild != null)
                    {
                        filteredChildren.Add(filteredChild);
                    }
                }
                if (filteredChildren.Any())
                {
                    var filteredItem = CreateFilteredItem(item);
                    filteredItem.Children = filteredChildren;
                    return filteredItem;
                }
            }
            return null;
        }

        private FileItem CreateFilteredItem(FileItem original)
        {
            return new FileItem
            {
                Name = original.Name,
                Icon = CreateIcon(original.FullPath, original.IsFolder), // Recreate icon to ensure correctness
                FullPath = original.FullPath,
                IsFolder = original.IsFolder,
                Children = original.IsFolder ? new ObservableCollection<FileItem>() : null
            };
        }

        private UIElement CreateIcon(string path, bool isFolder)
        {
            bool isDarkMode = Microsoft.UI.Xaml.Application.Current.RequestedTheme == Microsoft.UI.Xaml.ApplicationTheme.Dark;

            if (isFolder)
            {
                return new FontIcon
                {
                    Glyph = "\uE8B7",
                    FontSize = 16,
                    Foreground = isDarkMode ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White) : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black)
                };
            }
            else
            {
                string extension = Path.GetExtension(path).ToLower();
                try
                {
                    if (extension == ".py")
                    {
                        return new Microsoft.UI.Xaml.Controls.Image
                        {
                            Source = new BitmapImage(new Uri(isDarkMode ? "ms-appx:///Assets/pl.png" : "ms-appx:///Assets/pd.png")),
                            Width = 16,
                            Height = 16
                        };
                    }
                    else if (extension == ".cpp" || extension == ".h" || extension == ".hpp")
                    {
                        return new Microsoft.UI.Xaml.Controls.Image
                        {
                            Source = new BitmapImage(new Uri(isDarkMode ? "ms-appx:///Assets/cl.png" : "ms-appx:///Assets/cd.png")),
                            Width = 16,
                            Height = 16
                        };
                    }
                    else
                    {
                        return new FontIcon
                        {
                            Glyph = "\uE8A5",
                            FontSize = 16,
                            Foreground = isDarkMode ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White) : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black)
                        };
                    }
                }
                catch
                {
                    return new FontIcon
                    {
                        Glyph = "\uE8A5",
                        FontSize = 16,
                        Foreground = isDarkMode ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White) : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black)
                    };
                }
            }
        }

        private void LoadFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (FilesTreeView.XamlRoot == null)
                        return;

                    var dialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = "The specified folder does not exist!",
                        CloseButtonText = "OK",
                        XamlRoot = FilesTreeView.XamlRoot
                    };
                    await dialog.ShowAsync();
                });
                return;
            }

            _currentFolder = folderPath;
            _items.Clear();
            _allItems.Clear();

            try
            {
                foreach (var dir in Directory.GetDirectories(folderPath))
                {
                    var item = CreateTreeItem(dir, true);
                    _items.Add(item);
                    _allItems.Add(item);
                }

                foreach (var file in Directory.GetFiles(folderPath))
                {
                    var item = CreateTreeItem(file, false);
                    _items.Add(item);
                    _allItems.Add(item);
                }
            }
            catch { /* Ignore access errors */ }

            _fileSystemWatcher.Path = folderPath;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        private FileItem CreateTreeItem(string path, bool isFolder)
        {
            var item = new FileItem
            {
                Name = Path.GetFileName(path) == "" ? path : Path.GetFileName(path),
                Icon = CreateIcon(path, isFolder),
                FullPath = path,
                IsFolder = isFolder,
                Children = isFolder ? new ObservableCollection<FileItem>() : null
            };

            if (isFolder)
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(path))
                        item.Children.Add(CreateTreeItem(dir, true));

                    foreach (var file in Directory.GetFiles(path))
                        item.Children.Add(CreateTreeItem(file, false));
                }
                catch { /* Ignore access errors */ }
            }

            return item;
        }

        private int CountItems(FileItem item)
        {
            int count = 1;
            if (item.IsFolder)
            {
                foreach (var child in item.Children!)
                    count += CountItems(child);
            }
            return count;
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateTreeView();
            });
        }

        private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateTreeView();
            });
        }

        private void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateTreeView();
            });
        }

        private void FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateTreeView();
            });
        }

        private void UpdateTreeView()
        {
            if (string.IsNullOrEmpty(_currentFolder)) return;
            LoadFolder(_currentFolder);
        }

        private void RunPythonButton_Click(string cmd)
        {
            string command = cmd; // دستور دلخواه‌تون رو اینجا تغییر بدید

            // نمایش ورودی کاربر (مثل وقتی از TextBox وارد می‌کنید)
            if (!_isRunningPython)
                AppendOutput($">> {command}", isUserInput: true);
            else
                AppendOutput(command, isUserInput: true);

            if (_process == null || _process.HasExited)
            {
                AppendOutput("[cmd not running — restarting]");
                StartProcess();
                return;
            }

            // ارسال دستور به پروسس (مشابه منطق OnInputKeyDown)
            if (_isRunningPython)
            {
                // اگر قبلاً Python در حال اجراست، raw بفرست
                try
                {
                    Debug.WriteLine($"Sending raw to Python: '{command}'");
                    _process.StandardInput.WriteLine(command);
                    _process.StandardInput.Flush();
                }
                catch (Exception ex)
                {
                    AppendOutput($"ERROR: Unable to send input to python -> {ex.Message}");
                    _isRunningPython = false;
                }
            }
            else if (command.Equals("cls", StringComparison.OrdinalIgnoreCase))
            {
                OutputText.Blocks.Clear();
                OutputText.Blocks.Add(new Paragraph());
            }
            else if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                try { _process.StandardInput.WriteLine("exit"); } catch { }
            }
            else if (command.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
            {
                HandleCdCommand(command);
            }
            else if (command.StartsWith("python ", StringComparison.OrdinalIgnoreCase))
            {
                _isRunningPython = true;
                try
                {
                    Debug.WriteLine($"Starting Python: '{command}'");
                    _process.StandardInput.WriteLine(command);
                    _process.StandardInput.Flush();
                }
                catch (Exception ex)
                {
                    AppendOutput($"ERROR: Unable to start python -> {ex.Message}");
                    _isRunningPython = false;
                }
            }
            else
            {
                // دستور عادی مثل python test.py
                try
                {
                    Debug.WriteLine($"Sending command: '{command}'");
                    _process.StandardInput.WriteLine(command);
                    _process.StandardInput.Flush();
                }
                catch (Exception ex)
                {
                    AppendOutput($"ERROR: Unable to send command -> {ex.Message}");
                }
            }

            // اسکرول به پایین (اختیاری)
            OutputScroll.ChangeView(null, OutputScroll.ScrollableHeight, null, false);

            // فوکوس روی InputText (اختیاری، برای ادامه کار)
            InputText.Focus(FocusState.Programmatic);
        }

        private void MenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
}