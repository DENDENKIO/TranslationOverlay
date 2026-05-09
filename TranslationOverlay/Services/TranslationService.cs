using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TranslationOverlay.Services
{
    /// <summary>
    /// LibreTranslate ローカルHTTPサーバーへリクエストして翻訳する
    /// 事前に "libretranslate --host 127.0.0.1 --port 5000 --load-only en,ja" を実行しておくこと
    /// </summary>
    public class TranslationService
    {
        private readonly HttpClient _http    = new();
        private const    string     ApiUrl   = "http://localhost:5000/translate";

        /// <summary>
        /// テキストを翻訳して (翻訳結果, 処理時間ms) を返す
        /// </summary>
        public async Task<(string text, long ms)> TranslateAsync(
            string text,
            string sourceLang = "en",
            string targetLang = "ja")
        {
            var sw = Stopwatch.StartNew();

            var payload = new
            {
                q      = text,
                source = sourceLang,
                target = targetLang,
                format = "text"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var resp = await _http.PostAsync(ApiUrl, content);
            resp.EnsureSuccessStatusCode();

            var json   = await resp.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(json)
                .RootElement.GetProperty("translatedText")
                .GetString() ?? string.Empty;

            sw.Stop();
            return (result, sw.ElapsedMilliseconds);
        }

        /// <summary>短いチャンク（2〜3語）を翻訳して返す</summary>
        public async Task<string> TranslateChunkAsync(string chunk,
            string sourceLang, string targetLang)
        {
            // 短いテキストはそのままTranslateAsync再利用でOK
            var (text, _) = await TranslateAsync(chunk, sourceLang, targetLang);
            return text;
        }
    }
}
