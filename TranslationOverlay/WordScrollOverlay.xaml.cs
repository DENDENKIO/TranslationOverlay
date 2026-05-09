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
        // ── Win32 ──────────────────────────────────
        [DllImport("user32.dll")]
        static extern int  GetWindowLong(IntPtr hwnd, int idx);
        [DllImport("user32.dll")]
        static extern int  SetWindowLong(IntPtr hwnd, int idx, int val);
        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr w, IntPtr l);

        const int GWL_EXSTYLE       = -20;
        const int WS_EX_TRANSPARENT = 0x20;
        const int WS_EX_LAYERED     = 0x80000;
        const int WM_NCLBUTTONDOWN  = 0xA1;
        const int HTBOTTOMRIGHT     = 17;
        // ─────────────────────────────────────

        const int MAX_ROWS   = 30;
        const int CHUNK_SIZE = 3;

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
            // 本文エリアのみクリックスルー
            // DragBar / ResizeHandle / FontSlider は上記XAML側で
            // IsHitTestVisible="True" なのでWPFレベルでは反応する。
            // ただしWindowStyle=Noneの稼流しウィンドウはWin32側の
            // Transparentビットがあるとマウスイベントそのものが
            // OS層でブロックされるため、
            // 最初はクリックスルーなしで起動する。
            // テキストエリアだけクリックスルーにするには
            // WM_NCHITTESTのサブクラス指定が必要なので
            // 今回は全体にクリックを受ける方式にする。
            // 必要なら後でWS_EX_TRANSPARENTを追加可能。
        }

        // ── ドラッグ移動 ──────────────────────────
        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DragMove();
        }

        // スライダーバーのクリックをDragMoveに少れないよう捕捉
        private void ControlBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Slider以外の領域をドラッグで移動
            if (e.OriginalSource is not Slider)
                DragMove();
        }

        // ── リサイズ ─────────────────────────────
        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ReleaseCapture();
            SendMessage(_hwnd, WM_NCLBUTTONDOWN,
                new IntPtr(HTBOTTOMRIGHT), IntPtr.Zero);
        }

        // ── フォントサイズスライダー ──────────────────
        private void FontSlider_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            _fontSize = e.NewValue;
            if (FontSizeLabel != null)
                FontSizeLabel.Text = $"{(int)_fontSize}px";

            if (WordList == null) return;
            foreach (var child in WordList.Children)
            {
                if (child is Border border &&
                    border.Child is StackPanel panel)
                {
                    foreach (var item in panel.Children)
                        if (item is TextBlock tb)
                            tb.FontSize = _fontSize;
                }
            }
        }

        // ── 行を追加 ──────────────────────────────
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
                        ? string.Join(" ", origWords[i..Math.Min(i+CHUNK_SIZE, origWords.Length)])
                        : "―";
                    string transChunk = i < transWords.Length
                        ? string.Join(" ", transWords[i..Math.Min(i+CHUNK_SIZE, transWords.Length)])
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
                Background   = new SolidColorBrush(Color.FromArgb(0x33,0x22,0x22,0x22)),
                CornerRadius = new CornerRadius(3),
                Opacity      = 0
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            panel.Children.Add(new TextBlock
            {
                Text       = orig,
                FontSize   = _fontSize,
                Foreground = Brushes.White,
                Margin     = new Thickness(0,0,4,0),
                FontFamily = new FontFamily("Yu Gothic UI")
            });
            panel.Children.Add(new TextBlock
            {
                Text       = "→",
                FontSize   = _fontSize - 1,
                Foreground = new SolidColorBrush(Color.FromArgb(0xAA,0xFF,0xFF,0xFF)),
                Margin     = new Thickness(0,0,4,0)
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
