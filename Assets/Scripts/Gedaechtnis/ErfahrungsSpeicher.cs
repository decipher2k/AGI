using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Kern;
using Newtonsoft.Json;
using UnityEngine;

namespace BilligAGI.Gedaechtnis
{
    public class ErfahrungsSpeicher
    {
        private readonly VektorDB vektorDB;
        private readonly LLMAdapter llm;
        private readonly AGIConfig config;
        private List<Erfahrung> alleErfahrungen;
        private const string COLLECTION = "erfahrungen";

        public ErfahrungsSpeicher(VektorDB vektorDB, LLMAdapter llm, AGIConfig config)
        {
            this.vektorDB = vektorDB;
            this.llm = llm;
            this.config = config;
            alleErfahrungen = new List<Erfahrung>();
        }

        public async Task Speichere(Erfahrung erfahrung)
        {
            if (erfahrung == null) return;

            if (string.IsNullOrEmpty(erfahrung.id))
                erfahrung.id = Guid.NewGuid().ToString();

            erfahrung.zeitstempel = DateTime.UtcNow.ToString("o");
            alleErfahrungen.Add(erfahrung);

            // Embedding via LLM
            string text = $"{erfahrung.aktion} {erfahrung.kontext} {erfahrung.ergebnis}";
            float[] embedding = await GeneriereEmbedding(text);

            var metadata = new Dictionary<string, object>
            {
                ["aktion"] = erfahrung.aktion ?? "",
                ["zielId"] = erfahrung.zielId ?? "",
                ["belohnung"] = erfahrung.belohnung,
                ["zeitstempel"] = erfahrung.zeitstempel
            };

            await vektorDB.Speichere(erfahrung.id, embedding, COLLECTION, metadata);

            // One-Shot prufen
            if (IstOneShotWuerdig(erfahrung))
                SpeichereOneShot(erfahrung);
        }

        public async Task<List<Erfahrung>> FindeAehnliche(string query, int topK = 5)
        {
            float[] embedding = await GeneriereEmbedding(query);
            var results = await vektorDB.Suche(embedding, COLLECTION, topK);

            var gefunden = new List<Erfahrung>();
            foreach (var r in results)
            {
                var erf = alleErfahrungen.FirstOrDefault(e => e.id == r.id);
                if (erf != null)
                    gefunden.Add(erf);
            }
            return gefunden;
        }

        public List<Erfahrung> FindeZeitlich(string vorher, string nachher)
        {
            return alleErfahrungen.Where(e =>
                string.Compare(e.zeitstempel, vorher) >= 0 &&
                string.Compare(e.zeitstempel, nachher) <= 0
            ).OrderBy(e => e.zeitstempel).ToList();
        }

        public List<Erfahrung> FindeNachZiel(string zielId)
        {
            return alleErfahrungen.Where(e => e.zielId == zielId).ToList();
        }

        public List<Erfahrung> FindeNachAktion(string aktion)
        {
            string lower = aktion.ToLowerInvariant();
            return alleErfahrungen.Where(e =>
                e.aktion != null && e.aktion.ToLowerInvariant().Contains(lower)
            ).ToList();
        }

        public bool IstOneShotWuerdig(Erfahrung erfahrung)
        {
            if (erfahrung.emotionalerZustand == null) return false;

            bool hoheUeberraschung = erfahrung.emotionalerZustand.ueberraschung >
                config.oneShotSchwelle;

            float vakog = erfahrung.vakog != null
                ? (erfahrung.vakog.visuell + erfahrung.vakog.auditiv +
                   erfahrung.vakog.kinesthetisch + erfahrung.vakog.olfaktorisch +
                   erfahrung.vakog.gustatorisch) / 5f
                : 0f;

            return hoheUeberraschung && vakog > 0.7f;
        }

        public void SpeichereOneShot(Erfahrung erfahrung)
        {
            Debug.Log($"[OneShot] Dramatische Erfahrung gespeichert: {erfahrung.aktion} → {erfahrung.ergebnis}");
            erfahrung.belohnung *= 2f; // Doppelte Gewichtung
            erfahrung.konzepte.Add("one-shot");
        }

        public void MarkiereFuerNeuauswertung(List<string> erfahrungsIds)
        {
            foreach (var erf in alleErfahrungen)
            {
                if (erfahrungsIds.Contains(erf.id))
                {
                    if (!erf.konzepte.Contains("neuauswertung_pending"))
                        erf.konzepte.Add("neuauswertung_pending");
                }
            }
        }

        public List<Erfahrung> GetPendingNeuauswertung()
        {
            return alleErfahrungen.Where(e =>
                e.konzepte.Contains("neuauswertung_pending")).ToList();
        }

        public int Anzahl() => alleErfahrungen.Count;

        public List<Erfahrung> Alle() => new List<Erfahrung>(alleErfahrungen);

        private async Task<float[]> GeneriereEmbedding(string text)
        {
            // Vereinfacht: Hash-basiertes Embedding als Fallback
            // In Production: LLM-Embedding-API nutzen
            var embedding = new float[128];
            if (string.IsNullOrEmpty(text)) return embedding;

            for (int i = 0; i < text.Length && i < 128; i++)
            {
                embedding[i % 128] += (float)text[i] / 256f;
            }

            // Normalisieren
            float norm = 0f;
            for (int i = 0; i < 128; i++) norm += embedding[i] * embedding[i];
            norm = Mathf.Sqrt(norm);
            if (norm > 0)
                for (int i = 0; i < 128; i++) embedding[i] /= norm;

            await Task.CompletedTask; // Placeholder fuer async API-Call
            return embedding;
        }
    }
}
