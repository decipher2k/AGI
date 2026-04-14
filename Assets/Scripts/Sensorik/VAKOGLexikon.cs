using System.Collections.Generic;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Kern;
using Newtonsoft.Json;
using UnityEngine;

namespace BilligAGI.Sensorik
{
    public class VAKOGLexikon
    {
        private Dictionary<string, VAKOGProfil> basisProfile;
        private Dictionary<string, VAKOGProfil> gelernteProfile;
        private readonly LLMAdapter llm;

        public VAKOGLexikon(LLMAdapter llm)
        {
            this.llm = llm;
            basisProfile = Daten.DatenLader.LadeDict<VAKOGProfil>("vakog_basis.json");
            gelernteProfile = new Dictionary<string, VAKOGProfil>();

            Debug.Log($"[VAKOGLexikon] {basisProfile.Count} Basis-Profile geladen");
        }

        public VAKOGProfil GetProfil(string wort)
        {
            wort = wort.ToLowerInvariant().Trim();

            // 1. Gelernte Profile (hoechste Prioritaet)
            if (gelernteProfile.TryGetValue(wort, out var gelernt))
                return gelernt;

            // 2. Basis-Profile
            if (basisProfile.TryGetValue(wort, out var basis))
                return basis;

            // 3. Teilwort-Match
            foreach (var kvp in basisProfile)
            {
                if (wort.Contains(kvp.Key) || kvp.Key.Contains(wort))
                    return kvp.Value;
            }

            return null; // Nicht gefunden — Caller muss LLM fragen
        }

        public async Task<VAKOGProfil> GetProfilMitFallback(string wort)
        {
            var profil = GetProfil(wort);
            if (profil != null) return profil;

            // LLM schaetzen lassen
            if (llm != null && llm.IstVerfuegbar())
            {
                var antwort = await llm.Analysiere(
                    $"Schaetze das sensorische Profil fuer '{wort}'.\n" +
                    "Antworte NUR als JSON: {\"visuell\": 0-1, \"auditiv\": 0-1, \"kinaesthetisch\": 0-1, " +
                    "\"olfaktorisch\": 0-1, \"gustatorisch\": 0-1}",
                    "Du schaetzt VAKOG sensorische Profile. Antworte nur mit JSON.");

                try
                {
                    profil = JsonConvert.DeserializeObject<VAKOGProfil>(
                        ExtrahiereJson(antwort.text));
                    if (profil != null)
                    {
                        gelernteProfile[wort.ToLowerInvariant()] = profil;
                        return profil;
                    }
                }
                catch { }
            }

            // Default
            return new VAKOGProfil
            {
                visuell = 0.5f, auditiv = 0.5f, kinaesthetisch = 0.5f,
                olfaktorisch = 0.2f, gustatorisch = 0.1f
            };
        }

        public void AktualisiereAusErfahrung(string wort, SensorDaten daten)
        {
            wort = wort.ToLowerInvariant().Trim();
            var profil = new VAKOGProfil
            {
                visuell = daten.helligkeit,
                auditiv = Mathf.Clamp01(daten.audioPegel),
                kinaesthetisch = Mathf.Clamp01(daten.kollisionsKraft * 0.1f + daten.geschwindigkeit * 0.05f),
                olfaktorisch = 0.2f, // Schwer aus Sensordaten
                gustatorisch = 0.1f,
            };

            // Mittelung mit bestehendem Profil falls vorhanden
            if (gelernteProfile.TryGetValue(wort, out var vorher))
            {
                profil.visuell = (vorher.visuell + profil.visuell) / 2f;
                profil.auditiv = (vorher.auditiv + profil.auditiv) / 2f;
                profil.kinaesthetisch = (vorher.kinaesthetisch + profil.kinaesthetisch) / 2f;
                profil.olfaktorisch = (vorher.olfaktorisch + profil.olfaktorisch) / 2f;
                profil.gustatorisch = (vorher.gustatorisch + profil.gustatorisch) / 2f;
            }

            gelernteProfile[wort] = profil;
        }

        public void Persistiere()
        {
            Daten.DatenLader.Speichere("vakog_gelernt.json", gelernteProfile);
        }

        public int AnzahlProfile => basisProfile.Count + gelernteProfile.Count;

        private static string ExtrahiereJson(string text)
        {
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                return text.Substring(start, end - start + 1);
            return "{}";
        }
    }
}
