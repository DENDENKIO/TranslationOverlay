using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Vosk;
using Newtonsoft.Json.Linq;

namespace TranslationOverlay.Services
{
    /// <summary>
    /// Vosk を使ったローカル音声認識（STT）サービス（チャンク即時出力対応版）
    /// </summary>
    public class SttService : IDisposable
    {
        private readonly Model           _model;
        private readonly VoskRecognizer  _recognizer;

        // ── チャンク管理 ──────────────────────────────
        private const int CHUNK_WORD_COUNT = 3;   // 何語ごとに送るか
        private List<string> _pendingWords    = new();  // 送信済みの語
        private string       _lastPartialText = "";     // 前回のPartial

        /// <summary>2〜3語チャンクが揃ったときに発火</summary>
        public event Action<string>? ChunkReady;
        // ─────────────────────────────────────────────────────

        /// <summary>認識途中テキスト</summary>
        public event Action<string>? PartialResult;

        /// <summary>認識確定テキスト</summary>
        public event Action<string>? FinalResult;

        public SttService(string modelPath)
        {
            Vosk.Vosk.SetLogLevel(-1);  // VoskのLOGを非表示
            _model      = new Model(modelPath);
            _recognizer = new VoskRecognizer(_model, 16000.0f);
            _recognizer.SetMaxAlternatives(0);
            _recognizer.SetWords(false);
        }

        /// <summary>
        /// NAudio の WASAPIバッファ（IEEE Float / 48kHz / Stereo）を受け取り
        /// 16kHz / 16bit / Mono に変換して Vosk へ渡す
        /// </summary>
        public void FeedAudio(byte[] buffer, WaveFormat sourceFormat)
        {
            try
            {
                // IEEE Float → PCM 16bit 変換パイプライン
                using var ms       = new MemoryStream(buffer);
                var raw            = new RawSourceWaveStream(ms, sourceFormat);
                var ieee           = new WaveToSampleProvider(raw);
                var mono           = new StereoToMonoSampleProvider(ieee);
                var resampled      = new WdlResamplingSampleProvider(mono, 16000);
                var pcm            = new SampleToWaveProvider16(resampled);

                var converted = new byte[buffer.Length * 2];
                int read      = pcm.Read(converted, 0, converted.Length);
                if (read == 0) return;

                var chunk = new byte[read];
                Array.Copy(converted, chunk, read);

                // Vosk へ渡して結果を取得
                if (_recognizer.AcceptWaveform(chunk, chunk.Length))
                {
                    // Final確定
                    var result  = JObject.Parse(_recognizer.Result());
                    var text    = result["text"]?.ToString() ?? "";

                    // 残りの未送信語を確定として送る
                    var finalWords = text.Split(' ',
                        StringSplitOptions.RemoveEmptyEntries).ToList();

                    // 送信済みの語数をスキップして残りだけ送る
                    var remaining = finalWords.Skip(_pendingWords.Count).ToList();
                    if (remaining.Count > 0)
                        ChunkReady?.Invoke(string.Join(" ", remaining));

                    // リセット
                    _pendingWords.Clear();
                    _lastPartialText = "";
                    FinalResult?.Invoke(text);
                }
                else
                {
                    // Partial処理
                    var partial = JObject.Parse(_recognizer.PartialResult());
                    var partialText = partial["partial"]?.ToString() ?? "";

                    if (partialText == _lastPartialText) return;
                    _lastPartialText = partialText;
                    PartialResult?.Invoke(partialText);

                    // ── チャンク分割ロジック ─────────────────────────
                    var words = partialText.Split(' ',
                        StringSplitOptions.RemoveEmptyEntries).ToList();

                    // _pendingWords より語数が増えた分だけ処理
                    while (words.Count >= _pendingWords.Count + CHUNK_WORD_COUNT)
                    {
                        // 次のチャンクを取り出す
                        var nextChunk = words
                            .GetRange(_pendingWords.Count, CHUNK_WORD_COUNT);

                        _pendingWords.AddRange(nextChunk);
                        ChunkReady?.Invoke(string.Join(" ", nextChunk));
                    }
                    // ─────────────────────────────────────────────────
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STT ERROR] {ex.Message}");
            }
        }

        public void Dispose()
        {
            _recognizer.Dispose();
            _model.Dispose();
        }
    }
}
