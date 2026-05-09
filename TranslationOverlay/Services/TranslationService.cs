using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TranslationOverlay.Services
{
    /// <summary>
    /// ejdict-hand ローカル辞書を使った超軽量英和・和英翻訳サービス
    /// HTTPサーバー不要・翻訳遅延ほぼゼロ
    /// </summary>
    public class TranslationService
    {
        // 英単語 → 日本語訳 （大文字小文字無視）
        private readonly Dictionary<string, string> _enToJa
            = new(StringComparer.OrdinalIgnoreCase);

        // 日本語訳 → 英単語 （和英逆引き）
        private readonly Dictionary<string, string> _jaToEn
            = new(StringComparer.Ordinal);

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

                // 最初の「、」より前だけ使う
                var comma = meaning.IndexOf('、');
                if (comma > 0) meaning = meaning[..comma].Trim();

                // 最初の「(」より前だけ使う
                var paren = meaning.IndexOf('(');
                if (paren > 0) meaning = meaning[..paren].Trim();

                if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(meaning))
                    continue;

                // 英和登録
                if (!_enToJa.ContainsKey(word))
                    _enToJa[word] = meaning;

                // 和英逆引き登録（日本語訳をキーに、英単語を値に）
                // 同じ日本語訳が複数登録される場合は最初の英単語を使う
                if (!_jaToEn.ContainsKey(meaning))
                    _jaToEn[meaning] = word;

                count++;
            }

            Console.WriteLine($"[Dict] {count} 語読み込み完了 (英和:{_enToJa.Count} / 和英:{_jaToEn.Count})");
        }

        /// <summary>
        /// チャンク（2〜3語）を単語ごとに辞書引きして返す
        /// </summary>
        public Task<string> TranslateChunkAsync(
            string chunk, string sourceLang, string targetLang)
        {
            return sourceLang == "en"
                ? Task.FromResult(TranslateEnToJa(chunk))
                : Task.FromResult(TranslateJaToEn(chunk));
        }

        // ── 英和（en→ja） ─────────────────────────────────
        private string TranslateEnToJa(string chunk)
        {
            var words   = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var results = new List<string>();

            foreach (var w in words)
            {
                var clean = w.ToLowerInvariant().Trim('.', ',', '?', '!', ':', ';', '"', '\'');
                if (string.IsNullOrEmpty(clean)) continue;

                if (_enToJa.TryGetValue(clean, out var meaning))
                    results.Add(meaning);
                else
                {
                    var stemmed = TryStem(clean);
                    if (stemmed != null && _enToJa.TryGetValue(stemmed, out var sm))
                        results.Add(sm);
                    else
                        results.Add(clean);
                }
            }

            return string.Join(" / ", results);
        }

        // ── 和英（ja→en） ─────────────────────────────────
        private string TranslateJaToEn(string chunk)
        {
            // 日本語はスペースで分割できないため、辞書の日本語訳をキーに総当たり検索
            var results = new List<string>();

            // 入力文字列を辞書の和訳キーと照合
            // 最長マッチアルゴリズム
            int pos = 0;
            while (pos < chunk.Length)
            {
                bool matched = false;

                // 長い文字列から順に全体照合を試みる
                for (int len = Math.Min(chunk.Length - pos, 10); len >= 1; len--)
                {
                    var candidate = chunk.Substring(pos, len);
                    if (_jaToEn.TryGetValue(candidate, out var eng))
                    {
                        results.Add(eng);
                        pos += len;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    // 照合しない文字は1文字ずつ進む
                    results.Add(chunk[pos].ToString());
                    pos++;
                }
            }

            return string.Join(" ", results);
        }

        /// <summary>簡易ステミング</summary>
        private static string? TryStem(string word)
        {
            if (word.Length < 4) return null;
            if (word.EndsWith("ing") && word.Length > 5) return word[..^3];
            if (word.EndsWith("ed")  && word.Length > 4) return word[..^2];
            if (word.EndsWith('s')   && word.Length > 3) return word[..^1];
            return null;
        }

        /// <summary>後方互換用</summary>
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
