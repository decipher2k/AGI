using System.Threading.Tasks;
using UnityEngine;
using BilligAGI.Modelle;
using BilligAGI.Sensorik;
using BilligAGI.Welt;
using BilligAGI.Physik;
using BilligAGI.Sozial;
using BilligAGI.Gedaechtnis;
using BilligAGI.Intentionalitaet;
using BilligAGI.Evaluation;

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
        public WeltGenerator weltGenerator;
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
        private DQNLerner dqn;
        private InstanzClusterer clusterer;
        private AgentNetzwerk agentNetzwerk;
        private MetaKognition metaKognition;
        private float[] letzterZustandsVektor;

        // Phase 16: A+B+C+D
        private ArbeitsGedaechtnis arbeitsGedaechtnis;
        private PrediktivesWeltModell prediktivesModell;

        // Phase 18: Selbstoptimierung / Fine-Tuning
        private ErfahrungsExporter erfahrungsExporter;
        private FineTuningManager fineTuningManager;
        private SelbstOptimierung selbstOptimierung;

        // Phase 19: WeltManipulator (Sprache → Weltveraenderung)
        private WeltManipulator weltManipulator;

        // Phase 20: Transfer-Learning
        private TransferLerner transferLerner;

        // Phase 21: Konzeptbildung / Abstraktion
        private KonzeptBildung konzeptBildung;

        // Phase 22: Kausales Reasoning + Hypothesenbildung
        private KausalesReasoning kausalesReasoning;
        private HypothesenEngine hypothesenEngine;

        // Phase 23: Kontinuierliches Lernen + Hierarchische Abstraktion
        private KonzeptBaum konzeptBaum;

        // Phase 24: MetaZielSystem + GroundingBruecke
        private MetaZielSystem metaZielSystem;
        private GroundingBruecke groundingBruecke;

        // Phase 25: Intuitive Physik + Mentale Simulation
        private IntuitiverPhysikSimulator physikSimulator;
        private MentaleSimulation mentaleSimulation;

        // Phase 26: Langzeit-Planung + Selbst-Curriculum
        private LangzeitPlaner langzeitPlaner;
        private SelbstCurriculum selbstCurriculum;

        // Phase 27: Grounded Sprachproduktion
        private GroundedSprachproduktion groundedSprache;

        // Stabilitaets-Refactor: QoS fuer Zykluslatenz
        private ZyklusStabilisator zyklusStabilisator;

        // Phase 28 (Start): Autonome Missions-Sessions
        private AutonomieMissionen autonomieMissionen;

        // ARC-2 Evaluation
        private Arc2Evaluator arc2Evaluator;

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
            kreativitaet = new KreativitaetsEngine(config, llm);

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
            if (config.dqnStattTabular)
                dqn = new DQNLerner(zustandsEncoder, config);
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

            // Phase 16: Arbeitsgedaechtnis + Prediktives Weltmodell
            if (config.arbeitsGedaechtnisAktiv)
                arbeitsGedaechtnis = new ArbeitsGedaechtnis(
                    config.arbeitsGedaechtnisMaxInteraktionen, 5,
                    config.arbeitsGedaechtnisTokenBudget);
            prediktivesModell = new PrediktivesWeltModell(config.weltModellAktiv);

            // Phase 18: Selbstoptimierung / Fine-Tuning
            erfahrungsExporter = new ErfahrungsExporter();
            fineTuningManager = new FineTuningManager(config);
            selbstOptimierung = GetComponent<SelbstOptimierung>();
            if (selbstOptimierung != null)
                selbstOptimierung.Initialisiere(config, llm, erfahrungen, erfahrungsExporter, fineTuningManager);

            // Phase 19: WeltManipulator
            weltManipulator = new WeltManipulator(llm, weltController, weltGenerator, config);

            // Phase 20: Transfer-Learning
            transferLerner = new TransferLerner(llm, erfahrungen, analogie, kausalGraph, subsymbolik, clusterer, config);

            // Phase 21: Konzeptbildung / Abstraktion
            konzeptBildung = new KonzeptBildung(llm, subsymbolik, erfahrungen, konzeptRevision, kausalGraph, config);

            // Phase 22: Kausales Reasoning + Hypothesenbildung
            kausalesReasoning = new KausalesReasoning(llm, kausalGraph, prediktivesModell, erfahrungen, config);
            hypothesenEngine = new HypothesenEngine(llm, kausalGraph, kausalesReasoning, erfahrungen, neugier, selbstModell, config);

            // Phase 23: Hierarchische Abstraktion (EWC ist in DQNLerner integriert)
            konzeptBaum = new KonzeptBaum(llm, konzeptRevision, erfahrungen, config);

            // Phase 24: MetaZielSystem + GroundingBruecke
            metaZielSystem = new MetaZielSystem(
                selbstModell, neugier, hypothesenEngine, metaKognition,
                konzeptBaum, kausalGraph, zielManager, config);
            groundingBruecke = new GroundingBruecke(vakogLexikon, erfahrungen, config);

            // Phase 25: Intuitive Physik + Mentale Simulation
            physikSimulator = new IntuitiverPhysikSimulator(physikEngine, prediktivesModell, config);
            mentaleSimulation = new MentaleSimulation(prediktivesModell, physikSimulator, config);

            // Phase 26: Langzeit-Planung + Selbst-Curriculum
            langzeitPlaner = new LangzeitPlaner(
                planer, mentaleSimulation, zielManager, selbstModell, metaKognition, llm, config);
            selbstCurriculum = new SelbstCurriculum(
                selbstModell, metaKognition, mentaleSimulation, zielManager, config);

            // Phase 27: Grounded Sprachproduktion
            groundedSprache = new GroundedSprachproduktion(
                groundingBruecke, weltModell, mentaleSimulation, physikSimulator);

            // Stabilitaets-Refactor: Zyklus-QoS initialisieren
            zyklusStabilisator = new ZyklusStabilisator();

            // Phase 28 (Start): Autonome Missionen
            autonomieMissionen = new AutonomieMissionen(zielManager, selbstCurriculum);

            // ARC-2 Evaluation
            arc2Evaluator = new Arc2Evaluator(this, config);

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
            float zyklusStart = Time.realtimeSinceStartup;
            zeitModell.Tick();
            string input = pendingInput;
            string systemKontext = apiSystemPrompt;
            pendingInput = null;
            apiSystemPrompt = null;

            // ==== 0b. PHYSIK-INTUITION ====
            SensorDaten sensorDaten = sensorSuite != null ? sensorSuite.AktualisiereSensoren() : null;
            if (physikSimulator != null)
                physikSimulator.ZyklusTick(weltModell?.zustand, sensorDaten, config.zyklusIntervall);

            // ==== 1. WAHRNEHMEN ====
            VAKOGProfil vakogSensor = vakogEngine.AnalysiereSensorisch(sensorDaten);
            VAKOGProfil vakogText = null;

            if (!string.IsNullOrEmpty(input))
                vakogText = await vakogEngine.AnalysiereText(input);

            VAKOGProfil vakogGesamt = (vakogSensor != null && vakogText != null)
                ? await vakogEngine.AnalysiereDual(input, sensorDaten)
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

            // ==== 8b+. ARBEITSGEDAECHTNIS AKTUALISIEREN ====
            if (arbeitsGedaechtnis != null)
            {
                arbeitsGedaechtnis.AktualisiereZiel(aktuellesZiel, aktuellerPlan, aktuellerPlanSchritt);
                arbeitsGedaechtnis.AktualisiereEmotionen(emotionen.zustand);
                arbeitsGedaechtnis.AktualisiereSelbstModell(selbstModell);
                arbeitsGedaechtnis.AktualisiereWelt(weltModell?.zustand);
                arbeitsGedaechtnis.AktualisiereSozialesUmfeld(sozialAnalyse);
            }

            // ==== 8c. BLACKBOARD FUETTERN (fuer Mikroagenten) ====
            var bb = agentNetzwerk.Blackboard;
            bb.Schreibe("sensor_vakog", vakogGesamt);
            bb.Schreibe("sensor_roh", sensorDaten);
            bb.Schreibe("emotionen", emotionen.zustand);
            bb.Schreibe("zustand_vektor", zustandsVektor);
            bb.Schreibe("hat_plan", aktuellerPlan != null);
            bb.Schreibe("entscheidung_noetig", aktuellerPlan == null && aktuellesZiel != null);
            bb.Schreibe("sozial_analyse", sozialAnalyse);
            var aktiverRL = (config.dqnStattTabular && dqn != null) ? (object)dqn : rl;
            bb.Schreibe("rl_gesamtUpdates", config.dqnStattTabular && dqn != null ? dqn.GetGesamtUpdates() : rl.GetGesamtUpdates());
            bb.Schreibe("rl_bekannteZustaende", config.dqnStattTabular && dqn != null ? dqn.GetBekannteZustaende() : rl.GetBekannteZustaende());
            bb.Schreibe("rl_exploration", config.dqnStattTabular && dqn != null ? dqn.GetExplorationRate() : rl.GetExplorationRate());
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
            var (rlAktion, rlKonfidenz2) = (config.dqnStattTabular && dqn != null)
                ? dqn.WaehleAktion(zustandsVektor)
                : rl.WaehleAktion(zustandsVektor);
            bool rlUeberstimmt = false;
            if (rlAktion != null && rlKonfidenz2 > 0.7f)
            {
                string empfohleneStrategie = metaKognition.EmpfiehlStrategie("allgemein");
                if (empfohleneStrategie == "rl_empfehlung")
                    rlUeberstimmt = true; // RL hat genuegend Vertrauen aufgebaut
            }

            // Prediktives Weltmodell: Imagination-basierte Planung
            if (prediktivesModell != null && prediktivesModell.Aktiv)
            {
                var (besteAktion, erwarteterReward) = prediktivesModell.PlaneMitModell(zustandsVektor);
                bb.Schreibe("weltmodell_beste_aktion", besteAktion);
                bb.Schreibe("weltmodell_erwarteter_reward", erwarteterReward);
            }

            // ==== 11. PLANEN ====
            if (aktuellesZiel != null && aktuellerPlan == null)
            {
                // Phase 26: Langzeit-Planer versucht hierarchische Zerlegung
                if (langzeitPlaner != null && !langzeitPlaner.HatAktivenPlan())
                {
                    var lzPlan = await langzeitPlaner.ErstelleLangzeitPlan(
                        aktuellesZiel, weltModell.zustand, zustandsVektor);
                    if (lzPlan != null)
                        Debug.Log($"[AGI] Langzeit-Plan: {lzPlan.meilensteine.Count} Meilensteine");
                }

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
                // AGI-Kontext: Arbeitsgedaechtnis oder manueller Aufbau
                string enrichedSystem;
                if (arbeitsGedaechtnis != null)
                {
                    enrichedSystem = arbeitsGedaechtnis.BaueSystemKontext(
                        systemKontext, aehnliche, analogien, physikCheck);
                }
                else
                {
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
                    enrichedSystem = sb.Length > 0 ? sb.ToString() : null;
                }

                // LLM-basiert: Iteratives Reasoning oder direkt
                LLMAntwort mod = null;
                if (robustheit.SollLLMGenutztWerden())
                {
                    mod = config.iterativesReasoningAktiv
                        ? await llm.IterativesNachdenken(input, enrichedSystem, config.reasoningIterationen)
                        : await llm.FreieAnfrage(input, enrichedSystem);
                }
                antwort = mod?.inhalt ?? semantik.LokaleDegradation(frame, weltModell.zustand, agentZustand);

                // Interaktion im Arbeitsgedaechtnis registrieren
                arbeitsGedaechtnis?.RegistriereInteraktion(input, antwort);
            }

            // ==== 12b. WELT MANIPULIEREN ====
            // Pruefe ob der Input oder die Antwort eine Weltveraenderung impliziert
            if (weltManipulator != null && !string.IsNullOrEmpty(input))
            {
                var manipErgebnis = await weltManipulator.ParseUndFuehreAus(input);
                if (manipErgebnis != null && manipErgebnis.erfolg)
                {
                    string weltInfo = $"[Welt veraendert: {manipErgebnis.beschreibung}]";
                    antwort = string.IsNullOrEmpty(antwort)
                        ? weltInfo
                        : antwort + "\n" + weltInfo;
                }
            }

            // ==== 12c. GROUNDED SPRACHE ==== 
            if (groundedSprache != null && !string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(antwort))
            {
                groundedSprache.ZyklusTick();
                bool erweiterteAntworten = zyklusStabilisator?.ErlaubeErweiterteAntworten() ?? true;
                try
                {
                    antwort = groundedSprache.VeredleAntwort(
                        input,
                        antwort,
                        erweiterteAntworten ? zustandsVektor : null);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AGI] GroundedSprache deaktiviert (Fehler): {ex.Message}");
                }
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

                    if (config.dqnStattTabular && dqn != null)
                        dqn.Lerne(letzterZustandsVektor, aktionsTyp, erfahrung.belohnung, zustandsVektor);
                    else
                        rl.Lerne(letzterZustandsVektor, aktionsTyp, erfahrung.belohnung, zustandsVektor);

                    // Prediktives Weltmodell: Transition lernen
                    prediktivesModell?.RegistriereTransition(
                        letzterZustandsVektor, aktionsTyp, zustandsVektor, erfahrung.belohnung);

                    // ==== 15b+. MENTALE SIMULATION: Kontrafaktische Analyse ====
                    if (mentaleSimulation != null)
                        mentaleSimulation.ZyklusTick(letzterZustandsVektor, aktionsTyp, erfahrung.belohnung);

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
                    erfahrung.belohnung,
                    config.dqnStattTabular && dqn != null ? dqn.GetBekannteZustaende() : rl.GetBekannteZustaende(),
                    archGed.GesamtInstanzen);
                metaKognition.Tick();

                // ==== 15d. TRANSFER-LERNEN ====
                if (transferLerner != null)
                {
                    var transferErgebnis = await transferLerner.ZyklusTick(input, zustandsVektor);
                    if (transferErgebnis != null && transferErgebnis.schemaGefunden)
                    {
                        foreach (var anw in transferErgebnis.anwendungen)
                        {
                            Debug.Log($"[Transfer] Schema {anw.schemaId} anwendbar: {anw.konkreteAktion} ({anw.vorhergesagteErfolgsChance:P0})");
                            // Transfer-Info dem Arbeitsgedaechtnis mitgeben
                            arbeitsGedaechtnis?.SetzeBeliefs(new System.Collections.Generic.List<string>
                            {
                                $"Transfer-Schema anwendbar: {anw.begruendung}",
                                $"Empfohlene Aktion: {anw.konkreteAktion}"
                            });
                        }
                    }
                }

                // ==== 16. KONZEPTE PRUEFEN ====
                // Spontane Konzeptbildung: Unbenannte Cluster → neue Kategorien
                if (konzeptBildung != null)
                {
                    var bildungErgebnis = await konzeptBildung.ZyklusTick();
                    if (bildungErgebnis != null && bildungErgebnis.neuesKonzeptEntdeckt)
                    {
                        Debug.Log($"[KonzeptBildung] {bildungErgebnis.zusammenfassung}");
                    }
                }
                // (Periodisch Revisionen triggern)

                // ==== 16b. KAUSALES REASONING ====
                if (kausalesReasoning != null)
                {
                    bool warGeplant = aktuellerPlan != null && aktuellerPlanSchritt > 0;
                    kausalesReasoning.RegistriereBeobachtung(
                        erfahrung.aktion, erfahrung.ergebnis, erfahrung.belohnung, warGeplant);
                }

                // ==== 16c. HYPOTHESEN PRUEFEN ====
                if (hypothesenEngine != null)
                {
                    var hypErgebnis = await hypothesenEngine.ZyklusTick(erfahrung);
                    if (hypErgebnis != null && hypErgebnis.neueHypothese)
                    {
                        Debug.Log($"[Hypothesen] {hypErgebnis.zusammenfassung}");
                    }
                }

                // ==== 16d. KONZEPTBAUM REORGANISIEREN ====
                if (konzeptBaum != null)
                {
                    var baumErgebnis = await konzeptBaum.ZyklusTick();
                    if (baumErgebnis != null)
                        Debug.Log($"[KonzeptBaum] {baumErgebnis}");
                }

                // ==== 16e. GROUNDING: Erfahrung → Wort-Bindung ====
                if (groundingBruecke != null)
                {
                    groundingBruecke.ZyklusTick(erfahrung);
                }

                // ==== 16f. LANGZEIT-PLANER: Fortschritt pruefen ====
                if (langzeitPlaner != null && langzeitPlaner.HatAktivenPlan())
                {
                    var lzFortschritt = await langzeitPlaner.ZyklusTick(
                        erfahrung.belohnung, zustandsVektor);
                    if (!string.IsNullOrEmpty(lzFortschritt))
                        Debug.Log($"[LangzeitPlaner] {lzFortschritt}");
                }

                // ==== 16g. SELBST-CURRICULUM: Uebung auswerten ====
                if (selbstCurriculum != null)
                {
                    bool aktionErfolg = erfahrung.belohnung > 0f;
                    var uebung = selbstCurriculum.ZyklusTick(erfahrung.belohnung, aktionErfolg);
                    if (uebung != null)
                        Debug.Log($"[Curriculum] Uebung: {uebung.beschreibung} ({uebung.domaene})");
                }

                // ==== 18. NARRATIV + NEUGIER ====
                await narrativ.ErfahrungIntegrieren(erfahrung);
            }

            // ==== 17. ROBUSTHEITSMODUS ====
            robustheit.BestimmeModus(metriken);

            // Neugier: neue Hypothesen/Ziele
            neugier.GeneriereHypothesen(weltModell.zustand, selbstModell, kausalGraph);

            // ==== 19. META-ZIEL-GENERIERUNG ====
            if (metaZielSystem != null)
            {
                var mzErgebnis = metaZielSystem.ZyklusTick();
                if (mzErgebnis != null && mzErgebnis.zieleGeneriert > 0)
                    Debug.Log($"[MetaZielSystem] {mzErgebnis.zusammenfassung}");
            }

            // ==== 19b. AUTONOMIE-MISSIONEN ====
            if (autonomieMissionen != null)
            {
                var missionInfo = autonomieMissionen.ZyklusTick(autonomerModus, bewertung?.gesamtRelevanz ?? 0f);
                if (!string.IsNullOrEmpty(missionInfo))
                    Debug.Log($"[Mission] {missionInfo}");
            }

            // Antwort speichern + an UI senden
            letzteAntwort = antwort;
            if (!string.IsNullOrEmpty(antwort))
                Debug.Log($"[AGI] {antwort}");

            float zyklusMs = (Time.realtimeSinceStartup - zyklusStart) * 1000f;
            zyklusStabilisator?.RegistriereZyklus(zyklusMs);
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
        public DQNLerner GetDQN() => dqn;
        public ArbeitsGedaechtnis GetArbeitsGedaechtnis() => arbeitsGedaechtnis;
        public PrediktivesWeltModell GetPrediktivesWeltModell() => prediktivesModell;
        public MetaKognition GetMetaKognition() => metaKognition;
        public AgentNetzwerk GetAgentNetzwerk() => agentNetzwerk;
        public InstanzClusterer GetClusterer() => clusterer;
        public ErfahrungsExporter GetErfahrungsExporter() => erfahrungsExporter;
        public FineTuningManager GetFineTuningManager() => fineTuningManager;
        public SelbstOptimierung GetSelbstOptimierung() => selbstOptimierung;
        public WeltManipulator GetWeltManipulator() => weltManipulator;
        public TransferLerner GetTransferLerner() => transferLerner;
        public KonzeptBildung GetKonzeptBildung() => konzeptBildung;
        public KausalesReasoning GetKausalesReasoning() => kausalesReasoning;
        public HypothesenEngine GetHypothesenEngine() => hypothesenEngine;
        public KonzeptBaum GetKonzeptBaum() => konzeptBaum;
        public MetaZielSystem GetMetaZielSystem() => metaZielSystem;
        public GroundingBruecke GetGroundingBruecke() => groundingBruecke;
        public IntuitiverPhysikSimulator GetPhysikSimulator() => physikSimulator;
        public MentaleSimulation GetMentaleSimulation() => mentaleSimulation;
        public LangzeitPlaner GetLangzeitPlaner() => langzeitPlaner;
        public SelbstCurriculum GetSelbstCurriculum() => selbstCurriculum;
        public GroundedSprachproduktion GetGroundedSprache() => groundedSprache;
        public ZyklusStabilisator GetZyklusStabilisator() => zyklusStabilisator;
        public AutonomieMissionen GetAutonomieMissionen() => autonomieMissionen;
        public Arc2Evaluator GetArc2Evaluator() => arc2Evaluator;
        public float[] GetLetzterZustandsVektor() => letzterZustandsVektor;

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
