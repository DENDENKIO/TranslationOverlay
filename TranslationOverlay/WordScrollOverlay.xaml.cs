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
        [DllImport("user32.dll")] static extern int  GetWindowLong(IntPtr hwnd, int idx);
        [DllImport("user32.dll")] static extern int  SetWindowLong(IntPtr hwnd, int idx, int val);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr w, IntPtr l);

        const int GWL_EXSTYLE      = -20;
        const int WS_EX_TRANSPARENT= 0x20;
        const int WS_EX_LAYERED    = 0x80000;
        const int WM_NCLBUTTONDOWN = 0xA1;
        const int HTBOTTOMRIGHT    = 17;

        const int MAX_ROWS = 30;

        private double _fontSize = 12;
        private IntPtr _hwnd;

        public WordScrollOverlay()
        {
            InitializeComponent();
            Left = SystemParameters.PrimaryScreenWidth - 320;
            Top  = 100;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
        }

        // ── ドラッグ移動 ────────────────────────
        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DragMove();
        }

        private void ControlBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not Slider)
                DragMove();
        }

        // ── リサイズ ───────────────────────────
        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ReleaseCapture();
            SendMessage(_hwnd, WM_NCLBUTTONDOWN,
                new IntPtr(HTBOTTOMRIGHT), IntPtr.Zero);
        }

        // ── フォントサイズスライダー ───────────────
        private void FontSlider_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            _fontSize = e.NewValue;
            if (FontSizeLabel != null)
                FontSizeLabel.Text = $"{(int)_fontSize}px";

            if (WordList == null) return;
            foreach (var child in WordList.Children)
                if (child is Border b && b.Child is StackPanel sp)
                    foreach (var item in sp.Children)
                        if (item is TextBlock tb)
                            tb.FontSize = _fontSize;
        }

        // ── 認識テキスト + 翻訳テキストを 1行で表示 ──
        /// <summary>
        /// orig / trans をそのまま 1行として追加する。
        /// チャンク分割は行わず、認識単位でそのまま表示。
        /// </summary>
        public void AddChunks(string originalText, string translatedText)
        {
            if (string.IsNullOrWhiteSpace(originalText)) return;

            Dispatcher.InvokeAsync(() =>
            {
                AddRow(originalText.Trim(), translatedText.Trim());

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
                Padding      = new Thickness(6, 3, 6, 3),
                Background   = new SolidColorBrush(Color.FromArgb(0x44, 0x22, 0x22, 0x22)),
                CornerRadius = new CornerRadius(4),
                Opacity      = 0
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            // 元テキスト（白）
            panel.Children.Add(new TextBlock
            {
                Text                = orig,
                FontSize            = _fontSize,
                Foreground          = Brushes.White,
                Margin              = new Thickness(0, 0, 6, 0),
                FontFamily          = new FontFamily("Yu Gothic UI"),
                TextWrapping        = TextWrapping.NoWrap,
                VerticalAlignment   = VerticalAlignment.Center
            });

            // 矢印
            panel.Children.Add(new TextBlock
            {
                Text              = "→",
                FontSize          = _fontSize - 1,
                Foreground        = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
                Margin            = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            // 翻訳（黄）
            panel.Children.Add(new TextBlock
            {
                Text              = string.IsNullOrWhiteSpace(trans) ? "--" : trans,
                FontSize          = _fontSize,
                Foreground        = Brushes.Yellow,
                FontFamily        = new FontFamily("Yu Gothic UI"),
                TextWrapping      = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            });

            row.Child = panel;
            WordList.Children.Add(row);

            var anim = new DoubleAnimation(0, 1,
                new Duration(TimeSpan.FromMilliseconds(250)));
            row.BeginAnimation(OpacityProperty, anim);
        }
    }
}
