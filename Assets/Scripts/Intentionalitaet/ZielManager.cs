using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using BilligAGI.Kern;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Intentionalitaet
{
    public class ZielManager
    {
        private readonly LLMAdapter llm;
        private readonly SelbstModell selbstModell;
        private readonly EmotionsSystem emotionen;
        private readonly ZeitModell zeitModell;
        private readonly AGIConfig config;
        private List<Ziel> alleZiele;
        private const int MAX_AKTIVE = 3;

        public ZielManager(LLMAdapter llm, SelbstModell selbstModell,
            EmotionsSystem emotionen, ZeitModell zeitModell, AGIConfig config)
        {
            this.llm = llm;
            this.selbstModell = selbstModell;
            this.emotionen = emotionen;
            this.zeitModell = zeitModell;
            this.config = config;
            alleZiele = new List<Ziel>();
            LadeZiele();
        }

        public Ziel FormuliereZiel(string ausloeser, ZielTyp typ, float prioritaet = 0.5f)
        {
            var ziel = new Ziel
            {
                id = Guid.NewGuid().ToString(),
                beschreibung = ausloeser,
                typ = typ,
                prioritaet = prioritaet,
                status = ZielStatus.AKTIV,
                erstelltAm = DateTime.UtcNow.ToString("o")
            };

            alleZiele.Add(ziel);
            Priorisiere();
            PersistiereZiele();
            return ziel;
        }

        public List<Ziel> Priorisiere()
        {
            var aktive = alleZiele.Where(z => z.status == ZielStatus.AKTIV).ToList();
            var modulation = emotionen?.GetModulation();

            foreach (var z in aktive)
            {
                float prio = z.prioritaet;

                // Emotionale Modulation
                if (modulation != null)
                {
                    if (z.typ == ZielTyp.EXPLORATION)
                        prio *= modulation.explorationsFaktor;
                    if (z.typ == ZielTyp.EXPERIMENT && emotionen.zustand.frustration > 0.5f)
                        prio *= modulation.kreativitaetsFaktor;
                }

                // Selbstmodell: Kann ich das?
                if (selbstModell != null && !selbstModell.KannIchDas(z))
                    prio *= 0.5f;

                // Deadline-Naehe
                if (zeitModell != null)
                {
                    float zeitBis = zeitModell.ZeitBisDeadline(z.id);
                    if (zeitBis < 30f && zeitBis > 0f)
                        prio *= 1.5f; // Dringend
                }

                z.effektivePrioritaet = prio;
            }

            aktive.Sort((a, b) => b.effektivePrioritaet.CompareTo(a.effektivePrioritaet));

            // Max 3 aktive
            for (int i = MAX_AKTIVE; i < aktive.Count; i++)
                aktive[i].status = ZielStatus.GEPARKT;

            return aktive.Take(MAX_AKTIVE).ToList();
        }

        public void ZielErreicht(string zielId, string ergebnis)
        {
            var ziel = alleZiele.FirstOrDefault(z => z.id == zielId);
            if (ziel != null)
            {
                ziel.status = ZielStatus.ERREICHT;
                ziel.ergebnis = ergebnis;
                Debug.Log($"[ZielManager] Ziel erreicht: {ziel.beschreibung}");
                PersistiereZiele();
            }
        }

        public void ZielGescheitert(string zielId, string grund)
        {
            var ziel = alleZiele.FirstOrDefault(z => z.id == zielId);
            if (ziel != null)
            {
                ziel.status = ZielStatus.GESCHEITERT;
                ziel.ergebnis = grund;
                Debug.Log($"[ZielManager] Ziel gescheitert: {ziel.beschreibung} — {grund}");
                PersistiereZiele();
            }
        }

        public Ziel GetAktivesZiel()
        {
            return alleZiele.FirstOrDefault(z => z.status == ZielStatus.AKTIV);
        }

        public List<Ziel> GetAlleAktiven()
        {
            return alleZiele.Where(z => z.status == ZielStatus.AKTIV)
                .OrderByDescending(z => z.effektivePrioritaet).ToList();
        }

        public List<Ziel> GetHistorie()
        {
            return alleZiele.Where(z =>
                z.status == ZielStatus.ERREICHT || z.status == ZielStatus.GESCHEITERT).ToList();
        }

        private void PersistiereZiele()
        {
            DatenLader.Speichere("ziele_aktiv.json", alleZiele);
        }

        private void LadeZiele()
        {
            var geladen = DatenLader.LadeListe<Ziel>("ziele_aktiv.json");
            if (geladen != null)
                alleZiele = geladen;
        }
    }
}
