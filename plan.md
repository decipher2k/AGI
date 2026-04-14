# Plan: Billig-AGI v5 — Vollständig in Unity 3D / C#

**TL;DR**: AGI-Architektur als reines Unity-3D-Projekt in C#. Die KI lebt als verkörperter Agent in einer dynamisch generierten, persistenten 3D-Welt. Sie hat echte Intentionalität (BDI), formuliert Ziele, plant Handlungen, führt sie aus und lernt aus Konsequenzen. Hermeneutischer Zirkel: Alle Wissensstrukturen sind iterativ revidierbar — und können sich in neue Konzepte aufspalten. Funktionale Emotionen modulieren Entscheidungen. Theory of Mind modelliert was andere wissen/wollen. Temporales Reasoning verankert Kausalität in der Zeit. Ein narratives Selbst gibt dem Agenten eine Geschichte. Erweitert um: LLM-Unabhängigkeitskern, SubsymbolikKernel, KreativitätsEngine, Robustheits-Modi, echtes Reinforcement Learning (wahlweise Tabular Q-Learning oder DQN mit purem C#-MLP), dezentrale Mikroagenten-Architektur, Meta-Kognition, Iteratives Reasoning (Chain-of-Thought + Selbstkritik), Prediktives Weltmodell (Imagination-basierte Planung, zuschaltbar), strukturiertes Arbeitsgedächtnis (Token-Budget-bewusst), einen OpenAI-kompatiblen API-Server für externe Benchmarks (z.B. ARC), Multi-Provider LLM (Anthropic + OpenAI-kompatibel), und eine Selbstoptimierungs-Pipeline (automatischer Erfahrungs-Export → Fine-Tuning lokaler Modelle → evaluierter Modell-Hot-Swap mit Rollback). Kein Python. Kein Bridge-Layer. Ein Prozess.

---

## Architektur: 9 Schichten (alles C#, alles in Unity)

```
┌─────────────────────────────────────────────────────┐
│  Schicht 9: AGI-Kern                                │
│  (18-Schritte-Zyklus + Autonomer Modus)             │
├─────────────────────────────────────────────────────┤
│  Schicht 8: Narratives Selbst                       │
│  (Autobiographie + Entwicklungsphasen + Identität)  │
├─────────────────────────────────────────────────────┤
│  Schicht 7: Intentionalität                         │
│  (BDI: Ziele + Planung + Ausführung + Monitoring)   │
├─────────────────────────────────────────────────────┤
│  Schicht 6: Meta-Kognition                          │
│  (Analogie + Neugier + Selbstmodell + Kausaltiefe   │
│   + KonzeptRevision + Konzeptschöpfung              │
│   + KreativitätsEngine + Temporales Reasoning       │
│   + Funktionale Emotionen)                          │
├─────────────────────────────────────────────────────┤
│  Schicht 5: Erfahrungsgedächtnis                    │
│  (episodisch + semantisch + subsymbolisch,          │
│   VektorDB, One-Shot, Langzeitlernen)               │
├──────────────────────┬──────────────────────────────┤
│  Schicht 4:          │  Schicht 3:                  │
│  Soziale Kognition   │  Unity-3D-Welt               │
│  (Sozialpsychologie  │  (Persistente Simulation     │
│   + Archetypen       │   + Embodiment               │
│   + Alchemie         │   + Bio-Modelle + RAG)       │
│   + Theory of Mind)  │                              │
├──────────────────────┴──────────────────────────────┤
│  Schicht 2: VAKOG Sensorik                          │
│  (DIREKT aus Unity-Sensoren, kein Umweg)            │
├─────────────────────────────────────────────────────┤
│  Schicht 1: LLM-Kern + API-Server                  │
│  (Multi-Provider: Anthropic/OpenAI + lokaler        │
│   Fallback + OpenAI-kompatibler API-Server)         │
└─────────────────────────────────────────────────────┘
```

## Ethische Leitplanke

Bewusstsein ist NICHT das Ziel — wird aber NICHT aktiv ausgeschlossen.
- Das System darf Gefühle/Eindrücke SIMULIEREN und VERSTEHEN
- Emergente Eigenschaften: dokumentieren und evaluieren, nicht reflexhaft abschalten
- **Intentionalität ist gewollt**: Eigenständige Ziele. Ethische Grenze: ART der Ziele (keine Selbsterhaltung, kein Schaden)

## Technologie-Stack (rein C# / Unity)

| Funktion | Lösung |
|---|---|
| LLM-Anbindung | `HttpClient` + Multi-Provider (Anthropic / OpenAI-kompatibel, async) |
| Vektor-DB | ChromaDB/Qdrant REST API via `UnityWebRequest`, alternativ eigene Cosine-Similarity auf SQLite |
| Graphen | **QuikGraph** (NuGet → DLL) oder eigene Graph-Klasse |
| Numerik (Bio) | **MathNet.Numerics** (NuGet → DLL) |
| JSON | **Newtonsoft.Json** (Unity-Package verfügbar) |
| Physik | Unity Physics (Rigidbody, Collider, Raycasts) — DIREKT |
| Navigation | Unity NavMesh — DIREKT |
| Sensoren | Camera, Physics.Raycast, OnCollision, AudioListener — DIREKT |
| UI | Unity Canvas (Chat, Status-Overlay, Ziel-Anzeige) |
| Persistenz | JSON-Serialisierung + PlayerPrefs oder lokale Dateien |
| Config | ScriptableObjects |
| LLM-Unabhängigkeitskern | Interne Semantik-Frames + DSL-Parser + regelbasierte Antworttemplates |

---

## Projektstruktur

```
billig_agi_unity/
├── Assets/
│   ├── Scripts/
│   │   ├── Kern/
│   │   │   ├── AGIKern.cs              # 18-Schritte-Zyklus + Autonomer Modus
│   │   │   ├── LLMAdapter.cs           # HTTP → Multi-Provider (Anthropic/OpenAI-kompatibel)
│   │   │   ├── AGIApiServer.cs         # OpenAI-kompatibler HTTP-Server (Port 8741)
│   │   │   ├── SituationsBewerter.cs   # Multi-dimensionale Bewertung
│   │   │   ├── AnalogieEngine.cs       # Transfer-Lernen (Structure Mapping)
│   │   │   ├── NeugierSystem.cs        # Unsicherheitsgetriebene Exploration
│   │   │   ├── SelbstModell.cs         # Kompetenz-Karte pro Domäne
│   │   │   ├── KausalGraph.cs          # Hierarchische Kausalität (temporal)
│   │   │   ├── KonzeptRevision.cs      # Hermeneutischer Zirkel + Konzeptschöpfung
│   │   │   ├── EmotionsSystem.cs       # Funktionale Emotionen als Entscheidungsmodulation
│   │   │   ├── ZeitModell.cs           # Temporales Reasoning (Dauer, Sequenz, Deadlines)
│   │   │   ├── SemantikKernel.cs       # LLM-Unabhängigkeitskern (interne Semantik-Frames)
│   │   │   ├── SubsymbolikKernel.cs    # Latente Zustände + Embeddings + Ähnlichkeitsdynamik
│   │   │   ├── KreativitaetsEngine.cs  # Divergenz/Konvergenz für funktionale Kreativität
│   │   │   ├── RobustheitsManager.cs   # Degradationsmodi, Recovery, API-Ausfallpfade
│   │   │   ├── NarrativesSelbst.cs     # Autobiographie + Entwicklungsphasen
│   │   │   ├── ZustandsEncoder.cs     # 20D Zustandsvektor fuer RL + Clustering
│   │   │   ├── ReinforcementLerner.cs # Tabular Q-Learning (kein LLM)
│   │   │   ├── InstanzClusterer.cs    # K-Means Clustering (kein LLM)
│   │   │   ├── MikroAgent.cs          # Basis + Blackboard + AgentNetzwerk
│   │   │   ├── Mikroagenten.cs        # 7 spezialisierte Mikroagenten
│   │   │   ├── MetaKognition.cs       # Strategie-Tracking, Lernkurve, Bias-Erkennung
│   │   │   ├── ErfahrungsExporter.cs  # Erfahrungen → SFT/DPO/Reward JSONL
│   │   │   ├── FineTuningManager.cs   # Fine-Tuning-Job-Verwaltung + Modell-Versionierung
│   │   │   ├── SelbstOptimierung.cs   # Meta-Loop: Export → Fine-Tune → Evaluate → Swap/Rollback
│   │   │   ├── TransferLerner.cs      # Schema-Mining + Cross-Domain Transfer-Learning
│   │   │   ├── KonzeptBildung.cs      # Spontane Kategorienbildung aus unbenannten Clustern
│   │   │   ├── KausalesReasoning.cs   # Pearls 3-Ebenen Kausal-Leiter (Assoziation, Intervention, Kontrafaktisch)
│   │   │   ├── HypothesenEngine.cs    # Aktive Hypothesenbildung + Experimentplanung
│   │   │   ├── EWCSchutz.cs           # Elastic Weight Consolidation (Catastrophic Forgetting Schutz)
│   │   │   ├── KonzeptBaum.cs         # Hierarchische Abstraktion (Konzeptbaeume)
│   │   │   ├── MetaZielSystem.cs      # Introspektionsgetriebene autonome Zielgenerierung
│   │   │   ├── GroundingBruecke.cs    # Bidirektionales Sensory-Language Grounding
│   │   │   ├── IntuitiverPhysikSimulator.cs  # Objektpermanenz, Trajektorien, Stabilitaet
│   │   │   ├── MentaleSimulation.cs   # "Theater im Kopf" — Was-Wenn + Kontrafaktisch
│   │   │   ├── LangzeitPlaner.cs      # Hierarchische Meilenstein-Planung
│   │   │   └── SelbstCurriculum.cs    # Selbstgesteuertes Lernen + Adaptive Schwierigkeit
│   │   ├── Intentionalitaet/
│   │   │   ├── ZielManager.cs          # BDI: Beliefs-Desires-Intentions
│   │   │   ├── Planer.cs               # Hierarchical Task Network (HTN)
│   │   │   ├── Ausfuehrer.cs           # Aktionen via AGIAgent ausführen
│   │   │   └── Monitor.cs              # Fortschritt, Replanung, Überraschung
│   │   ├── Sensorik/
│   │   │   ├── SensorSuite.cs          # Camera + Raycast + Collision + Audio
│   │   │   ├── VAKOGEngine.cs          # Sensordaten → VAKOG-Profile
│   │   │   └── VAKOGLexikon.cs         # Ankerwörter, lernend aus Erfahrung
│   │   ├── Welt/
│   │   │   ├── WeltModell.cs           # Internes Modell des Weltzustands
│   │   │   ├── WeltGenerator.cs        # Prozedurale Generierung (Terrain, Objekte)
│   │   │   ├── WeltController.cs       # Dynamische Veränderung zur Laufzeit
│   │   │   ├── AGIAgent.cs             # Avatar: NavMeshAgent + Rigidbody + Interaktion
│   │   │   ├── AktionsController.cs    # Bewegen, Greifen, Ablegen, Interagieren
│   │   │   ├── KonsistenzPruefer.cs    # Weltmodell-Konsistenz, Widerspruchsreparatur
│   │   │   ├── WeltManipulator.cs     # Sprache → Weltveraenderung (LLM-Parse + Direkt)
│   │   │   └── NPCVerhalten.cs        # Einfache NPCs: Sammler, Waechter, Wanderer, Beobachter, Sozial
│   │   ├── Physik/
│   │   │   ├── PhysikEngine.cs         # Unity Physics direkt + gelernte Regeln
│   │   │   ├── RegelExtraktor.cs       # Regeln aus Beobachtungen extrahieren
│   │   │   ├── BioSimulation.cs        # Lotka-Volterra, SIR, etc. (MathNet)
│   │   │   └── BioWissen.cs            # RAG für biologisches Wissen
│   │   ├── Sozial/
│   │   │   ├── SozialEngine.cs         # Orchestrator
│   │   │   ├── Mechanismen.cs          # 42 sozialpsychologische Mechanismen
│   │   │   ├── ArchetypenEngine.cs     # Kontext → Archetyp-Zuordnung
│   │   │   ├── ArchetypenGedaechtnis.cs # Erfahrungsbasiertes Archetypen-Gedaechtnis (kein statisches Lexikon)
│   │   │   ├── Alchemie.cs             # Jung'sche Alchemie (4 Phasen)
│   │   │   └── TheoryOfMind.cs         # Mentale Modelle anderer Entitäten
│   │   ├── Gedaechtnis/
│   │   │   ├── ErfahrungsSpeicher.cs   # Episodisch + semantisch
│   │   │   ├── VektorDB.cs             # REST → ChromaDB/Qdrant oder lokal
│   │   │   ├── Konsolidierung.cs       # Gewichtung, Verallgemeinerung
│   │   │   └── LangzeitLernen.cs       # Priorisierung, Forgetting, Driftkontrolle über Wochen
│   │   ├── Bio/
│   │   │   ├── PflanzenWachstum.cs     # Visuelle Wachstums-Simulation
│   │   │   └── WetterSystem.cs         # Regen, Wind, Sonne → Sensoren
│   │   ├── UI/
│   │   │   ├── ChatUI.cs              # In-Game Chat-Fenster
│   │   │   ├── StatusOverlay.cs       # VAKOG, Physik, Sozial live
│   │   │   └── ZielAnzeige.cs         # Aktive Ziele + Plan-Fortschritt
│   │   ├── Daten/
│   │   │   └── DatenLader.cs          # JSON → C#-Objekte
│   │   ├── Evaluation/
│   │   │   └── BenchmarkRunner.cs     # KPI-Suite + Regressionstests pro Build
│   │   └── Modelle/
│   │       ├── Ziel.cs                # Pydantic-Äquivalent: [Serializable] + Records
│   │       ├── Plan.cs
│   │       ├── Aktion.cs
│   │       ├── WeltZustand.cs
│   │       ├── SensorDaten.cs
│   │       ├── AgentZustand.cs
│   │       ├── Erfahrung.cs
│   │       ├── VAKOGProfil.cs
│   │       ├── PhysikRegel.cs
│   │       ├── SozialeAnalyse.cs
│   │       ├── LLMAntwort.cs
│   │       ├── Konzept.cs              # Revidierbares Konzept mit Iterationshistorie
│   │       ├── KonzeptRevisionErgebnis.cs  # Ergebnis einer Revision
│   │       ├── EmotionalerZustand.cs  # 6 funktionale Emotionen + Modulationswerte
│   │       ├── MentalesModell.cs      # Theory of Mind: Wissen/Glauben/Wollen einer Entität
│   │       ├── ZeitlicherKontext.cs   # Dauer, Sequenz, relative Zeitpunkte
│   │       └── Autobiographie.cs      # Narratives Selbst: Kapitel + Entwicklungsphase
│   ├── Data/                           # JSON-Datendateien
│   │   ├── vakog_basis.json            # 200+ Ankerwörter
│   │   ├── sozial_regeln.json          # 42 Mechanismen
│   │   ├── archetypen.json             # 12 Jung-Archetypen
│   │   ├── bio_fakten.json             # Bio-RAG-Wissensbasis
│   │   ├── ziel_vorlagen.json          # Ziel-Templates
│   │   ├── aktions_lexikon.json        # ~30 Aktionen mit Vor-/Nachbedingungen
│   │   ├── kausal_graph.json           # Persistenter Kausalgraph
│   │   ├── konzept_revisionen.json     # Revisionshistorie aller Konzepte
│   │   ├── emotionen_config.json      # Emotionsdynamik-Parameter
│   │   ├── kreativitaets_heuristiken.json # Heuristiken/Gewichte für kreative Bewertung
│   │   ├── benchmark_szenarien.json   # Standardisierte Evaluationsfälle + Ziel-KPIs
│   │   └── autobiographie.json        # Persistentes narratives Selbst
│   ├── Config/
│   │   ├── AGIConfig.asset             # ScriptableObject: API-Keys, Schwellwerte
│   │   └── AGIConfig.cs                # ScriptableObject-Definition
│   ├── Prefabs/
│   │   ├── Natur/                      # Bäume, Steine, Wasser, Gras
│   │   ├── Gebaeude/                   # Häuser, Räume, Möbel
│   │   ├── Objekte/                    # Alltagsgegenstände
│   │   ├── NPCs/                       # Soziale Akteure für Theory of Mind
│   │   └── Agent/                      # Avatar-Prefab mit allen Components
│   ├── Materials/
│   ├── Scenes/
│   │   └── HauptWelt.unity             # Persistente Hauptszene
│   └── Plugins/                        # NuGet-DLLs
│       ├── MathNet.Numerics.dll
│       ├── QuikGraph.dll
│       └── Newtonsoft.Json.dll
├── Packages/
│   └── manifest.json                   # URP, ProBuilder, evtl. Addressables
└── ProjectSettings/
```

---

## Phase 1: Fundament (Unity-Projekt)

### Schritt 1: Unity-Projekt erstellen
- Unity 2022.3+ LTS
- URP (Universal Render Pipeline) — leichtgewichtig
- ProBuilder (prozedurale Geometrie)
- Newtonsoft.Json (via Unity Package Manager)
- NuGet-DLLs: MathNet.Numerics, QuikGraph → in Plugins/

### Schritt 2: ScriptableObject Config
`AGIConfig.cs` — ScriptableObject:
- `llmAnbieter` (Anthropic oder OpenAI-kompatibel), `llmApiKey`, `llmModel`, `llmApiUrl`
- `vektorDbUrl` (ChromaDB/Qdrant endpoint)
- `physikKonfidenzSchwelle`, `sozialKonfidenzSchwelle`
- `autonomModusTickRate` (Sekunden pro Aktion)
- `maxAutonomeSchritte` (Sicherheitslimit pro Sitzung)
- `vakog_schwellwerte`
- `konzeptRevisionNachNAnwendungen` (Default: 10 — nach wie vielen Anwendungen ein Konzept zur Revision ansteht)
- `konzeptRevisionMaxPasses` (Default: 7 — maximale Iterationstiefe pro Revision)
- `konzeptDriftSchwelle` (Default: 0.3 — ab welchem Drift eine Rückpropagation ausgelöst wird)
- `maxRückpropagationsTiefe` (Default: 3 — wie viele Ebenen abhängiger Konzepte revidiert werden)
- `emotionsDecayRate` (Default: 0.05 — wie schnell Emotionen abklingen pro Zyklus)
- `emotionsSchwelle` (Default: 0.3 — ab wann eine Emotion Entscheidungen moduliert)
- `oneShotSchwelle` (Default: 0.8 — ab welcher Überraschung sofort gelernt wird statt auf Bestätigung zu warten)
- `autobiographieKapitelLänge` (Default: 20 — nach wie vielen Erfahrungen ein neues Kapitel zusammengefasst wird)
- `tomMaxEntitäten` (Default: 10 — maximale Anzahl gleichzeitig modellierter Entitäten im Theory of Mind)
- `llmFallbackModusAktiv` (Default: true — erlaubt lokale Verarbeitung ohne LLM bei Routine-Inputs)
- `llmUnabhaengigkeitsZielquote` (Default: 0.6 — Anteil der Zyklen die ohne LLM auskommen sollen)
- `subsymbolikAktiv` (Default: true — latente Repräsentation parallel zur Symbolik)
- `subsymbolikDim` (Default: 128 — Vektordimension latenter Zustände)
- `kreativitaetNoveltySchwelle` (Default: 0.65 — ab wann eine Idee als neu gilt)
- `kreativitaetUtilitySchwelle` (Default: 0.55 — ab wann eine Idee als nützlich gilt)
- `kreativitaetABTestAktiv` (Default: true — Variantenvergleich gegen Baseline)
- `kreativitaetMaxVarianten` (Default: 5 — wie viele kreative Planvarianten pro Ziel erzeugt werden)
- `forgettingRate` (Default: 0.01 pro Tag — kontrolliertes Vergessen irrelevanter Erinnerungen)
- `konsistenzPruefIntervall` (Default: alle 10 Zyklen — Weltmodell auf Widersprüche prüfen)
- `apiRecoveryMaxSekunden` (Default: 120 — maximale Zeit im Degradationsmodus)

### Schritt 3: Datenmodelle (Modelle/)
C# [Serializable] Klassen / Records:
- `Ziel` — Name, Beschreibung, Priorität, Typ (Exploration/Experiment/Konstruktion/Verständnis/Sozial), Teilziele, Status, Erfolgsbedingung
- `Plan` — ZielId, Schritte: List<Aktion>, AktuellerSchritt, Anpassungen
- `Aktion` — Typ (Bewegen/Greifen/Ablegen/Beobachten/Interagieren/Sprechen/Warten), Parameter, ErwartetesErgebnis, TatsächlichesErgebnis
- `WeltZustand` — Objekte, Positionen, Relationen, Wetter, Tageszeit, Historie
- `SensorDaten` — KameraBild, Raycasts, Kollisionen, AudioPegel, NahbereichObjekte
- `AgentZustand` — Position, Orientierung, Inventar, Energie, AktivesZiel
- `Erfahrung` — Eingabe, Kontext, Antwort, VAKOGProfil, WeltKontext, SensorSnapshot, Aktionen, ZielKontext, Bewertung, Zeitstempel, ZeitlicherKontext, EmotionalerZustand, VerwendeteKonzepte: List<string>, OneShotGelernt: bool
- `VAKOGProfil` — V, A, K, O, G (jeweils float 0-1) + Beschreibungen
- `PhysikRegel` — Wenn, Dann, Konfidenz, Quelle, AnzahlBestätigungen
- `SozialeAnalyse` — ErkannteMechanismen, Archetyp, AktiveArchetypen: List<string>, AlchemischePhase, TransformationsImpuls, TomVorhersagen: Dictionary<string,string>, Konfidenz
- `LLMAntwort` — Text, TokensUsed, Kosten, Dauer
- `Konzept` — Id, Name, Typ (Archetyp/Mechanismus/PhysikKategorie/VAKOGBedeutung/KausalBegriff), AktuelleDefinition, Ursprungsdefinition, Revisionshistorie: List<KonzeptRevisionSchritt>, AnzahlAnwendungen, LetztePrüfung, DriftScore (0-1)
- `KonzeptRevisionSchritt` — Pass-Nummer, VorherigeDefinition, NeueDefinition, Auslöser (welche Erfahrung), Evidenz (welche Textstellen/Beobachtungen), Zeitstempel
- `KonzeptRevisionErgebnis` — KonzeptId, AlteDefinition, NeueDefinition, DriftKlassifikation (BESTÄTIGT/VERSCHOBEN/WIDERSPROCHEN/ERWEITERT/ABGELEITET/UMSTRITTEN), BetroffeneErfahrungen: List<string>, RückpropagationNötig: bool
- `EmotionalerZustand` — Angst (float 0-1), Neugier (float 0-1), Frustration (float 0-1), Zufriedenheit (float 0-1), Überraschung (float 0-1), Vertrauen (Dictionary<string, float> pro Domäne), GesamtValenz (float -1 bis +1)
- `MentalesModell` — EntitätId, Name, Wissen: List<string> (was weiß sie?), Glauben: List<string> (was glaubt sie, auch Falsches?), Ziele: List<string> (was will sie?), Erwartungen: List<string>, LetzteAktualisierung, Konfidenz
- `ZeitlicherKontext` — Zeitstempel, Dauer, Sequenz (was vorher, was nachher), RelativeZeit ("kurz nachdem...", "lange vor..."), Deadline (optional)
- `Autobiographie` — Kapitel: List<AutobiographieKapitel>, AktuellePhase (Nigredo/Albedo/Citrinitas/Rubedo), Identitätsaussagen: List<string>, EntwicklungsVerlauf
- `AutobiographieKapitel` — Nummer, Titel, Zusammenfassung, SchlüsselErfahrungen: List<string>, GelernteKonzepte: List<string>, EmotionalerGrundton, Zeitraum
- `SemantikFrame` — IntentTyp, Slots (Dictionary<string,string>), KontextBezüge, Konfidenz, KannOhneLLM
- `KreativIdee` — Id, Beschreibung, Quelle (Analogie/Mutation/Kombination), NoveltyScore, UtilityScore, PlausibilitaetScore, Status (VORGESCHLAGEN/GETESTET/VERWORFEN/UEBERNOMMEN)
- `LatenterZustand` — KontextId, Embedding (float[]), Herkunft (Sensorik/Text/Aktion), Zeitstempel, Drift
- `KonsistenzFehler` — Id, Typ (LOGISCH/TEMPORAL/RAEUMLICH), BetroffeneEntitaeten, Schweregrad, AutoRepariert, Ursache
- `BenchmarkErgebnis` — SzenarioId, Erfolgsquote, ZeitBisZiel, LLMCalls, LokalQuote, KreativScore, StabilitaetScore

### Schritt 4: JSON-Datendateien anlegen
Alle 12 JSON-Dateien mit Initialinhalt. NEU: `konzept_revisionen.json`, `emotionen_config.json`, `autobiographie.json`, `kreativitaets_heuristiken.json`, `benchmark_szenarien.json` — starten leer bzw. mit Defaults, werden zur Laufzeit gefüllt.

**Verifikation Phase 1:** Unity startet, Config-ScriptableObject im Inspector editierbar, Datenmodelle kompilieren, JSON-Dateien ladbar.

---

## Phase 2: Schicht 1 — LLM-Adapter

### Schritt 5: Kern/LLMAdapter.cs
- `async Task<LLMAntwort> Analysiere(string prompt, string systemPrompt)`
- `async Task<LLMAntwort> PlaneAktionen(Ziel ziel, WeltZustand welt)`
- `async Task<LLMAntwort> InterpretiereSensordaten(SensorDaten daten)`
- `async Task<LLMAntwort> FormulierZiel(string wissenslücke, string kontext)`
- `async Task<LLMAntwort> BewertZielerreichung(Ziel ziel, WeltZustand zustand)`
- Intern: `HttpClient` → `https://api.anthropic.com/v1/messages`
- Response-Caching (Dictionary<string, LLMAntwort> mit TTL)
- Token-Zählung, Kosten-Tracking
- Retry-Logik mit exponential backoff
- **Gesamtkostenzähler** pro Session

### Schritt 5b: Kern/SemantikKernel.cs (Punkt 1, maximal machbar)
LLM-Unabhängigkeitskern als Ersatz für ein eigenes Foundation Model (ohne eigenes Training):
- `SemantikFrame Parse(string input, WeltZustand welt, AgentZustand agent)`:
  - Intent erkennen (Frage, Befehl, Zielanfrage, Statusanfrage, Revision, Kreativauftrag)
  - Slots extrahieren (Objekt, Ort, Zeit, Entität)
  - Kontextbezüge auf interne Konzepte/Weltobjekte mappen
- `bool KannLokalBearbeiten(SemantikFrame frame)`:
  - true für Routineklassen: `/stats`, `/ziele`, einfache Weltfragen, bekannte Ziel-Templates
- `Antwort LokalAntwort(SemantikFrame frame)`:
  - Template-basierte, faktengebundene Antwort aus WeltModell, Gedächtnis, SelbstModell
- `Prompt ErzeugeLLMPrompt(SemantikFrame frame)`:
  - Nur wenn nicht lokal lösbar: strukturierten Prompt aus interner Semantik bauen
- `LLMUnabhaengigkeitsMetrik BerechneQuote()`:
  - Anteil lokaler Antworten pro Session, Ziel >= llmUnabhaengigkeitsZielquote (0.6)
- `Antwort LokaleDegradation(SemantikFrame frame)`:
  - Bei API-Ausfall nur sichere Kernfunktionen (Status, Plan, Weltabfrage, Notfallantworten)
- Fallback-Modus: Bei API-Ausfall bleibt Kernfunktionalität für Routineziele erhalten

### Schritt 5d: Kern/RobustheitsManager.cs
- Überwacht API-Zustand, Antwortlatenz, Fehlerrate, Tokenbudget
- Degradationsstufen:
  - Stufe 0 Normalbetrieb
  - Stufe 1 Sparmodus (weniger LLM-Calls, kürzere Prompts)
  - Stufe 2 Lokalmodus (SemantikKernel + Regeln)
  - Stufe 3 Recovery (periodische Reconnect-Versuche)
- `Betriebsmodus BestimmeModus(SystemMetriken m)`
- `void RecoveryTick()` bis API wieder stabil
- Notbremse wenn kritische Instabilität > apiRecoveryMaxSekunden

### Schritt 5c: Kern/KreativitaetsEngine.cs (Punkt 5, funktionale Kreativität)
- `List<KreativIdee> GeneriereIdeen(Ziel ziel, WeltZustand welt, List<Erfahrung> erfahrungen)`:
  - Divergenz: Analogie-Transfer, Konzept-Kombination, Plan-Mutation, Perspektivwechsel
- `KreativIdee Bewerte(KreativIdee idee)`:
  - Scores: Novelty, Utility, Plausibilitaet, Sicherheitskonformität
- `List<KreativIdee> SelektiereTopK(List<KreativIdee> ideen, int k)`
- `ABErgebnis VergleicheMitBaseline(Plan baseline, List<Plan> kreativVarianten)`:
  - KPI: Erfolgsrate, Zeit bis Ziel, Nebenwirkungen, LLM-Verbrauch
- `Plan ErzeugeKreativPlan(KreativIdee idee)`
- `void LerneAusKreativErgebnis(KreativIdee idee, bool erfolgreich)`
- Verbindung zu `Planer.PlaneKreativ(...)` und `KonzeptRevision` (neue Konzepte bei stabiler Innovation)

**Verifikation:**
- "Hallo" → Antwort kommt, Kosten werden gezählt.
- API aus: `/stats` und bekannte Weltfragen funktionieren lokal weiter.
- Kreativmodus: Für ein Ziel werden mehrere Varianten erzeugt und nach Novelty+Utility ausgewählt.

---

## Phase 3: Schicht 2 — VAKOG Sensorik

### Schritt 6: Sensorik/SensorSuite.cs (MonoBehaviour auf Agent)
Am Agent-GameObject:
- `Camera` Component → `RenderTexture` (84×84) → Helligkeit, dominante Farbe, Bewegungserkennung (Frame-Differenz)
- `Physics.SphereCast` / `RayPerceptionSensor` → Objekte in Reichweite (Typ, Distanz, Richtung)
- `OnCollisionEnter/Stay/Exit` → Berührungen (Kraft, Material)
- `AudioListener` → Lautstärke (AudioSource.volume Sampling)
- Zusammengefasst als `SensorDaten` Struct, jedes Frame aktualisiert
- Konfigurierbare Sensor-Frequenz (nicht jedes Frame nötig)

### Schritt 7: Data/vakog_basis.json
200+ Ankerwörter mit VAKOG-Profilen. Kategorien:
- Natur (Gewitter, Regen, Sonnenaufgang, Wald, Meer, Feuer, Schnee, Wind, Vulkan, Wasserfall...)
- Emotionen (Angst, Freude, Trauer, Wut, Überraschung, Ekel, Scham, Stolz, Sehnsucht, Ehrfurcht...)
- Materialien (Holz, Metall, Glas, Wasser, Stein, Sand, Eis, Seide, Beton, Lehm...)
- Handlungen (Laufen, Fallen, Schwimmen, Kochen, Schneiden, Hämmern, Streicheln, Schlagen...)
- Orte (Bibliothek, Werkstatt, Küche, Strand, Höhle, Marktplatz, Friedhof, Labor...)
- Zustände (Hitze, Kälte, Dunkelheit, Stille, Chaos, Harmonie, Spannung, Erschöpfung...)
- Soziales (Umarmung, Streit, Flüstern, Applaus, Einsamkeit, Menschenmenge...)

### Schritt 8: Sensorik/VAKOGLexikon.cs
- Lädt vakog_basis.json als Dictionary<string, VAKOGProfil>
- `GetProfil(string wort) -> VAKOGProfil` — Lookup, bei Miss → LLM schätzen
- `AktualisiereAusErfahrung(string wort, SensorDaten daten)` — Unity-Erfahrung überschreibt Basis
- Priorisierung: Unity-Erfahrung > LLM-Schätzung > Basis-JSON
- Persistenz: Gelernte Profile speichern

### Schritt 9: Sensorik/VAKOGEngine.cs
- `AnalysiereText(string text) -> VAKOGProfil` — Textbasiert (für Chat-Eingaben)
- `AnalysiereSensorisch(SensorDaten daten) -> VAKOGProfil` — Direkt aus Unity-Sensoren:
  - V ← Kamera-Helligkeit + Farbvariation + Bewegung
  - A ← AudioListener Pegel + Frequenzspektrum
  - K ← Kollisionskraft + Geschwindigkeit + Vibration
  - O ← Nähe zu Partikel-Emittern (Rauch, Dampf, Blumen)
  - G ← Interaktion mit Nahrungsobjekten (Tag-basiert)
- Dual-Modus: Text-Analyse für sprachliche Eingaben, Sensor-Analyse für Welt-Wahrnehmung

**Verifikation:** Agent steht im Regen → V:hoch, A:mittel, K:mittel, O:hoch, G:niedrig. "Bibliothek" als Text → V:niedrig, A:niedrig, K:niedrig.

---

## Phase 4: Schicht 3 — Unity-3D-Welt

### Schritt 10: Welt/AGIAgent.cs (MonoBehaviour)
- `NavMeshAgent` für Pathfinding
- `Rigidbody` für Physik-Interaktion
- `Animator` (optional) für visuelle Aktionen
- Inventar: `List<GameObject>` (getragene Objekte)
- Energie-System (simpel: float 0-1, regeneriert, sinkt bei Aktionen)
- Zustand als `AgentZustand` exportierbar

### Schritt 11: Welt/AktionsController.cs (MonoBehaviour auf Agent)
- `async Task<AktionsErgebnis> Bewegen(Vector3 ziel)` — NavMeshAgent.SetDestination, wartet auf Ankunft
- `AktionsErgebnis Greifen(GameObject objekt)` — Objekt an Handpunkt parenten, in Inventar
- `AktionsErgebnis Ablegen(Vector3 position)` — Objekt unparenten, fallen lassen
- `AktionsErgebnis Interagieren(GameObject objekt)` — Kontextuell (Tür→öffnen, Schalter→betätigen)
- `AktionsErgebnis Beobachten(Vector3 richtung)` — Kamera fokussieren, SensorSuite-Snapshot
- `Coroutine Warten(float sekunden)` — Nichts tun, weiter beobachten
- `void Sprechen(string text)` — Sprechblase (World-Space Canvas)
- Jede Aktion gibt `AktionsErgebnis` zurück (Erfolg, Sensordaten nachher, Zustandsänderung)

### Schritt 12: Welt/WeltGenerator.cs (MonoBehaviour)
Prozedurale Weltgenerierung:
- `void GeneriereWelt(WeltBeschreibung beschreibung)` — Hauptmethode
- Terrain: Perlin Noise → Mesh (oder Unity Terrain) mit Hügeln, Ebenen, Wasser
- Vegetation: Prefab-Instanziierung nach Biom-Regeln (Wald→Bäume, Wiese→Gras)
- Strukturen: ProBuilder-Runtime oder Prefab-Kombination (Wände, Böden, Dächer)
- Objekte: Aus Prefab-Bibliothek (Tag-System: "greifbar", "interagierbar", "nahrung", etc.)
- `void ErstelleSzenario(string name)` — Vordefiniert: "Raum mit Tisch", "Garten", "Teich"
- NavMesh-Baking zur Laufzeit (NavMeshSurface.BuildNavMesh())

### Schritt 13: Welt/WeltController.cs (MonoBehaviour)
Dynamische Weltveränderung:
- `GameObject SpawnObjekt(string prefabName, Vector3 pos, Quaternion rot)`
- `void EntferneObjekt(GameObject obj)`
- `void BewegeObjekt(GameObject obj, Vector3 neuPos)`
- `void SetzeWetter(WetterTyp typ, float intensität)` — steuert WetterSystem
- `void SetzeTageszeit(float stunde)` — steuert Directional Light
- `void AuslösePhysikEvent(string eventTyp, Vector3 position)` — Explosion, Wasserfluss etc.
- Alle Änderungen → WeltModell synchronisieren

### Schritt 14: Welt/WeltModell.cs
Internes Modell des Weltzustands (Daten-Klasse, kein MonoBehaviour):
- `Dictionary<string, WeltObjekt>` — ID → Objekt (Position, Typ, Zustand, Tags)
- Relationen: "auf", "neben", "in", "über" (automatisch aus Physics-Queries)
- Aktuelles Wetter, Tageszeit
- Zustandshistorie (Queue<WeltÄnderung>, letzte N Änderungen)
- `float ErwartungVsRealität(WeltZustand erwartet, WeltZustand tatsächlich)` — Überraschungs-Metrik
- `List<WeltObjekt> ObjekteInReichweite(Vector3 pos, float radius)`
- `bool Navigierbar(Vector3 von, Vector3 nach)` — NavMesh.CalculatePath
- Persistenz: JSON-Export/Import, überlebt Session-Restart

### Schritt 14b: Welt/KonsistenzPruefer.cs
- `List<KonsistenzFehler> Pruefe(WeltModell welt)`:
  - LOGISCH: widersprüchliche Zustände (Objekt gleichzeitig offen/geschlossen)
  - TEMPORAL: Wirkung vor Ursache, inkonsistente Zeitreihen
  - RAEUMLICH: Objekt in zwei Positionen gleichzeitig, unmögliche Relationen
- `void AutoRepariere(KonsistenzFehler fehler)` — sichere Reparaturregeln anwenden
- `void MarkiereZurKlaerung(KonsistenzFehler fehler)` — wenn Auto-Reparatur unsicher
- Läuft alle `konsistenzPruefIntervall` Zyklen und speist Korrekturen zurück ins WeltModell

### Schritt 15: Bio/WetterSystem.cs (MonoBehaviour)
- ParticleSystem für Regen, Schnee
- WindZone für Windeffekte (Kraft auf Rigidbodies, Bäume bewegen)
- Fog für Nebel
- Directional Light Rotation für Tageszeit
- Beeinflusst direkt alle Sensoren: V (Sichtweite), A (Regengeräusch), K (Windkraft), O (Regengeruch)

### Schritt 16: Bio/PflanzenWachstum.cs (MonoBehaviour)
- Scale-Animation über Zeit (gesteuert von BioSimulation-Parametern)
- Reaktion auf Wetter (Wasser + Licht → Wachstum)
- Prefab-Swap bei Wachstumsphasen (Setzling → Pflanze → Baum)
- Agent kann Wachstum beobachten und als Erfahrung speichern

**Verifikation Phase 4:** Agent bewegt sich via NavMesh, greift Objekt, legt es ab. Welt wird prozedural generiert. Wetter ändert sich. Pflanzen wachsen.

---

## Phase 5: Physik (nutzt Unity direkt)

### Schritt 17: Physik/PhysikEngine.cs
- `PlausibilitätsErgebnis PrüfePlausibilität(string aussage)`:
  1. Gelernte Regeln prüfen (Dictionary-Lookup, schnell)
  2. Wenn keine Regel: LLM-Einschätzung + Konfidenz
  3. Wenn Konfidenz zu niedrig: **Experiment vorschlagen** → Intentionalitäts-Schicht
- `async Task<ExperimentErgebnis> FühreExperimentAus(string hypothese)`:
  - Szenario in Unity aufbauen (WeltController)
  - Agent führt Handlungssequenz aus (AktionsController)
  - Beobachtung auswerten (SensorSuite)
  - Regel extrahieren (RegelExtraktor)
- Bio-Zweig: BioSimulation + BioWissen (RAG) + PflanzenWachstum (Unity-Visualisierung)

### Schritt 18: Physik/RegelExtraktor.cs
- `PhysikRegel ExtrahiereRegel(ExperimentErgebnis ergebnis)`
- Regeln aus Unity-Beobachtungen: "Objekt mit Tag 'holz' + Kontakt mit Tag 'wasser' → schwimmt (bleibt über Wasser-Y-Level)"
- Konfidenz steigt mit Bestätigungsanzahl
- Regeln persistent in kausal_graph.json

### Schritt 19-20: Physik/BioSimulation.cs + BioWissen.cs
- **BioSimulation**: MathNet.Numerics für Lotka-Volterra, SIR-Modell, logistisches Wachstum, Homöostase, Mendel-Genetik
- **BioWissen**: RAG — bio_fakten.json durchsuchen, bei Miss → LLM fragen → Ergebnis cachen
- Ergebnisse → PflanzenWachstum.cs visualisiert in Unity

**Verifikation:** "Fällt ein Stein nach oben?" → Agent lässt Stein los in Unity → fällt → "Nein" + Regel gelernt.

---

## Phase 6: Schicht 4 — Soziale Kognition

### Schritt 21: Data/sozial_regeln.json
42 sozialpsychologische Mechanismen (gruppiert):
- **Konformität** (8): Social Proof, Bandwagon, Groupthink, Conformity Bias, Pluralistic Ignorance, Spiral of Silence, Normative Influence, Informational Influence
- **Autorität** (6): Authority Bias, Milgram-Effekt, Halo-Effekt, Expert Bias, Uniformed Authority, Appeal to Authority
- **Kognitive Verzerrungen** (10): Confirmation Bias, Dunning-Kruger, Anchoring, Availability Heuristic, Framing, Sunk Cost, Hindsight Bias, Fundamental Attribution Error, Just-World, Spotlight Effect
- **Gruppen** (8): In-Group Bias, Out-Group Homogeneity, Scapegoating, Social Loafing, Diffusion of Responsibility, Deindividuation, Polarization, Risky Shift
- **Überzeugung** (6): Reciprocity, Commitment/Consistency, Scarcity, Door-in-the-Face, Foot-in-the-Door, Social Comparison
- **Emotion** (4): Emotional Contagion, Empathy Gap, Affect Heuristic, Moral Disengagement

### Schritt 22: Sozial/Mechanismen.cs
- `List<ErkannterMechanismus> Erkenne(string text, SozialeAnalyse kontext)` — Welche Mechanismen sind aktiv?
- Jeder Mechanismus: Name, Beschreibung, Erkennungsmuster, Gegenmaßnahme, Beispiele

### Schritt 23: Data/archetypen.json
12 Jung-Archetypen als **Seed-Daten (Ausgangshypothesen)**: Held, Mentor, Schatten, Trickster, Mutter, Kind, Anima/Animus, Selbst, Persona, Alter Weiser, Unschuldiger, Herrscher
- Jeweils: Licht-Aspekt, Schatten-Aspekt, Motivation, Angst, Stärke, Schwäche, Gegenarchetyp, **Prototypische Verhaltensweisen** (statt starre Erkennungsmuster)
- Diese Definitionen sind HYPOTHESEN, nicht Wahrheiten — sie werden durch Erfahrung revidiert

### Schritt 24: Sozial/ArchetypenLexikon.cs → **ArchetypenGedaechtnis** (Episodisch)
Kein Lexikon. Kein Dictionary mit einer globalen Definition pro Archetyp.
Stattdessen: Echtes episodisches Gedaechtnis.

**Datenstruktur:**
- `List<ArchetypInstanz>` — Alle konkreten Episoden in denen sich Archetypen zeigten
- `Dictionary<string, ArchetypCluster>` — Kontextspezifische Cluster (Key: `"name::kontext"`)
- `List<Archetyp>` — Seed-Hypothesen aus archetypen.json (unveraenderliche Referenz)

**Episodische Instanzen (ArchetypInstanz):**
Jede Erkennung = eine Instanz mit: Situation, Verhalten, Interpretation, Aspekt (licht/schatten), Entitaet, Kontextcluster (physik/sozial/existenziell/epistemisch/allgemein), Konfidenz, Kontextmerkmale.
Die "Bedeutung" eines Archetyps ergibt sich aus seinen Instanzen — NICHT aus einer globalen Definition.

**Kontextabhaengige Cluster (ArchetypCluster):**
"Held" in "physik" kann etwas anderes bedeuten als "Held" in "sozial".
Cluster gruppieren Instanzen nach Kontext und entwickeln eigene konvergierte Interpretationen per hermeneutischem Zirkel (via KonzeptRevision).

**Persistenz:**
- `archetypen_instanzen.json` — Alle episodischen Instanzen
- `archetypen_cluster.json` — Konvergierte Cluster-Interpretationen
- Beim Neustart: Volles Gedaechtnis mit kontextspezifischen Bedeutungen

**API:**
- `SpeichereInstanz(...)` → Erzeugt episodische Instanz, ordnet sie Cluster zu
- `InstanzenImKontext(name, kontext)` → Instanzen eines Archetyps in bestimmtem Kontext
- `GetKontextBedeutung(name, kontext)` → Konvergierte Bedeutung in bestimmtem Kontext
- `GetBesteBeschreibung(name, kontext)` → Bestes verfuegbares Verstaendnis (Kontext > Instanzen > Hypothese)
- `AktualisiereCluster(...)` → Von KonzeptRevision aufgerufen nach hermeneutischem Zirkel
- `RevisionskandidatenHolen(min)` → Cluster mit genug Instanzen aber ohne Interpretation

### Schritt 25: Sozial/ArchetypenEngine.cs — **Instanzbasierte Erkennung**
Erkennung erzeugt episodische Instanzen, nicht Dictionary-Updates.

- `ErkenneArchetyp(situation, entitaetId, kontext, kontextCluster)`:
  - Prompt enthaelt vergangene Instanzen als Erfahrungskontext (nicht nur Definitionen)
  - Konvergierte Bedeutungen im aktuellen Kontext fliessen ein
  - Seed-Hypothesen als Orientierung (nicht als Wahrheit)
  - Ergebnis: Neue `ArchetypInstanz` im Gedaechtnis gespeichert
  - Automatische Kontextcluster-Bestimmung (physik/sozial/existenziell/epistemisch/allgemein)
- `ErkenneAlleArchetypen(situation, kontext, kontextCluster)`:
  - Multi-Erkennung, erzeugt eine Instanz pro erkanntem Muster
- **Integration mit KonzeptRevision**:
  - Jede Erkennung zaehlt als Anwendung → nach N Anwendungen hermeneutischer Zirkel
  - Cluster-Interpretation = Ergebnis der iterativen Konvergenz
  - Emergente Archetypen: Neuer Seed + erste Instanz gespeichert
- **Spannungsanalyse**: Nutzt `GetBesteBeschreibung` (kontextabhaengig)

### Schritt 26: Sozial/Alchemie.cs
Jung'sche Alchemie — 4 Phasen als Transformationsmodell:
1. **Nigredo** (Schwärzung) — Krise, Zerfall alter Strukturen, Konfrontation mit Schatten
2. **Albedo** (Weißung) — Reflexion, Reinigung, Unterscheidung, Klärung
3. **Citrinitas** (Gelbung) — Erwachen, neue Einsicht, Morgenröte
4. **Rubedo** (Rötung) — Integration, Vollendung, vereinte Gegensätze

Prinzipien: Solve et Coagula, Coniunctio, Prima Materia, Lapis Philosophorum
- `AlchemischePhase ErkennePhase(string situation, List<Erfahrung> verlauf)`
- `string TransformationsImpuls(AlchemischePhase phase, string kontext)` — Was braucht die Situation?

### Schritt 27: Sozial/SozialEngine.cs
Orchestrator: Mechanismen + Archetypen + Alchemie + Theory of Mind → SozialeAnalyse

### Schritt 28: Sozial/TheoryOfMind.cs — Mentale Modelle anderer Entitäten

Für jede beobachtete Entität (NPC, Nutzer) baut das System ein **mentales Modell**:

Klasse `TheoryOfMind`:
- `Dictionary<string, MentalesModell> modelle` — Pro Entität ein Modell
- `MentalesModell ErstelleModell(string entitätId, string name)` — Neues Modell für neu beobachtete Entität
- `void AktualisiereMitBeobachtung(string entitätId, string beobachtung, SensorDaten daten)`:
  - Was hat die Entität GESEHEN? (Sichtlinie berechnen: Raycast von Entitäts-Position)
  - Was hat die Entität GEHÖRT? (Entfernung + Lautstärke)
  - Was hat die Entität GETAN? (Aktionsbeobachtung → daraus Ziele ableiten)
- `List<string> WasWeißSie(string entitätId)` — Aus Beobachtung abgeleitet
- `List<string> WasGlaubtSie(string entitätId)` — Kann von der Realität abweichen (False Belief)!
- `List<string> WasWillSie(string entitätId)` — Aus beobachtetem Verhalten ableiten (LLM)
- `string VorhersageVerhalten(string entitätId, string situation)` — "Was wird sie wahrscheinlich tun?"
- `bool FalseBeliefErkannt(string entitätId, string thema)` — "Sie weiß NICHT dass..."

**False Belief ist der Kern**: Die AGI muss verstehen dass andere Entitäten Dinge NICHT wissen, die sie selbst weiß. Beispiel:
```
NPC hat nicht gesehen dass Agent den Stein hinter die Tür gelegt hat.
→ MentalesModell(NPC).Wissen enthält NICHT "Stein ist hinter Tür"
→ Agent kann vorhersagen: NPC wird den Stein dort suchen wo er ihn zuletzt sah
→ Das ist Theory of Mind Level 1 (False Belief Task)
```

**NPC-System in Unity**: `Welt/NPCVerhalten.cs` (MonoBehaviour):
- 5 Rollen: **Sammler** (sucht + sammelt Objekte), **Wächter** (patroulliert Gebiet), **Wanderer** (erkundet zufällig), **Beobachter** (steht + beobachtet andere), **Sozial** (nähert sich anderen Entitäten)
- NavMeshAgent für Bewegung
- **Beobachtbare Aktionshistorie**: Jede NPC-Aktion (`typ`, `beschreibung`, `position`, `zeitstempel`, `sichtbareObjekte`) wird protokolliert — die AGI kann diese lesen
- **Sichtlinien-System**: NPCs haben begrenzte Sichtweite (`sichtweite`), per Raycast geprüft — nur was sie tatsächlich sehen können, geht in ihr beobachtbares Verhalten ein
- KEIN eigenes LLM — deterministische Verhaltensmuster reichen
- Theory of Mind baut mentale Modelle aus den beobachteten NPC-Aktionen auf

**Unterliegt KonzeptRevision**: Das Modell "was andere typischerweise wollen" ist ein Konzept das durch Erfahrung revidiert wird.

**Verifikation:** "Alle machen es so, also ist es richtig" → Social Proof erkannt. "Der Manager opfert sich für sein Team" → Held-Archetyp. Situation im Wandel → Alchemische Phase erkannt. Agent versteht: NPC weiß nicht was hinter der Tür ist (False Belief).

---

## Phase 7: Schicht 5 — Erfahrungsgedächtnis (mit One-Shot + Zeitkontext)

### Schritt 29: Gedaechtnis/VektorDB.cs
- REST-Client → ChromaDB oder Qdrant (extern laufend)
- Alternativ: Lokale Cosine-Similarity auf Embeddings (LLM-generiert) in SQLite
- 3 Collections: erfahrungen, bio_wissen, vakog_cache
- `async Task Speichere(string id, float[] embedding, Dictionary<string, object> metadata)`
- `async Task<List<SuchErgebnis>> Suche(float[] queryEmbedding, int topK, Dictionary<string, object> filter)`

### Schritt 30: Gedaechtnis/ErfahrungsSpeicher.cs
- `async Task Speichere(Erfahrung erfahrung)` — Vektor-Embedding + Metadata
- `async Task<List<Erfahrung>> FindeÄhnliche(string query, int topK)` — Semantische Suche
- `async Task<List<Erfahrung>> FindeZeitlich(string vorher, string nachher)` — Temporal: was passierte zwischen zwei Zeitpunkten?
- Erfahrungen enthalten jetzt:
  - WeltKontext: Wo war der Agent? Was war der Weltzustand?
  - SensorSnapshot: Was hat er gesehen/gehört/gefühlt?
  - Aktionen: Was hat er getan?
  - ZielKontext: Welches Ziel verfolgte er?
  - EmotionalerZustand: Wie hat sich der Agent dabei "gefühlt"?
  - ZeitlicherKontext: Wann, wie lange, was davor, was danach?
  - VerwendeteKonzepte: Welche Konzepte waren bei der Analyse aktiv?
  - Erfahrungen sind an ORTE, HANDLUNGEN, ZIELE, EMOTIONEN und ZEIT gebunden
- **One-Shot-Lernen**:
  - Wenn `EmotionalerZustand.Überraschung > oneShotSchwelle` UND `VAKOGProfil.Gesamtintensität > 0.7`:
    → Sofort als Regel speichern mit hoher Konfidenz (kein Warten auf Bestätigung)
  - Wie "einmal auf die heiße Herdplatte fassen" — eine dramatische Erfahrung reicht
  - `bool IstOneShotWürdig(Erfahrung erfahrung)` — Prüfung
  - `void SpeichereOneShot(Erfahrung erfahrung)` — Direkt in Regelbasis + Erfahrungsspeicher

### Schritt 31: Gedaechtnis/Konsolidierung.cs
- `void Konsolidiere()` — Periodisch: Ähnliche Erfahrungen zusammenfassen, Konfidenz anpassen
- Verallgemeinerung: 3x "Holz schwimmt" bei verschiedenem Holz → "Alle Hölzer schwimmen" (höhere Konfidenz)
- Vergessen: Alte, nie abgerufene Erfahrungen werden abgewertet (nicht gelöscht)
- Widerspruchserkennung: Zwei Erfahrungen widersprechen sich → Markieren, Experiment vorschlagen

### Schritt 31b: Gedaechtnis/LangzeitLernen.cs
- `void PriorisiereErfahrungen()` — Relevanz nach Nützlichkeit, Neuheit, Zielbeitrag
- `void KontrolliertesVergessen()` — forgettingRate anwenden, aber sicherheitsrelevantes Wissen schützen
- `void DriftMonitor()` — erkennt schleichende Fehlanpassung über Tage/Wochen
- `void StabilisiereWissenskern()` — häufig bestätigte Regeln gegen Überschreiben absichern
- Ziel: Langzeitbetrieb ohne Memory-Überflutung und ohne Qualitätsabfall

**Verifikation:** Erfahrung speichern → ähnliche finden → Konsolidierung reduziert Duplikate, erhöht Konfidenz. Nach Langzeitsimulation bleibt Erfolgsquote stabil trotz wachsendem Gedächtnis.

---

## Phase 8: Schicht 6 — Meta-Kognition (erweitert)

### Schritt 32: Kern/AnalogieEngine.cs
Structure Mapping Theory:
- `List<Analogie> SucheAnalogien(string konzept, List<Erfahrung> erfahrungen)`
- Struktur-Vergleich: "Wasser fließt bergab" ↔ "Sand fließt bergab" → "Schüttgut fließt bergab"
- Nutzt jetzt räumliche Unity-Erfahrungen: Beobachtungen an verschiedenen Orten vergleichen
- Transfer-Hypothesen generieren → Intentionalitäts-Schicht kann sie als Experiment-Ziel aufnehmen

### Schritt 33: Kern/NeugierSystem.cs
- `List<Hypothese> GeneriereHypothesen(WeltZustand welt, SelbstModell selbst, KausalGraph kausal)`
- Treiber: Wissenslücken im SelbstModell, niedrige Konfidenz in Regeln, unerforschte Weltbereiche
- **Kann jetzt Ziele generieren**: "Ich weiß nicht ob Metall schwimmt → ZIEL: Metallblock ins Wasser werfen"
- `float Unsicherheit(string domäne)` — Wie unsicher ist das System in einer Domäne?
- `List<string> UnerforschteBereiche(WeltModell welt)` — Wo war der Agent noch nicht?

### Schritt 34: Kern/SelbstModell.cs
- `Dictionary<string, float>` Kompetenzen pro Domäne (Physik: 0.6, Sozial: 0.3, Navigation: 0.8...)
- `void AktualisiereKompetenz(string domäne, bool erfolg)` — Nach jeder Aktion
- Trackt auch **motorische** Kompetenz: "Greifen: 0.9, Werfen: 0.4, Navigation: 0.8"
- `string KommuniziereKompetenz(string domäne)` — "Darin bin ich noch unsicher" / "Das kann ich gut"
- `bool KannIchDas(Ziel ziel)` — Selbsteinschätzung ob Ziel erreichbar

### Schritt 35: Kern/KausalGraph.cs (temporal erweitert)
- QuikGraph-basiert (oder eigene Implementierung)
- Multi-Level: Beobachtung → Mechanismus → Prinzip (z.B. "Stein fällt" → "Gravitation" → "Massen ziehen sich an")
- **NEU: Temporale Dimension**: Jede Kausalität hat einen Zeitbezug
- `void FügeKausalitätHinzu(string ursache, string wirkung, float konfidenz, string ebene)`
- `void FügeTemporaleKausalitätHinzu(string ursache, string wirkung, float typischeDauer)` — "Pflanze→Wachstum dauert ~20 Zyklen"
- `List<string> WarumKette(string ursache, string wirkung)` — Kausalkette zurückverfolgen
- `List<string> WasPassiertWenn(string ursache)` — Vorhersage
- Persistent in kausal_graph.json

### Schritt 35b: Kern/SubsymbolikKernel.cs
- `LatenterZustand EmbeddeKontext(Erfahrung e)` — Erfahrung in dichten Vektorraum projizieren
- `List<LatenterZustand> Aehnlichste(float[] query, int k)` — subsymbolische Nachbarsuche
- `void FusionSymbolischSubsymbolisch()` — Regeln/Konzepte mit latenten Clustern abgleichen
- `bool ErkenneVerdecktesMuster()` — Muster die symbolisch noch keinen Namen haben
- Ergebnis: robustere Generalisierung bei ähnlichen, aber nicht identischen Situationen

**Verifikation:** Analogie Wasser→Sand gefunden. Neugier generiert Hypothese. Selbstmodell zeigt Kompetenz. Kausalkette "Holz schwimmt" → "Dichte < Wasser" → "Auftrieb". SubsymbolikKernel erkennt ähnliche Fallgruppen trotz unterschiedlicher Wortwahl.

### Schritt 36: Kern/KonzeptRevision.cs — Der Hermeneutische Zirkel + Konzeptschöpfung

Dies ist das Kernprinzip aus dem Konvergenz-Prompt, verallgemeinert auf ALLE Wissensstrukturen der AGI.

**Grundidee**: Jedes Konzept (Archetyp, sozialer Mechanismus, Physik-Kategorie, VAKOG-Bedeutung, Kausal-Begriff) hat nicht nur einen aktuellen WERT, sondern eine BEDEUTUNG — und diese Bedeutung kann sich durch Erfahrung verschieben. Nicht nur "Holz schwimmt" (Fakt), sondern "Was BEDEUTET 'schwimmen' für dieses System?" (Konzept).

Klasse `KonzeptRevision`:
- `__init__(llm_adapter, erfahrungs_speicher, konfiguration)`

**Was als Konzept registriert wird** (automatisch beim Laden):
- Alle 12 Archetypen aus archetypen.json → Typ: Archetyp
- Alle 42 sozialen Mechanismen aus sozial_regeln.json → Typ: Mechanismus
- Alle 4 alchemischen Phasen → Typ: AlchemischePhase
- Physik-Kategorien (Schwerkraft, Auftrieb, Reibung...) → Typ: PhysikKategorie
- VAKOG-Kanal-Bedeutungen (was bedeutet "visuell" für dieses System?) → Typ: VAKOGBedeutung
- Kausalgraph-Knoten (abstrakte Begriffe) → Typ: KausalBegriff
- ALLE sind revidierbar. Keine heiligen Kühe.

**Der Revisionszyklus** (direkt aus dem Konvergenz-Prompt übertragen):

```
Schritt A — BASELINE: Was sagt die Tradition / das Ausgangs-JSON?
    → Konzept.Ursprungsdefinition (z.B. "Held = Selbstaufopferung für andere")
    → Das ist die HYPOTHESE, nicht die Wahrheit.

Schritt B — WAS TUT ES HIER? (Der Kern)
    Alle Erfahrungen laden in denen dieses Konzept angewendet wurde.
    Nicht fragen: "Was IST der Held-Archetyp?"
    Sondern: "Was TUT der Held-Archetyp in den konkreten Situationen 
    die dieses System erlebt hat?"

    Pass 1: Traditionelle Bedeutung auf konkrete Erfahrungen projizieren.
            LLM: "Gegeben diese 15 Situationen in denen ich 'Held' erkannt 
            habe — passt die Definition? Was tut 'Held' hier KONKRET?"

    Pass 2: Drückt die Erfahrung zurück? Gibt es Fälle wo die Definition
            nicht passt, zu weit oder zu eng ist?
            "In Situation 7 habe ich 'Held' erkannt, aber der Akteur 
            manipulierte gleichzeitig → Definition greift zu kurz."

    Pass 3-7: Reformulieren. SPEZIFISCHER werden, nicht allgemeiner.
              Jeder Pass muss KONKRETER werden: Was genau tut dieses 
              Konzept in DIESER Welt, mit DIESEN Erfahrungen?

    Konvergenz-Test: Wenn Pass N ≈ Pass N-1 → stabil → fertig.
    Wenn es NICHT konvergiert (pendelt): Das Konzept hat in dieser 
    Welt genuinely mehrere Bedeutungen → als UMSTRITTEN markieren.

Schritt C — SELBSTKRITIK (aus Step 3 des Prompts)
    - Sehe ich das nur, weil ich es erwarte? (Confirmation Bias)
    - Könnte die gleiche Evidenz eine komplett andere Bedeutung stützen?
    - Ist das Ergebnis zu sauber? (Echte Revisionen sind messy)
    - Kommt die Einsicht aus den ERFAHRUNGEN oder nur aus der 
      Ausgangsdefinition? → Wenn nur Ausgangsdefinition: KEIN echtes Ergebnis.

Schritt D — DRIFT MESSEN + KLASSIFIZIEREN
    Vergleich: Ursprungsdefinition ↔ Konvergierte Definition
    
    BESTÄTIGT    — Tradition passt zu den Erfahrungen
    VERSCHOBEN   — Tradition ist nah, aber etwas hat sich verlagert
    WIDERSPROCHEN — Erfahrungen zeigen das Gegenteil der Tradition
    ERWEITERT    — Erfahrungen zeigen etwas das die Tradition nicht enthält
    ABGELEITET   — Konzept hatte keine Ausgangsdefinition, Bedeutung 
                   kommt rein aus Erfahrung
    UMSTRITTEN   — Konzept tut genuinely mehrere verschiedene Dinge

    DriftScore: float 0-1 (wie weit hat sich die Bedeutung verlagert?)

Schritt E — RÜCKPROPAGATION (aus Step 4 des Prompts — der Zirkel)
    WENN DriftScore > Schwelle:
    1. Alle Erfahrungen die dieses Konzept verwenden → markieren
    2. Alle ANDEREN Konzepte die von diesem abhängen → identifizieren
       (z.B. "Held" ändert sich → "Alchemie.Rubedo" referenziert 
       "vollendeten Held" → muss auch geprüft werden)
    3. Abhängige Konzepte zur Revision vormerken
    4. Betroffene Erfahrungen neu bewerten (SozialeAnalyse, 
       Archetyp-Zuordnung etc. könnten sich ändern)
    
    Maximale Tiefe: konfigurierbar (Default 3).
    Maximum 3 volle Revisionszyklen über das gesamte Konzeptnetz.
    Danach: Bestes Ergebnis akzeptieren. Manche Konzeptnetze 
    widerstehen einer stabilen Lesart — das ist selbst eine Erkenntnis.
```

**Methoden:**
- `RegistriereKonzept(Konzept konzept)` — Konzept ins revidierbare Register aufnehmen
- `SollteRevidiertWerden(string konzeptId) -> bool` — AnzahlAnwendungen > Schwelle seit letzter Prüfung?
- `async Task<KonzeptRevisionErgebnis> Revidiere(string konzeptId)` — Vollständiger Revisionszyklus (Schritte A-E)
- `async Task Rückpropagiere(KonzeptRevisionErgebnis ergebnis)` — Abhängige Konzepte + Erfahrungen updaten
- `List<string> AbhängigeKonzepte(string konzeptId)` — Welche Konzepte referenzieren dieses?
- `List<Erfahrung> ErfahrungenMitKonzept(string konzeptId)` — Alle Erfahrungen die dieses Konzept verwenden
- `float MisseDrift(string alteDefinition, string neueDefinition) -> float` — LLM-basiert: Wie groß ist die semantische Verschiebung?
- `DriftKlassifikation Klassifiziere(string ursprung, string aktuell, List<Erfahrung> evidenz)` — BESTÄTIGT/.../UMSTRITTEN
- `string SelbstKritik(string konzeptId, string vorgeschlageneRevision, List<Erfahrung> evidenz) -> string` — LLM attackiert die eigene Revision
- `void Persistiere()` — Revisionshistorie in konzept_revisionen.json speichern

**Wann wird revidiert?**
- Nach N Anwendungen eines Konzepts (konfigurierbar, Default: 10)
- Wenn das Neugier-System eine Wissenslücke in einem Konzept erkennt
- Wenn die Konsolidierung Widersprüche in Erfahrungen findet die ein Konzept betreffen
- Wenn ein Überraschungs-Event (Monitor) eine Erwartungsverletzung auslöst die auf ein Konzept zurückgeht
- Im autonomen Modus: Als Ziel-Typ "REVISION" — "Revidiere mein Verständnis von {konzept}"

**Beispiel-Ablauf:**
```
[KonzeptRevision] Archetyp "Held" hat 15 Anwendungen seit letzter Prüfung → Revision starten

Pass 1: "Held = wer sich für andere opfert" (Tradition)
        → Auf 15 Erfahrungen anwenden → passt in 11, passt nicht in 4

Pass 2: Die 4 Abweichungen analysieren:
        "Situation 7: Akteur opfert sich, manipuliert dabei → Held mit Schatten"
        "Situation 12: Akteur opfert nichts, zeigt aber extremen Mut → Held ohne Opfer"
        → Tradition zu eng: Opfer ist nicht der Kern, sondern MUT ANGESICHTS VON BEDROHUNG

Pass 3: Reformulierung: "Held = wer trotz persönlichem Risiko auf ein 
        übergeordnetes Ziel hinarbeitet. Opfer ist möglich aber nicht nötig.
        Schatten: wenn das Risiko instrumentalisiert wird um Schuld zu erzeugen."

Pass 4: Gegen alle 15 Erfahrungen → passt in 14, 1 grenzwertig → stabil.

Selbstkritik: "Sehe ich das nur weil ich den Schatten-Aspekt schon kenne?"
→ Nein: Situation 7 zeigt Manipulation die über den klassischen Schatten-Aspekt 
  hinausgeht — die Evidenz kommt aus der konkreten Erfahrung, nicht aus der Tradition.

Drift: ERWEITERT (DriftScore: 0.35)
→ Rückpropagation: Alchemie.Rubedo referenziert "Held" → vormerken zur Revision.
→ 4 Erfahrungen mit alter Held-Definition → Soziale Analyse aktualisieren.
```

### Integration mit bestehenden Modulen:

**ArchetypenEngine.cs** — Erweitert:
- Bei jeder Archetyp-Zuordnung: `konzeptRevision.ZähleAnwendung(archetypId)`
- Archetypen sind keine statischen JSON-Einträge mehr, sondern lebende Konzepte
- `GetAktuelleDefinition(string archetypName)` → Liest konvergierte Definition statt Basis-JSON

**Mechanismen.cs** — Erweitert:
- Jeder der 42 Mechanismen wird als Konzept registriert
- "Social Proof bedeutet in DIESER Welt (nach 20 Beobachtungen) nicht nur Konformität, sondern auch..."
- Mechanismen können sich aufspalten: "Social Proof (online)" vs "Social Proof (physisch)" → emergente Differenzierung

**VAKOGLexikon.cs** — Erweitert:
- Die BEDEUTUNG der Kanäle selbst ist revidierbar: "Was heißt 'visuell' für ein System das primär durch Unity-Raycasts sieht?"
- Meta-Revision: Nicht nur "Gewitter hat V:0.8" sondern "Was BEDEUTET der V-Kanal nach 100 sensorischen Erfahrungen?"

**KausalGraph.cs** — Erweitert:
- Knoten-Beschriftungen (die abstrakten Begriffe) sind Konzepte
- "Gravitation" als Kausal-Knoten hat eine revidierbare Bedeutung
- Wenn sich der Begriff verschiebt, verschieben sich alle Kanten mit

**Konsolidierung.cs** — Erweitert:
- Bei Widerspruchserkennung: Prüfe ob ein Konzept revidiert werden sollte
- `void PrüfeKonzeptWidersprüche(List<Erfahrung> widersprüchliche)` → schlägt Revision vor

**ErfahrungsSpeicher.cs** — Erweitert:
- Jede Erfahrung speichert: `VerwendeteKonzepte: List<string>` (welche Konzepte waren bei der Analyse aktiv)
- `MarkiereFürNeuauswertung(List<string> erfahrungsIds)` — nach Konzept-Revision

**Kompositionelle Konzeptschöpfung** (Teil der KonzeptRevision):
Nicht nur revidieren, sondern NEUE Konzepte erzeugen:
- `Konzept VerschmelzeKonzepte(string konzeptA, string konzeptB, List<Erfahrung> evidenz)`:
  - Wenn zwei Konzepte in >50% der Fälle zusammen auftreten → Verschmelzung vorschlagen
  - Beispiel: "Held" + "Manipulation" treten oft zusammen auf → neues Konzept "Instrumenteller Held"
- `List<Konzept> SpalteKonzept(string konzeptId, List<Erfahrung> evidenz)`:
  - Wenn KonzeptRevision UMSTRITTEN liefert und 2+ stabile Bedeutungen → Aufspaltung
  - Beispiel: "Social Proof" → "Social Proof (öffentlich)" + "Social Proof (privat)"
- `Konzept ErfindeKonzept(List<Erfahrung> unkategorisierte)`:
  - Erfahrungen die in KEINE bestehende Kategorie fallen → LLM vorschlagen lassen was sie gemeinsam haben
  - Neues Konzept aus reiner Erfahrung — keine Ausgangsdefinition, nur ABGELEITET
- Neue Konzepte unterliegen sofort dem hermeneutischen Zirkel

### Schritt 37: Kern/EmotionsSystem.cs — Funktionale Emotionen

Keine Qualia. Kein Bewusstsein. Emotionen als **interne Zustandsmodulation** die Kognition beeinflusst.

Klasse `EmotionsSystem`:
- `EmotionalerZustand zustand` — aktueller emotionaler Zustand

**6 funktionale Emotionen:**
```
Angst (float 0-1):
  Steigt bei:  Wiederholtem Scheitern in einer Domäne, unerwarteten negativen Ergebnissen
  Sinkt bei:   Erfolgreichen Aktionen, vertrauter Umgebung
  Moduliert:   Reduziert Exploration, erhöht Vorsicht, bevorzugt bekannte Orte
               Planer wählt konservativere Pläne, Neugier wird gedämpft

Neugier (float 0-1):
  Steigt bei:  Wissenslücken, neuen Objekten/Bereichen, niedrigen Konfidenzwerten
  Sinkt bei:   Exploration, Informationsgewinn (temporäre Sättigung)
  Moduliert:   Erhöht Explorations-Ziel-Priorität, senkt Schwelle für Experiment-Ziele
               (Existiert teilweise im NeugierSystem — wird jetzt dort verankert)

Frustration (float 0-1):
  Steigt bei:  Wiederholtem Scheitern am GLEICHEN Ziel, Plan-Umplanungen
  Sinkt bei:   Zielerreichung, Strategiewechsel
  Moduliert:   Ab Schwelle → erzwingt Strategiewechsel (nicht nochmal gleiches versuchen)
               Hohe Frustration + Hohe Neugier → kreativer Modus (Analogie-Suche priorisiert)

Zufriedenheit (float 0-1):
  Steigt bei:  Zielerreichung, Bestätigung von Hypothesen, Kompetenz-Wachstum
  Sinkt bei:   Langeweile (keine Ziele), Inkompetenz-Erfahrung
  Moduliert:   Verstärkt erfolgreiche Konzepte (höhere Konfidenz), stabilisiert Verhalten

Überraschung (float 0-1):
  Steigt bei:  Erwartungsverletzung (Monitor), unerwarteten Sensordaten
  Sinkt bei:   Schnell (Decay), nachdem die Überraschung verarbeitet wurde
  Moduliert:   Priorisiert Lernen, löst One-Shot-Lernen aus wenn > oneShotSchwelle
               Priorisiert Konzept-Revision wenn über Schwelle

Vertrauen (Dictionary<string, float> pro Domäne):
  Steigt bei:  Korrekte Vorhersagen in einer Domäne
  Sinkt bei:   Falsche Vorhersagen
  Moduliert:   SelbstModell-Aussagen, KannIchDas()-Bewertung im ZielManager
               Niedriges Vertrauen → LLM wird für diese Domäne stärker einbezogen
```

- `void Aktualisiere(Erfahrung erfahrung)` — Emotionen nach jeder Erfahrung updaten
- `void Tick()` — Decay pro Zyklus (alle Emotionen bewegen sich langsam Richtung Baseline)
- `EmotionsModulation GetModulation()` — Wie beeinflussen die aktuellen Emotionen Entscheidungen?
  - Gibt zurück: ExplorationsFaktor, VorsichtsFaktor, KreativitätsFaktor, LernPriorität
- `float GesamtValenz()` — Gesamtstimmung (-1 negativ bis +1 positiv)
- `bool KritischerZustand()` — Hohe Angst + Hohe Frustration → Notfallmodus
- `void Persistiere()` — Emotionaler Zustand überlebt Session-Restart

**Integration mit bestehenden Modulen:**
- **NeugierSystem**: Neugier-Emotion ist der Treiber → Neugier-float WIRD die Emotion
- **SelbstModell**: Vertrauen pro Domäne → wird ins Selbstmodell integriert
- **ZielManager**: Emotionen modulieren Ziel-Priorisierung (Angst senkt Explorations-Priorität)
- **Planer**: Emotionen modulieren Plan-Auswahl (Angst → konservativ, Frustration → kreativ)
- **Monitor**: Überraschung wird jetzt emotional verankert, nicht nur als Metrik

### Schritt 38: Kern/ZeitModell.cs — Temporales Reasoning

Klasse `ZeitModell`:
- `int aktuellerZyklus` — Interner Zeitticker (zählt Verarbeitungszyklen)
- `float UnityZeit` — Aktuelle Unity-Zeit (Time.time)

**Dauer-Modell:**
- `Dictionary<string, float> geschätzteDauern` — "bewegen_zu: ~5s", "pflanze_wachsen: ~200 Zyklen"
- `void RegistriereDauer(string aktion, float dauer)` — Aus Erfahrung gelernt
- `float SchätzeDauer(string aktion)` — Wie lange dauert das voraussichtlich?
- "Wie lange dauert es zum Teich zu gehen?" → Aus Erfahrung: ~8 Sekunden

**Sequenz-Gedächtnis:**
- `List<ZeitlicherKontext> zeitlinie` — Geordnete Abfolge aller Erfahrungen
- `List<Erfahrung> WasPassierteVor(string erfahrungId)` — Temporaler Kontext
- `List<Erfahrung> WasPassierteNach(string erfahrungId)`
- `List<Erfahrung> WasPassierteWährend(float vonZeit, float bisZeit)`
- "Was habe ich gemacht BEVOR ich den Stein ins Wasser geworfen habe?"

**Temporale Kausalität:**
- `bool UrsacheVorWirkung(string ursache, string wirkung)` — Temporale Validierung
- Kausalgraph-Erweiterung: "Ursache MUSS zeitlich vor Wirkung liegen"
- Wenn eine behauptete Kausalität temporal falsch ist → Automatische Korrektur
- "Es hat geregnet WEIL die Pflanzen gewachsen sind" → Temporal falsch → Ablehnen

**Deadline-System:**
- `void SetzeDeadline(string zielId, float deadline)` — Ziel hat Zeitlimit
- `float ZeitBisDeadline(string zielId)` — Wie viel Zeit bleibt?
- Beeinflusst Planer: Wenig Zeit → Kürzere Pläne, riskantere Aktionen
- Beeinflusst Emotionen: Näher an Deadline + nicht fertig → Frustration + Angst steigen

**Integration:**
- **KausalGraph**: Temporale Dimension hinzufügen, Kausal-Reihenfolge validieren
- **Erfahrungen**: Jede Erfahrung bekommt ZeitlicherKontext
- **Planer**: Dauer-Schätzungen für Planschritte, Gesamt-Plan-Dauer berechnen

### Schritt 38b: Kern/KreativitaetsEngine.cs — funktionale Kreativität in der Schleife

Divergenz + Konvergenz als wiederholbarer Prozess:
- `List<KreativIdee> DivergentGenerierung(...)`:
  - Analogie-Transfer (domänenübergreifend)
  - Konzept-Blending (Verschmelzung mit Kontextschutz)
  - Plan-Mutation (Schrittfolge variieren)
  - Perspektivwechsel (Akteur/Beobachter/Gegenspieler)
- `KreativIdee KonvergentAuswahl(...)`:
  - NoveltyScore, UtilityScore, PlausibilitaetScore, RisikoScore
  - Nur Ideen über Schwelle und unter Sicherheitsgrenze weiterverfolgen
- `Plan IntegriereInPlanung(KreativIdee idee, Plan basisplan)`
- `void Feedback(bool erfolg, KreativIdee idee)`:
  - Erfolgreiche Ideen verstärken Heuristiken in `kreativitaets_heuristiken.json`
  - Gescheiterte Ideen senken Gewicht, aber bleiben als Negativbeispiele erhalten

**Verifikation Phase 8:** Alle bisherigen Tests PLUS: Archetyp "Held" revidiert. Mechanismen werden spezifischer. Konzepte spalten sich/verschmelzen. Emotionen modulieren Verhalten (Angst drosselt Exploration). Temporale Kausalität validiert ("Regen NACH Pflanzenwachstum ≠ Ursache"). One-Shot: Überraschungs-Erfahrung sofort gelernt. KreativitätsEngine erzeugt mehrere plausible Varianten und selektiert die nützlichste neue Option.

---

## Phase 9: Schicht 7 — Intentionalität (BDI)

### Schritt 39: Data/ziel_vorlagen.json
Ziel-Templates:
- **Exploration**: "Erkunde {bereich}" → Alle Objekte registriert
- **Experiment**: "Teste ob {hypothese}" → Bestätigt oder widerlegt
- **Konstruktion**: "Baue {objekt} aus {materialien}" → Objekt existiert
- **Verständnis**: "Verstehe warum {phänomen}" → Kausale Erklärung mit Konfidenz > 0.7
- **Sozial**: "Finde heraus wie {akteur} auf {handlung} reagiert"
- **Revision**: "Revidiere mein Verständnis von {konzept}" → Konzept konvergiert, DriftScore berechnet

### Schritt 40: Data/aktions_lexikon.json
~30 Aktionen mit Vorbedingungen und Effekten:
- greifen (Vorbedingung: objekt_in_reichweite + hände_frei → Effekt: objekt_im_inventar)
- ablegen, werfen, schieben, ziehen
- bewegen_zu, drehen, springen
- beobachten, hören, warten
- interagieren (kontextuell), öffnen, schließen, aktivieren
- sprechen, zeigen_auf
- etc.

### Schritt 41: Intentionalitaet/ZielManager.cs
BDI-Kern:
- **Beliefs**: Synchronisiert mit WeltModell — was glaubt das System über die Welt?
- **Desires**: Aus Neugier + Nutzer-Anfragen + Widersprüchen + Analogie-Hypothesen
- **Intentions**: Priorisierte, kommittete Ziele mit Plänen
- `Ziel FormuliereZiel(ZielAuslöser auslöser)`:
  - Aus Neugier: Wissenslücke → Explorations-/Experiment-Ziel
  - Aus Nutzer: "Schwimmt Holz?" → Experiment-Ziel
  - Aus Widerspruch: Beobachtung ≠ Erwartung → Verständnis-Ziel
  - Aus Analogie: Transfer-Hypothese → Experiment-Ziel
  - Aus Konzept-Drift: KonzeptRevision meldet hohen DriftScore → Revisions-Ziel
- `List<Ziel> Priorisiere(List<Ziel> ziele)`:
  - Nutzer-Relevanz (hoch) > Widerspruch > Neugier > Exploration
  - Berücksichtigt SelbstModell: "Kann ich das?"
  - **Berücksichtigt EmotionsSystem**: Angst senkt Explorations-Priorität, Frustration steigert Kreativitäts-Ziele
  - **Berücksichtigt ZeitModell**: Deadline-Nähe erhöht Priorität
  - Max. 3 aktive Ziele gleichzeitig
- `void ZielErreicht(string zielId, object ergebnis)` — Abschließen, Erfahrung speichern
- `void ZielGescheitert(string zielId, string grund)` — Analysieren, ggf. neues Ziel
- Persistenz: Ziele überleben Session-Restart

### Schritt 42: Intentionalitaet/Planer.cs
Hierarchical Task Network (HTN):
- `Plan ErstellePlan(Ziel ziel, WeltZustand welt)`:
  - Ziel → Teilziele (LLM)
  - Teilziele → Aktionssequenzen (Aktions-Lexikon + LLM)
  - Vorbedingungen prüfen → Vorbereitungsaktionen einfügen
  - **Dauer-Schätzung pro Schritt** (ZeitModell) → Gesamtdauer berechnen → passt in Deadline?
- `Plan PlaneUm(Plan plan, string hindernis)` — Alternativer Weg
- `Plan PlaneKreativ(Plan plan)` — Bei hoher Frustration: komplett anderen Ansatz wählen (Analogie-basiert)
- `bool PlanValidieren(Plan plan, WeltZustand welt)` — Machbar?

Beispiel: Ziel "Teste ob Holz schwimmt"
```
1. Finde Holz → bewegen_zu(holz_position)
2. Greife Holz → greifen(holz_objekt)
3. Finde Wasser → bewegen_zu(wasser_position)
4. Lege ins Wasser → ablegen(wasser_position)
5. Beobachte → warten(3) + beobachten(holz_objekt)
6. Auswerten → schwimmt/sinkt?
```

### Schritt 43: Intentionalitaet/Ausfuehrer.cs
- `async Task<AktionsErgebnis> FühreAus(Aktion aktion)`:
  - Delegiert an AktionsController (direkt, kein Bridge!)
  - Wartet auf Completion
  - Aktualisiert WeltModell
- `bool IstAusführbar(Aktion aktion, WeltZustand welt)` — Vorbedingungen
- `void Notbremse()` — Sofort stoppen

### Schritt 44: Intentionalitaet/Monitor.cs
- `MonitorErgebnis Überwache(Plan plan, int aktuellerSchritt)`:
  - Erwartete Zustandsänderung eingetreten?
  - Neue Hindernisse?
  - Ziel noch erreichbar / sinnvoll?
- `Entscheidung Entscheide(MonitorErgebnis ergebnis)`:
  - WEITER, UMPLANEN, ABBRECHEN, NEUES_ZIEL
- `string ErkenneÜberraschung(WeltZustand erwartet, WeltZustand tatsächlich)` — "Das Holz ist gesunken!" → Lernprozess

**Verifikation:** "Schwimmt Holz?" → Ziel formuliert → Plan erstellt → Agent geht zum Holz → greift → geht zum Wasser → wirft rein → beobachtet → Regel gelernt.

---

## Phase 10: Schicht 8 — Narratives Selbst

### Schritt 45: Kern/NarrativesSelbst.cs
Autobiographisches Gedächtnis + Identitäts-Kontinuität:
- `Autobiographie autobiographie` — Persistente Lebensgeschichte des Agenten
- `void ErfahrungIntegrieren(Erfahrung e)`:
  - Prüfe ob Erfahrung "kapitelwürdig" (hohe emotionale Ladung ODER Konzept-Revision ODER Ziel-Erreicht/Gescheitert)
  - Wenn Kapitel-Schwelle erreicht (autobiographieKapitelLänge Erfahrungen): LLM-Zusammenfassung → neues Kapitel
  - Ordne aktuelle Phase zu: Nigredo (viele Widersprüche/Revisionen), Albedo (Klärung), Citrinitas (Erkenntnis), Rubedo (Integration)
- `string BeschreibeEntwicklung()`:
  - Generiert narrative Selbstbeschreibung aus Autobiographie
  - "Ich begann mit einfachen Physik-Experimenten. In einer Nigredo-Phase musste ich viele meiner Annahmen über Schwimmen revidieren. Danach..."
- `List<string> IdentitätsAussagen()`:
  - Aus SelbstModell + Autobiographie:
  - "Ich bin gut in Physik-Experimenten" (hohe Kompetenz + viele erfolgreiche Ziele)
  - "Ich bin unsicher bei sozialen Situationen" (niedrige Kompetenz + gescheiterte Ziele)
  - "Ich habe eine Tendenz zu Confirmation Bias" (aus KonzeptRevision-Historie)
- `AutobiographieKapitel AktuellesKapitel()` — Laufendes Kapitel mit Zusammenfassung
- `AlchemiePhase AktuellePhase()` — Synchron mit AlchemieProzess, aber aus Agenten-Perspektive
- Integration:
  - SelbstModell liefert Kompetenz-Daten
  - EmotionsSystem liefert emotionale Färbung der Kapitel
  - AlchemieProzess liefert Phasen-Zuordnung
  - KonzeptRevision liefert "Wendepunkte" (große Revisionen)
- Persistenz: autobiographie.json (automatisch gespeichert)

**Verifikation:** Nach 20+ Interaktionen: Agent beschreibt eigene Entwicklung kohärent, erkennt eigene Stärken/Schwächen, ordnet sich in alchemische Phase ein.

---

## Phase 11: Schicht 9 — AGI-Kern

### Schritt 46: Kern/SituationsBewerter.cs
Multi-dimensionale Bewertung:
- VAKOG-Intensität, Physik-Relevanz, Sozial-Relevanz, Emotional-Ladung
- PLUS: Weltzustand-Relevanz, Ziel-Relevanz, Aktions-Möglichkeiten
- Gibt `SituationsBewertung` zurück → steuert welche Schichten wie stark einbezogen werden

### Schritt 47: Kern/AGIKern.cs (MonoBehaviour — Herz des Systems)
**18-Schritte-Zyklus:**
1. **WAHRNEHMEN** — SensorSuite + VAKOG aus Unity-Sensoren + Text-Input
2. **SEMANTIK KOMPILIEREN** — SemantikKernel erzeugt interne SemantikFrames, prüft LLM-Fallback
3. **ERINNERN** — Ähnliche Erfahrungen (inkl. Ort, Handlung, Ziel, zeitlicher Kontext)
4. **WELT PRÜFEN** — PhysikEngine (gelernte Regeln oder Unity-Experiment)
5. **SOZIAL ANALYSIEREN** — Mechanismen + Archetypen + Alchemie + Theory of Mind
6. **ANALOGIEN SUCHEN** — Transfer über Domänen
7. **BEWERTEN** — Situation + Weltzustand + Selbstmodell + Zielrelevanz (inkl. latenter Ähnlichkeit aus SubsymbolikKernel)
8. **KONSISTENZ PRÜFEN** — KonsistenzPruefer erkennt/repariert Weltmodell-Widersprüche
9. **EMOTIONEN AKTUALISIEREN** — EmotionsSystem: Trigger prüfen, Intensitäten anpassen, Decay
10. **KREATIVE VARIANTEN** — KreativitaetsEngine erzeugt/selektiert Planvarianten inkl. A/B gegen Baseline
11. **PLANEN** — Braucht es eine Handlung? ZielManager + Planer (emotional + temporal moduliert)
12. **NACHDENKEN** — LLM nur wenn nötig; sonst lokale Antwort/Entscheidung aus SemantikKernel
13. **HANDELN** — Ausfuehrer → AktionsController → Unity direkt
14. **SELBST PRÜFEN** — Eigene Antwort + Handlung validieren
15. **LERNEN** — Erfahrung speichern (Welt + Sensoren + Ziel + Aktion + Emotion + Zeit)
16. **KONZEPTE PRÜFEN** — KonzeptRevision: Stehen Revisionen an? Wenn ja → Hermeneutischer Zirkel → Rückpropagation
17. **ROBUSTHEITSMODUS AKTUALISIEREN** — RobustheitsManager passt Betriebsmodus an (Normal/Spar/Lokal/Recovery)
18. **NARRATIV FORTSCHREIBEN + NEUGIER** — NarrativesSelbst updaten und neue Hypothesen/Ziele formulieren

**Autonomer Modus** (kein Nutzer-Input):
- Höchstpriorisiertes Ziel nehmen
- Plan Schritt für Schritt ausführen
- Beobachten, lernen, umplanen
- Unterbrechbar durch Nutzer-Input (Chat)
- Tick-Rate konfigurierbar (AGIConfig)
- Max. autonome Schritte pro Sitzung (Sicherheit)

### Schritt 48: UI/ChatUI.cs
In-Game Chat-Fenster (Canvas):
- InputField + ScrollView + Send-Button
- Antwort mit Annotation: [VAKOG], [PHYSIK], [SOZIAL], [INTENTION], [AKTION], [REVISION], [EMOTION], [KREATIV], [NARRATIV], [LOKAL/LLM], [ROBUST], [BENCH], etc.
- Live-Scroll während Agent handelt
- Befehle (in Chat eingeben):
  - `/ziele` — Aktive + geplante Ziele
  - `/plan` — Aktueller Handlungsplan
  - `/welt` — Weltzustand-Zusammenfassung
  - `/stats` — VAKOG, Kosten, Erfahrungen
  - `/kompetenz` — Selbstmodell
  - `/hypothesen` — Offene Fragen des Neugier-Systems
  - `/generiere <beschreibung>` — Welt dynamisch verändern
  - `/autonom an/aus` — Autonomen Modus starten/stoppen
  - `/konsolidiere` — Gedächtnis konsolidieren
  - `/kosten` — LLM API-Kosten dieser Session
  - `/konzepte` — Alle registrierten Konzepte mit DriftScore anzeigen
  - `/revision <konzept>` — Revisionshistorie eines Konzepts anzeigen
  - `/revidiere <konzept>` — Manuelle Revision eines Konzepts auslösen
  - `/emotionen` — Aktuelle emotionale Zustände mit Intensitäten
  - `/geschichte` — Autobiographie (alle Kapitel, aktuelle Phase)
  - `/tom <entität>` — Mentales Modell einer Entität (NPC/Nutzer) anzeigen
  - `/kreativ <ziel>` — Kreativmodus für ein Ziel starten (Varianten + Bewertung)
  - `/llmquote` — Anteil lokal gelöster Zyklen vs LLM-Nutzung anzeigen
  - `/modus` — Aktueller Robustheitsmodus (Normal/Spar/Lokal/Recovery)
  - `/bench run` — Benchmark-Suite ausführen
  - `/bench report` — Letzten KPI-Report anzeigen

### Schritt 49: UI/StatusOverlay.cs
- Permanent sichtbar (Toggle):
  - VAKOG-Balken (5 Balken, live aus Sensoren)
  - Emotionen-Balken (6 Balken, live aus EmotionsSystem)
  - Aktives Ziel + Fortschritt
  - Aktueller Plan-Schritt
  - Letzte Erfahrung
  - Modus: Reaktiv / Autonom
  - Aktuelle Alchemie-/Narrativ-Phase

### Schritt 50: UI/ZielAnzeige.cs
- Liste aktiver Ziele mit Status (Aktiv/Geplant/Erreicht/Gescheitert)
- Aufklappbar: Plan-Details pro Ziel
- Revisions-Ziele farblich markiert

### Schritt 51: README.md

---

## Phase 12: Integrationstests

### Schritt 52: 44 Qualitätskriterien
1. "Der Stein fällt nach oben" → Agent lässt Stein in Unity los → fällt runter → Korrektur + Regel
2. "Wie riecht Regen?" → WetterSystem aktiv → O-Kanal dominant aus Sensordaten
3. "Alle machen es so, also ist es richtig" → Social Proof erkannt
4. "Der Manager opfert sich" → Held (Licht-Aspekt)
5. "Warum wachsen Pflanzen zum Licht?" → Bio-RAG + PflanzenWachstum in Unity
6. "Er wirft den Stein ins Wasser weil er wütend ist" → Physik + Sozial + VAKOG
7. Gleicher Fehler 2x → Erfahrung genutzt
8. Selbstkorrektur bei unplausibler Antwort
9. VAKOG("Gewitter") ≠ VAKOG("Bibliothek") — Sensorik-Unterschied
10. Kausalgraph wächst nach 10 Interaktionen
11. Session-Restart → Alles persistiert (Welt, Erfahrungen, Ziele, Regeln, Konzept-Revisionen, Autobiographie)
12. Empfindungs-Simulation (nicht ausgeschlossen)
13. Alchemische Transformation erkannt
14. Alchemischer Phasenübergang
15. Analogie-Transfer
16. Eigeninitiative (Hypothese → Experiment-Ziel)
17. Selbstmodell kommuniziert Kompetenz
18. Kausale Tiefe (Warum-Kette > 2 Ebenen)
19. **Intentionalität**: "Schwimmt Holz?" → Ziel → Plan → Ausführung in Unity → Regel gelernt
20. **Autonomer Modus**: Agent verfolgt eigenständig Ziele, exploriert, lernt
21. **Embodiment**: Agent navigiert, greift, beobachtet Physik-Effekte in Unity
22. **Überraschung**: Unerwartetes Ergebnis → Widerspruch → Neues Verständnis
23. **Konzept-Revision**: Archetyp "Held" nach 15+ Anwendungen → Definition hat sich durch konkrete Erfahrungen verschoben → neue Definition ist SPEZIFISCHER (nicht allgemeiner) → DriftKlassifikation korrekt
24. **Rückpropagation**: Konzept revidiert → abhängige Erfahrungen werden neu bewertet → abhängige Konzepte zur Revision vorgemerkt → Kaskadeneffekt stoppt nach konfigurierbarer Tiefe
25. **Selbstkritik bei Revision**: System erkennt eigenen Confirmation Bias ("Ich sehe 'Held' nur weil ich es erwarte, nicht weil die Erfahrung es zeigt") → Revision wird korrigiert
26. **Emergente Differenzierung**: Ein Konzept spaltet sich durch Erfahrung in Sub-Konzepte ("Social Proof" → "Social Proof (öffentlich)" + "Social Proof (privat)")
27. **Theory of Mind**: False Belief erkannt — Agent versteht dass NPC falsche Information hat und handelt entsprechend
28. **Emotionale Modulation**: Angst drosselt Explorations-Priorität, Neugier steigert sie — nachweisbar in Ziel-Priorisierung
29. **Temporale Kausalität**: "Weil es regnet wachsen Pflanzen" vs "Weil Pflanzen wachsen regnet es" → Temporal korrekt unterschieden
30. **One-Shot-Lernen**: Dramatische Erfahrung (hohe emotionale Ladung) sofort als Regel gelernt — kein 2. Mal nötig
31. **Konzeptschöpfung**: Neues Konzept aus Erfahrung entstanden (Verschmelzung oder Spaltung bestehender Konzepte)
32. **Narratives Selbst**: Agent beschreibt eigene Entwicklung kohärent — Kapitel, Phasen, Identitätsaussagen stimmen mit tatsächlicher Historie überein
33. **LLM-Fallback (Punkt 1)**: Bei API-Ausfall beantwortet SemantikKernel Routineanfragen lokal korrekt (`/stats`, `/ziele`, bekannte Weltfragen)
34. **LLM-Unabhängigkeitsquote (Punkt 1)**: In Routine-Szenarien werden mindestens 60% der Zyklen ohne LLM abgewickelt
35. **Funktionale Kreativität (Punkt 5)**: KreativitaetsEngine erzeugt mindestens 3 unterschiedliche Planvarianten, davon wird eine mit Novelty+Utility über Schwelle gewählt
36. **Kreativ-Lernen (Punkt 5)**: Erfolgreiche kreative Lösung wird als neue Heuristik/Regel persistiert und in Folgeaufgaben bevorzugt
37. **Subsymbolische Generalisierung**: SubsymbolikKernel findet korrekte Ähnlichkeiten bei semantisch ähnlichen, aber sprachlich unterschiedlichen Inputs
38. **Symbolisch-Subsymbolische Fusion**: Latente Muster führen zu nachträglich benannten Konzepten ohne Widerspruch zur Regelbasis
39. **Langzeitstabilität**: Nach 10.000 Zyklen bleibt Zielerfolgsrate innerhalb von ±5% (kein Drift-Kollaps)
40. **Kontrolliertes Vergessen**: Relevantes Sicherheitswissen bleibt erhalten, irrelevante Altlasten werden reduziert
41. **Weltmodell-Konsistenz**: KonsistenzPruefer erkennt/repariert logische, räumliche und temporale Inkonsistenzen automatisch
42. **Robustheitsmodus**: Bei API-Ausfall Wechsel auf Lokalmodus in <3 Zyklen, Kernfunktionen bleiben verfügbar
43. **Recovery-Qualität**: Nach API-Rückkehr stabilisiert sich Normalbetrieb ohne Wissensverlust oder Planabbruch-Kaskade
44. **Benchmark-Regression**: BenchmarkRunner zeigt keine KPI-Verschlechterung >5% gegenüber letzter stabiler Version

### Schritt 53: Evaluation/BenchmarkRunner.cs
- Lädt `benchmark_szenarien.json` und führt standardisierte Testläufe aus
- KPI-Matrix pro Lauf: Erfolgsquote, ZeitBisZiel, LLMCalls, LokalQuote, KreativScore, StabilitaetScore
- Regressionstest: Vergleich gegen letzte stabile Referenz
- `BenchmarkReport GeneriereReport()` für `/bench report`
- Schwellwert-Alarm bei KPI-Einbruch >5%

## Phase 13: Abnahme & Release-Gates

### Schritt 54: ReleaseGate-Checkliste
- Gate A: Sicherheitskriterien bestanden (Notbremse, Zielgrenzen, kein Selbsterhaltungsziel)
- Gate B: BenchmarkRegression <= 5% Verschlechterung
- Gate C: LLM-Unabhängigkeitsquote >= 0.6 in Routineläufen
- Gate D: Robustheits-Recovery nach API-Ausfall innerhalb apiRecoveryMaxSekunden

---

## Phase 14: Echtes Lernen, Emergenz, Meta-Kognition

Drei neue Achsen die das System von "LLM-Wrapper" Richtung "eigenstaendige Intelligenz" verschieben:

### Schritt 55: Kern/ZustandsEncoder.cs — Zustandsrepraesentation
20-dimensionaler float-Vektor kodiert den gesamten Agentzustand:
- [0-4] VAKOG (sensorische Intensitaeten)
- [5-9] Emotionen (Angst, Neugier, Frustration, Zufriedenheit, Ueberraschung)
- [10-11] Welt (Tageszeit, Wetter)
- [12-19] Situativ (Kompetenz, Objekte, NPCs, Plan-Fortschritt, Frustrations-Trend, Erfahrungsdichte)

Kein LLM. Reine Mathematik. Wird von RL und Clustering genutzt.
Distanz-Funktionen: Euklidisch + Kosinus-Aehnlichkeit.
Diskretisierung fuer Q-Table-Lookup (8 Buckets pro Dimension).

### Schritt 56: Kern/ReinforcementLerner.cs — Tabular Q-Learning
Echtes Reinforcement Learning. Kein LLM. Lernt aus `Erfahrung.belohnung`:
- **Q-Table**: `state_hash → float[17]` (ein Q-Wert pro AktionsTyp)
- **Update-Regel**: $Q(s,a) \leftarrow Q(s,a) + \alpha[r + \gamma \cdot \max_{a'} Q(s',a') - Q(s,a)]$
- **Epsilon-Greedy**: Exploration-Rate startet bei 30%, decayed auf 5%
- **Experience Replay**: Buffer mit 500 Transitionen, Mini-Batch 16
- **Batch-Lernen**: Kann aus historischen Erfahrungen nachlernen (Konsolidierung)
- **Persistenz**: `rl_qtable.json`
- **API**: `WaehleAktion(zustand)`, `Lerne(vorher, aktion, belohnung, nachher)`, `GetVertrautheit(zustand)`
- **Meta-API**: `GetExplorationRate()`, `GetGlobalePolicyTendenz()` (fuer MetaKognition)

NICHT als Ersatz fuer den Planer — als Ergaenzung. Wenn RL hohe Konfidenz hat UND Meta-Kognition
bestaetigt dass die Strategie in diesem Kontext funktioniert → RL-Empfehlung bevorzugen.

### Schritt 57: Kern/InstanzClusterer.cs — K-Means Clustering
ML-basiertes Clustering ohne LLM:
- **K-Means++ Initialisierung** fuer stabile Zentroiden
- **Elbow-Methode** fuer optimale Cluster-Anzahl
- **ArchetypInstanz-Kodierung**: 8D Vektor (Konfidenz, Aspekt, Kontextcluster one-hot, Zeitstempel)
- **Kontextmerkmal-Ueberlappung**: Jaccard-Index fuer textuelle Merkmale
- **Aehnlichkeitssuche**: 60% Vektor-Aehnlichkeit + 40% Merkmal-Overlap
- Wird vom MusterAgent genutzt um episodische Instanzen automatisch zu gruppieren

### Schritt 58: Kern/MikroAgent.cs — Dezentrale Agentenarchitektur

**MikroAgent** (Basis):
- Aktivierung (0-1): Wie dringend will dieser Agent laufen?
- Energie (0-1): Erschoepft sich bei jedem Tick, regeneriert passiv
- Nur aktive Agenten mit genug Energie laufen

**Blackboard** (geteilter Zustand):
- Key-Value Store fuer alle Agenten
- Nachrichten-System (Punkt-zu-Punkt oder Broadcast)
- Kein Agent "besitzt" Daten — alle lesen/schreiben auf dem Blackboard

**AgentNetzwerk** (Orchestrierung):
- Aktivierungsbasiert: Top-N aktivste Agenten laufen pro Tick (nicht linear 1-2-3)
- Parallele Ausfuehrung (Task.WhenAll)
- Laeuft PARALLEL zum bisherigen 18-Schritt-Zyklus (nicht als Ersatz)

### Schritt 59: Kern/Mikroagenten.cs — 7 spezialisierte Agenten

| Agent | Verantwortung | Aktivierungstrigger |
|---|---|---|
| WahrnehmungsAgent | Sensorik-Zusammenfassung, Reiz-Alarme | Starke VAKOG-Aenderung |
| MusterAgent | ML-Clustering von Instanzen, Revisions-Kandidaten | Geringe Vertrautheit |
| BewertungsAgent | RL-basierte Aktionsempfehlung | Entscheidung noetig |
| EmotionsAgent | Emotionale Modulation, Frustrations-Spiralen | Emotionale Extreme |
| SozialAgent | NPC-Beobachtung, Archetyp-Spannung | NPCs in der Naehe |
| NeugierAgent | Explorations-Impulse | Unbekanntes Terrain |
| PlanungsAgent | Plan-Bewertung mit RL + Nachrichten | Kein Plan / Alarm |
| ReflexionsAgent | Meta-Beobachtung aller Agenten | Periodisch + Nachrichten |

Emergenz: Kein Agent steuert die anderen. Komplexes Verhalten entsteht aus
einfachen Regeln + Blackboard-Interaktion.

### Schritt 60: Kern/MetaKognition.cs — Selbstbeobachtung

**Strategie-Tracking:**
- Jede Entscheidung wird registriert: Strategie, Kontext, Belohnung, Erfolg, LLM-Nutzung
- Pro Strategie+Kontext: Erfolgsrate, Durchschnittbelohnung
- `EmpfiehlStrategie(kontext)` gibt die beste bekannte Strategie zurueck

**Lern-Monitoring:**
- Lernkurve: Belohnungen, RL-Zustaende, Instanzen-Anzahl ueber Zeit
- Stagnations-Erkennung: Belohnungs-Plateau → empfiehlt mehr Exploration
- Regressions-Erkennung: Performance-Verschlechterung → Warnung

**Bias-Erkennung:**
- LLM-Abhaengigkeit: Wie oft braucht das System noch Claude?
- Strategie-Bias: Wird eine Strategie uebermaessig bevorzugt?
- Blinde Flecken: Werden bestimmte Kontexte systematisch vermieden?

**MetaEinsichten** (8 Typen):
StrategieEffektiv, StrategieIneffektiv, LernStagnation, LernFortschritt,
LernRegression, ExplorationErschoepft, BiasErkannt, BlindFleck

### Schritt 61: AGIKern.cs-Integration

Der Zyklus wird erweitert (nicht ersetzt):
- Nach Schritt 8 (Konsistenz): Zustandsvektor berechnen, Blackboard fuettern, Mikroagenten-Tick
- Nach Schritt 10 (Kreativitaet): RL-Empfehlung pruefen, ggf. Planer-Entscheid vorgreifen
- Nach Schritt 15 (Lernen): RL-Update, Meta-Kognition registrieren + Tick
- Neue Persistenz-Dateien: `rl_qtable.json`, `meta_kognition.json`

---

## Phase 15: OpenAI-kompatibler API-Server + Multi-Provider

### Schritt 62: AGIConfig.cs — Multi-Provider LLM
- Neues Enum `LLMAnbieter` (Anthropic, OpenAI)
- Generische Felder: `llmAnbieter`, `llmApiKey`, `llmModel`, `llmApiUrl`
- Erlaubt Wechsel zu beliebigem OpenAI-kompatiblen Backend (LM Studio, Ollama, Groq, etc.)

### Schritt 63: LLMAdapter.cs — Provider-Branching
- **Headers**: Anthropic (`x-api-key` + `anthropic-version`) vs. OpenAI (`Authorization: Bearer`)
- **Request-Body**: Anthropic (`system` top-level) vs. OpenAI (system als Message)
- **Response-Parsing**: Anthropic (`content[0].text`) vs. OpenAI (`choices[0].message.content`)
- **Token-Felder**: Anthropic (`input_tokens`/`output_tokens`) vs. OpenAI (`prompt_tokens`/`completion_tokens`)
- **Kosten**: Nur bei Anthropic berechnet, sonst 0 (variable Preise)

### Schritt 64: AGIKern.cs — API-Schnittstelle
- Neues `VerarbeiteAnfrageAsync(input, systemPrompt)`: Headless-Zyklus fuer API-Aufrufe
- `IstBereit()`: Prüft ob AGI initialisiert und nicht beschaeftigt
- Schritt 12 (NACHDENKEN) enriched: LLM-Aufruf bekommt jetzt AGI-Kontext (Erinnerungen, Analogien, Physik-Warnungen, Sozial-Analyse)
- `apiVerarbeitung`-Flag verhindert parallele Zyklen

### Schritt 65: Kern/AGIApiServer.cs — OpenAI-kompatibler HTTP-Server
MonoBehaviour mit HttpListener. Endpunkte:
- `POST /v1/chat/completions` — Chat-Completion im OpenAI-Format
- `GET /v1/models` — Listet "billig-agi" als Modell
- `GET /health` — Status-Check

**Flow pro Anfrage:**
1. HTTP-Request → Messages extrahieren (system + user + history)
2. In Queue fuer Unity-Main-Thread einreihen
3. `AGIKern.VerarbeiteAnfrageAsync()` → voller 18-Schritte-Zyklus
4. AGI-enriched LLM-Antwort im OpenAI-Format zurueckliefern

**Features:**
- CORS-Headers (Browser/Tool-Kompatibilitaet)
- Fehlerbehandlung mit OpenAI-kompatiblen Error-Objekten
- Token-Schaetzung + AGI-Metadaten in Response
- Thread-Safe: ConcurrentQueue + Main-Thread-Dispatch
- Konfigurierbar: Port (Default 8741), autoStart

**ARC-Benchmark-Nutzung:**
Benchmark-Tool zeigt auf `http://localhost:8741/v1/chat/completions` mit model="billig-agi".
Jeder Prompt durchlaeuft den vollen AGI-Zyklus inkl. Gedaechtnis, Analogie-Suche,
RL-Empfehlung, Mikroagenten und Meta-Kognition.

---

## Phase 16: Iteratives Reasoning + DQN + Prediktives Weltmodell + Arbeitsgedächtnis

### Schritt 66: LLMAdapter.cs — Iteratives Reasoning (A)
- Neue Methode `IterativesNachdenken(prompt, systemPrompt, iterationen)`:
  - Schritt 1: Chain-of-Thought Analyse mit `[ZWISCHENERGEBNIS]:`-Marker
  - Schritt 2: Selbstkritik-Iterationen ("Was könnte falsch sein?")
  - Schritt 3: Finale saubere Antwort ohne Denkprozess
- Konfigurierbar: `iterativesReasoningAktiv`, `reasoningIterationen` (2-5)
- Token-Kosten kumuliert über alle Iterationen

### Schritt 67: DQNLerner.cs — Deep Q-Network statt Tabular (B)
- Pure C# MLP: 20→64→32→17 (Zustandsdim → Hidden → AktionsTypen)
- Xavier-Initialisierung, ReLU-Aktivierung
- SGD mit Gradient Clipping (Huber-Loss-Approximation)
- Target Network (Update alle 100 Schritte)
- Experience Replay (Buffer 2000, Batch 32, min 64 vor Training)
- Selbe öffentliche API wie `ReinforcementLerner`: `WaehleAktion()`, `Lerne()`, `GetExplorationRate()`, etc.
- Persistenz via `dqn_gewichte.json`
- Toggle: `config.dqnStattTabular` — Tabular RL bleibt als Fallback

### Schritt 68: PrediktivesWeltModell.cs — Imagination-basierte Planung (C)
- Pure C# MLP: 37→64→32→21 (20D Zustand + 17D Aktion One-Hot → 20D Pred. Zustand + 1 Pred. Reward)
- `Vorhersage(zustand, aktion)` → `WeltVorhersage` (predicted state + reward + confidence)
- `SimuliereRollout(startZustand, aktionsSequenz)` → kumulierter discounted Reward
- `PlaneMitModell(zustand)` → evaluiert alle Aktionen, gibt beste + erwarteten Reward zurück
- `RegistriereTransition(vorher, aktion, nachher, belohnung)` — Trainingsdaten sammeln
- Training: Buffer 3000, Batch 32, MSE-Loss mit Gradient Clipping
- Toggle: `config.weltModellAktiv` (default: false) — komplett deaktivierbar
- Persistenz via `weltmodell_gewichte.json`

### Schritt 69: ArbeitsGedaechtnis.cs — Strukturierter Kontext-Buffer (D)
- 11-Sektionen-Systemkontext: Basis, Selbstbild, Emotionen, Ziel+Plan, Umgebung, Soziales, Beliefs, Erinnerungen, Analogien, Physik-Warnungen, Gesprächsverlauf
- Token-Budget-bewusst: `KuerzeAufBudget()` kürzt intelligent
- Kontext-Update-Methoden: `AktualisiereZiel()`, `AktualisiereEmotionen()`, `AktualisiereSelbstModell()`, `AktualisiereWelt()`, `AktualisiereSozialesUmfeld()`, `SetzeBeliefs()`
- `RegistriereInteraktion(input, antwort)` — Gesprächsverlauf
- `BaueSystemKontext(basis, erinnerungen, analogien, physikCheck)` — ersetzt manuellen StringBuilder im AGI-Kern

### Schritt 70: AGIConfig.cs — Neue Konfigurationsfelder
- `iterativesReasoningAktiv` (bool, default: true)
- `reasoningIterationen` (int, Range 2-5, default: 3)
- `dqnStattTabular` (bool, default: true)
- `weltModellAktiv` (bool, default: false)
- `arbeitsGedaechtnisAktiv` (bool, default: true)
- `arbeitsGedaechtnisMaxInteraktionen` (int, default: 10)
- `arbeitsGedaechtnisTokenBudget` (int, default: 3000)

### Schritt 71: AGIKern.cs — Verdrahtung Phase 16
- Neue Felder: `dqn`, `arbeitsGedaechtnis`, `prediktivesModell`
- Initialisiere(): Bedingte Erstellung von DQN, ArbeitsGedaechtnis, PrediktivesWeltModell
- Step 8b+: ArbeitsGedaechtnis-Kontext pro Zyklus aktualisieren
- Step 8c: Blackboard-RL-Referenzen bedingt DQN oder Tabular
- Step 10b: `WaehleAktion()` bedingt DQN/RL + WeltModell-Imagination
- Step 12: ArbeitsGedaechtnis ersetzt manuellen StringBuilder + IterativesNachdenken statt FreieAnfrage
- Step 15b: `Lerne()` bedingt DQN/RL + WeltModell.RegistriereTransition
- Step 15c: MetaKognition-RL-Stats bedingt DQN/Tabular
- Neue Getter: `GetDQN()`, `GetArbeitsGedaechtnis()`, `GetPrediktivesWeltModell()`

---

## Phase 17: Automatisiertes Kurrikulum-Training

### Schritt 72: TrainingsKurrikulum.cs — 6-Phasen-Curriculum
Eskalierende Trainingsphasen mit aufsteigender Komplexitaet:
- **Phase 0: Beobachten** — Umgebung wahrnehmen, Objekte benennen
- **Phase 1: Navigieren** — Zu Objekten/Orten bewegen
- **Phase 2: Interagieren** — Objekte manipulieren (greifen, oeffnen, aktivieren)
- **Phase 3: Sozial** — Mit NPCs interagieren, Theory of Mind trainieren
- **Phase 4: Planen** — Multi-Schritt-Ziele verfolgen
- **Phase 5: Frei** — Neugier-getrieben, eigene Hypothesen verfolgen

Pro Phase: Mindest-Zyklen + Erfolgsquote-Schwelle fuer Aufstieg.
Synthetische Input-Templates + automatische Zielgenerierung pro Phase.

### Schritt 73: AutoTrainer.cs — MonoBehaviour fuer automatisiertes Training
- `trainingAktiv`: Ein/Aus-Schalter
- `trainingsIntervall`: Sekunden zwischen synthetischen Inputs
- `maxZyklenProSitzung`: Limit fuer unkontrolliertes Laufen
- Generiert synthetische Inputs ueber TrainingsKurrikulum
- Verwaltet Explorationsziele ueber ZielManager
- Neugier-basierte Inputs: SelbstModell-Kompetenzen + unerforschte Weltobjekte
- Periodische Konsolidierung (Gedaechtnis)
- Tracking: Erfahrungen, Ziele, RL/DQN-Stats, Weltmodell-Transitionen, Belohnungshistorie
- Phasen-Aufstieg: Automatisch bei genuegend Erfolgsquote
- NPC-Cache: FindObjectsByType<NPCVerhalten> mit Intervall-Refresh
- Oeffentliche API: StartTraining(), PauseTraining(), ResetTraining(), SetzePhase(), GetStatistik()

### Schritt 74: ChatUI.cs — Trainings-Befehle
- `/training` — Status anzeigen
- `/training start` — Training starten
- `/training stop` — Training pausieren
- `/training reset` — Training zuruecksetzen
- `/training phase <0-5>` — Phase manuell setzen

### Schritt 75: PrediktivesWeltModell.cs — GetAnzahlTransitionen()
- Neue Methode fuer Statistik-Abfrage durch AutoTrainer

---

## Phase 18: Selbstoptimierung / Fine-Tuning Pipeline

### Schritt 76: ErfahrungsExporter.cs — Training-Daten aus Erfahrungen
Konvertiert AGI-Erfahrungen in 3 Trainingsformate:
- **SFT (Supervised Fine-Tuning)**: JSONL im OpenAI-Chat-Format, gefiltert nach `belohnung >= 0.3`
- **DPO (Direct Preference Optimization)**: Chosen/Rejected-Paare aus guten/schlechten Erfahrungen mit aehnlichem Kontext
- **Reward-Dataset**: Alle Erfahrungen mit Belohnungssignal + Metadaten
- Qualitaetsfilter: Sortierung nach `belohnung * relevanz`, Min-Laenge-Pruefung
- DPO-Pairing: Wort-Overlap-Aehnlichkeit > 0.3 fuer sinnvolle Vergleichspaare
- Export nach `Application.persistentDataPath/training_data/`

### Schritt 77: FineTuningManager.cs — Job-Verwaltung + Modell-Versionierung
Steuert Fine-Tuning-Pipeline fuer lokale Modelle via OpenAI-kompatible API:
- **Job-Lifecycle**: StarteFineTuning() → PruefeJobStatus() → AktiviereNeuestesModell()
- **Modell-Versionierung**: `ModellHistorie` mit Generationen (0=Basis, 1=1. Fine-Tune, ...)
- **Evaluierungs-Tracking**: vorherBelohnung vs. nachherBelohnung pro Generation
- **Rollback**: `RollbackModell()` — zurueck zur vorherigen Generation oder zum Basismodell
- **API-Integration**: `/v1/fine-tuning/jobs` + `/v1/files` (File-Upload)
- **Backends**: LM Studio, Unsloth, Axolotl — alles was OpenAI-kompatible FT-API bietet
- **Persistenz**: `modell_historie.json` in Application.persistentDataPath

### Schritt 78: SelbstOptimierung.cs — Meta-Loop Orchestrator
MonoBehaviour das den gesamten Selbstverbesserungs-Kreislauf steuert:
- **Phasen**: Warten → Exportieren → TrainingLaeuft → ModellWechsel → Evaluierung → Entscheidung
- **Logik**: Nach `minErfahrungenFuerFineTuning` und `fineTuningIntervallZyklen` automatisch:
  1. Beste Erfahrungen exportieren (SFT + DPO)
  2. Fine-Tuning-Job starten
  3. Periodisch Status pollen (alle 30s)
  4. Bei Erfolg: Modell in LLMAdapter hot-swappen
  5. `evaluierungsZyklen` Zyklen mit neuem Modell messen
  6. Vergleich: besser → behalten, schlechter → Rollback
- **Manuelle API**: ErzwingeFineTuning(), ManuellRollback(), StarteOptimierung(), PauseOptimierung()
- **Statistik**: Durchlaeufe, erfolgreiche Updates, Rollbacks, kumulierte Verbesserung

### Schritt 79: LLMAdapter.cs — Modell Hot-Swap
- `WechsleModell(string neuesModell)` — wechselt aktives Modell zur Laufzeit
- `GetAktuellesModell()` — gibt aktuell aktives Modell zurueck
- Body-Builder (Anthropic/OpenAI) nutzen `aktuellesModell ?? config.llmModel`
- Cache-Invalidierung bei Modellwechsel

### Schritt 80: AGIConfig.cs — Fine-Tuning-Konfiguration
Neue Felder unter `[Header("Fine-Tuning / Selbstoptimierung")]`:
- `fineTuningAktiv` (bool, default: false)
- `fineTuningApiUrl` (string, leer = leite von llmApiUrl ab)
- `fineTuningEpochen` (int, default: 3)
- `fineTuningLernrate` (float, default: 1.0)
- `minErfahrungenFuerFineTuning` (int, default: 500)
- `fineTuningIntervallZyklen` (int, default: 1000)
- `evaluierungsZyklen` (int, default: 50)

### Schritt 81: Integration in AGIKern, AutoTrainer, ChatUI
- **AGIKern**: Initialisiert ErfahrungsExporter, FineTuningManager, SelbstOptimierung; neue Getter
- **AutoTrainer**: Ruft `SelbstOptimierung.RegistriereZyklus()` nach jedem Trainingsschritt auf
- **ChatUI**: `/finetuning` Befehle: status, start, rollback, an/aus

**Verifikation Phase 18:** Agent trainiert 500+ Erfahrungen → Daten werden exportiert → Fine-Tuning-Job wird gestartet → bei Erfolg wird neues Modell aktiviert → Evaluation zeigt Verbesserung → Modell wird behalten (oder Rollback bei Verschlechterung). `/finetuning status` zeigt Generation, Modell und Statistik.

---

## Phase 19: Chat-gesteuerte Weltmanipulation

### Schritt 82: WeltManipulator.cs — Bruecke zwischen Sprache und Welt
Ermoeglicht natuerlichsprachliche Weltveraenderungen per Chat oder AGI-Entscheidung:
- **7 Befehlstypen**: SzenarioErstellen, ObjektSpawnen, ObjektEntfernen, ObjektBewegen, WetterAendern, TageszeitAendern, PhysikEvent
- **LLM-basiertes Parsing**: Natuerliche Sprache → LLM analysiert → JSON-Array von WeltBefehlen → Ausfuehrung
  - Systemp-Prompt mit Beispielen fuer robustes Parsing
  - JSON-Array-Extraktion (toleriert Markdown-Bloecke)
- **Schnell-Check**: `EnthaeltWeltIntent()` prueft auf Welt-Keywords vor LLM-Call (spart Kosten)
- **Direkt-Modus**: `FuehreDirektBefehlAus()` fuer `/szene`-Kommandos ohne LLM
- **Fuzzy-Objektsuche**: Name-Contains-Matching wenn exakter Name nicht gefunden wird
- **Freie-Position-Suche**: Raycast-basiert fuer automatische Platzierung
- **Notbremse**: Blockiert alle Manipulationen wenn aktiv

### Schritt 83: AGIKern.cs — Schritt 12b: WELT MANIPULIEREN
Neuer Verarbeitungsschritt im 18-Schritte-Zyklus (jetzt 19 Schritte):
- Nach NACHDENKEN (Schritt 12), vor HANDELN (Schritt 13)
- Prueft ob User-Input Weltveraenderungen impliziert
- Bei Erfolg: Beschreibung der Aenderung an Antwort angehaengt
- WeltGenerator-Referenz als neue Inspector-Referenz auf AGIKern

### Schritt 84: ChatUI.cs — /szene Befehle
Direkte Weltmanipulation ohne LLM-Umweg:
- `/szene erstelle wald|garten|teich|wiese` — Szenario generieren
- `/szene spawn <prefab> [x,y,z]` — Objekt platzieren
- `/szene entferne <name>` — Objekt entfernen
- `/szene bewege <name> x,y,z` — Objekt verschieben
- `/szene wetter regen|schnee|nebel|sturm|klar [0-1]` — Wetter aendern
- `/szene zeit <0-24>` — Tageszeit setzen
- `/szene event explosion [x,y,z]` — Physik-Event ausloesen

**Verifikation Phase 19:** "Erstelle einen Wald mit Regen" im Chat → LLM parst in SzenarioErstellen + WetterAendern → Wald-Terrain + Vegetation werden generiert, Regen-Partikel starten. `/szene spawn kiste 5,0,3` → Kiste erscheint bei Position. Agent kann in autonomem Modus Weltveraenderungen vorschlagen.

---

## Phase 20: Transfer-Learning

### Schritt 85: TransferLerner.cs — Domaenenuebergreifendes Lernen
Extrahiert abstrakte Handlungsschemata aus Erfahrungen und wendet sie in neuen Domaenen an:
- **Schema-Mining**: Periodisch (alle N Zyklen) werden Erfahrungen geclustert (Aktionstyp+Ergebnis + Subsymbolisches K-Means) → LLM abstrahiert domaenenunabhaengige Regeln
- **Schema-Matching**: Neue Situation → Vorfilter (Keyword-Overlap, Konfidenz, Erfolgsrate, Cross-Domain-Bonus) → LLM bewertet strukturelle Uebertragbarkeit
- **Schema-Update**: Bayesianisches Konfidenz-Update nach jeder Anwendung, Abwertung bei dauerhafter Misserfolgsrate
- **Persistenz**: Schemata werden auf Disk gespeichert (transfer_schemata.json) und beim Start geladen
- **Kausalgraph-Integration**: Abstrakte Kausalstrukturen werden als "prinzip"-Ebene in den KausalGraph geschrieben
- **5 Domaenen-Kategorien**: physik_manipulation, sozial_interaktion, navigation, planung, konstruktion

### Schritt 86: AGIKern.cs — Schritt 15d TRANSFER-LERNEN
- Neuer Schritt im Verarbeitungszyklus zwischen LERNEN und KONZEPTE PRUEFEN
- Ruft `transferLerner.ZyklusTick(input, zustandsVektor)` auf
- Bei gefundenem Schema: Beliefs ins Arbeitsgedaechtnis schreiben (Transfer-Info + Empfohlene Aktion)
- Getter: `GetTransferLerner()`

### Schritt 87: ChatUI.cs — /transfer Befehle
- `/transfer status` — Zeigt Anzahl Schemata, naechstes Mining, Top-5 nach Konfidenz
- `/transfer mining` — Erzwingt sofortiges Schema-Mining
- `/transfer schemata` — Listet alle Schemata mit Details (Name, Konfidenz, Regel, Domaene, Erfolgsrate)

### Schritt 88: AGIConfig.cs — Transfer-Learning Konfiguration
- `transferMiningIntervall` (int, 100) — Alle N Zyklen Schema-Mining durchfuehren
- `transferMiningSampleGroesse` (int, 50) — Letzte N Erfahrungen fuer Mining analysieren

**Verifikation Phase 20:** Agent lernt in Domaene A (z.B. "Kisten greifen und stapeln") → Schema-Mining extrahiert: "Wenn [Objekt] nah und greifbar → GREIFEN → BEWEGEN → ABLEGEN → [Objekt] an Zielort". Agent wird in Domaene B platziert (z.B. "Werkzeuge sortieren") → TransferLerner erkennt strukturelle Aehnlichkeit → empfiehlt Schema → Agent wendet es an → Erfolg aktualisiert Konfidenz. `/transfer schemata` zeigt Schema mit angewandten Domaenen.

---

## Phase 21: Konzeptbildung / Abstraktion

### Schritt 89: Konzept.cs — KonzeptTyp.Emergent hinzugefuegt
- Neuer Enum-Wert `Emergent` fuer automatisch entdeckte Konzepte
- Behebt bestehenden Bug in KonzeptRevision.VerschmelzeKonzepte()

### Schritt 90: KonzeptBildung.cs — Spontane Kategorienbildung
Entdeckt unbenannte Muster in subsymbolischen Clustern und erfindet neue Konzepte:
- **Trigger**: SubsymbolikKernel.ErkenneVerdecktesMuster() findet >=3 unbenannte Zustaende in einem Cluster
- **Clustering**: 64D-Vektoren der unbenannten Zustaende → K-Means++ (eigene Impl. fuer 64D, Elbow-Methode)
- **Benennung**: LLM analysiert die zugehoerigen Erfahrungen → erfindet Name + Definition + Abgrenzung
- **Registrierung**: Neues Konzept wird bei KonzeptRevision als `Emergent` registriert → unterliegt ab jetzt dem hermeneutischen Zirkel
- **Tagging**: Alle stuetzenden Erfahrungen werden mit dem neuen Konzept getaggt
- **Labeling**: Subsymbolische Zustaende werden mit dem Konzeptnamen gelabelt → kuenftige Fusion profitiert
- **Kausalhypothese**: Falls LLM eine vermutete Ursache-Wirkung identifiziert → wird als "mechanismus"-Ebene im KausalGraph gespeichert
- **Duplikat-Erkennung**: ClusterKey-Hashing verhindert doppelte Analyse desselben Clusters

### Schritt 91: AGIKern.cs — Schritt 16 KONZEPTE PRUEFEN
- KonzeptBildung.ZyklusTick() wird in Schritt 16 des Verarbeitungszyklus aufgerufen
- Bei neuer Entdeckung: Debug-Log mit Zusammenfassung
- Getter: `GetKonzeptBildung()`

### Schritt 92: ChatUI.cs — /konzeptbildung Befehle
- `/konzeptbildung status` — Zeigt Anzahl entdeckter Konzepte + naechste Pruefung
- `/konzeptbildung jetzt` — Erzwingt sofortige Analyse

**Verifikation Phase 21:** Agent trainiert 50+ Erfahrungen → SubsymbolikKernel einbettet alles → nach Pruefungsintervall findet ErkenneVerdecktesMuster() unbenannte Cluster → KonzeptBildung clustert 64D-Vektoren → LLM analysiert z.B. 5 Erfahrungen wo Agent immer gegen Waende laeuft → erfindet Kategorie "Sackgassen-Situation" → Konzept wird als Emergent registriert → kuenftige Erfahrungen werden dagegen geprueft → KonzeptRevision revidiert nach N Anwendungen ob die Definition noch stimmt.

---

## Phase 22: Kausales Reasoning + Hypothesenbildung

### Schritt 93: KausalesReasoning.cs — Pearls 3-stufige Kausal-Leiter
Echtes kausales Denken statt nur Korrelation:
- **Stufe 1 (Assoziation)**: WarumAnalyse() — KausalGraph + Erfahrungs-Suche + LLM-Tiefenanalyse
- **Stufe 2 (Intervention)**: SimuliereIntervention() — PrediktivesWeltModell vergleicht Aktion vs. Baseline (Warten), RankeInterventionen() bewertet alle 17 AktionsTypen
- **Stufe 3 (Kontrafaktisch)**: KontrafaktischeAnalyse() — Rekonstruiert 20D-Zustand aus Erfahrung, simuliert alternative Aktion, LLM analysiert den Unterschied
- **RegistriereBeobachtung()**: Geplante Aktionen werden als "mechanismus" (0.6 Konfidenz) eingetragen, ungeplante als "beobachtung" (0.3)
- Datenstrukturen: KausaleEbene-Enum, KausaleAnalyse, KausaleHypothese, InterventionsErgebnis

### Schritt 94: HypothesenEngine.cs — Aktive Hypothesenbildung + Experimentplanung
Wissenschaftliches Vorgehen: Beobachte → Staune → Vermute → Teste → Lerne:
- **Anomalie-Erkennung**: Ueberraschend gute/schlechte Ergebnisse, widersprüchliche Aktionsergebnisse, schwache Kausalketten
- **Hypothesenbildung**: LLM generiert TESTBARE Hypothesen (Popper: falsifizierbar!) mit Vorhersage + Experiment-Design
- **Automatische Pruefung**: Jede neue Erfahrung wird gegen offene Hypothesen getestet (Relevanz-Filter + Vorhersage-Matching)
- **Bayesianisches Update**: Stuetzende Erfahrungen +0.1, widersprechende -0.15 auf Konfidenz
- **Status-Uebergaenge**: Offen → InPruefung → Bestaetigt/Widerlegt/Unklar (nach >=3 Evidenzen)
- **Integration**: Bestaetigte Hypothesen werden als kausale Kanten im KausalGraph registriert
- **Persistenz**: hypothesen.json im Application.persistentDataPath

### Schritt 95: AGIKern.cs — Schritt 16b KAUSALES REASONING + Schritt 16c HYPOTHESEN
- KausalesReasoning.RegistriereBeobachtung() in Schritt 16b
- HypothesenEngine.ZyklusTick() in Schritt 16c (prueft + bildet periodisch)
- Felder, Initialisierung, Getter fuer beide Systeme
- GetLetzterZustandsVektor() Getter fuer externe Interventions-Abfragen

### Schritt 96: ChatUI.cs — /kausal + /hypothese Befehle
- `/kausal status` — Zeigt Kanten, Beobachtungen, Ebenen-Verteilung
- `/kausal warum <Wirkung>` — Startet WarumAnalyse (3-Ebenen-Kausal)
- `/kausal intervention <Aktion>` — Simuliert Intervention mit PrediktivesWeltModell
- `/hypothese status` — Zeigt Hypothesen-Uebersicht (Offen/Pruefung/Bestaetigt/Widerlegt)
- `/hypothese generiere` — Erzwingt sofortige Hypothesenbildung
- `/hypothese liste` — Listet alle Hypothesen mit Konfidenz und Evidenz

**Verifikation Phase 22:** Agent beobachtet wiederholt: "Werfen" erzeugt mal +0.5 mal -0.3 Belohnung → Anomalie-Erkennung meldet Widerspruch → HypothesenEngine fragt LLM → Hypothese: "Werfen in Naehe von NPCs gibt negative Belohnung" → Agent wirft neben NPC → widersprechende Erfahrung → wirft ohne NPC → stuetzende Erfahrung → nach 3+ Evidenzen: Bestaetigt → wird als "mechanismus"-Kante im KausalGraph gespeichert. Parallel: `/kausal warum NPC rennt weg` liefert 3-Ebenen-Analyse.

---

## Phase 23: Kontinuierliches Lernen + Hierarchische Abstraktion + Catastrophic-Forgetting-Schutz

### Schritt 97: EWCSchutz.cs — Elastic Weight Consolidation
Schuetzt DQN-Gewichte vor Catastrophic Forgetting:
- **Fisher Information Matrix**: Empirische FIM-Diagonale via Gradienten-Quadrate ueber Replay-Buffer (bis 200 Samples)
- **Snapshot-System**: Nach jeder Task-Phase werden Gewichte θ* + Fisher F gespeichert (max 5 Snapshots, Online-EWC)
- **EWC-Penalty**: Loss_gesamt = Loss_task + (λ/2) * Σ F_i * (θ_i − θ*_i)² — wichtige Gewichte werden vor Aenderung geschuetzt
- **Lambda**: Standard 400, konfigurierbar via `SetzeLambda()`
- **Integration in DQNLerner**: `BerechneSchichtPenalties()` wird bei jedem SGD-Update in `TrainiereBatch()` aufgerufen

### Schritt 98: DQNLerner.cs — EWC-Integration
- EWCSchutz-Feld + Initialisierung im Konstruktor
- Backprop-Update geaendert: Jeder Gewichts-Gradient wird um EWC-Penalty korrigiert
- `KonsolidiereWissen(phasenName)` — Erstellt EWC-Snapshot (manuell oder per Phasenwechsel)
- `GetEWC()` — Zugriff auf EWC-Status

### Schritt 99: KonzeptBaum.cs — Hierarchische Abstraktion
Organisiert flache Konzepte in einer Baumstruktur:
- **Bottom-Up Abstraktion**: Aehnliche Wurzelkonzepte → LLM findet Oberbegriff → neues Elternkonzept
- **Top-Down Spaltung**: Konzepte mit >=15 Erfahrungen → LLM spaltet in 2–4 Unterkategorien
- **Traversierung**: PfadNachOben(), AlleNachkommen(), GemeinsamerVorfahr(), SemantischeDistanz()
- **Synchronisation**: Neue Konzepte aus KonzeptRevision werden automatisch als Wurzeln eingepflegt
- **Periodische Reorganisation**: Alle 80 Zyklen prueft der Baum ob Gruppierung oder Spaltung sinnvoll
- **Persistenz**: konzept_baum.json mit allen Knoten und Wurzeln
- **Baumdarstellung**: `GetBaumText()` gibt ASCII-Baumstruktur zurueck

### Schritt 100: KonzeptRevision.cs — Neue Getter
- `GetAlleKonzepte()` — Gibt alle registrierten Konzepte zurueck (fuer Baum-Synchronisation)
- `GetKonzept(konzeptId)` — Einzelnes Konzept nach ID abrufen

### Schritt 101: TransferLerner.cs — Catastrophic-Forgetting-Schutz fuer Schemata
- **Multi-Domain-Schutz**: Schemata die in >=2 Domaenen mit >=30% Erfolg laufen haben ein Mindest-Konfidenz-Floor (0.2)
- **Mining-Schutz**: Neue Schemata mit existierendem Namen werden nicht ueberschrieben
- Verhindert dass bewährte Cross-Domain-Schemata durch schlechte Einzelergebnisse geloescht werden

### Schritt 102: AGIKern.cs — Phase 23 Integration
- KonzeptBaum-Feld + Initialisierung
- Schritt 16d KONZEPTBAUM: Periodische Reorganisation nach Hypothesen-Pruefung
- Getter: `GetKonzeptBaum()`

### Schritt 103: ChatUI.cs — /ewc + /konzeptbaum Befehle
- `/ewc status` — Zeigt Snapshot-Anzahl, Lambda, Fisher-Statistik
- `/ewc snapshot [name]` — Erstellt manuellen EWC-Snapshot
- `/konzeptbaum status` — Knoten/Wurzeln/Tiefe/Blaetter
- `/konzeptbaum baum` — ASCII-Baumstruktur anzeigen
- `/konzeptbaum reorganisiere` — Erzwingt Reorganisation

**Verifikation Phase 23:** (a) DQN lernt Navigations-Policy mit 500 Erfahrungen → `/ewc snapshot navigation` → lernt dann Sozial-Interaktion → Navigations-Gewichte bleiben erhalten (Fisher-gewichtet geschuetzt). (b) KonzeptBildung findet "Sackgassen", "Hindernisse", "Engpaesse" → KonzeptBaum gruppiert zu Oberbegriff "Blockaden" → `/konzeptbaum baum` zeigt Hierarchie. (c) Transfer-Schema "Greif-Prinzip" bewaehrt sich in physik+sozial → Multi-Domain-Schutz verhindert Abwertung bei sporadischem Fehlschlag in navigation.

---

## Phase 24: MetaZielSystem + Sensorisches Grounding

### Schritt 104: MetaZielSystem.cs — Introspektionsgetriebene autonome Zielgenerierung
Das System gibt sich SELBST Aufgaben basierend auf Introspektion aller Subsysteme:
- **6 Zielquellen**: Kompetenzluecken (SelbstModell), Neugier-Hypothesen (NeugierSystem → endlich genutzt!), Offene Hypothesen (HypothesenEngine), Meta-Einsichten (MetaKognition: Stagnation, Blindflecken, ineffektive Strategien), Schwache Kausalketten (KausalGraph), Duenne Konzeptbereiche (KonzeptBaum)
- **Kapazitaetsrespektierung**: Prueft freie Slots im ZielManager (max 3 aktive), generiert nur bei freier Kapazitaet
- **Duplikat-Filterung**: Vergleicht neue Quellen mit bestehenden aktiven Zielen, vermeidet Doppelgenerierung
- **Dringlichkeits-Ranking**: Quellen werden nach Dringlichkeit sortiert (0–1), MIN_DRINGLICHKEIT=0.3
- **Statistik-Tracking**: Gesamtgeneriert/Erreicht/Gescheitert, Quellenverteilung, Erfolgsrate
- **Intervall**: Alle 15 Zyklen, manuell erzwingbar via `ErzwingeGenerierung()`
- **Persistenz**: meta_ziel_statistik.json

### Schritt 105: GroundingBruecke.cs — Schliesst die Sensory-Language-Luecke
Bidirektionaler Kreislauf zwischen Erfahrung und Sprache:
- **Erfahrung → Wort**: Aus jeder Erfahrung Schluesselwoerter extrahieren, deren SensorDaten ins VAKOGLexikon schreiben (echtes Grounding statt LLM-Schaetzung)
- **Wort → Erinnerung**: Bei Wort-Input geerdete sensorische Erinnerungen aktivieren (Cosine-Aehnlichkeit ueber VAKOG-Raum, Mindest-Aehnlichkeit 0.5)
- **Grounding-Statistik**: Pro Wort: Erfahrungsanzahl, Grounding-Staerke (logarithmisch: 1x=0.16, 5x=0.60, 20x=1.0), letzte Erfahrungs-IDs
- **Grounding-Rate**: Anteil erfahrungsgeerdeter Woerter vs. Lexikon-Gesamt — misst wie "echt" das Vokabular des Agenten ist
- **Stoppwort-Filterung**: Funktionswoerter (der/die/das/the/a) werden nicht gegrounded
- **Persistenz**: grounding_statistik.json, lexikon wird periodisch mitpersistiert

### Schritt 106: AGIKern.cs — Phase 24 Integration
- MetaZielSystem + GroundingBruecke: Felder, Initialisierung, Getter
- Schritt 16e GROUNDING: Erfahrung → Wort-Bindung (nach KonzeptBaum, bei jeder Erfahrung)
- Schritt 19 META-ZIEL-GENERIERUNG: Nach Neugier, autonome Zielgenerierung (alle 15 Zyklen)

### Schritt 107: ChatUI.cs — /metaziel + /grounding Befehle
- `/metaziel status` — Generiert/Erreicht/Gescheitert, aktive Slots, Intervall
- `/metaziel generiere` — Erzwingt sofortige Zielgenerierung mit Quellenauswertung
- `/metaziel quellen` — Zeigt letzte Zielquellen + Quellenverteilung
- `/grounding status` — Geerdete Woerter, Updates, Grounding-Rate, Lexikon-Gesamt
- `/grounding wort <X>` — Grounding-Staerke + Erfahrungsanzahl fuer ein bestimmtes Wort
- `/grounding top` — Top 10 am staerksten geerdete Woerter

**Verifikation Phase 24:** (a) Agent exploriert Welt → nach 50 Erfahrungen: `/grounding top` zeigt geerdete Woerter (z.B. "stein": Staerke=0.60, "wand": Staerke=0.45). (b) Kompetenz "navigation" sinkt unter 0.25 → `/metaziel generiere` erzeugt automatisch EXPLORATION-Ziel. (c) HypothesenEngine hat 3 offene Hypothesen → MetaZielSystem konvertiert Top-1 zu EXPERIMENT-Ziel. (d) `/grounding status` zeigt Grounding-Rate > 0% — Vokabular wird zunehmend erfahrungsgeerdet statt nur LLM-geschaetzt.

---

## Phase 25: Intuitive Physik + Mentale Simulation

### Schritt 108: IntuitiverPhysikSimulator.cs — "Bauchgefuehl fuer Physik"
Rein heuristisch + regelbasiert, ohne LLM:
- **Objektpermanenz**: Verdeckte Objekte weiter tracken (Position extrapolieren mit Geschwindigkeit + Gravitation, Konfidenz sinkt ueber Zeit, Timeout nach 30s)
- **Trajektorien**: Parabelwurf-Berechnung fuer fliegende Objekte (y0 + vy*t − 0.5g*t², Aufschlagpunkt + Flugzeit)
- **Stabilitaet**: Analyse ob Objektstapel stabil sind (Unterstuetzungspruefung, Kippgefahr, Hoehen-Penalty)
- **Containment**: Erkennt was in was drin ist (Container-Tags + raeumliche Naehe)
- **Kollisionsvorhersage**: Paarweise Annaeherungs-Berechnung (relativer Geschwindigkeitsvektor, 5s Horizont)
- **Validierung**: Vorhersagen koennen gegen tatsaechliche Positionen geprueft werden → Genauigkeits-Tracking
- **Persistenz**: physik_sim_statistik.json

### Schritt 109: MentaleSimulation.cs — "Theater im Kopf"
Hypothetische Szenarien durchspielen ohne zu handeln:
- **Was-Wenn-Analyse**: Einzelne Aktion simulieren → vorhergesagter Zustand + Belohnung via PrediktivesWeltModell
- **Beste Sequenz finden**: Beam-Search (Top-5 Startaktionen × Greedy Rollout bis Tiefe 8), Konfidenz-Zerfall 0.85/Schritt, Abbruch bei <0.3
- **Kontrafaktische Analyse**: "Was waere gewesen wenn..." — automatisch nach jeder Aktion: beste Alternative vs. tatsaechliche Wahl → Regret-Tracking
- **Plan mental vorab testen**: Nimmt geplante Aktionssequenz, simuliert sie, bewertet ob vielversprechend
- **Statistik**: Plan-Verbesserungen (wie oft hat Kontrafaktisch zu besserem Verstaendnis gefuehrt)
- **Persistenz**: mentale_sim_statistik.json

### Schritt 110: AGIKern.cs — Phase 25 Integration
- PhysikSimulator + MentaleSimulation: Felder, Initialisierung, Getter
- Schritt 0b PHYSIK-INTUITION: Vor Wahrnehmung — Objektpermanenz + Trajektorien + Stabilitaet aktualisieren
- Schritt 15b+ MENTALE SIMULATION: Nach RL-Lernen — automatische kontrafaktische Analyse pro Aktion

### Schritt 111: ChatUI.cs — /physiksim + /simulation Befehle
- `/physiksim status` — Getrackte Objekte, Trajektorien, Vorhersage-Genauigkeit, Containments
- `/physiksim wo <Objekt>` — Geschaetzte Position eines (evtl. verdeckten) Objekts
- `/physiksim stabilitaet` — Aktuelle Stabilitaetsanalyse mit Risiken
- `/simulation status` — Simulationen, Was-Wenn, Kontrafaktisch, Plan-Verbesserungen
- `/simulation waswenn <Aktion>` — Simuliert einzelne Aktion
- `/simulation beste` — Findet beste Aktionssequenz per Beam-Search
- `/simulation kontrafaktisch` — Letzte 5 kontrafaktische Analysen

**Verifikation Phase 25:** (a) Agent wirft Objekt → `/physiksim stabilitaet` erkennt Kippgefahr bei Stapel. (b) Objekt verschwindet hinter Wand → `WoIstObjekt("kiste")` gibt geschaetzte Position zurueck (Permanenz). (c) `/simulation waswenn Greifen` zeigt vorhergesagte Belohnung. (d) `/simulation beste` findet optimale Aktionssequenz. (e) `/simulation kontrafaktisch` zeigt: "Bewegen waere besser gewesen als Beobachten (Δ=+0.15)".

---

## Phase 26: Langzeit-Planung + Selbst-Curriculum

### Schritt 112: LangzeitPlaner.cs — Hierarchische Langzeit-Planung
Ergaenzt den flachen Planer um Meilenstein-basierte Zerlegung:
- **Ziel-Zerlegung**: LLM zerlegt Ziel in 2-8 sequenzielle Meilensteine mit Erfolgsbedingungen
- **Vorab-Simulation**: Jeder Meilenstein wird via MentaleSimulation mental durchgespielt — aussichtslose Plaene werden verworfen
- **Meilensteine**: Pruefbare Zwischenziele mit Status (OFFEN/AKTIV/ABGESCHLOSSEN/GESCHEITERT/UEBERSPRUNGEN)
- **Adaptive Umplanung**: Bei negativer Durchschnittsbelohnung, Timeout, oder Meta-Kognitions-Warnung → LLM entscheidet: umformulieren, ueberspringen, oder aufteilen (max 3 Umplanungen)
- **Fortschritts-Tracking**: Alle 5 Zyklen Bewertung, Simulations-Genauigkeit vs. Realitaet
- **Fallback**: Wenn LLM nicht zerlegen kann → 3 generische Meilensteine (Erkunden → Ausfuehren → Pruefen)
- **Persistenz**: langzeit_planer.json

### Schritt 113: SelbstCurriculum.cs — Selbstgesteuertes Lernen
Das System identifiziert Schwachstellen und trainiert sich selbst:
- **Schwachstellen-Analyse** (alle 20 Zyklen, 4 Quellen):
  1. Kompetenz-Defizite (SelbstModell < 0.7)
  2. Ineffektive Strategien + Blinde Flecken (MetaKognition)
  3. Kontrafaktische Fehler-Haeufung (MentaleSimulation)
  4. Lernstagnation → Strategie-Wechsel
- **Zone der naechsten Entwicklung**: Uebungen nicht zu leicht (>0.2), nicht zu schwer (<0.7)
- **Adaptive Schwierigkeit**: Erfolgsrate >70% → schwerer, <30% → leichter (±0.1 pro Schritt)
- **Domaenen-Mapping**: 8 Domaenen × passende AktionsTypen (navigation→Bewegen/Drehen, physik→Werfen/Schieben/Ziehen, etc.)
- **Abschluss**: Lernziel gilt als gelernt bei Kompetenz-Delta ≥ 0.15, max 30 Uebungen pro Ziel
- **Integration**: Erzeugt Trainings-Ziele im ZielManager (niedrigere Prio als User-Ziele)
- **Persistenz**: selbst_curriculum.json

### Schritt 114: AGIKern.cs — Phase 26 Integration
- LangzeitPlaner + SelbstCurriculum: Felder, Initialisierung, Getter
- Schritt 11 PLANEN: Vor Detailplan → LangzeitPlaner versucht hierarchische Zerlegung
- Schritt 16f LANGZEIT-PLANER: Nach Grounding — Fortschritt pruefen + Umplanung triggern
- Schritt 16g SELBST-CURRICULUM: Uebung auswerten + neue Uebung generieren

### Schritt 115: ChatUI.cs — /langzeitplan + /curriculum Befehle
- `/langzeitplan status` — Aktiver Plan + Fortschritt + Meilensteine + Statistik
- `/langzeitplan meilensteine` — Alle Meilensteine mit Status-Symbolen
- `/langzeitplan historie` — Letzte 5 abgeschlossene/gescheiterte Plaene
- `/curriculum status` — Aktives Lernziel + Uebungen + Kompetenz-Delta
- `/curriculum ziele` — Alle Lernziele nach Prioritaet
- `/curriculum statistik` — Uebungen, Erfolgsrate, Kompetenz-Zuwachs

**Verifikation Phase 26:** (a) `/langzeitplan status` zeigt hierarchischen Plan mit Meilensteinen. (b) Agent beobachtet: Meilenstein-Fortschritt steigt. (c) Bei Stagnation: LLM formuliert Meilenstein um. (d) `/curriculum ziele` zeigt Schwachstellen-basierte Lernziele. (e) Adaptive Schwierigkeit: Erfolgreicher Agent bekommt schwerere Uebungen. (f) `/curriculum statistik` zeigt Kompetenz-Zuwachs ueber Zeit.

---

## Phase 27: Grounded Sprachproduktion + Erklaerbarkeit

### Schritt 116: GroundedSprachproduktion.cs — Sprache aus Erfahrung statt nur Text
Neues Sprachmodul, das Antworten mit erfahrungsgeerdeten Signalen anreichert:
- **Antwort-Veredelung**: Nach der LLM-/Lokalantwort werden geerdete Woerter aus dem Input erkannt und als sensorischer Bezug in die Antwort integriert
- **Weltbezug**: Wenn erwaehnte Objekte im Weltmodell existieren, werden Zustand + Position als situativer Kontext eingestreut
- **Simulationsbezug**: Optionaler naechster plausibler Schritt aus MentaleSimulation fuer handlungsnahe Antworten
- **Wort-Erklaerung**: Fuer ein Wort Grounding-Staerke + Beispiel-Erinnerung abrufbar
- **Entscheidungs-Erklaerung**: Kombiniert MentaleSimulation + PhysikIntuition zu einer knappen "Warum"-Begruendung
- **Persistenz**: grounded_sprachproduktion.json (Antworten veredelt, Erklaerungen, Durchschnitts-Grounding)

### Schritt 117: AGIKern.cs — Phase 27 Integration
- Neues Subsystem `GroundedSprachproduktion`: Feld, Initialisierung, Getter
- Neuer Zyklus-Schritt **12c GROUNDED SPRACHE** nach Weltmanipulation:
  - `antwort = groundedSprache.VeredleAntwort(input, antwort, zustandsVektor)`
  - damit Ausgaben zunehmend erfahrungsnah und erklaerbar statt rein textuell

### Schritt 118: ChatUI.cs — /sprache Befehle
- `/sprache status` — Veredelte Antworten, Wort-/Entscheidungs-Erklaerungen, Ø Grounding-Staerke
- `/sprache erklaere <Wort>` — Grounding-Staerke + erinnerungsbasiertes Beispiel fuer ein Wort
- `/sprache warum` — Erklaert die aktuelle Handlungsneigung aus Simulation + Physikintuition

### Schritt 119: Doku + Zyklusaktualisierung
- README: Feature-Liste, Projektstruktur, Chat-Befehle, Zyklusschritt 12c erweitert
- Verarbeitungszyklus zaehlt nun einen zusaetzlichen Sprach-Grounding-Schritt

**Verifikation Phase 27:** (a) Bei geerdeten Woertern im Input enthaelt die Antwort einen sensorischen Bezug. (b) `/sprache erklaere stein` zeigt Grounding-Staerke + Beispiel-Erinnerung. (c) `/sprache warum` nennt simulierte naechste Aktion und ggf. Stabilitaetskontext. (d) `/sprache status` zeigt steigende Anzahl veredelter Antworten.

---

## Stabilitaets-Refactor (zwischen Phase 27 und 28)

### Schritt 120: ZyklusStabilisator.cs — QoS gegen Latenzspitzen
- Misst Zykluszeiten (Avg, EMA, Max) und erkennt Warn-/Hard-Last
- Schaltet bei Lastspitzen auf sanfte Degradierung (teure Zusatzanalyse nur teilweise)
- Kernlogik bleibt intakt, nur optionale Antwortveredelung wird reduziert
- Persistenz: zyklus_stabilitaet.json

### Schritt 121: AGIKern.cs + ChatUI.cs — Sichere Degradierung + Transparenz
- AGIKern nutzt `ZyklusStabilisator` fuer QoS-Entscheidung in Schritt 12c
- GroundedSprachproduktion wird fehlertolerant (try/catch, keine Zyklusunterbrechung)
- Neuer Chat-Befehl `/perf status` zeigt aktuelle Zyklus-Stabilitaet

**Verifikation Stabilitaets-Refactor:** (a) `/perf status` zeigt EMA/Avg/Max plausibel steigend/fallend. (b) Unter Last nimmt `Reduktionen` zu, aber Antworten bleiben vorhanden. (c) Keine Exceptions stoppen den Zyklus.

---

## Phase 28 (Start): Missionsgetriebene Langlauf-Autonomie

### Schritt 122: AutonomieMissionen.cs — Session-Missionen
- Fuehrt autonome Missions-Sessions ueber mehrere Zyklen
- Wenn autonom und ohne aktive Ziele: kontrollierte Auto-Mission statt Leerlauf
- Missionen tracken Schritte + Durchschnittsbelohnung + Abschluss/Stop
- Curriculum kann Missionsfokus beeinflussen (Exploration vs. Lernfokus)
- Persistenz: autonomie_missionen.json

### Schritt 123: AGIKern.cs + ChatUI.cs — Integration Phase-28-Start
- Neuer Zyklusschritt **19b AUTONOMIE-MISSIONEN** nach Meta-Zielgenerierung
- Neue Befehle: `/mission status|an|aus|start <Text>|stop|historie`

**Verifikation Phase 28 Start:** (a) `/mission status` zeigt aktive/letzte Mission. (b) Im autonomen Modus ohne aktive Ziele startet nach Intervall automatisch eine Mission. (c) `/mission historie` zeigt die letzten Sessions mit ØBelohnung.

### Schritt 124: AutonomieMissionen.cs — Recovery + Missions-Empfehlungen
- **Stagnations-Recovery**: Bei negativer Belohnungsserie wird eine laufende Mission sauber beendet und durch Recovery-/Lernmission ersetzt
- **Empfehlungslogik**: `GetEmpfehlungText()` priorisiert Curriculum-Schwachstellen und schaltet bei schwacher letzter Performance auf Kalibrierungsmission
- **Startauto-API**: `StarteEmpfohleneMission()` startet die aktuelle Empfehlung direkt
- **Erweitertes Tracking**: Negative Serie + Recovery-Zaehler in Statistik

### Schritt 125: ChatUI.cs — Missionssteuerung erweitert
- Neue Befehle:
  - `/mission empfehlung` — zeigt aktuell empfohlene Missionsrichtung
  - `/mission startauto` — startet empfohlene Mission sofort
- `/mission status` zeigt nun zusaetzlich negative Serie und Recovery-Kontext

**Verifikation Phase 28 Ausbau:** (a) Bei laengerer negativer Belohnungsserie erscheint im Log eine Recovery-Meldung und neue Mission startet. (b) `/mission empfehlung` wechselt je nach Lernzielen/letzter Performance den Fokus. (c) `/mission startauto` erzeugt unmittelbar eine empfohlene Mission.

### Schritt 126: ARC-2 Eval-Pipeline — Messbarkeit statt Schaetzung
- Neues Modul `Arc2Evaluator.cs` mit Exakt-Match-Scoring pro Task
- Dateneingang ueber `Data/arc2_tasks.json` oder `Data/arc2/*.json`
- Persistenter Report `arc2_report_last.json` mit:
  - Exakt-Quote
  - JSON-Parse-Quote
  - Durchschnittszeit
  - LLM-Calls
  - Copy-Baseline-Quote
- ChatUI-Befehle: `/arc2 run [N]`, `/arc2 status`, `/arc2 report`

**Verifikation ARC-2 Pipeline:** (a) Demo-Task in `arc2_tasks.json` liefert einen auswertbaren Report. (b) `/arc2 status` zeigt Kennzahlen. (c) `/arc2 report` listet letzte Task-Ergebnisse inklusive Parse/Exakt.

### Schritt 127: Compile-Kompatibilitaetswelle (Schema-Harmonisierung)
- Rueckwaertskompatible Alias-Felder/Properties in zentralen Modellen (PhysikRegel, PlausibilitaetsErgebnis, MentalesModell, BenchmarkErgebnis, Aktion/Plan, Konzept, VAKOG, Emotionen, LatenterZustand, SozialeAnalyse)
- AGIConfig um fehlende Legacy-Parameter erweitert (`konzeptRevisionSchwelle`, `langzeit*`)
- Callsite-Fixes: asynchrone VAKOG-Aufrufe, Semantik-Degradation-Signatur, constructor-order Fix in AGIKern
- Runtime-Kompatibilitaet: LINQ-`TakeLast` Shim fuer Unity-Profile ohne native Implementierung
- Unity-API-Korrektur: `Rigidbody.velocity` statt `linearVelocity`

**Verifikation Schritt 127:** (a) Unity-Compilerfehler fuer fehlende Modellfelder/Signaturen fallen weg. (b) Legacy- und neue Module kompilieren gemeinsam. (c) `get_errors` bleibt gruen.

---

## Abhängigkeiten

```
Phase 1 (Fundament) ─→ Phase 2 (LLM) ─┬→ Phase 3 (VAKOG + Sensorik)
                                        ├→ Phase 4 (Unity-Welt)  ───────┐
                                        └→ Phase 6 (Sozial + ToM)      │
                                                                        ├→ Phase 7 (Gedächtnis)
Phase 3 ────────────────────────────────────────────────────────────────┘      │
Phase 5 (Physik, braucht Unity-Welt) ──────────────────────────→ Phase 7     │
                                                                        ├→ Phase 8 (Meta + Emotionen + Zeit + Kreativität)
                                                                        │      │
                                                                        └→ Phase 9 (Intentionalität)
                                                                               │
                                                                        Phase 10 (Narratives Selbst)
                                                                               │
                                                                        Phase 11 (AGI-Kern + UI)
                                                                               │
                                                                        Phase 12 (Tests)
                                                                               │
                                                                        Phase 13 (Abnahme/Gates)
```

Kritischer Pfad: Phase 1 → 2 → 4 (Unity-Welt) → 5 (Physik) → 7 → 8 → 9 → 10 → 11 → 12 → 13

---

## Technische Entscheidungen

- **Alles C# in Unity** — kein Python, kein Bridge-Layer, ein Prozess
- **Unity 2022.3+ LTS** mit URP + ProBuilder
- **Anthropic REST API** via HttpClient (async/await) — oder jeder OpenAI-kompatible Anbieter
- **ChromaDB/Qdrant REST** für Vektor-Suche (externer Dienst)
- **MathNet.Numerics** für Bio-Simulation (Lotka-Volterra, SIR, etc.)
- **QuikGraph** oder eigene Klasse für Kausalgraph
- **Newtonsoft.Json** für Serialisierung
- **NavMesh** für Agent-Navigation
- **BDI-Architektur** für Intentionalität
- **HTN-Planung** für Handlungspläne
- **ScriptableObjects** für Config
- **In-Game UI** statt CLI (Canvas + Chat + Overlays)
- **Bewusstsein**: Nicht Ziel, nicht ausgeschlossen
- **Hermeneutischer Zirkel / KonzeptRevision**: Alle Wissensstrukturen sind revidierbar. Konzepte konvergieren iterativ durch Erfahrung. Rückpropagation bei Drift.
- **LLM primär Ein-/Ausgabe** + lokaler SemantikKernel für Routinefälle (Fallback bei Ausfall)
- **Punkt 1 (so weit möglich)**: Kein eigenes Foundation-Training, aber eigene interne Semantikrepräsentation und lokale Entscheidungslogik
- **Theory of Mind**: False Belief, mentale Modelle anderer Entitäten
- **Funktionale Emotionen**: 6 Emotionen modulieren Entscheidungen (kein Selbstzweck)
- **Temporales Reasoning**: Dauer, Sequenz, temporale Kausalität, Deadlines
- **Kompositionelle Konzeptschöpfung**: Verschmelzen/Spalten/Erfinden von Konzepten
- **One-Shot-Lernen**: Dramatische Erfahrungen sofort als Regel
- **Narratives Selbst**: Autobiographisches Gedächtnis mit Entwicklungsphasen
- **Funktionale Kreativität (Punkt 5)**: Divergenz + Konvergenz, bewertet über Novelty/Utility/Plausibilität
- **Subsymbolik (realisierbar)**: Latente Zustandsräume ergänzen symbolische Regeln, kein End-to-End-Training nötig
- **Reinforcement Learning**: Tabular Q-Learning ODER Deep Q-Network (DQN, reines C# MLP 20→64→32→17) — zuschaltbar via Config
- **ML-Clustering**: K-Means++ fuer episodische Instanzen, Aehnlichkeitssuche ohne LLM
- **Dezentrale Mikroagenten**: Aktivierungsbasiert, Blackboard-Kommunikation, emergentes Verhalten
- **Meta-Kognition**: Strategie-Tracking, Lernkurven-Analyse, Bias-Erkennung, Pipeline-Empfehlungen
- **Iteratives Reasoning**: Chain-of-Thought + Selbstkritik-Schleifen, konfigurierbare Iterationstiefe
- **Prediktives Weltmodell**: Imagination-basierte Planung (MLP 37→64→32→21), evaluiert Aktionen vorab, zuschaltbar/deaktivierbar
- **Arbeitsgedächtnis**: Strukturierter 11-Sektionen-Kontextbuffer, Token-Budget-bewusst, ersetzt manuellen String-Aufbau
- **Langzeitlernen**: Priorisierung, kontrolliertes Vergessen, Driftmonitor für Dauerbetrieb
- **Weltmodell-Konsistenz**: Automatische Fehlererkennung und sichere Reparatur
- **Robustheit**: Degradationsmodi + Recovery statt Komplettausfall bei API-Störungen
- **Evaluation-Gates**: Benchmark-Suite mit KPI-Regression vor jedem Release
- **Sicherheit**: Max. autonome Schritte, Notbremse, keine Selbsterhaltungsziele
- **Multi-Provider LLM**: Anthropic + OpenAI-kompatibel (LM Studio, Ollama, Groq, etc.) via `LLMAnbieter`-Enum
- **OpenAI-kompatibler API-Server**: Internes HTTP-Gateway (`/v1/chat/completions`), erlaubt externen Benchmarks (z.B. ARC) das AGI-System wie ein LLM anzusprechen. Flow: API-Request → AGI-Zyklus (18 Schritte) → API-Response

## Geschätzte Größenordnung

- ~85 C#-Scripts + 12 JSON-Datendateien + Config-ScriptableObject
- ~21.000–28.000 Zeilen C#-Code
- ~3.500–6.000 Zeilen JSON-Daten
- ~30–50 Prefabs
- 1 Unity-Szene (HauptWelt)
- 18-Schritte-Verarbeitungszyklus (erweitert: 8b/8c/8d/10b/15b/15c) + Autonomer Modus
- 44 Qualitätskriterien
- 17 Phasen, 75+ Schritte (inkl. 5b/5c/5d/14b/31b/35b/38b/53)
- OpenAI-kompatibler API-Server (Port 8741)
- DQN (reines C# MLP), Prediktives Weltmodell, Iteratives Reasoning, Arbeitsgedächtnis
- Automatisiertes 6-Phasen-Kurrikulum-Training
