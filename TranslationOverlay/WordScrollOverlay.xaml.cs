using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TranslationOverlay
{
    public partial class WordScrollOverlay : Window
    {
        // ── Win32 クリックスルー ────────────────────
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        private const int GWL_EXSTYLE       = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED     = 0x00080000;
        private const int WM_NCLBUTTONDOWN  = 0xA1;
        private const int HT_BOTTOMRIGHT    = 17;
        // ────────────────────────────────────────

        private const int MAX_ROWS   = 30;   // 表示最大行数
        private const int CHUNK_SIZE = 3;

        private double _fontSize = 12;

        public WordScrollOverlay()
        {
            InitializeComponent();

            Left = SystemParameters.PrimaryScreenWidth - 320;
            Top  = 100;

            Loaded += OnLoaded;

            DragBar.MouseLeftButtonDown     += OnDragStart;
            ResizeHandle.MouseLeftButtonDown += OnResizeStart;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SetClickThrough(true);
        }

        // ── クリックスルー ON/OFF ───────────────────
        private void SetClickThrough(bool enable)
        {
            var hwnd    = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (enable)
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            else
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
        }

        // ── ドラッグ移動 ──────────────────────────
        private void OnDragStart(object sender, MouseButtonEventArgs e)
        {
            SetClickThrough(false);
            DragMove();
            SetClickThrough(true);
        }

        // ── リサイズ ──────────────────────────────
        private void OnResizeStart(object sender, MouseButtonEventArgs e)
        {
            SetClickThrough(false);
            var hwnd = new WindowInteropHelper(this).Handle;
            ReleaseCapture();
            SendMessage(hwnd, WM_NCLBUTTONDOWN,
                new IntPtr(HT_BOTTOMRIGHT), IntPtr.Zero);
            // リサイズ完了後にクリックスルーを復元
            MouseUp += OnResizeEnd;
        }

        private void OnResizeEnd(object sender, MouseButtonEventArgs e)
        {
            SetClickThrough(true);
            MouseUp -= OnResizeEnd;
        }

        // ── フォントサイズスライダー ──────────────────
        private void FontSlider_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            _fontSize = e.NewValue;
            if (FontSizeLabel != null)
                FontSizeLabel.Text = $"{(int)_fontSize}px";

            // 既存の行すべてのフォントサイズを更新
            if (WordList == null) return;
            foreach (var child in WordList.Children)
            {
                if (child is Border border &&
                    border.Child is StackPanel panel)
                {
                    foreach (var item in panel.Children)
                    {
                        if (item is TextBlock tb)
                            tb.FontSize = _fontSize;
                    }
                }
            }
        }

        // ── 単語チャンクを追加して流す ──────────────
        public void AddChunks(string originalText, string translatedText)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var origWords  = originalText.Trim().Split(' ',
                    StringSplitOptions.RemoveEmptyEntries);
                var transWords = translatedText.Trim().Split(' ',
                    StringSplitOptions.RemoveEmptyEntries);

                int maxLen = Math.Max(origWords.Length, transWords.Length);
                for (int i = 0; i < maxLen; i += CHUNK_SIZE)
                {
                    string origChunk = i < origWords.Length
                        ? string.Join(" ",
                            origWords[i..Math.Min(i + CHUNK_SIZE, origWords.Length)])
                        : "―";

                    string transChunk = i < transWords.Length
                        ? string.Join(" ",
                            transWords[i..Math.Min(i + CHUNK_SIZE, transWords.Length)])
                        : "―";

                    AddRow(origChunk, transChunk);
                }

                while (WordList.Children.Count > MAX_ROWS)
                    WordList.Children.RemoveAt(0);

                Scroller.ScrollToBottom();
            });
        }

        private void AddRow(string orig, string trans)
        {
            var row = new Border
            {
                Margin       = new Thickness(0, 1, 0, 1),
                Padding      = new Thickness(4, 2, 4, 2),
                Background   = new SolidColorBrush(
                                   Color.FromArgb(0x33, 0x22, 0x22, 0x22)),
                CornerRadius = new CornerRadius(3),
                Opacity      = 0
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            panel.Children.Add(new TextBlock
            {
                Text       = orig,
                FontSize   = _fontSize,
                Foreground = Brushes.White,
                Margin     = new Thickness(0, 0, 4, 0),
                FontFamily = new FontFamily("Yu Gothic UI")
            });

            panel.Children.Add(new TextBlock
            {
                Text       = "→",
                FontSize   = _fontSize - 1,
                Foreground = new SolidColorBrush(
                                 Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
                Margin     = new Thickness(0, 0, 4, 0)
            });

            panel.Children.Add(new TextBlock
            {
                Text       = trans,
                FontSize   = _fontSize,
                Foreground = Brushes.Yellow,
                FontFamily = new FontFamily("Yu Gothic UI")
            });

            row.Child = panel;
            WordList.Children.Add(row);

            var anim = new DoubleAnimation(0, 1,
                new Duration(TimeSpan.FromMilliseconds(300)));
            row.BeginAnimation(OpacityProperty, anim);
        }
    }
}
