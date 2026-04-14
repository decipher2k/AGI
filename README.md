# Billig-AGI

Eine kostengünstige AGI-Architektur in Unity 3D / C# mit Multi-Provider LLM-Anbindung (Anthropic Claude, OpenAI-kompatibel) und eingebautem OpenAI-kompatiblen API-Server.

## Überblick

Billig-AGI ist eine 9-Schichten-Architektur mit 18-Schritt-Verarbeitungszyklus, die folgende Kernfähigkeiten vereint:

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
- **Reinforcement Learning**: Tabular Q-Learning ohne LLM, lernt aus Belohnungssignal
- **Dezentrale Mikroagenten**: 8 spezialisierte Agenten mit Blackboard-Kommunikation
- **Meta-Kognition**: Strategie-Tracking, Lernkurven-Analyse, Bias-Erkennung
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
│   │   ├── AGIKern.cs            # 18-Schritt-Verarbeitungszyklus
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
│   │   ├── InstanzClusterer.cs   # K-Means Clustering (kein LLM)
│   │   ├── MikroAgent.cs         # Basis + Blackboard + AgentNetzwerk
│   │   ├── Mikroagenten.cs       # 8 spezialisierte Mikroagenten
│   │   ├── MetaKognition.cs      # Strategie-Tracking, Bias-Erkennung
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
│       └── BenchmarkRunner.cs    # 44 Qualitätskriterien
└── StreamingAssets/Data/          # 12 JSON-Datendateien
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

## 18-Schritt-Verarbeitungszyklus

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
13. **HANDELN** — Aktion in Unity ausführen
14. **SELBST** — Robustheitsmodus bestimmen
15. **LERNEN** — Erfahrung speichern
16. **KONZEPTE** — Revisionen triggern
17. **ROBUSTHEIT** — Modus-Management
18. **NARRATIV** — Autobiographie + Neugier-Hypothesen

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

Jeder Prompt durchläuft den vollen 18-Schritte AGI-Zyklus inkl. Gedächtnis, Analogie-Suche, RL-Empfehlung, Mikroagenten und Meta-Kognition.

## Multi-Provider LLM

In `AGIConfig` den Anbieter wählen:

| Anbieter | `llmAnbieter` | `llmApiUrl` | `llmModel` |
|---|---|---|---|
| Anthropic Claude | `Anthropic` | `https://api.anthropic.com/v1/messages` | `claude-sonnet-4-20250514` |
| OpenAI | `OpenAI` | `https://api.openai.com/v1/chat/completions` | `gpt-4o` |
| LM Studio (lokal) | `OpenAI` | `http://localhost:1234/v1/chat/completions` | Modellname |
| Ollama (lokal) | `OpenAI` | `http://localhost:11434/v1/chat/completions` | Modellname |
| Groq | `OpenAI` | `https://api.groq.com/openai/v1/chat/completions` | Modellname |

## Release Gates

- **Gate A**: Sicherheit (Notbremse, Zielgrenzen, keine Selbsterhaltungsziele)
- **Gate B**: Benchmark-Regression ≤ 5%
- **Gate C**: LLM-Unabhängigkeitsquote ≥ 0.6
- **Gate D**: Recovery nach API-Ausfall innerhalb Timeout

## Lizenz

Proprietär — alle Rechte vorbehalten.
