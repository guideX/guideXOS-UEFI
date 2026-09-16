using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System.Drawing;
using System.Windows.Forms;

namespace guideXOS.DefaultApps {
    internal unsafe class WAVPlayer : Window {
        static byte[] _pcm;
        static int _index;
        static WAV.Header _header;
        public static WAVPlayer _player;
        public static string _song_name;

        Image audiopause;
        Image audioplay;

        public static bool playing;

        public WAVPlayer(int X, int Y) : base(X, Y, 200, 200) {
            ShowInTaskbar = true;
            ShowMinimize = true;
            ShowMaximize = true;
            ShowInStartMenu = true;
            IsResizable = true;
            ShowTombstone = true;
            audiopause = Icons.AudioPauseIcon(32);
            audioplay = Icons.AudioPlayIcon(32);
            Title = "WAV Player";
            _pcm = null;
            _index = 0;
            Interrupts.EnableInterrupt(0x20, &DoPlay);
            _player = this;
            _song_name = null;
            playing = false;
            clickLock = false;
        }

        bool clickLock;

        public override void OnInput() {
            base.OnInput();

            if (Control.MouseButtons.HasFlag(MouseButtons.Left)) {
                if (IsUnderMouse() && !clickLock) {
                    playing = !playing;
                    clickLock = true;
                }
            } else {
                clickLock = false;
            }
        }

        public override void OnDraw() {
            base.OnDraw();

            string s = $"Playing: {_song_name}";
            int len = WindowManager.font.MeasureString(s);
            WindowManager.font.DrawString(X + (Width / 2 - len / 2), Y + 25, s);
            s.Dispose();

            Framebuffer.Graphics.DrawImage(X + (Width / 2 - audioplay.Width / 2), Y + (Height / 2 - audioplay.Height / 2), playing ? audiopause : audioplay);
        }

        public void Play(byte[] wav, string name = "unknown") {
            TryPlay(wav, name);
        }

        /// <summary>
        /// Decode and start a bounded WAV request.  The input buffer is always
        /// consumed by this method; a failed decode leaves the previous song
        /// untouched so a reused application instance remains safe and
        /// inactive/activatable according to the App Model policy.
        /// </summary>
        public bool TryPlay(byte[] wav, string name = "unknown") {
            if (wav == null) return false;
            try {
                if (wav.Length < sizeof(WAV.Header)) return false;

                WAV.Header candidate;
                fixed (byte* p = wav) candidate = *(WAV.Header*)p;
                if (candidate.ChunkID != 0x46464952u ||
                    candidate.Format != 0x45564157u ||
                    candidate.Subchunk1ID != 0x20746d66u ||
                    candidate.Subchunk2ID != 0x61746164u ||
                    candidate.AudioFormat != 1 ||
                    candidate.NumChannels == 0 ||
                    candidate.BitsPerSample == 0 ||
                    candidate.Subchunk2Size == 0 ||
                    candidate.Subchunk2Size >
                        (uint)(wav.Length - sizeof(WAV.Header))) {
                    return false;
                }

                byte[] pcm;
                WAV.Header header;
                try {
                    WAV.Decode(wav, out pcm, out header);
                } catch {
                    return false;
                }
                if (pcm == null || pcm.Length == 0) {
                    if (pcm != null) pcm.Dispose();
                    return false;
                }

                byte[] oldPcm = _pcm;
                string oldName = _song_name;
                _pcm = pcm;
                _header = header;
                _index = 0;
                _song_name = string.IsNullOrEmpty(name) ? "unknown" : name;
                playing = true;
                if (oldPcm != null) oldPcm.Dispose();
                if (oldName != null) oldName.Dispose();
                return true;
            } finally {
                wav.Dispose();
            }
        }

        public override void Dispose() {
            playing = false;
            if (_player == this) _player = null;
            if (_pcm != null) {
                _pcm.Dispose();
                _pcm = null;
            }
            if (_song_name != null) {
                _song_name.Dispose();
                _song_name = null;
            }
            base.Dispose();
        }

        public static void DoPlay() {
            if (_pcm != null && _player.Visible && playing) {
                if (Audio.bytesWritten != 0) return;

                if (_index + Audio.CacheSize > _pcm.Length) _index = 0;

                fixed (byte* buffer = _pcm) {
                    _index += Audio.CacheSize;
                    Audio.snd_write(buffer + _index, Audio.CacheSize);
                }
            }
        }
    }
}
