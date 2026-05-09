using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TranslationOverlay
{
    public partial class OverlayWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        private const int GWL_EXSTYLE       = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED     = 0x00080000;

        // 最後に確定表示した翻訳テキストを保持する
        private string _lastTranslation = "";

        public OverlayWindow()
        {
            InitializeComponent();
            Width  = SystemParameters.PrimaryScreenWidth;
            Height = 140;
            Left   = 0;
            Top    = SystemParameters.PrimaryScreenHeight - 150;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var hwnd    = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }

        /// <summary>認識途中：原文だけ薄く更新。翻訳欄は前回の結果を保持したまま</summary>
        public void SetPartialText(string text)
        {
            Dispatcher.InvokeAsync(() =>
            {
                OriginalText.Text    = text;
                OriginalText.Opacity = 0.55;
                // 翻訳欄は前回確定テキストをそのまま維持（消さない）
                TranslationText.Text    = _lastTranslation;
                TranslationText.Opacity = _lastTranslation == "" ? 0.0 : 1.0;
            });
        }

        /// <summary>翻訳確定：原文・翻訳ともに通常表示し、翻訳をキャッシュ</summary>
        public void SetFinalText(string original, string translation)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _lastTranslation = translation;          // キャッシュ更新

                OriginalText.Text    = original;
                OriginalText.Opacity = 0.85;

                TranslationText.Text    = translation;
                TranslationText.Opacity = 1.0;
            });
        }
    }
}
