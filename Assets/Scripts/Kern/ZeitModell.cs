using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Kern
{
    public class ZeitModell
    {
        public int aktuellerZyklus { get; private set; }
        public float unityZeit => Time.time;

        private Dictionary<string, float> geschaetzteDauern;
        private List<ZeitlicherKontext> zeitlinie;
        private Dictionary<string, float> deadlines; // zielId → deadline Unity-Zeit

        public ZeitModell()
        {
            aktuellerZyklus = 0;
            geschaetzteDauern = new Dictionary<string, float>();
            zeitlinie = new List<ZeitlicherKontext>();
            deadlines = new Dictionary<string, float>();
        }

        public void Tick()
        {
            aktuellerZyklus++;
        }

        // Dauer-Modell
        public void RegistriereDauer(string aktion, float dauer)
        {
            if (geschaetzteDauern.ContainsKey(aktion))
            {
                // Gleitender Durchschnitt
                geschaetzteDauern[aktion] = geschaetzteDauern[aktion] * 0.7f + dauer * 0.3f;
            }
            else
            {
                geschaetzteDauern[aktion] = dauer;
            }
        }

        public float SchaetzeDauer(string aktion)
        {
            return geschaetzteDauern.TryGetValue(aktion, out float dauer) ? dauer : 5f; // Default: 5s
        }

        // Sequenz-Gedaechtnis
        public void RegistriereErfahrung(Erfahrung erfahrung)
        {
            zeitlinie.Add(new ZeitlicherKontext
            {
                erfahrungsId = erfahrung.id,
                zyklusNummer = aktuellerZyklus,
                unityZeit = Time.time,
                dauerSekunden = 0, // Wird nachtraeglich aktualisiert
                vorgaengerIds = zeitlinie.Count > 0
                    ? new List<string> { zeitlinie.Last().erfahrungsId }
                    : new List<string>()
            });
        }

        public List<ZeitlicherKontext> WasPassierteVor(string erfahrungId, int anzahl = 5)
        {
            int idx = zeitlinie.FindIndex(z => z.erfahrungsId == erfahrungId);
            if (idx < 0) return new List<ZeitlicherKontext>();
            int start = Mathf.Max(0, idx - anzahl);
            return zeitlinie.GetRange(start, idx - start);
        }

        public List<ZeitlicherKontext> WasPassierteNach(string erfahrungId, int anzahl = 5)
        {
            int idx = zeitlinie.FindIndex(z => z.erfahrungsId == erfahrungId);
            if (idx < 0 || idx >= zeitlinie.Count - 1) return new List<ZeitlicherKontext>();
            int count = Mathf.Min(anzahl, zeitlinie.Count - idx - 1);
            return zeitlinie.GetRange(idx + 1, count);
        }

        public List<ZeitlicherKontext> WasPassierteWaehrend(float vonZeit, float bisZeit)
        {
            return zeitlinie.Where(z => z.unityZeit >= vonZeit && z.unityZeit <= bisZeit).ToList();
        }

        // Temporale Kausalitaet
        public bool UrsacheVorWirkung(string ursacheId, string wirkungId)
        {
            var ursache = zeitlinie.FirstOrDefault(z => z.erfahrungsId == ursacheId);
            var wirkung = zeitlinie.FirstOrDefault(z => z.erfahrungsId == wirkungId);
            if (ursache == null || wirkung == null) return false;
            return ursache.zyklusNummer < wirkung.zyklusNummer;
        }

        // Deadline-System
        public void SetzeDeadline(string zielId, float deadline)
        {
            deadlines[zielId] = deadline;
        }

        public float ZeitBisDeadline(string zielId)
        {
            if (!deadlines.TryGetValue(zielId, out float deadline)) return float.MaxValue;
            return deadline - Time.time;
        }

        public bool DeadlineUeberschritten(string zielId)
        {
            if (!deadlines.TryGetValue(zielId, out float deadline)) return false;
            return Time.time > deadline;
        }

        public float PlanDauerSchaetzen(List<string> aktionen)
        {
            float gesamt = 0f;
            foreach (var a in aktionen)
                gesamt += SchaetzeDauer(a);
            return gesamt;
        }

        public int AnzahlErfahrungen() => zeitlinie.Count;
    }
}
