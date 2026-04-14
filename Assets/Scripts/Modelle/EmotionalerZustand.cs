using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class EmotionalerZustand
    {
        public float angst;
        public float neugier;
        public float frustration;
        public float zufriedenheit;
        public float ueberraschung;
        public Dictionary<string, float> vertrauen = new Dictionary<string, float>();

        public float GesamtValenz()
        {
            float positiv = neugier + zufriedenheit;
            float negativ = angst + frustration;
            float gesamt = positiv - negativ;
            return Clamp(gesamt / 2f, -1f, 1f);
        }

        public bool KritischerZustand() => angst > 0.7f && frustration > 0.7f;

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }

    [Serializable]
    public class EmotionsModulation
    {
        public float explorationsFaktor = 1f;   // < 1 = weniger Exploration
        public float vorsichtsFaktor = 1f;      // > 1 = mehr Vorsicht
        public float kreativitaetsFaktor = 1f;  // > 1 = mehr Kreativität
        public float lernPrioritaet = 1f;        // > 1 = mehr Lernen
    }
}
