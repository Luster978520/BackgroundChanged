using System.Collections.Generic;
using System.Windows;

namespace BackgroundChanged.Controls
{
    internal struct TrailSample
    {
        public Point Position;
        public double Time;
        public double Phase;
        public bool StartsStroke;
    }

    // Time and position are supplied by the view; this buffer has no ViewModel or event subscriptions.
    internal sealed class TrailBuffer
    {
        internal const int Capacity = 128;
        public List<TrailSample> Samples { get; } = new List<TrailSample>(Capacity);
        private bool startsStroke = true;

        public bool Add(Point position, double time)
        {
            if (Samples.Count > 0)
            {
                var previous = Samples[Samples.Count - 1];
                if (!startsStroke && time - previous.Time <= .16 && (position - previous.Position).LengthSquared < 4)
                    return false;
                if (time - previous.Time > .16) startsStroke = true;
            }
            Samples.Add(new TrailSample { Position = position, Time = time, Phase = (time / 8) % 1, StartsStroke = startsStroke });
            startsStroke = false;
            if (Samples.Count > Capacity) Samples.RemoveAt(0);
            return true;
        }
        public void Break() { startsStroke = true; }
        public void Clear() { Samples.Clear(); startsStroke = true; }
        public void Prune(double now, double lifetime)
        {
            int count = 0;
            while (count < Samples.Count && now - Samples[count].Time >= lifetime) count++;
            if (count > 0) Samples.RemoveRange(0, count);
            if (Samples.Count == 0) startsStroke = true;
        }
    }
}
