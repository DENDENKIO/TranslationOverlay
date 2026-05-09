using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TranslationOverlay.Services
{
    /// <summary>
    /// ローカル辞書ベースの超軽量翻訳サービス
    /// en→ja : dict/ejdict.txt
    /// ja→en : 埋め込み基本語彙
    /// ja→擬古文 : dict/giko.txt (ユーザー定義)
    /// </summary>
    public class TranslationService
    {
        // en→ja
        private readonly Dictionary<string, string> _enToJa
            = new(StringComparer.OrdinalIgnoreCase);

        // ja→en （埋め込み・最長マッチ用にキー長降順ソート）
        private readonly List<(string key, string val)> _jaToEn = new();

        // ja→擬古文 （giko.txt下読みまたは埋め込みフォールバック）
        private readonly List<(string key, string val)> _jaToKo = new();

        public TranslationService()
        {
            LoadEjdict();
            BuildJaEnDict();
            LoadGikoDict();
        }

        // ── en→ja: ejdict.txt ─────────────────────────
        private void LoadEjdict()
        {
            var path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "dict", "ejdict.txt");
            if (!File.Exists(path))
            {
                Console.WriteLine($"[Dict] ejdict.txtなし: {path}");
                return;
            }
            int c = 0;
            foreach (var line in File.ReadLines(path, System.Text.Encoding.UTF8))
            {
                var tab = line.IndexOf('\t');
                if (tab < 0) continue;
                var word    = line[..tab].Trim();
                var meaning = line[(tab+1)..].Trim();
                var sl = meaning.IndexOf('/');  if (sl > 0) meaning = meaning[..sl].Trim();
                var cm = meaning.IndexOf('、'); if (cm > 0) meaning = meaning[..cm].Trim();
                var pa = meaning.IndexOf('(');  if (pa > 0) meaning = meaning[..pa].Trim();
                if (!string.IsNullOrWhiteSpace(word) && !string.IsNullOrWhiteSpace(meaning)
                    && !_enToJa.ContainsKey(word))
                { _enToJa[word] = meaning; c++; }
            }
            Console.WriteLine($"[Dict] en→ja: {c}語");
        }

        // ── ja→擬古文: dict/giko.txt ──────────────────
        /// <summary>
        /// giko.txt のフォーマット：
        ///   現代語[TAB]擬古文  1行1ペア、#始まりはコメント
        ///   例: です[TAB]である
        ///         ます[TAB]まいる
        /// </summary>
        private void LoadGikoDict()
        {
            var path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "dict", "giko.txt");

            if (File.Exists(path))
            {
                int c = 0;
                foreach (var line in File.ReadLines(path, System.Text.Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
                    var tab = line.IndexOf('\t');
                    if (tab < 0) continue;
                    var modern  = line[..tab].Trim();
                    var classic = line[(tab+1)..].Trim();
                    if (!string.IsNullOrWhiteSpace(modern) && !string.IsNullOrWhiteSpace(classic))
                    { _jaToKo.Add((modern, classic)); c++; }
                }
                // 長いキーを優先（最長マッチ）
                _jaToKo.Sort((a, b) => b.key.Length.CompareTo(a.key.Length));
                Console.WriteLine($"[Dict] ja→擬古文: {c}語 （giko.txtから読み込み）");
            }
            else
            {
                // giko.txtがない場合は埋め込みフォールバックを使用
                Console.WriteLine("[Dict] giko.txtなし → 埋め込みフォールバック使用");
                var fallback = new (string, string)[]
                {
                    ("私","拘者"),("わたし","拘者"),("あなた","そなた"),("です","である"),
                    ("ます","まいる"),("でした","でありたる"),("ました","たりき"),
                    ("ている","てある"),("しない","せぬ"),("とても","いと"),
                    ("なぜ","なにゆえ"),("そして","かくして"),("でも","されど"),
                    ("今日","本日"),("明日","明日"),("昨日","昨日"),
                };
                _jaToKo.AddRange(fallback);
                _jaToKo.Sort((a, b) => b.key.Length.CompareTo(a.key.Length));
            }
        }

        // ── ja→en 埋め込み辞書 ───────────────────────
        private void BuildJaEnDict()
        {
            var d = new (string, string)[]
            {
                ("私","I"),("あなた","you"),("彼","he"),("彼女","she"),("彼ら","they"),("我々","we"),
                ("はい","yes"),("いいえ","no"),("ありがとう","thank you"),("ごめん","sorry"),
                ("おはよう","good morning"),("こんにちは","hello"),("こんばんは","good evening"),
                ("おやすみ","good night"),("さようなら","goodbye"),
                ("食べる","eat"),("飲む","drink"),("話す","speak"),("軳る","run"),("歩く","walk"),
                ("見る","see"),("聞く","hear"),("考える","think"),("知る","know"),("持つ","have"),
                ("行く","go"),("来る","come"),("する","do"),("作る","make"),("言う","say"),
                ("大きい","big"),("小さい","small"),("長い","long"),("短い","short"),
                ("新しい","new"),("古い","old"),("高い","high"),("低い","low"),
                ("暑い","hot"),("寒い","cold"),("いい","good"),("悪い","bad"),
                ("快い","fast"),("遅い","slow"),("正しい","correct"),("間違い","wrong"),
                ("简単","easy"),("難しい","difficult"),("大切","important"),
                ("今日","today"),("明日","tomorrow"),("昨日","yesterday"),
                ("今","now"),("すぐ","soon"),("ここ","here"),("そこ","there"),
                ("なぜ","why"),("どうやって","how"),("いつ","when"),("誰","who"),("何","what"),
                ("学校","school"),("家","house"),("車","car"),("電車","train"),
                ("食事","meal"),("水","water"),("お茶","tea"),("コーヒー","coffee"),
                ("時間","time"),("場所","place"),("人","person"),("子供","child"),("友達","friend"),
                ("天気","weather"),("雨","rain"),("雪","snow"),("風","wind"),("太陽","sun"),
                ("月","moon"),("星","star"),("海","sea"),("山","mountain"),("川","river"),
                ("病気","sick"),("音楽","music"),("映画","movie"),("本","book"),
                ("でも","but"),("そして","and"),("また","also"),("とても","very"),
                ("大丸夫","okay"),("ミーティング","meeting"),
                ("手洗いを徹底","thorough hand washing"),
            };
            _jaToEn.AddRange(d);
            _jaToEn.Sort((a, b) => b.key.Length.CompareTo(a.key.Length));
            Console.WriteLine($"[Dict] ja→en: {_jaToEn.Count}語");
        }

        // ── 公開インターフェース ──────────────────────
        public Task<string> TranslateChunkAsync(
            string chunk, string sourceLang, string targetLang)
        {
            if (sourceLang == "en")
                return Task.FromResult(LookupEnToJa(chunk));
            if (targetLang == "ko")   // ja→擬古文
                return Task.FromResult(LookupWithList(chunk, _jaToKo));
            return Task.FromResult(LookupWithList(chunk, _jaToEn));
        }

        public async Task<(string text, long ms)> TranslateAsync(
            string text, string sourceLang = "en", string targetLang = "ja")
        {
            var r = await TranslateChunkAsync(text, sourceLang, targetLang);
            return (r, 0L);
        }

        // ── 内部処理 ──────────────────────────────
        private string LookupEnToJa(string chunk)
        {
            var words   = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var results = new List<string>();
            foreach (var w in words)
            {
                var clean = w.ToLowerInvariant().Trim('.', ',', '?', '!', ':', ';', '"', '\'');
                if (string.IsNullOrEmpty(clean)) continue;
                if (_enToJa.TryGetValue(clean, out var m)) { results.Add(m); continue; }
                var s = TryStem(clean);
                if (s != null && _enToJa.TryGetValue(s, out var sm)) { results.Add(sm); continue; }
                results.Add(clean);
            }
            return string.Join(" / ", results);
        }

        /// <summary>最長マッチで入力文字列を変換</summary>
        private static string LookupWithList(string input, List<(string key, string val)> dict)
        {
            var sb  = new System.Text.StringBuilder();
            int pos = 0;
            while (pos < input.Length)
            {
                bool matched = false;
                foreach (var (key, val) in dict)
                {
                    if (pos + key.Length <= input.Length &&
                        input.AsSpan(pos, key.Length).SequenceEqual(key))
                    {
                        sb.Append(val);
                        pos += key.Length;
                        matched = true;
                        break;
                    }
                }
                if (!matched) { sb.Append(input[pos]); pos++; }
            }
            return sb.ToString();
        }

        private static string? TryStem(string word)
        {
            if (word.Length < 4) return null;
            if (word.EndsWith("ing") && word.Length > 5) return word[..^3];
            if (word.EndsWith("ed")  && word.Length > 4) return word[..^2];
            if (word.EndsWith('s')   && word.Length > 3) return word[..^1];
            return null;
        }
    }
}
