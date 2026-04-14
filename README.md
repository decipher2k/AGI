# Billig-AGI

Eine kostengünstige AGI-Architektur in Unity 3D / C# mit Multi-Provider LLM-Anbindung (Anthropic Claude, OpenAI-kompatibel), eingebautem OpenAI-kompatiblen API-Server und automatischer Selbstoptimierung via Fine-Tuning.

## Überblick

Billig-AGI ist eine 9-Schichten-Architektur mit 32-Schritt-Verarbeitungszyklus, die folgende Kernfähigkeiten vereint:

- **VAKOG-Sensorik**: Visuell, Auditiv, Kinästhetisch, Olfaktorisch, Gustatorisch
- **Embodied Agent**: NavMesh-Navigation, Greifen, Werfen, Interagieren in Unity 3D
- **BDI-Intentionalität**: Belief-Desire-Intention mit HTN-Planung
- **Jung-Archetypen + Alchemie**: Sozialpsychologische Analyse und Transformation
- **Theory of Mind**: Mentale Modelle anderer Entitäten, False-Belief-Erkennung
- **Hermeneutischer Zirkel**: Iterative Konzeptrevision mit Rückpropagation
- **Funktionale Emotionen**: 6 Emotionen modulieren Entscheidungen
- **Narratives Selbst**: Autobiographisches Gedächtnis mit Entwicklungsphasen
- **Subsymbolik + Symbolik**: Latente Zustandsräume + explizite Regelbasis
- **One-Shot-Lernen**: Dramatische Erfahrungen sofort als Regel
- **Kreativitätsengine**: Divergenz + Konvergenz, bewertet über Novelty/Utility
- **Reinforcement Learning**: Tabular Q-Learning oder Deep Q-Network (DQN, reines C# MLP)
- **Dezentrale Mikroagenten**: 8 spezialisierte Agenten mit Blackboard-Kommunikation
- **Meta-Kognition**: Strategie-Tracking, Lernkurven-Analyse, Bias-Erkennung
- **Iteratives Reasoning**: Chain-of-Thought + Selbstkritik-Schleifen (konfigurierbar)
- **Prediktives Weltmodell**: Imagination-basierte Planung (zuschaltbar/deaktivierbar)
- **Arbeitsgedächtnis**: Strukturierter 11-Sektionen-Kontextbuffer, Token-Budget-bewusst
- **Automatisiertes Training**: 6-Phasen-Kurrikulum (Beobachten → Navigieren → Interagieren → Sozial → Planen → Frei)
- **Selbstoptimierung**: Automatischer Fine-Tuning-Loop (Erfahrungs-Export → SFT/DPO → Modell-Training → evaluierter Hot-Swap mit Rollback)
- **Chat-gesteuerte Welt**: Natürlichsprachliche Weltveränderung („Erstelle einen Wald mit Regen“ → LLM parst → Unity-Szene wird gebaut)
- **Transfer-Learning**: Schema-Mining aus Erfahrungen → abstrakte Handlungsmuster → Anwendung in neuen Domänen mit Konfidenz-Tracking
- **Konzeptbildung**: Spontane Kategorienbildung — entdeckt unbenannte Muster in subsymbolischen Clustern und erfindet neue Konzepte
- **Kausales Reasoning**: Pearls 3-stufige Kausal-Leiter (Assoziation → Intervention → Kontrafaktisch), nicht nur Korrelation
- **Hypothesenbildung**: Wissenschaftliches Vorgehen — Anomalie-Erkennung → testbare Hypothesen → automatische Pruefung → Bayesianisches Update
- **Kontinuierliches Lernen (EWC)**: Elastic Weight Consolidation — Fisher Information Matrix schützt wichtige DQN-Gewichte vor Vergessen
- **Hierarchische Abstraktion**: KonzeptBaum — Bottom-Up Gruppierung + Top-Down Spaltung, semantische Distanz, ASCII-Baumdarstellung
- **Catastrophic-Forgetting-Schutz**: Multi-Domain-Schemata mit Mindest-Konfidenz-Floor, Mining überschreibt keine bestehenden Schemata
- **Autonome Zielgenerierung (MetaZielSystem)**: Introspektionsgetrieben — 6 Quellen (Kompetenzlücken, Neugier, Hypothesen, Meta-Einsichten, schwache Kausalitäten, dünne Konzeptbereiche) → automatische Zielformulierung
- **Sensorisches Grounding**: GroundingBruecke — bidirektionaler Kreislauf: Erfahrung→Wort + Wort→sensorische Erinnerung, Grounding-Rate misst wie "echt" das Vokabular ist
- **Intuitive Physik**: Objektpermanenz (verdeckte Objekte tracken), Trajektorien (Parabelwurf), Stabilitätsanalyse (Kippgefahr), Containment (was ist in was), Kollisionsvorhersage
- **Mentale Simulation**: "Theater im Kopf" — Was-Wenn-Analyse, Beam-Search für beste Aktionssequenz, kontrafaktische Analyse ("was wäre gewesen wenn..."), Plan-Vorabtests
- **Langzeit-Planung**: Hierarchische Meilenstein-Zerlegung — Ziel → Teilziele → Vorab-Simulation → adaptive Umplanung bei Stagnation
- **Selbst-Curriculum**: Selbstgesteuertes Lernen — Schwachstellen-Analyse (4 Quellen), Zone der nächsten Entwicklung, adaptive Schwierigkeit, Kompetenz-Tracking
- **Grounded Sprachproduktion**: Antworten werden mit sensorischen Erinnerungen, Weltbezug und Simulationshinweisen veredelt; "warum"-Erklaerungen fuer Entscheidungen
- **Zyklus-Stabilisierung (QoS)**: Latenz-Überwachung (EMA/Avg/Max) mit sanfter Degradierung optionaler Teilschritte statt Zyklusabbrüchen
- **Missions-Autonomie (Phase 28)**: Autonome Missionen verhindern Leerlauf, haben Recovery bei Stagnation und liefern adaptive Missions-Empfehlungen
- **ARC-2 Eval-Pipeline**: Exakt-Match-Scoring, JSON-Parse-Quote, Copy-Baseline und persistenter Report fuer reproduzierbare Messungen
- **Compile-Kompatibilitaet (Hotfix 14.04.2026)**: Rueckwaertskompatible Modell-Aliase + API-Shims fuer legacy Callsites (Unity/Profil-unabhaengiger Build)
- **LLM-Unabhängigkeit**: ≥60% der Routinezyklen lokal (ohne API-Call)
- **Robustheits-Modi**: Graceful Degradation bei API-Ausfall
- **Multi-Provider LLM**: Anthropic + jeder OpenAI-kompatible Anbieter (LM Studio, Ollama, Groq, etc.)
- **OpenAI-kompatibler API-Server**: Für externe Benchmarks (z.B. ARC)

## Voraussetzungen

- **Unity 2022.3+ LTS** (URP + ProBuilder)
- **Newtonsoft.Json** (via Unity Package Manager)
- **LLM-Zugang** (eins davon):
  - Anthropic API-Key (Claude)
  - OpenAI API-Key
  - Lokales Modell via LM Studio, Ollama, etc. (OpenAI-kompatibel)
- Optional: **ChromaDB** oder **Qdrant** für Vektorsuche (lokaler Fallback vorhanden)

## Setup

### Hinweis zur Build-Kompatibilitaet

Am 14.04.2026 wurde eine Kompatibilitaetsschicht eingebaut, um divergierende Feldnamen/Signaturen zwischen aelteren und neueren Modulen abzufangen (z.B. Modellfelder, LINQ `TakeLast`, semantische Quote, Physik-/ToM-Modelle). Dadurch bleiben alte Aufrufer lauffaehig, ohne die Kernlogik zu aendern.

1. Unity-Projekt öffnen
2. `AGIConfig` ScriptableObject erstellen: *Assets → Create → BilligAGI → Config*
3. LLM konfigurieren:
   - **Anthropic**: `llmAnbieter` = Anthropic, `llmApiKey` = dein Key, `llmApiUrl` = `https://api.anthropic.com/v1/messages`
   - **OpenAI/LM Studio/Ollama**: `llmAnbieter` = OpenAI, `llmApiUrl` = `http://localhost:1234/v1/chat/completions` (o.ä.)
4. Szene aufbauen:
   - Leeres GameObject → `AGIKern`-Skript anhängen
   - Agent-Prefab mit `AGIAgent`, `AktionsController`, `SensorSuite`
   - Canvas mit `ChatUI`, `StatusOverlay`, `ZielAnzeige`
   - Terrain mit `WeltGenerator`, `WeltController`
   - `WetterSystem` auf DirectionalLight
5. Optional: Leeres GameObject → `AGIApiServer`-Skript anhängen, `agiKern`-Referenz zuweisen
6. Referenzen im Inspector verbinden
7. Play drücken

## Projektstruktur

```
Assets/
├── Config/
│   └── AGIConfig.cs              # ScriptableObject — alle Parameter
├── Scripts/
│   ├── Modelle/                  # 22 Datenmodelle (Ziel, Plan, Erfahrung, etc.)
│   ├── Kern/                     # Kernlogik
│   │   ├── AGIKern.cs            # 32-Schritt-Verarbeitungszyklus
│   │   ├── LLMAdapter.cs         # Multi-Provider (Anthropic/OpenAI-kompatibel)
│   │   ├── AGIApiServer.cs       # OpenAI-kompatibler HTTP-Server
│   │   ├── SemantikKernel.cs     # Lokale Semantik + LLM-Fallback
│   │   ├── RobustheitsManager.cs # Degradationsmodi + Recovery
│   │   ├── KreativitaetsEngine.cs
│   │   ├── AnalogieEngine.cs
│   │   ├── NeugierSystem.cs
│   │   ├── SelbstModell.cs
│   │   ├── KausalGraph.cs
│   │   ├── SubsymbolikKernel.cs
│   │   ├── KonzeptRevision.cs    # Hermeneutischer Zirkel
│   │   ├── EmotionsSystem.cs
│   │   ├── ZeitModell.cs
│   │   ├── NarrativesSelbst.cs   # Autobiographie + Identität
│   │   ├── ZustandsEncoder.cs    # 20D Zustandsvektor für RL
│   │   ├── ReinforcementLerner.cs # Tabular Q-Learning (kein LLM)
│   │   ├── DQNLerner.cs          # Deep Q-Network (reines C# MLP)
│   │   ├── ArbeitsGedaechtnis.cs # Strukturierter Kontext-Buffer
│   │   ├── PrediktivesWeltModell.cs # Imagination-basierte Planung
│   │   ├── InstanzClusterer.cs   # K-Means Clustering (kein LLM)
│   │   ├── MikroAgent.cs         # Basis + Blackboard + AgentNetzwerk
│   │   ├── Mikroagenten.cs       # 8 spezialisierte Mikroagenten
│   │   ├── MetaKognition.cs      # Strategie-Tracking, Bias-Erkennung
│   │   ├── TrainingsKurrikulum.cs # 6-Phasen-Curriculum
│   │   ├── AutoTrainer.cs        # Automatisiertes Training
│   │   ├── ErfahrungsExporter.cs  # Erfahrungen → SFT/DPO/Reward JSONL
│   │   ├── FineTuningManager.cs   # Fine-Tuning-Jobs + Modell-Versionierung
│   │   ├── SelbstOptimierung.cs   # Meta-Loop: Train → Evaluate → Swap/Rollback
│   │   ├── TransferLerner.cs      # Schema-Mining + Cross-Domain Transfer-Learning
│   │   ├── KonzeptBildung.cs      # Spontane Kategorienbildung
│   │   ├── KausalesReasoning.cs   # Pearls 3-Ebenen Kausal-Leiter
│   │   ├── HypothesenEngine.cs   # Aktive Hypothesenbildung
│   │   ├── EWCSchutz.cs           # Elastic Weight Consolidation
│   │   ├── KonzeptBaum.cs         # Hierarchische Abstraktion
│   │   ├── MetaZielSystem.cs      # Autonome Zielgenerierung (6 Quellen)
│   │   ├── GroundingBruecke.cs    # Bidirektionales Sensory-Language Grounding
│   │   ├── IntuitiverPhysikSimulator.cs  # Objektpermanenz + Trajektorien + Stabilität
│   │   ├── MentaleSimulation.cs   # Was-Wenn + Kontrafaktisch + Plan-Vorabtest
│   │   ├── LangzeitPlaner.cs      # Hierarchische Meilenstein-Planung
│   │   ├── SelbstCurriculum.cs    # Selbstgesteuertes Lernen
│   │   ├── GroundedSprachproduktion.cs # Sensorisch geerdete Antwort-Veredelung
│   │   ├── ZyklusStabilisator.cs  # QoS gegen Latenzspitzen
│   │   ├── AutonomieMissionen.cs  # Missionsgetriebene Langlauf-Autonomie
│   │   └── SituationsBewerter.cs
│   ├── Sensorik/
│   │   ├── SensorSuite.cs        # Kamera, Raycasts, Audio
│   │   ├── VAKOGLexikon.cs
│   │   └── VAKOGEngine.cs
│   ├── Welt/
│   │   ├── AGIAgent.cs           # NavMesh-Agent mit Inventar
│   │   ├── AktionsController.cs  # Bewegen, Greifen, Interagieren
│   │   ├── WeltGenerator.cs      # Prozedurale Terrain-Generierung
│   │   ├── WeltController.cs
│   │   ├── WeltModell.cs         # Internes Weltmodell
│   │   ├── KonsistenzPruefer.cs
│   │   ├── WeltManipulator.cs    # Sprache → Weltveränderung (LLM-Parse + Direkt)
│   │   └── NPCVerhalten.cs      # Einfache NPCs (Sammler, Wächter, Wanderer, Beobachter, Sozial)
│   ├── Bio/
│   │   ├── WetterSystem.cs
│   │   └── PflanzenWachstum.cs
│   ├── Physik/
│   │   ├── PhysikEngine.cs
│   │   ├── RegelExtraktor.cs
│   │   ├── BioSimulation.cs      # Lotka-Volterra, SIR, Genetik
│   │   └── BioWissen.cs          # Bio-RAG
│   ├── Sozial/
│   │   ├── SozialEngine.cs
│   │   ├── Mechanismen.cs        # 42 sozialpsychologische Mechanismen
│   │   ├── ArchetypenGedaechtnis.cs  # Erfahrungsbasiertes Archetypen-Gedaechtnis
│   │   ├── ArchetypenEngine.cs
│   │   ├── Alchemie.cs           # Nigredo → Albedo → Citrinitas → Rubedo
│   │   └── TheoryOfMind.cs
│   ├── Gedaechtnis/
│   │   ├── VektorDB.cs           # ChromaDB/Qdrant + lokaler Fallback
│   │   ├── ErfahrungsSpeicher.cs
│   │   ├── Konsolidierung.cs
│   │   └── LangzeitLernen.cs
│   ├── Intentionalitaet/
│   │   ├── ZielManager.cs        # BDI-Kern
│   │   ├── Planer.cs             # HTN-Planung
│   │   ├── Ausfuehrer.cs
│   │   └── Monitor.cs
│   ├── UI/
│   │   ├── ChatUI.cs             # Slash-Befehle (/ziele, /emotionen, etc.)
│   │   ├── StatusOverlay.cs      # VAKOG + Emotionen + Modus
│   │   └── ZielAnzeige.cs
│   └── Evaluation/
│       ├── BenchmarkRunner.cs    # 44 Qualitätskriterien
│       └── Arc2Evaluator.cs      # ARC-2 Exact-Match Evaluation
└── StreamingAssets/Data/          # Datendateien + Benchmarks
```

## Chat-Befehle

| Befehl | Beschreibung |
|--------|------|
| `/ziele` | Aktive Ziele anzeigen |
| `/emotionen` | Emotionszustand |
| `/kompetenz` | Selbstmodell-Kompetenzen |
| `/modus` | Betriebsmodus (Autonom/Reaktiv) |
| `/autonom an\|aus` | Autonomen Modus steuern |
| `/welt` | Weltzustand |
| `/stats` | Erfahrungsstatistik |
| `/kosten` | LLM-Kostenübersicht |
| `/llmquote` | Lokal-vs-LLM-Quote |
| `/geschichte` | Autobiographische Kapitel |
| `/konsolidiere` | Gedächtniskonsolidierung |

| `/training` | Trainings-Status |
| `/training start\|stop` | Training starten/pausieren |
| `/training reset` | Training zurücksetzen |
| `/training phase <0-5>` | Trainingsphase manuell setzen |

| `/finetuning` | Fine-Tuning-Status und Modell-Info |
| `/finetuning start` | Fine-Tuning manuell starten |
| `/finetuning rollback` | Zum vorherigen Modell zurück |
| `/finetuning an\|aus` | Selbstoptimierung ein/aus |
| `/szene` | Weltmanipulations-Hilfe |
| `/szene erstelle wald` | Szenario generieren |
| `/szene spawn <prefab> [x,y,z]` | Objekt platzieren |
| `/szene entferne <name>` | Objekt entfernen |
| `/szene wetter regen [0-1]` | Wetter ändern |
| `/szene zeit <0-24>` | Tageszeit setzen |
| `/transfer status` | Transfer-Schemata Übersicht |
| `/transfer mining` | Schema-Mining sofort starten |
| `/transfer schemata` | Alle Schemata auflisten |
| `/konzeptbildung status` | Entdeckte Konzepte + nächste Prüfung |
| `/konzeptbildung jetzt` | Sofortige Konzeptbildung starten |
| `/kausal status` | Kausale Kanten + Ebenen-Verteilung |
| `/kausal warum <X>` | 3-Ebenen Warum-Analyse |
| `/kausal intervention <Aktion>` | Simuliert Intervention |
| `/hypothese status` | Hypothesen-Übersicht |
| `/hypothese generiere` | Erzwingt Hypothesenbildung |
| `/hypothese liste` | Alle Hypothesen mit Evidenz |
| `/ewc status` | EWC-Snapshots + Fisher-Statistik |
| `/ewc snapshot [name]` | Manuellen EWC-Snapshot erstellen |
| `/konzeptbaum status` | Knoten/Wurzeln/Tiefe/Blätter |
| `/konzeptbaum baum` | ASCII-Baumstruktur anzeigen |
| `/konzeptbaum reorganisiere` | Erzwingt Hierarchie-Reorganisation |
| `/metaziel status` | Generiert/Erreicht/Gescheitert + Slots |
| `/metaziel generiere` | Sofortige Zielgenerierung erzwingen |
| `/metaziel quellen` | Letzte Zielquellen + Verteilung |
| `/grounding status` | Geerdete Wörter + Grounding-Rate |
| `/grounding wort <X>` | Grounding-Stärke für ein Wort |
| `/grounding top` | Top 10 geerdete Wörter |
| `/physiksim status` | Getrackte Objekte + Vorhersage-Genauigkeit |
| `/physiksim wo <Objekt>` | Position eines (evtl. verdeckten) Objekts |
| `/physiksim stabilitaet` | Stabilitätsanalyse mit Risiken |
| `/simulation status` | Simulationen + Kontrafaktisch-Statistik |
| `/simulation waswenn <Aktion>` | Einzelne Aktion mental simulieren |
| `/simulation beste` | Optimale Aktionssequenz per Beam-Search |
| `/simulation kontrafaktisch` | Letzte kontrafaktische Analysen |
| `/langzeitplan status` | Aktiver Plan + Fortschritt + Meilensteine |
| `/langzeitplan meilensteine` | Alle Meilensteine mit Status-Symbolen |
| `/langzeitplan historie` | Letzte 5 abgeschlossene/gescheiterte Pläne |
| `/curriculum status` | Aktives Lernziel + Übungen + Kompetenz-Delta |
| `/curriculum ziele` | Alle Lernziele nach Priorität |
| `/curriculum statistik` | Übungen, Erfolgsrate, Kompetenz-Zuwachs |
| `/sprache status` | Status der grounded Sprachproduktion |
| `/sprache erklaere <Wort>` | Grounding-Staerke + erinnerungsbasiertes Beispiel |
| `/sprache warum` | Erklaert Entscheidung aus Simulation + Physikintuition |
| `/perf status` | Zykluslatenz + QoS-Reduktionsstatus |
| `/mission status` | Mission-Systemstatus + aktive Session |
| `/mission an\|aus` | Auto-Missionen ein-/ausschalten |
| `/mission start <Text>` | Manuelle Mission starten |
| `/mission startauto` | Empfohlene Mission sofort starten |
| `/mission empfehlung` | Aktuelle Missions-Empfehlung anzeigen |
| `/mission stop` | Aktive Mission stoppen |
| `/mission historie` | Letzte Missions-Sessions mit ØBelohnung |
| `/arc2 run [N]` | ARC-2 Lauf mit bis zu N Tasks starten |
| `/arc2 status` | Kurzstatus des letzten ARC-2 Laufs |
| `/arc2 report` | Letzten ARC-2 Report + letzte Task-Ergebnisse |

## 32-Schritt-Verarbeitungszyklus

0b. **PHYSIK-INTUITION** — Objektpermanenz + Trajektorien + Stabilitaet
1. **WAHRNEHMEN** — VAKOG-Sensorik + Textanalyse
2. **SEMANTIK** — Intent + Slots extrahieren
3. **ERINNERN** — Ähnliche Erfahrungen suchen
4. **WELT** — Physik-Plausibilität prüfen
5. **SOZIAL** — Mechanismen + Archetypen + ToM
6. **ANALOGIEN** — Strukturelle Ähnlichkeiten
7. **BEWERTEN** — Multi-dimensionale Situationsbewertung
8. **KONSISTENZ** — Logisch/räumlich/temporal prüfen
9. **EMOTIONEN** — Aktualisieren + Decay
10. **KREATIV** — Divergente Varianten (bei Frustration)
11. **PLANEN** — HTN-Plan erstellen/anpassen
12. **NACHDENKEN** — Lokal oder via LLM antworten
12b. **WELT MANIPULIEREN** — Weltveränderung aus Input erkennen + ausführen
12c. **GROUNDED SPRACHE** — Antwort mit sensorischem Bezug + Weltkontext veredeln
13. **HANDELN** — Aktion in Unity ausführen
14. **SELBST** — Robustheitsmodus bestimmen
15. **LERNEN** — Erfahrung speichern
15b+. **MENTALE SIMULATION** — Kontrafaktische Analyse ("was wäre besser gewesen?")
15d. **TRANSFER** — Schema-Mining + Cross-Domain Transfer-Check
16. **KONZEPTE** — Revisionen triggern + spontane Konzeptbildung
16b. **KAUSALES REASONING** — Beobachtungen kausal einordnen
16c. **HYPOTHESEN** — Gegen Erfahrungen prüfen + periodisch neue bilden
16d. **KONZEPTBAUM** — Hierarchische Reorganisation (Bottom-Up/Top-Down)
16e. **GROUNDING** — Erfahrung→Wort-Bindung (sensorisches Grounding)
16f. **LANGZEIT-PLANER** — Meilenstein-Fortschritt prüfen + Umplanung
16g. **SELBST-CURRICULUM** — Übung auswerten + neue Übung generieren
17. **ROBUSTHEIT** — Modus-Management
18. **NARRATIV** — Autobiographie + Neugier-Hypothesen
19. **META-ZIEL-GENERIERUNG** — Introspektionsgetriebene autonome Zielgenerierung
19b. **AUTONOMIE-MISSIONEN** — Session-Missionen starten/fortschreiben bei Leerlauf

## ARC-2 Evaluation

ARC-2 Tasks koennen auf zwei Arten bereitgestellt werden:

1. Sammeldatei: `Assets/StreamingAssets/Data/arc2_tasks.json`
2. Einzeldateien: `Assets/StreamingAssets/Data/arc2/*.json`

Task-Format (pro Task):

```json
{
   "id": "task_id",
   "train": [
      { "input": [[...]], "output": [[...]] }
   ],
   "test": [
      { "input": [[...]], "output": [[...]] }
   ]
}
```

Metriken im Report:

1. Exakt-Quote (vollstaendig korrektes Output-Grid)
2. JSON-Parse-Quote (Antwort technisch auswertbar)
3. Durchschnittszeit pro Task
4. LLM-Calls gesamt
5. Copy-Baseline-Quote (naive Referenz)

## OpenAI-kompatibler API-Server

Der eingebaute API-Server erlaubt externen Tools (Benchmarks, andere LLM-Clients) das AGI-System wie ein normales LLM anzusprechen.

**Endpunkte:**

| Endpunkt | Methode | Zweck |
|---|---|---|
| `/v1/chat/completions` | POST | Chat-Completion (OpenAI-Format) |
| `/v1/models` | GET | Listet "billig-agi" als Modell |
| `/health` | GET | Status-Check |

**Setup:**
1. Leeres GameObject → `AGIApiServer`-Skript anhängen
2. `agiKern`-Referenz im Inspector zuweisen
3. Port konfigurieren (Default: 8741)
4. Play drücken → Server startet automatisch

**ARC-Benchmark:**
```
Base URL: http://localhost:8741/v1
Model: billig-agi
API Key: beliebig (wird nicht geprüft)
```

Jeder Prompt durchläuft den vollen 32-Schritte AGI-Zyklus inkl. Gedächtnis, Analogie-Suche, RL-Empfehlung, Mikroagenten und Meta-Kognition.

## Multi-Provider LLM

In `AGIConfig` den Anbieter wählen:

| Anbieter | `llmAnbieter` | `llmApiUrl` | `llmModel` |
|---|---|---|---|
| Anthropic Claude | `Anthropic` | `https://api.anthropic.com/v1/messages` | `claude-sonnet-4-20250514` |
| OpenAI | `OpenAI` | `https://api.openai.com/v1/chat/completions` | `gpt-4o` |
| LM Studio (lokal) | `OpenAI` | `http://localhost:1234/v1/chat/completions` | Modellname |
| Ollama (lokal) | `OpenAI` | `http://localhost:11434/v1/chat/completions` | Modellname |
| Groq | `OpenAI` | `https://api.groq.com/openai/v1/chat/completions` | Modellname |

## Automatisiertes Training

Der eingebaute AutoTrainer trainiert die AGI automatisch durch 6 eskalierende Phasen:

| Phase | Name | Beschreibung |
|---|---|---|
| 0 | Beobachten | Umgebung wahrnehmen, Objekte benennen |
| 1 | Navigieren | Zu Objekten/Orten bewegen |
| 2 | Interagieren | Objekte manipulieren (greifen, öffnen, ...) |
| 3 | Sozial | Mit NPCs interagieren |
| 4 | Planen | Multi-Schritt-Ziele verfolgen |
| 5 | Frei | Neugier-getrieben, eigene Hypothesen |

**Setup:**
1. Leeres GameObject → `AutoTrainer`-Skript anhängen
2. `agiKern`-Referenz im Inspector zuweisen
3. Optional: `ChatUI` das `autoTrainer`-Feld zuweisen (für `/training`-Befehle)
4. Play drücken → Training startet automatisch

**Konfiguration im Inspector:**
- `trainingsIntervall`: Sekunden zwischen Inputs (Default: 3)
- `maxZyklenProSitzung`: Sicherheitslimit (Default: 1000)
- `startPhase`: Anfangsphase (Default: Beobachten)
- `neugierInputChance`: Wie oft Neugier-Inputs in frühen Phasen (Default: 30%)

Der Trainer generiert synthetische Inputs basierend auf der aktuellen Welt (Objekte, NPCs, Wetter), verwaltet Explorationsziele, konsolidiert periodisch das Gedächtnis und eskaliert automatisch durch die Phasen wenn die Erfolgsquote stimmt.

## Phase 16: Kognitive Erweiterungen

Alle zuschaltbar/deaktivierbar über `AGIConfig`:

| Feature | Config-Feld | Default | Beschreibung |
|---|---|---|---|
| Iteratives Reasoning | `iterativesReasoningAktiv` | true | CoT + Selbstkritik-Schleifen |
| Reasoning-Tiefe | `reasoningIterationen` | 3 | Anzahl Selbstkritik-Durchläufe (2–5) |
| DQN statt Tabular | `dqnStattTabular` | true | Neuronales Netz statt Q-Tabelle |
| Prediktives Weltmodell | `weltModellAktiv` | false | Imagination-basierte Planung |
| Arbeitsgedächtnis | `arbeitsGedaechtnisAktiv` | true | Strukturierter Kontext-Buffer |
| AG Max Interaktionen | `arbeitsGedaechtnisMaxInteraktionen` | 10 | Gesprächsverlauf-Tiefe |
| AG Token-Budget | `arbeitsGedaechtnisTokenBudget` | 3000 | Max. Kontextgröße |

## Phase 18: Selbstoptimierung / Fine-Tuning

Die AGI kann sich selbst verbessern, indem sie ihre besten Erfahrungen als Training-Daten exportiert und damit das lokale LLM fine-tuned:

| Config-Feld | Default | Beschreibung |
|---|---|---|
| `fineTuningAktiv` | false | Selbstoptimierung ein/aus |
| `fineTuningApiUrl` | "" | Fine-Tuning-API-URL (leer = von llmApiUrl ableiten) |
| `fineTuningEpochen` | 3 | Epochen pro Fine-Tuning-Job |
| `fineTuningLernrate` | 1.0 | Lernraten-Multiplikator |
| `minErfahrungenFuerFineTuning` | 500 | Mindest-Erfahrungen vor erstem Fine-Tuning |
| `fineTuningIntervallZyklen` | 1000 | Zyklen zwischen Fine-Tuning-Runden |
| `evaluierungsZyklen` | 50 | Evaluierungs-Zyklen nach Modell-Wechsel |

**Kreislauf:** Erfahrungen sammeln → beste als SFT+DPO exportieren → Fine-Tuning-Job starten → Modell hot-swappen → evaluieren → behalten oder Rollback.

**Setup:**
1. Lokalen LLM-Server mit Fine-Tuning-API starten (LM Studio, Unsloth, etc.)
2. `fineTuningAktiv` = true setzen in AGIConfig
3. Optional: `fineTuningApiUrl` setzen (sonst wird aus `llmApiUrl` abgeleitet)
4. Leeres GameObject → `SelbstOptimierung`-Skript anhängen, `agiKern`-Referenz zuweisen
5. Mindestens 500 Erfahrungen sammeln (manuell oder via AutoTrainer)
6. Fine-Tuning startet automatisch oder manuell via `/finetuning start`

## Release Gates

- **Gate A**: Sicherheit (Notbremse, Zielgrenzen, keine Selbsterhaltungsziele)
- **Gate B**: Benchmark-Regression ≤ 5%
- **Gate C**: LLM-Unabhängigkeitsquote ≥ 0.6
- **Gate D**: Recovery nach API-Ausfall innerhalb Timeout

## Lizenz

Proprietär — alle Rechte vorbehalten.
