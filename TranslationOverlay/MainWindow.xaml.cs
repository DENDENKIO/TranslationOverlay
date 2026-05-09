using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TranslationOverlay.Services;

namespace TranslationOverlay
{
    public partial class MainWindow : Window
    {
        private OverlayWindow?       _overlay;
        private AudioCaptureService? _audio;
        private SttService?          _stt;
        private WordScrollOverlay?   _wordScroll;
        private readonly TranslationService _mt = new();

        // STT確定テキストを翻訳ループへ渡す非同期チャネル
        private Channel<string> _sttChannel =
            Channel.CreateUnbounded<string>();

        public MainWindow()
        {
            InitializeComponent();
        }

        // ── 開始ボタン ──────────────────────────────────────
        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            // 翻訳方向を取得
            bool enToJa    = LangCombo.SelectedIndex == 0;
            string srcLang = enToJa ? "en" : "ja";
            string tgtLang = enToJa ? "ja" : "en";

            string modelName = enToJa
                ? "vosk-model-en-us-0.22-lgraph"
                : "vosk-model-ja-0.22";

            string modelPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "models", modelName);

            if (!Directory.Exists(modelPath))
            {
                MessageBox.Show(
                    $"Voskモデルが見つかりません:\n{modelPath}\n\nmodelsフォルダにモデルを配置してください。",
                    "モデル未検出",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // チャネルをリセット
            _sttChannel = Channel.CreateUnbounded<string>();

            // オーバーレイウィンドウを生成・表示
            _overlay = new OverlayWindow();
            _overlay.Show();

            _wordScroll = new WordScrollOverlay();
            _wordScroll.Show();

            // STTサービス初期化
            _stt = new SttService(modelPath);
            _stt.PartialResult += text => _overlay.SetPartialText(text);
            _stt.FinalResult   += text =>
            {
                _overlay.SetPartialText(text);           // 確定時点で即表示
                _sttChannel.Writer.TryWrite(text);       // 翻訳キューへ
            };
            _stt.ChunkReady += OnChunkReady;

            // 音声キャプチャ開始
            _audio = new AudioCaptureService();
            _audio.AudioDataAvailable += (buffer, format) =>
            {
                _stt?.FeedAudio(buffer, format);
            };
            _audio.Start();

            // 翻訳ループをバックグラウンドで起動
            _ = TranslationLoopAsync(srcLang, tgtLang);

            // UI更新
            StatusText.Text      = "● 認識中...";
            StatusText.Foreground = Brushes.Green;
            StartBtn.IsEnabled   = false;
            StopBtn.IsEnabled    = true;
        }

        // ── 翻訳ループ（バックグラウンド非同期）─────────────
        private async Task TranslationLoopAsync(string srcLang, string tgtLang)
        {
            await foreach (var text in _sttChannel.Reader.ReadAllAsync())
            {
                try
                {
                    var (translated, ms) = await _mt.TranslateAsync(text, srcLang, tgtLang);
                    _overlay?.SetFinalText(text, translated);
                    // _wordScroll?.AddChunks(text, translated);  // チャンクで送信済みのため削除
                    Console.WriteLine($"[翻訳 {ms}ms] {translated}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MT ERROR] {ex.Message}");
                    _overlay?.SetFinalText("[Error]", ex.Message);
                }
            }
        }

        // ── 停止ボタン ──────────────────────────────────────
        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _sttChannel.Writer.Complete();   // 翻訳ループを終了

            _audio?.Stop();
            _audio?.Dispose();
            _audio = null;

            if (_stt != null) _stt.ChunkReady -= OnChunkReady;
            _stt?.Dispose();
            _stt = null;

            _overlay?.Close();
            _overlay = null;

            _wordScroll?.Close();
            _wordScroll = null;

            StatusText.Text       = "● 停止中";
            StatusText.Foreground = Brushes.Gray;
            StartBtn.IsEnabled    = true;
            StopBtn.IsEnabled     = false;
        }

        private async void OnChunkReady(string chunk)
        {
            if (string.IsNullOrWhiteSpace(chunk)) return;

            // 翻訳方向を取得
            bool enToJa = true;
            Dispatcher.Invoke(() => enToJa = LangCombo.SelectedIndex == 0);
            string srcLang = enToJa ? "en" : "ja";
            string tgtLang = enToJa ? "ja" : "en";

            // チャンクを即翻訳（Partial中でも発火する）
            try
            {
                var translated = await _mt.TranslateChunkAsync(chunk, srcLang, tgtLang);
                _wordScroll?.AddChunks(chunk, translated);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Chunk MT ERROR] {ex.Message}");
            }
        }
    }
}