using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using WinRT.Interop;

namespace HelliComp10
{
    public sealed partial class EditorTab : UserControl
    {
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private const int VK_CONTROL = 0x11;
        private const int VK_R = 0x52;
        private const int VK_U = 0x55;
        private const int VK_P = 0x50;
        private const int VK_J = 0x4A;

        private TextBox editorTextBox; // TextBox دیالوگ همیشه قابل دسترس

        // FIXED: Property to hold reference to the parent Window (set this when creating the EditorTab in MainWindow)
        public Microsoft.UI.Xaml.Window ParentWindow { get; set; }

        // --- Win32 interop structs & functions for SendInput ---
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        // --- End interop ---

        public EditorTab()
        {
            this.InitializeComponent();
            _proc = HookCallback;
            _hookID = SetHook(_proc);
            InitializeWebView();

            // مدیریت تغییر سایز یوزرکنترل
            this.SizeChanged += EditorTab_SizeChanged;

            // Unhook when unloaded
            this.Unloaded += EditorTab_Unloaded;
        }

        private void EditorTab_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            UnhookWindowsHookEx(_hookID);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // بررسی کلیدهای Ctrl+R, Ctrl+U, Ctrl+P, Ctrl+J
                bool ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                if (ctrlPressed && (vkCode == VK_R || vkCode == VK_U || vkCode == VK_P || vkCode == VK_J))
                {
                    return (IntPtr)1; // ایگنور کردن کلید
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void EditorTab_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // هر وقت اندازه یوزرکنترل تغییر کرد، وب‌ویو هم اندازه‌اش آپدیت شود
            EditorWebView.Width = e.NewSize.Width;
            EditorWebView.Height = e.NewSize.Height;
        }

        private async void InitializeWebView()
        {
            try
            {
                await EditorWebView.EnsureCoreWebView2Async(null);

                string htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "index.html");
                string htmlUri = new Uri(htmlPath).AbsoluteUri;
                EditorWebView.CoreWebView2.Navigate(htmlUri);

                EditorWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "خطا در بارگذاری ویرایشگر",
                    Content = ex.Message,
                    CloseButtonText = "بستن",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                string json = args.WebMessageAsJson;
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var type))
                {
                    string msgType = type.GetString();

                    if (msgType == "status")
                    {
                        var data = root.GetProperty("data");

                        string statusText = $"کل خطوط: {data.GetProperty("totalLines").GetInt32()} | " +
                                            $"کل کاراکترها: {data.GetProperty("totalChars").GetInt32()}\n" +
                                            $"خط نشانگر: {data.GetProperty("cursorLine").GetInt32()} | " +
                                            $"ستون: {data.GetProperty("cursorColumn").GetInt32()}";

                        if (data.TryGetProperty("selection", out var sel) && sel.ValueKind != JsonValueKind.Null)
                        {
                            statusText += $"\n--- Selection ---\n" +
                                          $"خط شروع: {sel.GetProperty("startLine").GetInt32()} | ستون شروع: {sel.GetProperty("startColumn").GetInt32()}\n" +
                                          $"خط پایان: {sel.GetProperty("endLine").GetInt32()} | ستون پایان: {sel.GetProperty("endColumn").GetInt32()}\n" +
                                          $"تعداد خطوط انتخاب شده: {sel.GetProperty("selectedLines").GetInt32()} | " +
                                          $"تعداد کاراکترهای انتخاب شده: {sel.GetProperty("selectedChars").GetInt32()}";
                        }

                        // نمایش در TextBlock status
                        status.Text = statusText;
                    }
                    else if (msgType == "content")
                    {
                        string codeContent = root.GetProperty("content").GetString();
                        Debug.WriteLine("📩 محتوای دریافتی از WebView2:");
                        Debug.WriteLine(codeContent);

                        // برای تست یک دیالوگ هم نشون بده
                        var ignored = ShowDialogAsync(codeContent);
                    }

                    else if (msgType == "save")
                    {
                        string codeContent = root.GetProperty("content").GetString();
                        Debug.WriteLine("Ctrl+S دریافت شد، محتوا:");
                        Debug.WriteLine(codeContent);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing JSON from WebView2: {ex.Message}");
            }
        }

        // دکمه برای دریافت محتوا از WebView2
        public void RunButton_Click(object sender, RoutedEventArgs e)
        {
            var payload = new { type = "getContent" };
            string json = JsonSerializer.Serialize(payload);
            EditorWebView.CoreWebView2.PostWebMessageAsJson(json);
        }

        // دکمه برای ست کردن محتوای ویرایشگر
        private async void SetContentButton_Click(object sender, RoutedEventArgs e)
        {
            editorTextBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 200,
                Width = 400
            };

            var dialog = new ContentDialog
            {
                Title = "تنظیم محتوای ویرایشگر",
                Content = editorTextBox,
                PrimaryButtonText = "تأیید",
                CloseButtonText = "لغو",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string newContent = editorTextBox.Text;

                // چاپ متن وارد شده برای بررسی
                Debug.WriteLine("مقدار TextBox قبل از ارسال به WebView2:");
                Debug.WriteLine(newContent);

                if (!string.IsNullOrEmpty(newContent))
                {
                    var payload = new
                    {
                        type = "setContent",
                        content = newContent
                    };
                    string json = JsonSerializer.Serialize(payload);

                    // چاپ JSON نهایی
                    Debug.WriteLine("JSON ارسالی به WebView2:");
                    Debug.WriteLine(json);

                    EditorWebView.CoreWebView2.PostWebMessageAsJson(json);
                }
            }
        }

        private async void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EditorWebView.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);

                // FIXED: Use ParentWindow to get the handle (set this in MainWindow: editorTab.ParentWindow = this;)
                if (ParentWindow != null)
                {
                    IntPtr hwnd = WindowNative.GetWindowHandle(ParentWindow);
                    SetForegroundWindow(hwnd);
                }
                else
                {
                    Debug.WriteLine("Warning: ParentWindow not set; skipping SetForegroundWindow.");
                }

                await Task.Delay(30);

                SimulateCtrlCombo("v"); // بجای SimulateCtrlV()
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PasteButton_Click error: " + ex);
            }
        }

        private void SimulateCtrlCombo(string key)
        {
            const ushort VK_CONTROL = 0x11;

            if (string.IsNullOrEmpty(key))
                return;

            // گرفتن Virtual-Key از کاراکتر
            ushort vk = (ushort)char.ToUpper(key[0]);

            INPUT[] inputs = new INPUT[4];

            // Ctrl down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL } };

            // Key down
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U = new InputUnion { ki = new KEYBDINPUT { wVk = vk } };

            // Key up
            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } };

            // Ctrl up
            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP } };

            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != inputs.Length)
            {
                int err = Marshal.GetLastWin32Error();
                Debug.WriteLine($"SendInput sent {sent}/{inputs.Length} (GetLastError={err})");
            }
        }

        private async Task ShowDialogAsync(string text)
        {
            var dialog = new ContentDialog
            {
                Title = "محتوای ویرایشگر",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap
                    },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                },
                CloseButtonText = "بستن",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}