using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Kern;
using UnityEngine;

namespace BilligAGI.Sensorik
{
    public class VAKOGEngine
    {
        private readonly VAKOGLexikon lexikon;

        public VAKOGEngine(VAKOGLexikon lexikon)
        {
            this.lexikon = lexikon;
        }

        // Text-basierte Analyse (fuer Chat-Eingaben)
        public async Task<VAKOGProfil> AnalysiereText(string text)
        {
            var woerter = text.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?', '\n', '\t' },
                    System.StringSplitOptions.RemoveEmptyEntries);

            var profile = new List<VAKOGProfil>();

            foreach (var wort in woerter)
            {
                var profil = await lexikon.GetProfilMitFallback(wort);
                if (profil != null)
                    profile.Add(profil);
            }

            if (profile.Count == 0)
            {
                return new VAKOGProfil
                {
                    visuell = 0.5f, auditiv = 0.5f, kinaesthetisch = 0.5f,
                    olfaktorisch = 0.2f, gustatorisch = 0.1f
                };
            }

            // Gewichteter Durchschnitt mit Dominanz-Boost
            return AggregiereProfile(profile);
        }

        // Sensor-basierte Analyse (direkt aus Unity)
        public VAKOGProfil AnalysiereSensorisch(SensorDaten daten)
        {
            var profil = new VAKOGProfil();

            // V ← Kamera-Helligkeit + Farbvariation + Bewegung
            profil.visuell = Mathf.Clamp01(
                daten.helligkeit * 0.4f +
                FarbVariation(daten.dominanteFarbe) * 0.3f +
                daten.bewegungsIntensitaet * 0.3f);
            profil.beschreibungV = BeschreibeVisuell(daten);

            // A ← AudioListener Pegel
            profil.auditiv = Mathf.Clamp01(daten.audioPegel);
            profil.beschreibungA = daten.audioPegel > 0.5f ? "Laut" :
                                   daten.audioPegel > 0.2f ? "Moderat" : "Leise";

            // K ← Kollisionskraft + Geschwindigkeit
            profil.kinaesthetisch = Mathf.Clamp01(
                daten.kollisionsKraft * 0.1f +
                daten.geschwindigkeit * 0.05f +
                (daten.nahbereichObjekte?.Length ?? 0) * 0.02f);
            profil.beschreibungK = daten.kollisionsKraft > 0.1f ? "Kontakt" :
                                   daten.geschwindigkeit > 1f ? "In Bewegung" : "Ruhig";

            // O ← Naeher zu Partikel-Emittern (heuristisch ueber Tags)
            float olfaktorisch = 0f;
            if (daten.nahbereichObjekte != null)
            {
                foreach (var obj in daten.nahbereichObjekte)
                {
                    if (obj.tags != null && obj.tags.Any(t =>
                        t == "Rauch" || t == "Dampf" || t == "Blume" || t == "Nahrung" || t == "Feuer"))
                    {
                        olfaktorisch += 0.3f / Mathf.Max(1f, obj.distanz);
                    }
                }
            }
            profil.olfaktorisch = Mathf.Clamp01(olfaktorisch);
            profil.beschreibungO = olfaktorisch > 0.3f ? "Intensiver Geruch" :
                                   olfaktorisch > 0.1f ? "Leichter Geruch" : "Geruchsneutral";

            // G ← Interaktion mit Nahrungsobjekten
            float gustatorisch = 0f;
            if (daten.nahbereichObjekte != null)
            {
                foreach (var obj in daten.nahbereichObjekte)
                {
                    if (obj.tags != null && obj.tags.Any(t => t == "Nahrung" || t == "Essen" || t == "Trinken"))
                    {
                        gustatorisch += 0.4f / Mathf.Max(1f, obj.distanz);
                    }
                }
            }
            profil.gustatorisch = Mathf.Clamp01(gustatorisch);
            profil.beschreibungG = gustatorisch > 0.3f ? "Geschmacksintensiv" : "Neutral";

            return profil;
        }

        // Dual-Modus: Text + Sensor kombinieren
        public async Task<VAKOGProfil> AnalysiereDual(string text, SensorDaten daten)
        {
            var textProfil = await AnalysiereText(text);
            var sensorProfil = AnalysiereSensorisch(daten);

            // Kombination: Sensor hat Vorrang, Text ergaenzt
            return new VAKOGProfil
            {
                visuell = sensorProfil.visuell * 0.6f + textProfil.visuell * 0.4f,
                auditiv = sensorProfil.auditiv * 0.6f + textProfil.auditiv * 0.4f,
                kinaesthetisch = sensorProfil.kinaesthetisch * 0.6f + textProfil.kinaesthetisch * 0.4f,
                olfaktorisch = sensorProfil.olfaktorisch * 0.6f + textProfil.olfaktorisch * 0.4f,
                gustatorisch = sensorProfil.gustatorisch * 0.6f + textProfil.gustatorisch * 0.4f,
                beschreibungV = sensorProfil.beschreibungV ?? textProfil.beschreibungV,
                beschreibungA = sensorProfil.beschreibungA ?? textProfil.beschreibungA,
                beschreibungK = sensorProfil.beschreibungK ?? textProfil.beschreibungK,
                beschreibungO = sensorProfil.beschreibungO ?? textProfil.beschreibungO,
                beschreibungG = sensorProfil.beschreibungG ?? textProfil.beschreibungG,
            };
        }

        private VAKOGProfil AggregiereProfile(List<VAKOGProfil> profile)
        {
            int n = profile.Count;
            float v = 0, a = 0, k = 0, o = 0, g = 0;
            foreach (var p in profile)
            {
                v += p.visuell; a += p.auditiv; k += p.kinaesthetisch;
                o += p.olfaktorisch; g += p.gustatorisch;
            }

            var aggregiert = new VAKOGProfil
            {
                visuell = v / n,
                auditiv = a / n,
                kinaesthetisch = k / n,
                olfaktorisch = o / n,
                gustatorisch = g / n,
            };

            // Dominanz-Boost: Der staerkste Kanal wird leicht verstaerkt
            float max = Mathf.Max(aggregiert.visuell,
                       Mathf.Max(aggregiert.auditiv,
                       Mathf.Max(aggregiert.kinaesthetisch,
                       Mathf.Max(aggregiert.olfaktorisch, aggregiert.gustatorisch))));

            if (aggregiert.visuell == max) aggregiert.visuell = Mathf.Min(1f, aggregiert.visuell * 1.15f);
            else if (aggregiert.auditiv == max) aggregiert.auditiv = Mathf.Min(1f, aggregiert.auditiv * 1.15f);
            else if (aggregiert.kinaesthetisch == max) aggregiert.kinaesthetisch = Mathf.Min(1f, aggregiert.kinaesthetisch * 1.15f);
            else if (aggregiert.olfaktorisch == max) aggregiert.olfaktorisch = Mathf.Min(1f, aggregiert.olfaktorisch * 1.15f);
            else if (aggregiert.gustatorisch == max) aggregiert.gustatorisch = Mathf.Min(1f, aggregiert.gustatorisch * 1.15f);

            return aggregiert;
        }

        private float FarbVariation(float[] farbe)
        {
            if (farbe == null || farbe.Length < 3) return 0f;
            float avg = (farbe[0] + farbe[1] + farbe[2]) / 3f;
            float var_ = Mathf.Abs(farbe[0] - avg) + Mathf.Abs(farbe[1] - avg) + Mathf.Abs(farbe[2] - avg);
            return Mathf.Clamp01(var_);
        }

        private string BeschreibeVisuell(SensorDaten daten)
        {
            if (daten.helligkeit > 0.7f) return "Hell";
            if (daten.helligkeit < 0.3f) return "Dunkel";
            if (daten.bewegungsIntensitaet > 0.3f) return "Bewegung erkannt";
            return "Normal";
        }
    }
}
