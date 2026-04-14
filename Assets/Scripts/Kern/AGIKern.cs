using System.Threading.Tasks;
using UnityEngine;
using BilligAGI.Modelle;
using BilligAGI.Sensorik;
using BilligAGI.Welt;
using BilligAGI.Physik;
using BilligAGI.Sozial;
using BilligAGI.Gedaechtnis;
using BilligAGI.Intentionalitaet;

namespace BilligAGI.Kern
{
    public class AGIKern : MonoBehaviour
    {
        [Header("Config")]
        public AGIConfig config;

        [Header("Unity-Referenzen")]
        public AGIAgent agent;
        public AktionsController aktionsController;
        public SensorSuite sensorSuite;
        public WeltController weltController;
        public Bio.WetterSystem wetterSystem;

        // Subsysteme
        private LLMAdapter llm;
        private SemantikKernel semantik;
        private RobustheitsManager robustheit;
        private KreativitaetsEngine kreativitaet;
        private VAKOGLexikon vakogLexikon;
        private VAKOGEngine vakogEngine;
        private PhysikEngine physikEngine;
        private SozialEngine sozialEngine;
        private VektorDB vektorDB;
        private ErfahrungsSpeicher erfahrungen;
        private Konsolidierung konsolidierung;
        private LangzeitLernen langzeitLernen;
        private AnalogieEngine analogie;
        private NeugierSystem neugier;
        private SelbstModell selbstModell;
        private KausalGraph kausalGraph;
        private SubsymbolikKernel subsymbolik;
        private KonzeptRevision konzeptRevision;
        private EmotionsSystem emotionen;
        private ZeitModell zeitModell;
        private ZielManager zielManager;
        private Planer planer;
        private Ausfuehrer ausfuehrer;
        private Monitor monitor;
        private NarrativesSelbst narrativ;
        private SituationsBewerter bewerter;
        private WeltModell weltModell;
        private KonsistenzPruefer konsistenz;

        // Neue Subsysteme: Echtes Lernen (B), Emergenz (C), Meta-Kognition (D)
        private ZustandsEncoder zustandsEncoder;
        private ReinforcementLerner rl;
        private InstanzClusterer clusterer;
        private AgentNetzwerk agentNetzwerk;
        private MetaKognition metaKognition;
        private float[] letzterZustandsVektor;

        // Zustand
        private bool initialisiert;
        private bool autonomerModus;
        private int autonomeSchritte;
        private float letzterTick;
        private string pendingInput;
        private int aktuellerPlanSchritt;
        private Plan aktuellerPlan;

        // API-Server Zustand
        private string letzteAntwort;
        private bool apiVerarbeitung;
        private string apiSystemPrompt;

        private async void Start()
        {
            await Initialisiere();
        }

        private async Task Initialisiere()
        {
            Debug.Log("[AGIKern] Initialisiere Billig-AGI...");

            // Kern-Subsysteme
            llm = new LLMAdapter(config);
            semantik = new SemantikKernel(config);
            robustheit = new RobustheitsManager(config);
            kreativitaet = new KreativitaetsEngine(llm, config);

            // Sensorik
            vakogLexikon = new VAKOGLexikon(llm);
            vakogEngine = new VAKOGEngine(vakogLexikon);

            // Physik
            physikEngine = new PhysikEngine(llm, config);

            // Gedaechtnis
            vektorDB = new VektorDB();
            await vektorDB.Initialisiere();
            erfahrungen = new ErfahrungsSpeicher(vektorDB, llm, config);
            konsolidierung = new Konsolidierung(erfahrungen, config);
            langzeitLernen = new LangzeitLernen(erfahrungen, config);

            // Meta-Kognition (KonzeptRevision frueh, wird von Sozial gebraucht)
            konzeptRevision = new KonzeptRevision(llm, erfahrungen, config);
            analogie = new AnalogieEngine(llm);
            selbstModell = new SelbstModell();
            kausalGraph = new KausalGraph();
            neugier = new NeugierSystem(config);
            subsymbolik = new SubsymbolikKernel();
            emotionen = new EmotionsSystem(config);
            emotionen.LadeZustand();
            zeitModell = new ZeitModell();

            // Sozial (bekommt KonzeptRevision fuer hermeneutische Archetyp-Erkennung)
            sozialEngine = new SozialEngine(llm, konzeptRevision);

            // Intentionalitaet
            zielManager = new ZielManager(llm, selbstModell, emotionen, zeitModell, config);
            planer = new Planer(llm, zeitModell, analogie);
            weltModell = new WeltModell();
            weltModell.LadeVonDisk();
            ausfuehrer = new Ausfuehrer(aktionsController, weltModell);
            monitor = new Monitor(weltModell, emotionen);

            // Narrativ + Bewertung
            var alchemie = sozialEngine.GetAlchemie();
            narrativ = new NarrativesSelbst(llm, selbstModell, emotionen, alchemie, konzeptRevision, config);
            bewerter = new SituationsBewerter(config);
            konsistenz = new KonsistenzPruefer(config);

            // B) Echtes Lernen: RL + ML-Clustering
            zustandsEncoder = new ZustandsEncoder();
            rl = new ReinforcementLerner(zustandsEncoder, config);
            clusterer = new InstanzClusterer();

            // C) Emergenz: Dezentrale Mikroagenten
            agentNetzwerk = new AgentNetzwerk(4);
            var archetypenGedaechtnis = sozialEngine.GetArchetypenGedaechtnis();
            agentNetzwerk.RegistriereAgent(new WahrnehmungsAgent());
            agentNetzwerk.RegistriereAgent(new MusterAgent(clusterer, archetypenGedaechtnis));
            agentNetzwerk.RegistriereAgent(new BewertungsAgent(rl, zustandsEncoder));
            agentNetzwerk.RegistriereAgent(new EmotionsAgent());
            agentNetzwerk.RegistriereAgent(new SozialAgent());
            agentNetzwerk.RegistriereAgent(new NeugierAgent());
            agentNetzwerk.RegistriereAgent(new PlanungsAgent());
            agentNetzwerk.RegistriereAgent(new ReflexionsAgent());

            // D) Meta-Kognition
            metaKognition = new MetaKognition();

            autonomerModus = config.autonomerModus;
            initialisiert = true;
            Debug.Log("[AGIKern] Initialisierung abgeschlossen.");
        }

        private void Update()
        {
            if (!initialisiert || apiVerarbeitung) return;
            if (Time.time - letzterTick < config.zyklusIntervall) return;
            letzterTick = Time.time;

            _ = Zyklus();
        }

        public void VerarbeiteInput(string input)
        {
            pendingInput = input;
        }

        private async Task Zyklus()
        {
            zeitModell.Tick();
            string input = pendingInput;
            string systemKontext = apiSystemPrompt;
            pendingInput = null;
            apiSystemPrompt = null;

            // ==== 1. WAHRNEHMEN ====
            SensorDaten sensorDaten = sensorSuite != null ? sensorSuite.AktualisiereSensoren() : null;
            VAKOGProfil vakogSensor = vakogEngine.AnalysiereSensorisch(sensorDaten);
            VAKOGProfil vakogText = null;

            if (!string.IsNullOrEmpty(input))
                vakogText = vakogEngine.AnalysiereText(input);

            VAKOGProfil vakogGesamt = (vakogSensor != null && vakogText != null)
                ? vakogEngine.AnalysiereDual(input, sensorDaten)
                : vakogSensor ?? vakogText;

            // ==== 2. SEMANTIK KOMPILIEREN ====
            SemantikFrame frame = null;
            bool lokal = false;
            if (!string.IsNullOrEmpty(input))
            {
                frame = semantik.Parse(input);
                lokal = semantik.KannLokalBearbeiten(frame);
            }

            // ==== 3. ERINNERN ====
            var aehnliche = !string.IsNullOrEmpty(input)
                ? await erfahrungen.FindeAehnliche(input, 3)
                : new System.Collections.Generic.List<Erfahrung>();

            // ==== 4. WELT PRUEFEN ====
            PlausibilitaetsErgebnis physikCheck = null;
            if (!string.IsNullOrEmpty(input))
                physikCheck = await physikEngine.PruefePlausibilitaetMitLLM(input);

            // ==== 5. SOZIAL ANALYSIEREN ====
            var sozialAnalyse = await sozialEngine.Analysiere(
                input ?? "", "aktuellen_kontext", aehnliche, sensorDaten);

            // ==== 6. ANALOGIEN SUCHEN ====
            var analogien = await analogie.SucheAnalogien(input ?? "", aehnliche);

            // ==== 7. BEWERTEN ====
            var agentZustand = agent?.ExportiereZustand();
            var aktuellesZiel = zielManager.GetAktivesZiel();
            var bewertung = bewerter.Bewerte(
                vakogGesamt, weltModell.zustand, agentZustand,
                emotionen.zustand, aktuellesZiel);

            // ==== 8. KONSISTENZ PRUEFEN ====
            var konsistenzFehler = konsistenz.Pruefe(weltModell);
            foreach (var f in konsistenzFehler)
                konsistenz.AutoRepariere(f, weltModell);

            // ==== 8b. ZUSTANDSVEKTOR BERECHNEN (fuer RL + Blackboard) ====
            int objekteNah = weltModell?.zustand?.objekte?.Count ?? 0;
            float kompetenz = selbstModell.GetKompetenz(
                aktuellesZiel?.typ.ToString().ToLowerInvariant() ?? "allgemein");
            float planFortschritt = (aktuellerPlan != null && aktuellerPlan.aktionen.Count > 0)
                ? aktuellerPlanSchritt / (float)aktuellerPlan.aktionen.Count : 0f;

            float[] zustandsVektor = zustandsEncoder.Kodiere(
                vakogGesamt, emotionen.zustand, weltModell?.zustand,
                bewertung.gesamtRelevanz, kompetenz, objekteNah, 0, planFortschritt, 0f);
            zustandsEncoder.Tick();

            // ==== 8c. BLACKBOARD FUETTERN (fuer Mikroagenten) ====
            var bb = agentNetzwerk.Blackboard;
            bb.Schreibe("sensor_vakog", vakogGesamt);
            bb.Schreibe("sensor_roh", sensorDaten);
            bb.Schreibe("emotionen", emotionen.zustand);
            bb.Schreibe("zustand_vektor", zustandsVektor);
            bb.Schreibe("hat_plan", aktuellerPlan != null);
            bb.Schreibe("entscheidung_noetig", aktuellerPlan == null && aktuellesZiel != null);
            bb.Schreibe("sozial_analyse", sozialAnalyse);
            bb.Schreibe("rl_gesamtUpdates", rl.GetGesamtUpdates());
            bb.Schreibe("rl_bekannteZustaende", rl.GetBekannteZustaende());
            bb.Schreibe("rl_exploration", rl.GetExplorationRate());
            var archGed = sozialEngine.GetArchetypenGedaechtnis();
            bb.Schreibe("instanzen_gesamt", archGed.GesamtInstanzen);
            bb.Schreibe("cluster_gesamt", archGed.GesamtCluster);

            // ==== 8d. MIKROAGENTEN TICK (Emergenz) ====
            await agentNetzwerk.Tick();

            // ==== 9. EMOTIONEN AKTUALISIEREN ====
            emotionen.Tick();

            // ==== 10. KREATIVE VARIANTEN ====
            // (aktiviert bei hoher Frustration oder Kreativitaets-Ziel)

            // ==== 10b. RL-EMPFEHLUNG PRUEFEN ====
            // Wenn RL eine Empfehlung mit hoher Konfidenz hat UND Meta-Kognition
            // sagt dass diese Strategie hier funktioniert → bevorzugen
            var (rlAktion, rlKonfidenz2) = rl.WaehleAktion(zustandsVektor);
            bool rlUeberstimmt = false;
            if (rlAktion != null && rlKonfidenz2 > 0.7f)
            {
                string empfohleneStrategie = metaKognition.EmpfiehlStrategie("allgemein");
                if (empfohleneStrategie == "rl_empfehlung")
                    rlUeberstimmt = true; // RL hat genuegend Vertrauen aufgebaut
            }

            // ==== 11. PLANEN ====
            if (aktuellesZiel != null && aktuellerPlan == null)
            {
                aktuellerPlan = await planer.ErstellePlan(aktuellesZiel, weltModell.zustand);
                aktuellerPlanSchritt = 0;
            }

            // ==== 12. NACHDENKEN ====
            string antwort = null;
            if (lokal && frame != null)
            {
                antwort = semantik.LokalAntwort(frame, weltModell.zustand, agentZustand);
                semantik.BerechneQuote();
            }
            else if (!string.IsNullOrEmpty(input))
            {
                // AGI-Kontext aus vorherigen kognitiven Schritten zusammenbauen
                var sb = new System.Text.StringBuilder();
                if (!string.IsNullOrEmpty(systemKontext))
                    sb.AppendLine(systemKontext);

                if (aehnliche.Count > 0)
                {
                    sb.AppendLine("\n[Relevante Erinnerungen]");
                    foreach (var e in aehnliche)
                        sb.AppendLine($"- {e.aktion}: {e.ergebnis}");
                }
                if (analogien != null && analogien.Count > 0)
                {
                    sb.AppendLine("\n[Analogien]");
                    foreach (var a in analogien)
                        sb.AppendLine($"- {a.quellDommaene} -> {a.zielDomaene}: {a.transferHypothese}");
                }
                if (physikCheck != null && !physikCheck.plausibel)
                    sb.AppendLine($"\n[Physik-Warnung] {physikCheck.begruendung}");
                if (sozialAnalyse?.erkannteMechanismen?.Count > 0)
                    sb.AppendLine($"\n[Sozial-Kontext] Archetyp: {sozialAnalyse.archetyp}, Phase: {sozialAnalyse.alchemischePhase}");

                string enrichedSystem = sb.Length > 0 ? sb.ToString() : null;

                // LLM-basiert mit AGI-Kontext
                var mod = robustheit.SollLLMGenutztWerden()
                    ? await llm.FreieAnfrage(input, enrichedSystem)
                    : null;
                antwort = mod?.inhalt ?? semantik.LokaleDegradation(input);
            }

            // ==== 13. HANDELN ====
            if (autonomerModus && aktuellerPlan != null && aktuellerPlanSchritt < aktuellerPlan.aktionen.Count)
            {
                var aktion = aktuellerPlan.aktionen[aktuellerPlanSchritt];
                var aktionsErgebnis = await ausfuehrer.FuehreAus(aktion);

                // Monitor
                var monitorErgebnis = monitor.Ueberwache(aktuellerPlan, aktuellerPlanSchritt);

                if (aktionsErgebnis.erfolg)
                {
                    aktuellerPlanSchritt++;
                    selbstModell.AktualisiereKompetenz(aktion.name, true);
                }
                else
                {
                    selbstModell.AktualisiereKompetenz(aktion.name, false);
                    if (monitorErgebnis.entscheidung == MonitorEntscheidung.UMPLANEN)
                        aktuellerPlan = await planer.PlaneUm(aktuellerPlan, aktionsErgebnis.beschreibung);
                }

                if (monitorErgebnis.planAbgeschlossen)
                {
                    zielManager.ZielErreicht(aktuellesZiel.id, "Plan abgeschlossen.");
                    aktuellerPlan = null;
                }

                autonomeSchritte++;
                if (autonomeSchritte >= config.maxAutonomeSchritte)
                    autonomerModus = false;
            }

            // ==== 14. SELBST PRUEFEN ====
            // (Validierung via Robustheit)
            var metriken = new SystemMetriken
            {
                apiErreichbar = true,
                fehlerRate = 0f,
                tokenBudgetVerbraucht = 0f
            };
            robustheit.BestimmeModus(metriken);

            // ==== 15. LERNEN ====
            if (!string.IsNullOrEmpty(input))
            {
                var erfahrung = new Erfahrung
                {
                    id = System.Guid.NewGuid().ToString(),
                    aktion = input,
                    kontext = aktuellesZiel?.beschreibung ?? "frei",
                    ergebnis = antwort ?? "",
                    belohnung = bewertung.gesamtRelevanz,
                    vakog = vakogGesamt,
                    emotionalerZustand = emotionen.zustand,
                    zeitstempel = System.DateTime.UtcNow.ToString("o"),
                    konzepte = new System.Collections.Generic.List<string>()
                };

                await erfahrungen.Speichere(erfahrung);
                emotionen.Aktualisiere(erfahrung);
                subsymbolik.EmbeddeKontext(erfahrung);
                zeitModell.RegistriereErfahrung(erfahrung);

                // ==== 15b. RL-LERNEN (ohne LLM) ====
                if (letzterZustandsVektor != null)
                {
                    AktionsTyp aktionsTyp = AktionsTyp.Beobachten;
                    if (erfahrung.aktionenListe != null && erfahrung.aktionenListe.Count > 0)
                        aktionsTyp = erfahrung.aktionenListe[0].typ;

                    rl.Lerne(letzterZustandsVektor, aktionsTyp, erfahrung.belohnung, zustandsVektor);
                    zustandsEncoder.RegistriereErfolg();
                }
                letzterZustandsVektor = zustandsVektor;

                // ==== 15c. META-KOGNITION ====
                bool hatLLMGenutzt = !lokal;
                float rlKonfidenz = bb.Lies<float>("rl_konfidenz", 0f);
                string strategie = lokal ? "lokal" : (rlKonfidenz > 0.6f ? "rl_empfehlung" : "llm_plan");
                string kontextCluster = "allgemein";
                if (sozialAnalyse?.aktiveArchetypen?.Count > 0) kontextCluster = "sozial";

                metaKognition.RegistriereEntscheidung(
                    strategie, kontextCluster, erfahrung.belohnung,
                    erfahrung.belohnung > 0, rlKonfidenz, hatLLMGenutzt);
                metaKognition.RegistriereLernSchritt(
                    erfahrung.belohnung, rl.GetBekannteZustaende(), archGed.GesamtInstanzen);
                metaKognition.Tick();

                // ==== 16. KONZEPTE PRUEFEN ====
                // (Periodisch Revisionen triggern)

                // ==== 18. NARRATIV + NEUGIER ====
                await narrativ.ErfahrungIntegrieren(erfahrung);
            }

            // ==== 17. ROBUSTHEITSMODUS ====
            robustheit.BestimmeModus(metriken);

            // Neugier: neue Hypothesen/Ziele
            neugier.GeneriereHypothesen(weltModell.zustand, selbstModell, kausalGraph);

            // Antwort speichern + an UI senden
            letzteAntwort = antwort;
            if (!string.IsNullOrEmpty(antwort))
                Debug.Log($"[AGI] {antwort}");
        }

        // Oeffentliche API
        public string GetModus() => autonomerModus ? "AUTONOM" : "REAKTIV";
        public void SetzeAutonom(bool an) { autonomerModus = an; autonomeSchritte = 0; }
        public EmotionsSystem GetEmotionen() => emotionen;
        public SelbstModell GetSelbstModell() => selbstModell;
        public ZielManager GetZielManager() => zielManager;
        public NarrativesSelbst GetNarativ() => narrativ;
        public WeltModell GetWeltModell() => weltModell;
        public SemantikKernel GetSemantik() => semantik;
        public SozialEngine GetSozialEngine() => sozialEngine;
        public KonzeptRevision GetKonzeptRevision() => konzeptRevision;
        public RobustheitsManager GetRobustheit() => robustheit;
        public LLMAdapter GetLLM() => llm;
        public ErfahrungsSpeicher GetErfahrungen() => erfahrungen;
        public Konsolidierung GetKonsolidierung() => konsolidierung;
        public KreativitaetsEngine GetKreativitaet() => kreativitaet;
        public ReinforcementLerner GetRL() => rl;
        public MetaKognition GetMetaKognition() => metaKognition;
        public AgentNetzwerk GetAgentNetzwerk() => agentNetzwerk;
        public InstanzClusterer GetClusterer() => clusterer;

        // ==== API-Server Schnittstelle ====
        public bool IstBereit() => initialisiert && !apiVerarbeitung;

        public async Task<string> VerarbeiteAnfrageAsync(string input, string systemPrompt = null)
        {
            if (!initialisiert) return "[FEHLER] AGI nicht initialisiert";
            if (apiVerarbeitung) return "[FEHLER] AGI verarbeitet bereits eine Anfrage";

            apiVerarbeitung = true;
            apiSystemPrompt = systemPrompt;
            pendingInput = input;

            try
            {
                await Zyklus();
                return letzteAntwort ?? "[Keine Antwort generiert]";
            }
            finally
            {
                apiVerarbeitung = false;
            }
        }
    }
}
