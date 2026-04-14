using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Kern;
using BilligAGI.Daten;

namespace BilligAGI.Physik
{
    public class BioWissen
    {
        private readonly LLMAdapter llm;
        private List<BioFakt> fakten;
        private Dictionary<string, string> cache;
        private string faktenPfad = "bio_fakten.json";

        public BioWissen(LLMAdapter llm)
        {
            this.llm = llm;
            fakten = DatenLader.LadeListe<BioFakt>(faktenPfad) ?? new List<BioFakt>();
            cache = new Dictionary<string, string>();
        }

        public string Suche(string frage)
        {
            frage = frage.Trim().ToLowerInvariant();

            // Cache pruefen
            if (cache.TryGetValue(frage, out string cached))
                return cached;

            // Lokale Fakten durchsuchen
            foreach (var fakt in fakten)
            {
                if (fakt.frage != null && frage.Contains(fakt.frage.ToLowerInvariant()))
                {
                    cache[frage] = fakt.antwort;
                    return fakt.antwort;
                }
                if (fakt.schluesselwoerter != null)
                {
                    bool match = fakt.schluesselwoerter.Any(k =>
                        frage.Contains(k.ToLowerInvariant()));
                    if (match)
                    {
                        cache[frage] = fakt.antwort;
                        return fakt.antwort;
                    }
                }
            }

            return null; // Kein lokales Wissen
        }

        public async Task<string> SucheMitFallback(string frage)
        {
            string lokal = Suche(frage);
            if (lokal != null) return lokal;

            // LLM fragen
            var antwort = await llm.FreieAnfrage(
                $"Biologisches Wissen: {frage}\nAntworte kurz und praezise (max 2 Saetze).");
            if (antwort == null) return null;

            // Cache + persistent speichern
            string text = antwort.inhalt;
            cache[frage.Trim().ToLowerInvariant()] = text;

            fakten.Add(new BioFakt
            {
                frage = frage,
                antwort = text,
                schluesselwoerter = new List<string>()
            });
            DatenLader.Speichere(faktenPfad, fakten);

            return text;
        }

        public int AnzahlFakten() => fakten.Count;

        [System.Serializable]
        public class BioFakt
        {
            public string frage;
            public string antwort;
            public List<string> schluesselwoerter = new List<string>();
        }
    }
}
