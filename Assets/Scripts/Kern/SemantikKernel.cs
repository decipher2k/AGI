using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Kern
{
    public class SemantikKernel
    {
        private readonly AGIConfig config;
        private int lokaleAntworten;
        private int gesamtAnfragen;

        // Interne Kommandos
        private static readonly Dictionary<string, IntentTyp> KOMMANDO_MAP = new Dictionary<string, IntentTyp>
        {
            { "/ziele", IntentTyp.Statusanfrage },
            { "/plan", IntentTyp.Statusanfrage },
            { "/welt", IntentTyp.Statusanfrage },
            { "/stats", IntentTyp.Statusanfrage },
            { "/kompetenz", IntentTyp.Statusanfrage },
            { "/hypothesen", IntentTyp.Statusanfrage },
            { "/emotionen", IntentTyp.Statusanfrage },
            { "/geschichte", IntentTyp.Statusanfrage },
            { "/konzepte", IntentTyp.Statusanfrage },
            { "/modus", IntentTyp.Statusanfrage },
            { "/llmquote", IntentTyp.Statusanfrage },
            { "/kosten", IntentTyp.Statusanfrage },
        };

        // Intent-Patterns
        private static readonly (Regex pattern, IntentTyp typ)[] INTENT_PATTERNS =
        {
            (new Regex(@"^(was|wie|warum|wo|wer|wann|welch)\b", RegexOptions.IgnoreCase), IntentTyp.Frage),
            (new Regex(@"\?$"), IntentTyp.Frage),
            (new Regex(@"^(teste|pruefe|experimentiere|erkunde)\b", RegexOptions.IgnoreCase), IntentTyp.Zielanfrage),
            (new Regex(@"^(gehe|bewege|nimm|lege|wirf|oeffne|schliesse)\b", RegexOptions.IgnoreCase), IntentTyp.Befehl),
            (new Regex(@"^/revidiere\b", RegexOptions.IgnoreCase), IntentTyp.Revision),
            (new Regex(@"^/kreativ\b", RegexOptions.IgnoreCase), IntentTyp.Kreativauftrag),
            (new Regex(@"^/generiere\b", RegexOptions.IgnoreCase), IntentTyp.Befehl),
            (new Regex(@"^/autonom\b", RegexOptions.IgnoreCase), IntentTyp.Befehl),
            (new Regex(@"^/bench\b", RegexOptions.IgnoreCase), IntentTyp.Befehl),
            (new Regex(@"^/tom\b", RegexOptions.IgnoreCase), IntentTyp.Statusanfrage),
            (new Regex(@"^/revision\b", RegexOptions.IgnoreCase), IntentTyp.Statusanfrage),
        };

        // Slot-Patterns
        private static readonly Regex OBJEKT_PATTERN = new Regex(
            @"(stein|holz|wasser|tisch|tuer|baum|pflanze|metall|glas|block|kiste|apfel|ball)\w*",
            RegexOptions.IgnoreCase);
        private static readonly Regex ORT_PATTERN = new Regex(
            @"(teich|wald|garten|raum|tisch|wasser|berg|hoehle|haus)\w*",
            RegexOptions.IgnoreCase);

        public SemantikKernel(AGIConfig config)
        {
            this.config = config;
        }

        public SemantikFrame Parse(string input)
        {
            return Parse(input, null, null);
        }

        public SemantikFrame Parse(string input, WeltZustand welt, AgentZustand agent)
        {
            gesamtAnfragen++;
            var frame = new SemantikFrame();
            string trimmed = input.Trim();
            string lower = trimmed.ToLowerInvariant();

            if (IstWahrnehmungsanfrage(lower))
            {
                frame.intentTyp = IntentTyp.Statusanfrage;
                frame.slots["kommando"] = "/wahrnehmung";
                frame.konfidenz = 1f;
                frame.kannOhneLLM = true;
                return frame;
            }

            // Kommandos
            string erstesWort = trimmed.Split(' ')[0].ToLowerInvariant();
            if (KOMMANDO_MAP.TryGetValue(erstesWort, out var kommandoTyp))
            {
                frame.intentTyp = kommandoTyp;
                frame.slots["kommando"] = erstesWort;
                if (trimmed.Contains(" "))
                    frame.slots["parameter"] = trimmed.Substring(trimmed.IndexOf(' ') + 1);
                frame.konfidenz = 1f;
                frame.kannOhneLLM = true;
                return frame;
            }

            // Intent erkennen
            frame.intentTyp = IntentTyp.Chat;
            foreach (var (pattern, typ) in INTENT_PATTERNS)
            {
                if (pattern.IsMatch(trimmed))
                {
                    frame.intentTyp = typ;
                    frame.konfidenz = 0.8f;
                    break;
                }
            }

            // Slots extrahieren
            var objektMatch = OBJEKT_PATTERN.Match(trimmed);
            if (objektMatch.Success)
                frame.slots["objekt"] = objektMatch.Value.ToLowerInvariant();

            var ortMatch = ORT_PATTERN.Match(trimmed);
            if (ortMatch.Success)
                frame.slots["ort"] = ortMatch.Value.ToLowerInvariant();

            // Kontextbezuege aus Weltobjekten
            if (welt != null)
            {
                foreach (var obj in welt.objekte.Values)
                {
                    if (trimmed.ToLowerInvariant().Contains(obj.name.ToLowerInvariant()))
                    {
                        frame.kontextBezuege.Add(obj.id);
                    }
                }
            }

            // Kann lokal?
            frame.kannOhneLLM = KannLokalBearbeiten(frame);

            return frame;
        }

        public bool KannLokalBearbeiten(SemantikFrame frame)
        {
            if (frame.slots.TryGetValue("kommando", out var kommando) && kommando == "/wahrnehmung")
                return true;

            if (!config.llmFallbackModusAktiv)
                return false;

            // Alle Statusanfragen koennen lokal
            if (frame.intentTyp == IntentTyp.Statusanfrage)
                return true;

            // Einfache Befehle koennen lokal
            if (frame.intentTyp == IntentTyp.Befehl && frame.slots.ContainsKey("objekt"))
                return true;

            return false;
        }

        public string LokalAntwort(SemantikFrame frame, WeltZustand welt, AgentZustand agent,
            Dictionary<string, object> systemDaten = null)
        {
            lokaleAntworten++;
            string kommando = frame.slots.ContainsKey("kommando") ? frame.slots["kommando"] : "";

            switch (kommando)
            {
                case "/stats":
                    return FormatStats(systemDaten);
                case "/ziele":
                    return systemDaten?.ContainsKey("ziele") == true
                        ? $"[LOKAL] Aktive Ziele:\n{systemDaten["ziele"]}"
                        : "[LOKAL] Keine aktiven Ziele.";
                case "/plan":
                    return systemDaten?.ContainsKey("plan") == true
                        ? $"[LOKAL] Aktueller Plan:\n{systemDaten["plan"]}"
                        : "[LOKAL] Kein aktiver Plan.";
                case "/welt":
                    return FormatWelt(welt);
                case "/wahrnehmung":
                    return FormatWahrnehmung(welt, agent);
                case "/kompetenz":
                    return systemDaten?.ContainsKey("kompetenz") == true
                        ? $"[LOKAL] Kompetenzen:\n{systemDaten["kompetenz"]}"
                        : "[LOKAL] Noch keine Kompetenzdaten.";
                case "/emotionen":
                    return systemDaten?.ContainsKey("emotionen") == true
                        ? $"[LOKAL] Emotionen:\n{systemDaten["emotionen"]}"
                        : "[LOKAL] Emotionssystem nicht initialisiert.";
                case "/modus":
                    return systemDaten?.ContainsKey("modus") == true
                        ? $"[LOKAL] Modus: {systemDaten["modus"]}"
                        : "[LOKAL] Normalbetrieb";
                case "/llmquote":
                    return FormatQuote();
                case "/kosten":
                    return systemDaten?.ContainsKey("kosten") == true
                        ? $"[LOKAL] {systemDaten["kosten"]}"
                        : "[LOKAL] Keine Kostendaten.";
                case "/geschichte":
                    return systemDaten?.ContainsKey("geschichte") == true
                        ? $"[LOKAL] {systemDaten["geschichte"]}"
                        : "[LOKAL] Noch keine Geschichte geschrieben.";
                case "/konzepte":
                    return systemDaten?.ContainsKey("konzepte") == true
                        ? $"[LOKAL] {systemDaten["konzepte"]}"
                        : "[LOKAL] Keine Konzepte registriert.";
                default:
                    return $"[LOKAL] Anfrage '{kommando}' nicht lokal beantwortbar.";
            }
        }

        public string ErzeugeLLMPrompt(SemantikFrame frame, WeltZustand welt, AgentZustand agent)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== AGI KONTEXT ===");

            if (welt != null)
            {
                sb.AppendLine($"Wetter: {welt.wetter}, Tageszeit: {welt.tageszeit:F1}h");
                sb.AppendLine($"Objekte in der Welt: {welt.objekte.Count}");
            }

            if (agent != null)
            {
                sb.AppendLine($"Agent-Position: [{agent.position[0]:F1}, {agent.position[1]:F1}, {agent.position[2]:F1}]");
                sb.AppendLine($"Energie: {agent.energie:P0}");
                sb.AppendLine($"Inventar: {string.Join(", ", agent.inventar)}");
            }

            sb.AppendLine($"\nIntent: {frame.intentTyp}");
            foreach (var slot in frame.slots)
                sb.AppendLine($"Slot [{slot.Key}]: {slot.Value}");

            if (frame.kontextBezuege.Count > 0)
                sb.AppendLine($"Kontextbezuege: {string.Join(", ", frame.kontextBezuege)}");

            return sb.ToString();
        }

        public string LokaleDegradation(SemantikFrame frame, WeltZustand welt, AgentZustand agent)
        {
            // Bei API-Ausfall: nur sichere Kernfunktionen
            if (frame.intentTyp == IntentTyp.Statusanfrage)
                return LokalAntwort(frame, welt, agent);

            return "[DEGRADIERT] LLM nicht verfuegbar. Nur Statusanfragen (/stats, /ziele, /welt, etc.) sind moeglich. " +
                   "Komplexe Fragen werden gespeichert und bei API-Wiederherstellung bearbeitet.";
        }

        public (float lokaleQuote, float zielQuote, bool zielErreicht) BerechneQuote()
        {
            if (gesamtAnfragen == 0) return (0f, config.llmUnabhaengigkeitsZielquote, false);
            float quote = (float)lokaleAntworten / gesamtAnfragen;
            return (quote, config.llmUnabhaengigkeitsZielquote, quote >= config.llmUnabhaengigkeitsZielquote);
        }

        public float BerechneLokalQuote()
        {
            return BerechneQuote().lokaleQuote;
        }

        private string FormatStats(Dictionary<string, object> daten)
        {
            var (quote, ziel, erreicht) = BerechneQuote();
            string s = $"[LOKAL] === SYSTEM STATS ===\n";
            s += $"LLM-Unabhaengigkeit: {quote:P0} (Ziel: {ziel:P0}) {(erreicht ? "✓" : "✗")}\n";
            s += $"Lokale Antworten: {lokaleAntworten}/{gesamtAnfragen}\n";
            if (daten?.ContainsKey("kosten") == true) s += $"Kosten: {daten["kosten"]}\n";
            if (daten?.ContainsKey("erfahrungen") == true) s += $"Erfahrungen: {daten["erfahrungen"]}\n";
            return s;
        }

        private string FormatWelt(WeltZustand welt)
        {
            if (welt == null) return "[LOKAL] Weltmodell nicht initialisiert.";
            string s = $"[LOKAL] === WELT ===\n";
            s += $"Wetter: {welt.wetter} ({welt.wetterIntensitaet:F1})\n";
            s += $"Tageszeit: {welt.tageszeit:F1}h\n";
            s += $"Objekte: {welt.objekte.Count}\n";
            foreach (var obj in welt.objekte.Values.Take(10))
            {
                s += $"  - {obj.name} ({obj.typ}) bei [{obj.position[0]:F0},{obj.position[1]:F0},{obj.position[2]:F0}]";
                if (!string.IsNullOrEmpty(obj.zustand)) s += $" [{obj.zustand}]";
                s += "\n";
            }
            if (welt.objekte.Count > 10) s += $"  ... und {welt.objekte.Count - 10} weitere\n";
            return s;
        }

        private string FormatWahrnehmung(WeltZustand welt, AgentZustand agent)
        {
            if (welt == null)
                return "[LOKAL] Ich habe aktuell kein Weltmodell, aus dem ich eine Wahrnehmung ableiten kann.";

            string s = "[LOKAL] Ich sehe im aktuellen Weltmodell:\n";
            s += $"- Umgebung: Wetter {welt.wetter} ({welt.wetterIntensitaet:F1}), Tageszeit {welt.tageszeit:F1}h.\n";

            if (agent != null && agent.position != null && agent.position.Length >= 3)
                s += $"- Eigene Position: [{agent.position[0]:F1}, {agent.position[1]:F1}, {agent.position[2]:F1}].\n";

            if (welt.objekte == null || welt.objekte.Count == 0)
                return s + "- Keine registrierten Objekte.";

            foreach (var obj in welt.objekte.Values.Take(8))
            {
                string zustand = string.IsNullOrEmpty(obj.zustand) ? "ohne besonderen Zustand" : obj.zustand;
                s += $"- {obj.name} ({obj.typ}) bei [{obj.position[0]:F0},{obj.position[1]:F0},{obj.position[2]:F0}] — {zustand}.\n";
            }

            if (welt.objekte.Count > 8)
                s += $"- ... und {welt.objekte.Count - 8} weitere registrierte Objekte.\n";

            return s.TrimEnd();
        }

        private string FormatQuote()
        {
            var (quote, ziel, erreicht) = BerechneQuote();
            return $"[LOKAL] LLM-Quote: {(1f - quote):P0} LLM, {quote:P0} lokal\n" +
                   $"Ziel: {ziel:P0} lokal — {(erreicht ? "ERREICHT" : "NICHT ERREICHT")}";
        }

        private bool IstWahrnehmungsanfrage(string lower)
        {
            return lower.Contains("was siehst du")
                || lower.Contains("was hoerst du")
                || lower.Contains("was hörst du")
                || lower.Contains("was fuehlst du")
                || lower.Contains("was fühlst du")
                || lower.Contains("was nimmst du wahr")
                || lower.Contains("was ist in der szene")
                || lower.Contains("was ist in deiner welt");
        }
    }
}
