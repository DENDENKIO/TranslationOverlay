using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;

namespace TranslationOverlay.Services
{
    /// <summary>
    /// WASAPIループバックでシステム音声（スピーカー出力）をキャプチャする
    /// </summary>
    public class AudioCaptureService : IDisposable
    {
        private WasapiLoopbackCapture? _capture;

        /// <summary>音声データが取得できたときに発火。引数は (byte[] buffer, WaveFormat format)</summary>
        public event Action<byte[], WaveFormat>? AudioDataAvailable;

        /// <summary>キャプチャ中のフォーマット（IEEE Float, 48kHz, 2ch が典型）</summary>
        public WaveFormat? CaptureFormat => _capture?.WaveFormat;

        public void Start()
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded > 0 && _capture != null)
            {
                var buffer = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, buffer, e.BytesRecorded);
                AudioDataAvailable?.Invoke(buffer, _capture.WaveFormat);
            }
        }

        public void Stop()
        {
            _capture?.StopRecording();
        }

        public void Dispose()
        {
            _capture?.Dispose();
            _capture = null;
        }
    }
}
