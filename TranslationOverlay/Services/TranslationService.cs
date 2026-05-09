using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TranslationOverlay.Services
{
    /// <summary>
    /// ローカル辞書ベースの超軽量翻訳サービス
    /// en→ja : ejdict.txt
    /// ja→en : 埋め込み基本語彙辞書
    /// ja→擬古文 : 埋め込み変換辞書
    /// </summary>
    public class TranslationService
    {
        // en→ja
        private readonly Dictionary<string, string> _enToJa
            = new(StringComparer.OrdinalIgnoreCase);

        // ja→en （埋め込み・最長マッチ用にキー長降順ソート）
        private readonly List<(string ja, string en)> _jaToEn = new();

        // ja→擬古文
        private readonly List<(string modern, string classic)> _jaToKo = new();

        public TranslationService()
        {
            LoadEjdict();
            BuildJaEnDict();
            BuildClassicDict();
        }

        // ───────────────────────────────────────
        private void LoadEjdict()
        {
            var path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "dict", "ejdict.txt");
            if (!File.Exists(path)) { Console.WriteLine($"[Dict] 辞書なし: {path}"); return; }

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

        // ── ja→en 埋め込み辞書 ────────────────────
        private void BuildJaEnDict()
        {
            // 日常語 基本500語
            var d = new (string, string)[]
            {
                ("私","I"),("あなた","you"),("彼","he"),("彼女","she"),("彼ら","they"),("我々","we"),
                ("はい","yes"),("いいえ","no"),("ありがとう","thank you"),("ごめん","sorry"),
                ("おはよう","good morning"),("こんにちは","hello"),("こんばんは","good evening"),
                ("おやすみ","good night"),("さようなら","goodbye"),
                ("食べる","eat"),("飲む","drink"),("話す","speak"),("軳る","run"),("歩く","walk"),
                ("見る","see"),("聞く","hear"),("考える","think"),("知る","know"),("持つ","have"),
                ("行く","go"),("来る","come"),("する","do"),("作る","make"),("言う","say"),
                ("楽しい","fun"),("気持ちいい","feel good"),("農気","feeling"),
                ("大きい","big"),("小さい","small"),("長い","long"),("短い","short"),
                ("新しい","new"),("古い","old"),("高い","high"),("低い","low"),
                ("暑い","hot"),("寒い","cold"),("いい","good"),("悪い","bad"),
                ("快い","fast"),("遅い","slow"),("帷い","wide"),("細い","narrow"),
                ("正しい","correct"),("間違い","wrong"),("简単","easy"),("難しい","difficult"),
                ("吉な","good"),("悪な","bad"),("大切","important"),
                ("今日","today"),("明日","tomorrow"),("昨日","yesterday"),
                ("今","now"),("すぐ","soon"),("あとで","later"),("いつも","always"),
                ("時々","sometimes"),("決して","never"),
                ("ここ","here"),("そこ","there"),("どこ","where"),
                ("なぜ","why"),("どうやって","how"),("いつ","when"),("誰","who"),("何","what"),
                ("学校","school"),("仍事","work"),("家","house"),("車","car"),("電車","train"),
                ("食事","meal"),("水","water"),("お茶","tea"),("コーヒー","coffee"),
                ("時間","time"),("場所","place"),("人","person"),("子供","child"),("友達","friend"),
                ("先生","teacher"),("医者","doctor"),("警察","police"),
                ("楽しむ","enjoy"),("休む","rest"),("勅む","study"),("授業","class"),
                ("天気","weather"),("雨","rain"),("雪","snow"),("風","wind"),("太陽","sun"),
                ("月","moon"),("星","star"),("海","sea"),("山","mountain"),("川","river"),
                ("手洗い","hand washing"),("徐菌","disinfection"),("マスク","mask"),
                ("病気","sick"),("音楽","music"),("映画","movie"),("本","book"),
                ("鉄很","certainly"),("必ず","surely"),("実は","actually"),
                ("でも","but"),("だから","because"),("だからこそ","therefore"),
                ("それで","then"),("そして","and"),("また","also"),
                ("あまり","not much"),("とても","very"),("すごく","extremely"),
                ("徐々に","gradually"),("すっかり","completely"),
                ("約束","promise"),("目標","goal"),("未来","future"),("過去","past"),
                ("現在","present"),("問題","problem"),("解決","solution"),
                ("手洗いを徹底","thorough hand washing"),
                ("大丈夫","okay"),("大変","hard"),("ミーティング","meeting"),
            };
            _jaToEn.AddRange(d);
            // 長いキーを優先にソート（最長マッチ用）
            _jaToEn.Sort((a, b) => b.ja.Length.CompareTo(a.ja.Length));
            Console.WriteLine($"[Dict] ja→en: {_jaToEn.Count}語");
        }

        // ── ja→擬古文 辞書 ──────────────────────
        private void BuildClassicDict()
        {
            var d = new (string, string)[]
            {
                // 人称
                ("私","拘者"),("わたし","拘者"),("ぼく","挙"),("あなた","そなた"),("きみ","そなた"),
                ("彼","彼の者"),("彼女","彼の女"),("彼ら","彼の者ども"),("我々","我ら"),
                // 動詞終止形
                ("です","である"),("ます","る"),("でした","でありたる"),("ました","たりき"),
                ("ている","てある"),("ています","てある"),("します","まいりる"),
                ("しない","せぬ"),("しません","まいらぬ"),
                ("できる","できる"),("できます","できやる"),
                ("いきます","まいりる"),("きます","まいる"),
                ("思います","思ひまいる"),("思っています","思ひてあります"),
                ("言います","申しまいる"),("言った","申したりき"),
                ("見ます","見まいる"),("見て","見て"),
                ("した","たりき"),("する","する"),("して","して"),
                ("ある","あり"),("ない","なし"),
                // 助詞・利用
                ("とても","いと"),("すごく","いと"),("なぜ","なにゆえ"),
                ("そして","かくして"),("でも","されど"),("だから","なればこそ"),
                ("また","また"),("どうぞ","いかにも"),
                // 副詞
                ("とても","いと"),("少し","しばし"),("もっと","いっそう"),
                ("まだ","いまだ"),("もう","すでに"),("まだまだ","いまだしかし"),
                // 指示語
                ("これ","これ"),("それ","それ"),("あれ","あれ"),
                ("ここ","ここ"),("そこ","そこ"),("あそこ","あそこ"),
                // 時制
                ("今日","本日"),("明日","明日"),("昨日","昨日"),("今","ただ今"),
                ("時に","をりに"),("少しの間","しばらくの間"),
                // 日常語哣
                ("手洗いを徹底に","手洗いを徹底にいたすべきなり"),
                ("おはようございます","いともおはようございます"),
                ("ありがとうございます","笙しきことに候います"),
                ("実は","さにあらば"),("大丸夫","期更なし"),
            };
            _jaToKo.AddRange(d);
            _jaToKo.Sort((a, b) => b.modern.Length.CompareTo(a.modern.Length));
            Console.WriteLine($"[Dict] ja→擬古文: {_jaToKo.Count}語");
        }

        // ── 公開インターフェース ────────────────────
        public Task<string> TranslateChunkAsync(
            string chunk, string sourceLang, string targetLang)
        {
            if (sourceLang == "en")
                return Task.FromResult(LookupEnToJa(chunk));
            if (targetLang == "ko")   // ja→擬古文
                return Task.FromResult(LookupWithList(chunk, _jaToKo));
            return Task.FromResult(LookupWithList(chunk,    // ja→en
                _jaToEn.ConvertAll(x => (x.ja, x.en))));
        }

        public async Task<(string text, long ms)> TranslateAsync(
            string text, string sourceLang = "en", string targetLang = "ja")
        {
            var r = await TranslateChunkAsync(text, sourceLang, targetLang);
            return (r, 0L);
        }

        // ── 内部処理 ─────────────────────────────
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
