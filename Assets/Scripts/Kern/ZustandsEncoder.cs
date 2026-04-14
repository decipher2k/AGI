using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;

namespace BilligAGI.Kern
{
    /// <summary>
    /// Kodiert Welt- und Agentzustand als float-Vektor fuer RL und Clustering.
    /// Kein LLM. Reine Mathematik. 20-dimensionaler Zustandsvektor.
    ///
    /// Dimensionen:
    /// [0-4]   VAKOG (visuell, auditiv, kinaesthetisch, olfaktorisch, gustatorisch)
    /// [5-9]   Emotionen (angst, neugier, frustration, zufriedenheit, ueberraschung)
    /// [10-11] Welt (tageszeit normiert, wetter-intensitaet)
    /// [12]    Bewertung (gesamtRelevanz der Situation)
    /// [13]    Objekte in der Naehe (normiert)
    /// [14]    Eigene Kompetenz im aktuellen Bereich
    /// [15]    Soziale Praesenz (Anzahl NPCs / max)
    /// [16]    Plan-Fortschritt (0 = kein Plan, 0-1 = Fortschritt)
    /// [17]    Frustrations-Trend (steigend/fallend)
    /// [18]    Erfahrungsdichte (viele aehnliche Erfahrungen = bekanntes Terrain)
    /// [19]    Zeit seit letztem Erfolg (normiert)
    /// </summary>
    public class ZustandsEncoder
    {
        public const int VEKTOR_GROESSE = 20;
        private const int DISKRETISIERUNG = 8; // Fuer Q-Table: pro Dimension 8 Buckets

        private float letzteFrustration;
        private float letzterErfolg;

        public ZustandsEncoder()
        {
            letzteFrustration = 0f;
            letzterErfolg = 0f;
        }

        /// <summary>
        /// Erzeugt einen 20D float-Vektor aus dem aktuellen Zustand.
        /// </summary>
        public float[] Kodiere(
            VAKOGProfil vakog,
            EmotionalerZustand emotionen,
            WeltZustand welt,
            float bewertung,
            float eigeneKompetenz,
            int objekteInDerNaehe,
            int npcsInDerNaehe,
            float planFortschritt,
            float erfahrungsDichte)
        {
            float[] v = new float[VEKTOR_GROESSE];

            // VAKOG
            if (vakog != null)
            {
                v[0] = vakog.visuell;
                v[1] = vakog.auditiv;
                v[2] = vakog.kinaesthetisch;
                v[3] = vakog.olfaktorisch;
                v[4] = vakog.gustatorisch;
            }

            // Emotionen
            if (emotionen != null)
            {
                v[5] = emotionen.angst;
                v[6] = emotionen.neugier;
                v[7] = emotionen.frustration;
                v[8] = emotionen.zufriedenheit;
                v[9] = emotionen.ueberraschung;
            }

            // Welt
            if (welt != null)
            {
                v[10] = welt.tageszeit / 24f;
                v[11] = welt.wetterIntensitaet;
            }

            // Situativ
            v[12] = Clamp01(bewertung);
            v[13] = Clamp01(objekteInDerNaehe / 20f);
            v[14] = Clamp01(eigeneKompetenz);
            v[15] = Clamp01(npcsInDerNaehe / 10f);
            v[16] = Clamp01(planFortschritt);

            // Frustrations-Trend
            float frustNow = emotionen?.frustration ?? 0f;
            v[17] = Clamp01((frustNow - letzteFrustration + 1f) / 2f); // 0.5 = stabil
            letzteFrustration = frustNow;

            v[18] = Clamp01(erfahrungsDichte);
            v[19] = Clamp01(letzterErfolg / 100f); // Ticks seit Erfolg, normiert auf 100

            return v;
        }

        /// <summary>
        /// Diskretisiert den Zustandsvektor zu einem Hash fuer Q-Table Lookup.
        /// </summary>
        public long Diskretisiere(float[] zustand)
        {
            long hash = 0;
            long faktor = 1;
            for (int i = 0; i < zustand.Length; i++)
            {
                int bucket = (int)(Clamp01(zustand[i]) * (DISKRETISIERUNG - 1));
                hash += bucket * faktor;
                faktor *= DISKRETISIERUNG;
            }
            return hash;
        }

        /// <summary>
        /// Euklidische Distanz zwischen zwei Zustandsvektoren.
        /// Fuer Clustering und Aehnlichkeitssuche.
        /// </summary>
        public static float Distanz(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return float.MaxValue;
            float summe = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                float d = a[i] - b[i];
                summe += d * d;
            }
            return (float)Math.Sqrt(summe);
        }

        /// <summary>
        /// Kosinus-Aehnlichkeit zwischen zwei Vektoren (0 = orthogonal, 1 = identisch).
        /// </summary>
        public static float KosinusAehnlichkeit(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 0f;
            float dot = 0f, normA = 0f, normB = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            float denom = (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
            return denom > 0.0001f ? dot / denom : 0f;
        }

        public void RegistriereErfolg() => letzterErfolg = 0f;
        public void Tick() => letzterErfolg++;

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
