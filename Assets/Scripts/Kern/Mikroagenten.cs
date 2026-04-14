using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Sozial;
using BilligAGI.Gedaechtnis;
using UnityEngine;

namespace BilligAGI.Kern
{
    // ======================================================================
    // Spezialisierte Mikroagenten
    //
    // Jeder Agent hat EINE Verantwortung. Keiner kontrolliert die anderen.
    // Alle arbeiten auf dem gleichen Blackboard. Emergenz entsteht
    // durch Interaktion, nicht durch Planung von oben.
    // ======================================================================

    /// <summary>
    /// WahrnehmungsAgent: Beobachtet die Umgebung und schreibt
    /// Wahrnehmungszusammenfassungen aufs Blackboard.
    /// Aktivierung steigt wenn sich die Sensorik stark aendert.
    /// </summary>
    public class WahrnehmungsAgent : MikroAgent
    {
        private float letzteIntensitaet;

        public WahrnehmungsAgent() : base("Wahrnehmung") { }

        protected override float BerechneAktivierung(Blackboard bb)
        {
            var vakog = bb.Lies<VAKOGProfil>("sensor_vakog");
            if (vakog == null) return 0.3f;

            float aenderung = System.Math.Abs(vakog.Gesamtintensitaet - letzteIntensitaet);
            return 0.3f + aenderung * 3f; // Starke Aenderung → hohe Aktivierung
        }

        protected override Task<AgentErgebnis> Tick(Blackboard bb)
        {
            var vakog = bb.Lies<VAKOGProfil>("sensor_vakog");
            var sensorDaten = bb.Lies<SensorDaten>("sensor_roh");

            var ergebnis = new AgentErgebnis { agent = Name, ausgefuehrt = true };

            if (vakog != null)
            {
                letzteIntensitaet = vakog.Gesamtintensitaet;

                // Auffaelligkeiten erkennen (ohne LLM — reine Schwellwerte)
                var auffaelligkeiten = new List<string>();
                if (vakog.visuell > 0.8f) auffaelligkeiten.Add("starke_visuelle_reize");
                if (vakog.auditiv > 0.7f) auffaelligkeiten.Add("laute_geraeusche");
                if (vakog.kinaesthetisch > 0.6f) auffaelligkeiten.Add("koerperliche_empfindung");
                if (vakog.Gesamtintensitaet > 0.7f) auffaelligkeiten.Add("hohe_reizintensitaet");

                ergebnis.erzeugteDaten["wahrnehmung_auffaellig"] = auffaelligkeiten;
                ergebnis.erzeugteDaten["wahrnehmung_dominantKanal"] = vakog.DominanterKanal();

                // Andere Agenten warnen bei extremen Reizen
                if (vakog.Gesamtintensitaet > 0.85f)
                {
                    ergebnis.nachrichten.Add(new AgentNachricht
                    {
                        von = Name, an = "*", typ = "WARNUNG",
                        inhalt = "Extreme Reizintensitaet — moeglicherweise gefaehrlich",
                        prioritaet = 0.9f
                    });
                }
            }

            return Task.FromResult(ergebnis);
        }
    }

    /// <summary>
    /// MusterAgent: Sucht nach Mustern in der aktuellen Situation.
    /// Nutzt den InstanzClusterer (ML) statt LLM fuer Aehnlichkeitssuche.
    /// Aktivierung steigt wenn die Situation "unbekannt" ist.
    /// </summary>
    public class MusterAgent : MikroAgent
    {
        private readonly InstanzClusterer clusterer;
        private readonly ArchetypenGedaechtnis gedaechtnis;

        public MusterAgent(InstanzClusterer clusterer, ArchetypenGedaechtnis gedaechtnis)
            : base("Muster")
        {
            this.clusterer = clusterer;
            this.gedaechtnis = gedaechtnis;
        }

        protected override float BerechneAktivierung(Blackboard bb)
        {
            // Hoch wenn neue Situation, niedrig wenn bekannt
            float vertrautheit = bb.Lies<float>("rl_vertrautheit", 0.5f);
            return 1f - vertrautheit; // Unbekannt → aktivieren
        }

        protected override Task<AgentErgebnis> Tick(Blackboard bb)
        {
            var ergebnis = new AgentErgebnis { agent = Name, ausgefuehrt = true };

            // Letzte Instanzen holen und clustern
            var letzteInstanzen = gedaechtnis.LetzteInstanzen(20);
            if (letzteInstanzen.Count >= 5)
            {
                var clusterZuordnung = clusterer.ClustereInstanzen(letzteInstanzen);
                ergebnis.erzeugteDaten["muster_cluster"] = clusterZuordnung;
                ergebnis.erzeugteDaten["muster_clusterAnzahl"] = clusterZuordnung.Count;

                // Pruefen ob ein Cluster auffaellig dominiert
                int maxCluster = clusterZuordnung.Values.Max(l => l.Count);
                if (maxCluster > letzteInstanzen.Count * 0.6f)
                {
                    ergebnis.nachrichten.Add(new AgentNachricht
                    {
                        von = Name, an = "Reflexion", typ = "MUSTER",
                        inhalt = $"Dominantes Muster erkannt: {maxCluster}/{letzteInstanzen.Count} Instanzen im gleichen Cluster",
                        prioritaet = 0.7f
                    });
                }

                // Revisionskandidaten pruefen
                var kandidaten = gedaechtnis.RevisionskandidatenHolen();
                if (kandidaten.Count > 0)
                {
                    ergebnis.erzeugteDaten["muster_revisionKandidaten"] = kandidaten.Count;
                    ergebnis.nachrichten.Add(new AgentNachricht
                    {
                        von = Name, an = "Reflexion", typ = "REVISION",
                        inhalt = $"{kandidaten.Count} Cluster brauchen hermeneutische Revision",
                        prioritaet = 0.6f
                    });
                }
            }

            return Task.FromResult(ergebnis);
        }
    }

    /// <summary>
    /// BewertungsAgent: Bewertet die aktuelle Situation und empfiehlt Aktionen.
    /// Nutzt den ReinforcementLerner (Q-Werte) statt LLM.
    /// Aktivierung steigt wenn eine Entscheidung ansteht.
    /// </summary>
    public class BewertungsAgent : MikroAgent
    {
        private readonly ReinforcementLerner rl;
        private readonly ZustandsEncoder encoder;

        public BewertungsAgent(ReinforcementLerner rl, ZustandsEncoder encoder)
            : base("Bewertung")
        {
            this.rl = rl;
            this.encoder = encoder;
        }

        protected override float BerechneAktivierung(Blackboard bb)
        {
            bool entscheidungNoetig = bb.Lies<bool>("entscheidung_noetig", false);
            return entscheidungNoetig ? 0.9f : 0.3f;
        }

        protected override Task<AgentErgebnis> Tick(Blackboard bb)
        {
            var ergebnis = new AgentErgebnis { agent = Name, ausgefuehrt = true };

            var zustand = bb.Lies<float[]>("zustand_vektor");
            if (zustand == null) return Task.FromResult(ergebnis);

            // RL-basierte Aktionsempfehlung
            var (aktion, konfidenz) = rl.WaehleAktion(zustand);
            if (aktion != null)
            {
                ergebnis.erzeugteDaten["rl_empfehlung"] = aktion.Value;
                ergebnis.erzeugteDaten["rl_konfidenz"] = konfidenz;

                if (konfidenz > 0.6f)
                {
                    ergebnis.nachrichten.Add(new AgentNachricht
                    {
                        von = Name, an = "Planung", typ = "VORSCHLAG",
                        inhalt = $"RL empfiehlt: {aktion.Value} (Konfidenz: {konfidenz:F2})",
                        prioritaet = konfidenz
                    });
                }
            }

            // Vertrautheit berechnen
            float vertrautheit = rl.GetVertrautheit(zustand);
            ergebnis.erzeugteDaten["rl_vertrautheit"] = vertrautheit;

            return Task.FromResult(ergebnis);
        }
    }

    /// <summary>
    /// EmotionsAgent: Reagiert auf emotionale Zustaende und beeinflusst
    /// die Aktivierung anderer Agenten indirekt (ueber Blackboard).
    /// Aktivierung steigt bei emotionalen Extremen.
    /// </summary>
    public class EmotionsAgent : MikroAgent
    {
        public EmotionsAgent() : base("Emotion") { }

        protected override float BerechneAktivierung(Blackboard bb)
        {
            var emo = bb.Lies<EmotionalerZustand>("emotionen");
            if (emo == null) return 0.2f;

            // Aktivierung steigt bei Extremen (hohe Angst, Frustration, oder Neugier)
            float intensitaet = (emo.angst + emo.frustration + emo.neugier + emo.ueberraschung) / 4f;
            return 0.2f + intensitaet;
        }

        protected override Task<AgentErgebnis> Tick(Blackboard bb)
        {
            var ergebnis = new AgentErgebnis { agent = Name, ausgefuehrt = true };
            var emo = bb.Lies<EmotionalerZustand>("emotionen");
            if (emo == null) return Task.FromResult(ergebnis);

            // Emotionale Modulation berechnen (beeinflusst andere Agenten)
            var modulation = new EmotionsModulation
            {
                explorationsFaktor = emo.neugier > 0.5f ? 1.5f : (emo.angst > 0.5f ? 0.5f : 1f),
                vorsichtsFaktor = emo.angst > 0.5f ? 1.5f : 1f,
                kreativitaetsFaktor = emo.frustration > 0.6f ? 1.5f : 1f,
                lernPrioritaet = emo.ueberraschung > 0.5f ? 1.5f : 1f
            };
            ergebnis.erzeugteDaten["emotionsModulation"] = modulation;

            // Kritischer Zustand → Alarm
            if (emo.KritischerZustand())
            {
                ergebnis.nachrichten.Add(new AgentNachricht
                {
                    von = Name, an = "*", typ = "ALARM",
                    inhalt = "Kritischer emotionaler Zustand — Angst + Frustration hoch",
                    prioritaet = 1f
                });
            }

            // Frustrations-Spirale erkennen
            float vorherigeFrust = bb.Lies<float>("letzte_frustration", 0f);
            if (emo.frustration > vorherigeFrust + 0.1f && emo.frustration > 0.5f)
            {
                ergebnis.nachrichten.Add(new AgentNachricht
                {
                    von = Name, an = "Reflexion", typ = "WARNUNG",
                    inhalt = "Frustration steigt — Strategiewechsel empfohlen",
                    prioritaet = 0.8f
                });
            }
            ergebnis.erzeugteDaten["letzte_frustration"] = emo.frustration;

            return Task.FromResult(ergebnis);
        }
    }

    /// <summary>
    /// SozialAgent: Beobachtet NPCs und soziale Dynamiken.
    /// Aktivierung steigt wenn NPCs in der Naehe sind.
    /// </summary>
    public class SozialAgent : MikroAgent
    {
        public SozialAgent() : base("Sozial") { }

        protected override float BerechneAktivierung(Blackboard bb)
        {
            int npcs = bb.Lies<int>("npcs_in_naehe", 0);
            return npcs > 0 ? 0.5f + npcs * 0.15f : 0.1f;
        }

        protected override Task<AgentErgebnis> Tick(Blackboard bb)
        {
            var ergebnis = new AgentErgebnis { agent = Name, ausgefuehrt = true };

            int npcs = bb.Lies<int>("npcs_in_naehe", 0);
            var sozialAnalyse = bb.Lies<SozialeAnalyse>("sozial_analyse");

            if (sozialAnalyse != null && sozialAnalyse.aktiveArchetypen.Count > 0)
            {
                ergebnis.erzeugteDaten["sozial_aktiveArchetypen"] = sozialAnalyse.aktiveArchetypen;

                // Wenn Spannungen zwischen Archetypen: Warnung
                if (sozialAnalyse.aktiveArchetypen.Count > 2)
                {
                    ergebnis.nachrichten.Add(new AgentNachricht
                    {
                        von = Name, an = "Planung", typ = "KONTEXT",
                        inhalt = $"Komplexe soziale Situation: {sozialAnalyse.aktiveArchetypen.Count} aktive Archetypen",
                        prioritaet = 0.5f
                    });
                }
            }

            return Task.FromResult(ergebnis);
        }
    }

    /// <summary>
    /// NeugierAgent: Generiert Explorations-Impulse.
    /// Aktivierung steigt bei geringer Vertrautheit und hoher Neugier.
    /// </summary>
    public class NeugierAgent : MikroAgent
    {
        public NeugierAgent() : base("Neugier") { }

        protected override float BerechneAktivierung(Blackboard bb)
        {
            float vertrautheit = bb.Lies<float>("rl_vertrautheit", 0.5f);
            var emo = bb.Lies<EmotionalerZustand>("emotionen");
            float neugier = emo?.neugier ?? 0.3f;

            // Hoch wenn unbekannt + neugierig
            return (1f - vertrautheit) * 0.5f + neugier * 0.5f;
        }

        protected override Task<AgentErgebnis> Tick(Blackboard bb)
        {
            var ergebnis = new AgentErgebnis { agent = Name, ausgefuehrt = true };

            float vertrautheit = bb.Lies<float>("rl_vertrautheit", 0.5f);
            if (vertrautheit < 0.3f)
            {
                ergebnis.nachrichten.Add(new AgentNachricht
                {
                    von = Name, an = "Planung", typ = "EXPLORATION",
                    inhalt = "Unbekanntes Terrain — Exploration empfohlen statt Exploitation",
                    prioritaet = 0.6f
                });
                ergebnis.erzeugteDaten["exploration_empfohlen"] = true;
            }

            return Task.FromResult(ergebnis);
        }
    }

    /// <summary>
    /// PlanungsAgent: Bewertet ob der aktuelle Plan noch gut ist.
    /// Nutzt RL-Empfehlungen und Nachrichten anderer Agenten.
    /// </summary>
    public class PlanungsAgent : MikroAgent
    {
        public PlanungsAgent() : base("Planung") { }

        protected override float BerechneAktivierung(Blackboard bb)
        {
            bool hatPlan = bb.Lies<bool>("hat_plan", false);
            bool entscheidungNoetig = bb.Lies<bool>("entscheidung_noetig", false);
            if (entscheidungNoetig) return 0.9f;
            return hatPlan ? 0.4f : 0.6f;
        }

        protected override Task<AgentErgebnis> Tick(Blackboard bb)
        {
            var ergebnis = new AgentErgebnis { agent = Name, ausgefuehrt = true };
            var nachrichten = bb.HoleNachrichten(Name);

            // RL-Empfehlung beruecksichtigen
            float rlKonfidenz = bb.Lies<float>("rl_konfidenz", 0f);
            bool rlWiderspricht = false;

            foreach (var n in nachrichten)
            {
                if (n.typ == "VORSCHLAG" && n.von == "Bewertung" && rlKonfidenz > 0.6f)
                {
                    ergebnis.erzeugteDaten["plan_rl_empfehlung"] = n.inhalt;
                }
                if (n.typ == "EXPLORATION" && n.von == "Neugier")
                {
                    ergebnis.erzeugteDaten["plan_exploration_empfohlen"] = true;
                }
                if (n.typ == "ALARM")
                {
                    ergebnis.erzeugteDaten["plan_umplanung_noetig"] = true;
                    ergebnis.nachrichten.Add(new AgentNachricht
                    {
                        von = Name, an = "*", typ = "UMPLANUNG",
                        inhalt = $"Umplanung wegen: {n.inhalt}",
                        prioritaet = 0.8f
                    });
                }
            }

            return Task.FromResult(ergebnis);
        }
    }

    /// <summary>
    /// ReflexionsAgent: Die Meta-Ebene.
    /// Beobachtet was die anderen Agenten tun und ob das System als Ganzes funktioniert.
    /// Aktivierung steigt periodisch und bei Nachrichten von anderen.
    /// </summary>
    public class ReflexionsAgent : MikroAgent
    {
        private int tickCounter;

        public ReflexionsAgent() : base("Reflexion") { }

        protected override float BerechneAktivierung(Blackboard bb)
        {
            tickCounter++;
            // Periodisch aktiv (alle 10 Ticks) oder wenn Nachrichten vorliegen
            float basis = tickCounter % 10 == 0 ? 0.7f : 0.2f;
            int kandidaten = bb.Lies<int>("muster_revisionKandidaten", 0);
            return basis + kandidaten * 0.1f;
        }

        protected override Task<AgentErgebnis> Tick(Blackboard bb)
        {
            var ergebnis = new AgentErgebnis { agent = Name, ausgefuehrt = true };
            var nachrichten = bb.HoleNachrichten(Name);

            // Meta-Beobachtungen sammeln
            var beobachtungen = new List<string>();

            foreach (var n in nachrichten)
            {
                if (n.typ == "MUSTER")
                    beobachtungen.Add($"Muster: {n.inhalt}");
                if (n.typ == "REVISION")
                    beobachtungen.Add($"Revision noetig: {n.inhalt}");
                if (n.typ == "WARNUNG")
                    beobachtungen.Add($"Warnung: {n.inhalt}");
            }

            if (beobachtungen.Count > 0)
                ergebnis.erzeugteDaten["reflexion_beobachtungen"] = beobachtungen;

            // Lernfortschritt beobachten
            int rlUpdates = bb.Lies<int>("rl_gesamtUpdates", 0);
            int rlZustaende = bb.Lies<int>("rl_bekannteZustaende", 0);
            float exploration = bb.Lies<float>("rl_exploration", 0.3f);

            var lernMetriken = new Dictionary<string, float>
            {
                ["rl_updates"] = rlUpdates,
                ["rl_zustaende"] = rlZustaende,
                ["rl_exploration"] = exploration,
                ["instanzen_gesamt"] = bb.Lies<int>("instanzen_gesamt", 0),
                ["cluster_gesamt"] = bb.Lies<int>("cluster_gesamt", 0)
            };
            ergebnis.erzeugteDaten["reflexion_lernMetriken"] = lernMetriken;

            return Task.FromResult(ergebnis);
        }
    }
}
