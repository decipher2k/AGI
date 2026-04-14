using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Gedaechtnis;
using BilligAGI.Sozial;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    public class NarrativesSelbst
    {
        private readonly LLMAdapter llm;
        private readonly SelbstModell selbstModell;
        private readonly EmotionsSystem emotionen;
        private readonly Alchemie alchemie;
        private readonly KonzeptRevision revision;
        private readonly AGIConfig config;
        private Autobiographie autobiographie;
        private List<Erfahrung> aktuelleErfahrungen;

        public NarrativesSelbst(LLMAdapter llm, SelbstModell selbst, EmotionsSystem emotionen,
            Alchemie alchemie, KonzeptRevision revision, AGIConfig config)
        {
            this.llm = llm;
            this.selbstModell = selbst;
            this.emotionen = emotionen;
            this.alchemie = alchemie;
            this.revision = revision;
            this.config = config;
            aktuelleErfahrungen = new List<Erfahrung>();
            LadeAutobiographie();
        }

        public async Task ErfahrungIntegrieren(Erfahrung e)
        {
            if (e == null) return;
            aktuelleErfahrungen.Add(e);

            // Kapitelwuerdigkeit pruefen
            bool kapitelWuerdig = false;
            if (e.emotionalerZustand != null &&
                (e.emotionalerZustand.ueberraschung > 0.6f ||
                 Mathf.Abs(e.belohnung) > 0.7f))
                kapitelWuerdig = true;

            if (e.konzepte.Contains("revision"))
                kapitelWuerdig = true;

            if (aktuelleErfahrungen.Count >= config.autobiographieKapitelLaenge || kapitelWuerdig)
            {
                await NeuesKapitel();
            }
        }

        public async Task<string> BeschreibeEntwicklung()
        {
            if (autobiographie.kapitel.Count == 0)
                return "Noch keine Geschichte geschrieben. Erfahrungen werden gesammelt.";

            string prompt = "Fasse diese Autobiographie eines KI-Agenten zusammen:\n";
            foreach (var k in autobiographie.kapitel)
                prompt += $"- Kapitel {k.nummer}: {k.zusammenfassung} (Phase: {k.alchemischePhase})\n";
            prompt += "\nErzeuge eine kohaerente narrative Selbstbeschreibung in 3-5 Saetzen.";

            var antwort = await llm.FreieAnfrage(prompt);
            return antwort?.inhalt ?? "Entwicklung nicht beschreibbar.";
        }

        public List<string> IdentitaetsAussagen()
        {
            var aussagen = new List<string>();

            // Aus SelbstModell
            if (selbstModell != null)
            {
                foreach (var kvp in selbstModell.GetAlleKompetenzen())
                {
                    if (kvp.Value > 0.7f)
                        aussagen.Add($"Ich bin gut in {kvp.Key}.");
                    else if (kvp.Value < 0.2f)
                        aussagen.Add($"Ich bin unsicher bei {kvp.Key}.");
                }
            }

            // Aus Emotionen
            if (emotionen != null)
            {
                if (emotionen.zustand.neugier > 0.6f)
                    aussagen.Add("Ich bin neugierig und explorationsfreudig.");
                if (emotionen.zustand.angst > 0.5f)
                    aussagen.Add("Ich bin derzeit vorsichtig und aengstlich.");
            }

            // Aus Autobiographie
            if (autobiographie.kapitel.Count > 3)
                aussagen.Add($"Ich habe {autobiographie.kapitel.Count} Kapitel erlebt.");

            return aussagen;
        }

        public AutobiographieKapitel AktuellesKapitel()
        {
            return autobiographie.kapitel.Count > 0
                ? autobiographie.kapitel.Last()
                : null;
        }

        public AlchemischePhase AktuellePhase()
        {
            return alchemie?.GetPhase("agent") ?? AlchemischePhase.Nigredo;
        }

        public Autobiographie GetAutobiographie() => autobiographie;

        private async Task NeuesKapitel()
        {
            int nummer = autobiographie.kapitel.Count + 1;

            // LLM-Zusammenfassung
            string prompt = $"Fasse diese {aktuelleErfahrungen.Count} Erfahrungen zu einem Kapitel zusammen:\n";
            foreach (var e in aktuelleErfahrungen.Take(20))
                prompt += $"- {e.aktion}: {e.ergebnis} (Belohnung: {e.belohnung:F1})\n";
            prompt += "\nAntworte in 2-3 Saetzen.";

            var antwort = await llm.FreieAnfrage(prompt);
            string zusammenfassung = antwort?.inhalt ?? "Kapitel ohne Zusammenfassung.";

            // Phase zuordnen
            var phase = alchemie?.ErkennePhase(zusammenfassung, aktuelleErfahrungen)
                ?? AlchemischePhase.Nigredo;

            autobiographie.kapitel.Add(new AutobiographieKapitel
            {
                nummer = nummer,
                zusammenfassung = zusammenfassung,
                alchemischePhase = phase,
                anzahlErfahrungen = aktuelleErfahrungen.Count,
                zeitstempel = System.DateTime.UtcNow.ToString("o")
            });

            // Phase setzen
            alchemie?.SetzePhase("agent", phase);

            aktuelleErfahrungen.Clear();
            PersistiereAutobiographie();

            Debug.Log($"[Narrativ] Kapitel {nummer}: {zusammenfassung} (Phase: {phase})");
        }

        private void PersistiereAutobiographie()
        {
            DatenLader.Speichere("autobiographie.json", autobiographie);
        }

        private void LadeAutobiographie()
        {
            autobiographie = DatenLader.Lade<Autobiographie>("autobiographie.json")
                ?? new Autobiographie();
        }
    }
}
