# argos_server.py
# Argos Translate を HTTP サーバーとしてラップする軽量翻訳サーバー
# WPF側のコードは変更不要。http://localhost:5000/translate で待ち受ける
# 使い方: python argos_server.py

import json
import argostranslate.translate
from http.server import HTTPServer, BaseHTTPRequestHandler

HOST = "127.0.0.1"
PORT = 5000

class TranslationHandler(BaseHTTPRequestHandler):

    # アクセスログを非表示
    def log_message(self, format, *args):
        pass

    def do_GET(self):
        # ブラウザで http://localhost:5000 を開いたときの確認用
        if self.path == "/":
            body = b"Argos Translate Server is running."
            self.send_response(200)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        else:
            self.send_response(404)
            self.end_headers()

    def do_POST(self):
        if self.path != "/translate":
            self.send_response(404)
            self.end_headers()
            return

        try:
            length = int(self.headers.get("Content-Length", 0))
            body   = self.rfile.read(length)
            data   = json.loads(body)

            text   = data.get("q", "").strip()
            src    = data.get("source", "en")
            tgt    = data.get("target", "ja")

            if not text:
                result = ""
            else:
                result = argostranslate.translate.translate(text, src, tgt)

            response = json.dumps(
                {"translatedText": result},
                ensure_ascii=False
            ).encode("utf-8")

            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(response)))
            self.end_headers()
            self.wfile.write(response)

        except Exception as e:
            error = json.dumps({"error": str(e)}).encode("utf-8")
            self.send_response(500)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(error)
            print(f"[ERROR] {e}")


def check_language_pairs():
    """起動時に en<->ja モデルが入っているか確認する"""
    installed = argostranslate.translate.get_installed_languages()
    codes     = {lang.code for lang in installed}

    missing = []
    if "en" not in codes or "ja" not in codes:
        missing.append("en -> ja")

    if missing:
        print("[WARN] 以下の言語モデルが未インストールです:")
        for m in missing:
            print(f"       {m}")
        print("[WARN] 以下のコマンドでインストールしてください:")
        print("       pip install argostranslate")
        print("       argospm install translate-en_ja")
        print("       argospm install translate-ja_en")
        return False

    print(f"[OK] インストール済み言語: {sorted(codes)}")
    return True


if __name__ == "__main__":
    print("=" * 50)
    print(" Argos Translate 軽量サーバー")
    print(f" http://{HOST}:{PORT}/translate")
    print(" Ctrl+C で停止")
    print("=" * 50)

    if not check_language_pairs():
        print("[ERROR] 言語モデルが不足しています。上記コマンドを実行してください。")
        input("Enterキーで終了...")
        exit(1)

    print(f"[START] サーバー起動中... http://{HOST}:{PORT}")
    server = HTTPServer((HOST, PORT), TranslationHandler)

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n[STOP] サーバーを停止しました。")
        server.server_close()
