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
        // ── Win32 クリックスルー ──────────────────────────
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        private const int GWL_EXSTYLE       = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED     = 0x00080000;
        // ─────────────────────────────────────────────────

        private const int MAX_ROWS   = 18;   // 表示する最大行数
        private const int CHUNK_SIZE = 3;    // 何単語ずつ区切るか

        public WordScrollOverlay()
        {
            InitializeComponent();

            // 初期位置：画面右側
            Left = SystemParameters.PrimaryScreenWidth - 280;
            Top  = 100;

            Loaded += OnLoaded;

            // ドラッグ移動（DragBarのみ）
            DragBar.MouseLeftButtonDown += OnDragStart;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // DragBar以外はクリックスルー
            var hwnd    = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }

        // ── ドラッグ移動 ────────────────────────────────
        private void OnDragStart(object sender, MouseButtonEventArgs e)
        {
            // ドラッグバーはクリックスルーを一時解除して移動
            var hwnd    = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            // WS_EX_TRANSPARENTを外す
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle & ~WS_EX_TRANSPARENT);

            DragMove();

            // ドラッグ終了後にクリックスルーを再設定
            exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT);
        }

        // ── 単語チャンクを追加して流す ─────────────────
        public void AddChunks(string originalText, string translatedText)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var origWords  = originalText.Trim().Split(' ',
                    StringSplitOptions.RemoveEmptyEntries);
                var transWords = translatedText.Trim().Split(' ',
                    StringSplitOptions.RemoveEmptyEntries);

                // CHUNK_SIZE ずつ区切ってペア生成
                int maxLen = Math.Max(origWords.Length, transWords.Length);
                for (int i = 0; i < maxLen; i += CHUNK_SIZE)
                {
                    // 原文チャンク
                    // i が origWords.Length を超える場合を考慮
                    string origChunk = i < origWords.Length
                        ? string.Join(" ",
                            origWords[i..Math.Min(i + CHUNK_SIZE, origWords.Length)])
                        : "―"; // 翻訳チャンクと同様にプレースホルダーを使用

                    // 翻訳チャンク（単語数が少ない場合は空白）
                    string transIdx  = i < transWords.Length
                        ? string.Join(" ",
                            transWords[i..Math.Min(i + CHUNK_SIZE, transWords.Length)])
                        : "―";

                    AddRow(origChunk, transIdx);
                }

                // 古い行を削除
                while (WordList.Children.Count > MAX_ROWS)
                    WordList.Children.RemoveAt(0);

                // 最下部へスクロール
                Scroller.ScrollToBottom();
            });
        }

        private void AddRow(string orig, string trans)
        {
            // 行コンテナ
            var row = new Border
            {
                Margin          = new Thickness(0, 1, 0, 1),
                Padding         = new Thickness(4, 2, 4, 2),
                Background      = new SolidColorBrush(Color.FromArgb(0x33, 0x22, 0x22, 0x22)),
                CornerRadius    = new CornerRadius(3),
                Opacity         = 0
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            // 原文（白・極小）
            panel.Children.Add(new TextBlock
            {
                Text       = orig,
                FontSize   = 10,
                Foreground = Brushes.White,
                Margin     = new Thickness(0, 0, 4, 0),
                FontFamily = new FontFamily("Yu Gothic UI")
            });

            // 矢印
            panel.Children.Add(new TextBlock
            {
                Text       = "→",
                FontSize   = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
                Margin     = new Thickness(0, 0, 4, 0)
            });

            // 翻訳（黄色・極小）
            panel.Children.Add(new TextBlock
            {
                Text       = trans,
                FontSize   = 10,
                Foreground = Brushes.Yellow,
                FontFamily = new FontFamily("Yu Gothic UI")
            });

            row.Child = panel;
            WordList.Children.Add(row);

            // フェードイン
            var anim = new DoubleAnimation(0, 1,
                new Duration(TimeSpan.FromMilliseconds(300)));
            row.BeginAnimation(OpacityProperty, anim);
        }
    }
}
