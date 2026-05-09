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
        private AudioCaptureService? _audio;
        private SttService?          _stt;
        private WordScrollOverlay?   _wordScroll;
        private readonly TranslationService _mt = new();

        private Channel<string> _sttChannel =
            Channel.CreateUnbounded<string>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
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

            _sttChannel = Channel.CreateUnbounded<string>();

            _wordScroll = new WordScrollOverlay();
            _wordScroll.Show();

            _stt = new SttService(modelPath);
            _stt.FinalResult += text => _sttChannel.Writer.TryWrite(text);
            _stt.ChunkReady  += OnChunkReady;

            _audio = new AudioCaptureService();
            _audio.AudioDataAvailable += (buffer, format) =>
                _stt?.FeedAudio(buffer, format);
            _audio.Start();

            StatusText.Text       = "● 認識中...";
            StatusText.Foreground = Brushes.Green;
            StartBtn.IsEnabled    = false;
            StopBtn.IsEnabled     = true;
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _sttChannel.Writer.Complete();

            _audio?.Stop();
            _audio?.Dispose();
            _audio = null;

            if (_stt != null) _stt.ChunkReady -= OnChunkReady;
            _stt?.Dispose();
            _stt = null;

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

            bool enToJa = true;
            Dispatcher.Invoke(() => enToJa = LangCombo.SelectedIndex == 0);
            string srcLang = enToJa ? "en" : "ja";
            string tgtLang = enToJa ? "ja" : "en";

            // 全スペースを削除（日本語はVoskが単語間にスペースを入れるため）
            var display = enToJa
                ? chunk                                 // 英語はスペースありのまま
                : chunk.Replace(" ", "");               // 日本語はスペース削除

            try
            {
                var translated = await _mt.TranslateChunkAsync(display, srcLang, tgtLang);
                _wordScroll?.AddChunks(display, translated);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Chunk MT ERROR] {ex.Message}");
            }
        }
    }
}
