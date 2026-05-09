using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TranslationOverlay.Services
{
    /// <summary>
    /// ejdict-hand ローカル辞書を使った超軽量英和翻訳サービス
    /// HTTPサーバー不要・翻訳遅延ほぼゼロ
    /// </summary>
    public class TranslationService
    {
        // 英単語 → 日本語訳 の辞書（大文字小文字無視）
        private readonly Dictionary<string, string> _dict
            = new(StringComparer.OrdinalIgnoreCase);

        public TranslationService()
        {
            LoadDictionary();
        }

        private void LoadDictionary()
        {
            var path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "dict", "ejdict.txt");

            if (!File.Exists(path))
            {
                Console.WriteLine($"[Dict] 辞書ファイルが見つかりません: {path}");
                return;
            }

            int count = 0;
            foreach (var line in File.ReadLines(path, System.Text.Encoding.UTF8))
            {
                var tab = line.IndexOf('\t');
                if (tab < 0) continue;

                var word    = line[..tab].Trim();
                var meaning = line[(tab + 1)..].Trim();

                // 最初の「/」より前だけ使う
                var slash = meaning.IndexOf('/');
                if (slash > 0) meaning = meaning[..slash].Trim();

                // 最初の「、」より前だけ使う（短くする）
                var comma = meaning.IndexOf('、');
                if (comma > 0) meaning = meaning[..comma].Trim();

                // 最初の「(」より前だけ使う
                var paren = meaning.IndexOf('(');
                if (paren > 0) meaning = meaning[..paren].Trim();

                if (!string.IsNullOrWhiteSpace(word) && !_dict.ContainsKey(word))
                {
                    _dict[word] = meaning;
                    count++;
                }
            }

            Console.WriteLine($"[Dict] {count} 語読み込み完了");
        }

        /// <summary>
        /// チャンク（2〜3語）を単語ごとに辞書引きして返す
        /// </summary>
        public Task<string> TranslateChunkAsync(
            string chunk, string sourceLang, string targetLang)
        {
            // ja→en は未対応（そのまま返す）
            if (sourceLang != "en")
                return Task.FromResult(chunk);

            var words   = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var results = new List<string>();

            foreach (var w in words)
            {
                // 記号を除去して小文字化
                var clean = w.ToLowerInvariant().Trim('.', ',', '?', '!', ':', ';', '"', '\'');

                if (string.IsNullOrEmpty(clean)) continue;

                if (_dict.TryGetValue(clean, out var meaning))
                    results.Add(meaning);
                else
                {
                    // 末尾 s / ed / ing の簡易ステミング
                    var stemmed = TryStem(clean);
                    if (stemmed != null && _dict.TryGetValue(stemmed, out var stemMeaning))
                        results.Add(stemMeaning);
                    else
                        results.Add(clean);  // 未知語はそのまま
                }
            }

            return Task.FromResult(string.Join(" / ", results));
        }

        /// <summary>簡易ステミング（s / ed / ing を除去して再検索）</summary>
        private static string? TryStem(string word)
        {
            if (word.Length < 4) return null;

            if (word.EndsWith("ing") && word.Length > 5)
                return word[..^3];            // running → run
            if (word.EndsWith("ing") && word.Length > 5)
                return word[..^3] + "e";      // coming → come
            if (word.EndsWith("ed") && word.Length > 4)
                return word[..^2];            // talked → talk
            if (word.EndsWith("ed") && word.Length > 4)
                return word[..^1];            // liked → like
            if (word.EndsWith('s') && word.Length > 3)
                return word[..^1];            // cats → cat

            return null;
        }

        /// <summary>後方互換用（TranslationLoopAsync から呼ばれる場合）</summary>
        public async Task<(string text, long ms)> TranslateAsync(
            string text,
            string sourceLang = "en",
            string targetLang = "ja")
        {
            var result = await TranslateChunkAsync(text, sourceLang, targetLang);
            return (result, 0L);
        }
    }
}
