using System;
using PocketWilds.Core;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T43: « Prêt dans 25 min » written into reused char buffers (no allocation). The time itself is the expeditions' plain
    /// real time (« 1 h 05 », « 25 min », « 40 s », rounded up: never « 0 » while the plant grows); <see cref="Key"/> tells the
    /// caller whether the shown text would change (once a minute above a minute).
    /// </summary>
    public sealed class GardenTimeText
    {
        private readonly ExpeditionTimeText _time = new ExpeditionTimeText();
        private string _readyIn = "{time}";

        public void Bind(ILocalization text)
        {
            if (text == null)
                return;
            _time.Bind(text);
            _readyIn = text.Get(GardenText.ReadyIn);
        }

        public static long Key(double seconds) => ExpeditionTimeText.Key(seconds);

        /// <summary>The time alone (« 25 min ») from <paramref name="i"/>; returns the new length.</summary>
        public int WriteTime(char[] buffer, int i, double seconds) => _time.WriteTime(buffer, i, seconds);

        /// <summary>« Prêt dans 25 min ».</summary>
        public int WriteReadyIn(char[] buffer, double seconds)
        {
            string template = _readyIn ?? "{time}";
            int at = template.IndexOf("{time}", StringComparison.Ordinal);
            if (at < 0)
                return _time.WriteTime(buffer, 0, seconds);
            int i = 0;
            for (int c = 0; c < at && i < buffer.Length; c++)
                buffer[i++] = template[c];
            i = _time.WriteTime(buffer, i, seconds);
            for (int c = at + 6; c < template.Length && i < buffer.Length; c++)
                buffer[i++] = template[c];
            return i;
        }

        /// <summary>« Prêt dans 25 min » as a string (the hub card, built when it changes; never per frame).</summary>
        public string ReadyInString(double seconds)
        {
            var buffer = new char[96];
            return new string(buffer, 0, WriteReadyIn(buffer, seconds));
        }
    }
}
