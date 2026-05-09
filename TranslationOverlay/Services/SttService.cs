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

        // ── チャンク管理 ────────────────────────────
        private const int    CHUNK_WORD_COUNT  = 3;
        private List<string> _pendingWords     = new();
        private string       _lastPartialText  = "";

        /// <summary>2〖3語チャンクが揃ったときまたはFinal確定時に発火</summary>
        public event Action<string>? ChunkReady;

        /// <summary>認識途中テキスト</summary>
        public event Action<string>? PartialResult;

        /// <summary>認識確定テキスト</summary>
        public event Action<string>? FinalResult;

        public SttService(string modelPath)
        {
            Vosk.Vosk.SetLogLevel(-1);
            _model      = new Model(modelPath);
            _recognizer = new VoskRecognizer(_model, 16000.0f);
            _recognizer.SetMaxAlternatives(0);
            _recognizer.SetWords(false);
        }

        public void FeedAudio(byte[] buffer, WaveFormat sourceFormat)
        {
            try
            {
                using var ms  = new MemoryStream(buffer);
                var raw        = new RawSourceWaveStream(ms, sourceFormat);
                var ieee       = new WaveToSampleProvider(raw);
                var mono       = new StereoToMonoSampleProvider(ieee);
                var resampled  = new WdlResamplingSampleProvider(mono, 16000);
                var pcm        = new SampleToWaveProvider16(resampled);

                var converted = new byte[buffer.Length * 2];
                int read      = pcm.Read(converted, 0, converted.Length);
                if (read == 0) return;

                var audioChunk = new byte[read];
                Array.Copy(converted, audioChunk, read);

                if (_recognizer.AcceptWaveform(audioChunk, audioChunk.Length))
                {
                    // ── Final確定 ──────────────────────────────
                    var result = JObject.Parse(_recognizer.Result());
                    var text   = result["text"]?.ToString() ?? "";

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var finalWords = text
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                            .ToList();

                        // Partialチャンクで未送信の残り語を送信
                        var remaining = finalWords.Skip(_pendingWords.Count).ToList();
                        if (remaining.Count > 0)
                            ChunkReady?.Invoke(string.Join(" ", remaining));
                        else if (_pendingWords.Count == 0)
                            // Partialチャンクが一度も発火されなかった場合は全文を送る
                            ChunkReady?.Invoke(text);
                    }

                    // リセット
                    _pendingWords.Clear();
                    _lastPartialText = "";
                    FinalResult?.Invoke(text);
                }
                else
                {
                    // ── Partial処理 ─────────────────────────────
                    var partial     = JObject.Parse(_recognizer.PartialResult());
                    var partialText = partial["partial"]?.ToString() ?? "";

                    if (partialText == _lastPartialText) return;
                    _lastPartialText = partialText;
                    PartialResult?.Invoke(partialText);

                    if (string.IsNullOrWhiteSpace(partialText)) return;

                    var words = partialText
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .ToList();

                    // CHUNK_WORD_COUNT 語たまるたびに発火
                    while (words.Count >= _pendingWords.Count + CHUNK_WORD_COUNT)
                    {
                        var nextChunk = words
                            .GetRange(_pendingWords.Count, CHUNK_WORD_COUNT);
                        _pendingWords.AddRange(nextChunk);
                        ChunkReady?.Invoke(string.Join(" ", nextChunk));
                    }
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
