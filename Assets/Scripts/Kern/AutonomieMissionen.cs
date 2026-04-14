using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Daten;
using BilligAGI.Intentionalitaet;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Kern
{
    [Serializable]
    public class MissionsEintrag
    {
        public string id;
        public string zielId;
        public string beschreibung;
        public string quelle;
        public string erstelltAm;
        public int startZyklus;
        public int endZyklus;
        public MissionStatus status = MissionStatus.AKTIV;
        public float durchschnittBelohnung;
        public int schritte;
        public int negativeSerie;
    }

    public enum MissionStatus { AKTIV, ABGESCHLOSSEN, GESTOPPT }

    [Serializable]
    public class MissionsStatistik
    {
        public bool aktiviert = true;
        public int missionenErstellt;
        public int missionenAbgeschlossen;
        public int missionenGestoppt;
        public int missionenRecovery;
        public float durchschnittBelohnung;
        public List<MissionsEintrag> historie = new();
    }

    // Phase 28 (Start): Langlauf-Autonomie ueber Missions-Sessions.
    // Wenn keine aktiven Ziele mehr da sind, wird kontrolliert ein neues
    // Missionsziel erzeugt statt im Leerlauf zu bleiben.
    public class AutonomieMissionen
    {
        private readonly ZielManager zielManager;
        private readonly SelbstCurriculum selbstCurriculum;

        private MissionsStatistik statistik;
        private MissionsEintrag aktiveMission;
        private int zyklusZaehler;
        private int letztesAutoZielZyklus;

        private const int AUTO_ZIEL_INTERVALL = 12;
        private const int NEGATIVE_SERIE_RECOVERY = 8;
        private const int MAX_HISTORIE = 40;
        private const string PERSISTENZ_DATEI = "autonomie_missionen.json";

        public AutonomieMissionen(ZielManager zielManager, SelbstCurriculum selbstCurriculum)
        {
            this.zielManager = zielManager;
            this.selbstCurriculum = selbstCurriculum;

            statistik = DatenLader.Lade<MissionsStatistik>(PERSISTENZ_DATEI) ?? new MissionsStatistik();
            aktiveMission = statistik.historie.LastOrDefault(h => h.status == MissionStatus.AKTIV);
        }

        public string ZyklusTick(bool autonomerModus, float letzteBelohnung)
        {
            zyklusZaehler++;

            if (!statistik.aktiviert)
                return null;

            if (aktiveMission != null)
            {
                string update = AktualisiereMission(letzteBelohnung);
                if (!string.IsNullOrEmpty(update))
                    return update;
            }

            if (!autonomerModus)
                return null;

            if (zyklusZaehler - letztesAutoZielZyklus < AUTO_ZIEL_INTERVALL)
                return null;

            var aktiveZiele = zielManager?.GetAlleAktiven();
            if (aktiveZiele != null && aktiveZiele.Count > 0)
                return null;

            var neu = ErzeugeAutoMission();
            if (neu == null)
                return null;

            letztesAutoZielZyklus = zyklusZaehler;
            return $"Auto-Mission gestartet: {neu.beschreibung}";
        }

        public string StarteEmpfohleneMission()
        {
            string beschreibung = BaueEmpfehlungsText(out var typ, out var prio);

            if (aktiveMission != null)
                return "Es laeuft bereits eine Mission.";

            var ziel = zielManager?.FormuliereZiel(beschreibung, typ, prio);
            if (ziel == null)
                return "Konnte keine empfohlene Mission erzeugen.";

            aktiveMission = new MissionsEintrag
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                zielId = ziel.id,
                beschreibung = beschreibung,
                quelle = "empfehlung",
                erstelltAm = DateTime.UtcNow.ToString("o"),
                startZyklus = zyklusZaehler,
                status = MissionStatus.AKTIV
            };

            statistik.historie.Add(aktiveMission);
            statistik.missionenErstellt++;
            Persistiere();
            return $"Empfohlene Mission gestartet: {beschreibung}";
        }

        public string StarteMission(string beschreibung)
        {
            if (string.IsNullOrWhiteSpace(beschreibung))
                beschreibung = "Selbststaendige Explorationsmission";

            if (aktiveMission != null)
                return "Es laeuft bereits eine Mission.";

            var ziel = zielManager?.FormuliereZiel(beschreibung, ZielTyp.AUFGABE, 0.55f);
            if (ziel == null)
                return "Konnte kein Ziel fuer Mission erzeugen.";

            aktiveMission = new MissionsEintrag
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                zielId = ziel.id,
                beschreibung = beschreibung,
                quelle = "manuell",
                erstelltAm = DateTime.UtcNow.ToString("o"),
                startZyklus = zyklusZaehler,
                status = MissionStatus.AKTIV
            };

            statistik.historie.Add(aktiveMission);
            statistik.missionenErstellt++;
            Persistiere();
            return $"Mission gestartet: {beschreibung}";
        }

        public string StoppeMission()
        {
            if (aktiveMission == null)
                return "Keine aktive Mission.";

            aktiveMission.status = MissionStatus.GESTOPPT;
            aktiveMission.endZyklus = zyklusZaehler;
            statistik.missionenGestoppt++;
            aktiveMission = null;
            Persistiere();
            return "Mission gestoppt.";
        }

        public void SetAktiviert(bool an)
        {
            statistik.aktiviert = an;
            Persistiere();
        }

        public bool IstAktiviert() => statistik.aktiviert;

        public string GetStatusText()
        {
            string aktivText = aktiveMission != null
                ? $"Aktiv: '{aktiveMission.beschreibung}' ({aktiveMission.schritte} Schritte, ØR={aktiveMission.durchschnittBelohnung:F2}, NegSerie={aktiveMission.negativeSerie})"
                : "Keine aktive Mission";

            return $"Missionen aktiviert: {statistik.aktiviert} | {aktivText} | " +
                   $"Erstellt={statistik.missionenErstellt}, Abgeschlossen={statistik.missionenAbgeschlossen}, Gestoppt={statistik.missionenGestoppt}, Recovery={statistik.missionenRecovery}";
        }

        public string GetEmpfehlungText()
        {
            string beschreibung = BaueEmpfehlungsText(out var typ, out var prio);
            return $"Empfehlung: {beschreibung} (Typ={typ}, Prio={prio:F2})";
        }

        public List<MissionsEintrag> GetHistorie() => statistik.historie;

        private MissionsEintrag ErzeugeAutoMission()
        {
            string basis = BaueEmpfehlungsText(out var typ, out var prio);

            var ziel = zielManager?.FormuliereZiel(basis, typ, prio);
            if (ziel == null)
                return null;

            var mission = new MissionsEintrag
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                zielId = ziel.id,
                beschreibung = basis,
                quelle = "auto",
                erstelltAm = DateTime.UtcNow.ToString("o"),
                startZyklus = zyklusZaehler,
                status = MissionStatus.AKTIV
            };

            statistik.historie.Add(mission);
            statistik.missionenErstellt++;
            aktiveMission = mission;
            Persistiere();
            return mission;
        }

        private string AktualisiereMission(float belohnung)
        {
            if (aktiveMission == null) return null;

            aktiveMission.schritte++;
            aktiveMission.durchschnittBelohnung =
                ((aktiveMission.durchschnittBelohnung * (aktiveMission.schritte - 1)) + belohnung) /
                Mathf.Max(1, aktiveMission.schritte);

            if (belohnung < -0.02f)
                aktiveMission.negativeSerie++;
            else
                aktiveMission.negativeSerie = Mathf.Max(0, aktiveMission.negativeSerie - 1);

            statistik.durchschnittBelohnung =
                ((statistik.durchschnittBelohnung * (statistik.missionenErstellt - 1)) + aktiveMission.durchschnittBelohnung) /
                Mathf.Max(1, statistik.missionenErstellt);

            if (aktiveMission.schritte >= 60 || aktiveMission.durchschnittBelohnung > 0.45f)
            {
                aktiveMission.status = MissionStatus.ABGESCHLOSSEN;
                aktiveMission.endZyklus = zyklusZaehler;
                statistik.missionenAbgeschlossen++;
                aktiveMission = null;
                Persistiere();
                return "Mission abgeschlossen.";
            }

            if (aktiveMission.negativeSerie >= NEGATIVE_SERIE_RECOVERY)
            {
                string alteBeschreibung = aktiveMission.beschreibung;
                aktiveMission.status = MissionStatus.GESTOPPT;
                aktiveMission.endZyklus = zyklusZaehler;
                statistik.missionenRecovery++;
                aktiveMission = null;

                var recovery = ErzeugeAutoMission();
                Persistiere();

                if (recovery != null)
                    return $"Mission-Recovery: '{alteBeschreibung}' beendet, neue Mission '{recovery.beschreibung}' gestartet.";

                return $"Mission-Recovery: '{alteBeschreibung}' beendet, keine neue Mission erzeugbar.";
            }

            if (statistik.historie.Count > MAX_HISTORIE)
                statistik.historie.RemoveRange(0, statistik.historie.Count - MAX_HISTORIE);

            if (zyklusZaehler % 15 == 0)
                Persistiere();

            return null;
        }

        private string BaueEmpfehlungsText(out ZielTyp typ, out float prio)
        {
            string basis = "Autonome Exploration mit Lernfokus";
            typ = ZielTyp.EXPLORATION;
            prio = 0.5f;

            if (selbstCurriculum != null)
            {
                string lernZiele = selbstCurriculum.GetLernZieleText();
                if (!string.IsNullOrWhiteSpace(lernZiele) && !lernZiele.Contains("Keine offenen Lernziele"))
                {
                    basis = "Autonome Uebungsmission: Curriculum-Schwachstelle trainieren";
                    typ = ZielTyp.VERSTAENDNIS;
                    prio = 0.6f;
                }
            }

            var letzte3 = statistik.historie.Where(h => h.status != MissionStatus.AKTIV).TakeLast(3).ToList();
            if (letzte3.Count >= 3 && letzte3.Average(h => h.durchschnittBelohnung) < 0.05f)
            {
                basis = "Recovery-Mission: kurze Beobachtungs- und Kalibrierungsrunde";
                typ = ZielTyp.EXPERIMENT;
                prio = 0.58f;
            }

            return basis;
        }

        private void Persistiere()
        {
            DatenLader.Speichere(PERSISTENZ_DATEI, statistik);
        }
    }
}
