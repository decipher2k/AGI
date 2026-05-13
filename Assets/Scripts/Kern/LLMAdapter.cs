using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using BilligAGI.Modelle;

namespace BilligAGI.Kern
{
    public class LLMAdapter
    {
        private readonly AGIConfig config;
        private readonly HttpClient httpClient;
        private readonly Dictionary<string, (LLMAntwort antwort, DateTime ablauf)> cache
            = new Dictionary<string, (LLMAntwort, DateTime)>();

        // Kosten-Tracking
        private int gesamtTokens;
        private float gesamtKosten;
        private int gesamtAnfragen;
        private int fehlgeschlageneAnfragen;
        private string aktuellesModell; // Null = config.llmModel, wird bei Fine-Tuning gewechselt

        public int GesamtTokens => gesamtTokens;
        public float GesamtKosten => gesamtKosten;
        public int GesamtAnfragen => gesamtAnfragen;
        public int FehlgeschlageneAnfragen => fehlgeschlageneAnfragen;

        public LLMAdapter(AGIConfig config)
        {
            this.config = config;
            httpClient = new HttpClient();

            if (config.llmAnbieter == LLMAnbieter.Anthropic)
            {
                httpClient.DefaultRequestHeaders.Add("x-api-key", config.llmApiKey);
                httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            }
            else
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.llmApiKey}");
            }
        }

        public async Task<LLMAntwort> Analysiere(string prompt, string systemPrompt = null)
        {
            return await SendeAnfrage(prompt, systemPrompt);
        }

        public async Task<LLMAntwort> PlaneAktionen(Ziel ziel, WeltZustand welt)
        {
            string prompt = $"Ziel: {ziel.name}\nBeschreibung: {ziel.beschreibung}\n" +
                           $"Erfolgsbedingung: {ziel.erfolgsbedingung}\n\n" +
                           $"Weltzustand:\nWetter: {welt.wetter}, Tageszeit: {welt.tageszeit}\n" +
                           $"Objekte: {welt.objekte.Count}\n\n" +
                           "Erstelle einen konkreten Aktionsplan mit Schritten. " +
                           "Jeder Schritt: Aktionstyp + Parameter + Erwartetes Ergebnis. " +
                           "Antworte als JSON-Array.";

            string system = "Du bist der Planungsmodul einer AGI. Erstelle praezise, ausfuehrbare Aktionsplaene.";
            return await SendeAnfrage(prompt, system);
        }

        public async Task<LLMAntwort> InterpretiereSensordaten(SensorDaten daten)
        {
            string prompt = $"Sensordaten-Interpretation:\n" +
                           $"Helligkeit: {daten.helligkeit:F2}, Bewegung: {daten.bewegungsIntensitaet:F2}\n" +
                           $"Audio-Pegel: {daten.audioPegel:F2}\n" +
                           $"Kollisionskraft: {daten.kollisionsKraft:F2}\n" +
                           $"Nahbereich-Objekte: {daten.nahbereichObjekte?.Length ?? 0}\n\n" +
                           "Was nimmt der Agent wahr? Beschreibe die Situation.";

            return await SendeAnfrage(prompt, "Du interpretierst Sensordaten eines AGI-Agenten in einer 3D-Welt.");
        }

        public async Task<LLMAntwort> FormulierZiel(string wissensluecke, string kontext)
        {
            string prompt = $"Wissensluecke: {wissensluecke}\nKontext: {kontext}\n\n" +
                           "Formuliere ein konkretes, testbares Ziel um diese Wissensluecke zu schliessen. " +
                           "Format: Name, Beschreibung, Typ (Exploration/Experiment/Verstaendnis), Erfolgsbedingung.";

            return await SendeAnfrage(prompt, "Du formulierst Forschungsziele fuer eine AGI.");
        }

        public async Task<LLMAntwort> BewertZielerreichung(Ziel ziel, WeltZustand zustand)
        {
            string prompt = $"Ziel: {ziel.name}\nErfolgsbedingung: {ziel.erfolgsbedingung}\n" +
                           $"Weltzustand: {JsonConvert.SerializeObject(zustand, Formatting.None)}\n\n" +
                           "Ist das Ziel erreicht? Antworte mit: ERREICHT/NICHT_ERREICHT/TEILWEISE + Begruendung.";

            return await SendeAnfrage(prompt, "Du bewertest Zielerreichung einer AGI.");
        }

        public async Task<LLMAntwort> FreieAnfrage(string prompt, string systemPrompt = null)
        {
            return await SendeAnfrage(prompt, systemPrompt);
        }

        /// <summary>
        /// Iteratives Reasoning: Chain-of-Thought → Selbstkritik → Korrektur → Finale Antwort.
        /// Kostet 3-4x mehr Tokens, aber deutlich bessere Qualitaet.
        /// </summary>
        public async Task<LLMAntwort> IterativesNachdenken(string prompt, string systemPrompt = null, int iterationen = 3)
        {
            var startTime = DateTime.UtcNow;
            int gesamtTokensLokal = 0;
            float gesamtKostenLokal = 0f;

            // Schritt 1: Erste Analyse mit Chain-of-Thought
            string cotSystem = (systemPrompt ?? "") +
                "\n\nDenke Schritt fuer Schritt nach. Zeige deinen Denkprozess. " +
                "Markiere dein Zwischenergebnis mit [ZWISCHENERGEBNIS]: am Ende.";

            var erstAntwort = await SendeAnfrage(prompt, cotSystem);
            if (erstAntwort.ausFallback || erstAntwort.text.StartsWith("[FEHLER]"))
                return erstAntwort;

            gesamtTokensLokal += erstAntwort.tokensUsed;
            gesamtKostenLokal += erstAntwort.kosten;
            string bisherig = erstAntwort.text;

            // Schritt 2-N: Iterative Selbstkritik und Verfeinerung
            for (int i = 1; i < iterationen; i++)
            {
                string kritikPrompt = i < iterationen - 1
                    ? $"Deine bisherige Analyse:\n\n{bisherig}\n\n" +
                      "Pruefe diese Analyse kritisch:\n" +
                      "1. Was koennte falsch oder unvollstaendig sein?\n" +
                      "2. Welche Annahmen hast du gemacht?\n" +
                      "3. Gibt es Gegenargumente?\n" +
                      "Korrigiere und verfeinere deine Antwort. Markiere mit [ZWISCHENERGEBNIS]:"
                    : $"Deine bisherige Analyse:\n\n{bisherig}\n\n" +
                      "Formuliere jetzt eine praeзise, finale Antwort. " +
                      "Beruecksichtige alle bisherigen Korrekturen. " +
                      "Antworte direkt und klar — OHNE Denkprozess.";

                string iterSystem = i < iterationen - 1
                    ? "Du bist ein kritischer Reviewer deiner eigenen Analyse."
                    : systemPrompt ?? "Antworte praezise und klar.";

                var iterAntwort = await SendeAnfrage(kritikPrompt, iterSystem);
                if (iterAntwort.ausFallback || iterAntwort.text.StartsWith("[FEHLER]"))
                    break;

                gesamtTokensLokal += iterAntwort.tokensUsed;
                gesamtKostenLokal += iterAntwort.kosten;
                bisherig = iterAntwort.text;
            }

            float dauer = (float)(DateTime.UtcNow - startTime).TotalMilliseconds;

            return new LLMAntwort
            {
                text = bisherig,
                tokensUsed = gesamtTokensLokal,
                kosten = gesamtKostenLokal,
                dauerMs = dauer,
                ausFallback = false
            };
        }

        private async Task<LLMAntwort> SendeAnfrage(string prompt, string systemPrompt, int maxRetries = 3)
        {
            // Cache pruefen
            string cacheKey = $"{systemPrompt}|{prompt}";
            if (cache.TryGetValue(cacheKey, out var cached) && cached.ablauf > DateTime.UtcNow)
                return cached.antwort;

            string jsonBody = config.llmAnbieter == LLMAnbieter.Anthropic
                ? BaueAnthropicBody(prompt, systemPrompt)
                : BaueOpenAIBody(prompt, systemPrompt);

            for (int versuch = 0; versuch <= maxRetries; versuch++)
            {
                try
                {
                    var startTime = DateTime.UtcNow;
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(config.llmApiUrl, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        string fehler = await response.Content.ReadAsStringAsync();
                        Debug.LogWarning($"[LLMAdapter] HTTP {response.StatusCode}: {fehler}");

                        if (versuch < maxRetries)
                        {
                            int wartezeit = (int)Math.Pow(2, versuch) * 1000;
                            await Task.Delay(wartezeit);
                            continue;
                        }

                        fehlgeschlageneAnfragen++;
                        return new LLMAntwort
                        {
                            text = $"[FEHLER] HTTP {response.StatusCode}",
                            ausFallback = false
                        };
                    }

                    string responseJson = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(responseJson))
                    {
                        Debug.LogWarning("[LLMAdapter] Leere HTTP-Antwort vom LLM-Server.");
                        if (versuch < maxRetries)
                        {
                            int wartezeit = (int)Math.Pow(2, versuch) * 1000;
                            await Task.Delay(wartezeit);
                            continue;
                        }

                        fehlgeschlageneAnfragen++;
                        return new LLMAntwort
                        {
                            text = "[FEHLER] Leere Antwort vom LLM-Server",
                            ausFallback = false
                        };
                    }

                    var responseObj = JObject.Parse(responseJson);

                    string antwortText;
                    int inputTokens, outputTokens;

                    if (config.llmAnbieter == LLMAnbieter.Anthropic)
                    {
                        antwortText = ExtrahiereText(responseObj["content"]);
                        inputTokens = responseObj["usage"]?["input_tokens"]?.Value<int>() ?? 0;
                        outputTokens = responseObj["usage"]?["output_tokens"]?.Value<int>() ?? 0;
                    }
                    else
                    {
                        antwortText = ExtrahiereOpenAIText(responseObj);
                        inputTokens = responseObj["usage"]?["prompt_tokens"]?.Value<int>() ?? 0;
                        outputTokens = responseObj["usage"]?["completion_tokens"]?.Value<int>() ?? 0;
                    }

                    if (string.IsNullOrWhiteSpace(antwortText))
                    {
                        Debug.LogWarning($"[LLMAdapter] LLM-Server lieferte keinen Antworttext: {responseJson}");
                        if (versuch < maxRetries)
                        {
                            int wartezeit = (int)Math.Pow(2, versuch) * 1000;
                            await Task.Delay(wartezeit);
                            continue;
                        }

                        fehlgeschlageneAnfragen++;
                        return new LLMAntwort
                        {
                            text = "[FEHLER] LLM-Server lieferte keinen Antworttext",
                            ausFallback = false
                        };
                    }

                    int totalTokens = inputTokens + outputTokens;
                    float kosten = BerechneKosten(inputTokens, outputTokens);
                    float dauer = (float)(DateTime.UtcNow - startTime).TotalMilliseconds;

                    gesamtTokens += totalTokens;
                    gesamtKosten += kosten;
                    gesamtAnfragen++;

                    var antwort = new LLMAntwort
                    {
                        text = antwortText,
                        tokensUsed = totalTokens,
                        kosten = kosten,
                        dauerMs = dauer,
                        ausFallback = false
                    };

                    // Cache (5 Minuten TTL)
                    cache[cacheKey] = (antwort, DateTime.UtcNow.AddMinutes(5));

                    return antwort;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LLMAdapter] Versuch {versuch + 1}/{maxRetries + 1} fehlgeschlagen: {ex.Message}");
                    if (versuch < maxRetries)
                    {
                        int wartezeit = (int)Math.Pow(2, versuch) * 1000;
                        await Task.Delay(wartezeit);
                    }
                }
            }

            fehlgeschlageneAnfragen++;
            return new LLMAntwort
            {
                text = "[FEHLER] Alle Versuche fehlgeschlagen",
                ausFallback = false
            };
        }


        private static string ExtrahiereOpenAIText(JObject responseObj)
        {
            var choice = responseObj["choices"]?[0];
            string content = ExtrahiereText(choice?["message"]?["content"]);
            if (!string.IsNullOrWhiteSpace(content))
                return content;

            content = ExtrahiereText(choice?["delta"]?["content"]);
            if (!string.IsNullOrWhiteSpace(content))
                return content;

            content = choice?["text"]?.ToString();
            if (!string.IsNullOrWhiteSpace(content))
                return content;

            return ExtrahiereText(responseObj["message"]?["content"]);
        }

        private static string ExtrahiereText(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return "";

            if (token.Type == JTokenType.String)
                return token.ToString();

            if (token.Type == JTokenType.Array)
            {
                var sb = new StringBuilder();
                foreach (var part in token.Children())
                {
                    string teil = ExtrahiereText(part);
                    if (string.IsNullOrEmpty(teil))
                        continue;
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(teil);
                }
                return sb.ToString();
            }

            if (token.Type == JTokenType.Object)
            {
                string text = token["text"]?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;

                text = token["content"]?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return token.ToString();
        }

        private string BaueAnthropicBody(string prompt, string systemPrompt)
        {
            var body = new JObject
            {
                ["model"] = aktuellesModell ?? config.llmModel,
                ["max_tokens"] = config.maxTokensProAnfrage,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "user", ["content"] = prompt }
                }
            };
            if (!string.IsNullOrEmpty(systemPrompt))
                body["system"] = systemPrompt;
            return body.ToString(Formatting.None);
        }

        private string BaueOpenAIBody(string prompt, string systemPrompt)
        {
            var messages = new JArray();
            if (!string.IsNullOrEmpty(systemPrompt))
                messages.Add(new JObject { ["role"] = "system", ["content"] = systemPrompt });
            messages.Add(new JObject { ["role"] = "user", ["content"] = prompt });

            var body = new JObject
            {
                ["model"] = aktuellesModell ?? config.llmModel,
                ["max_tokens"] = config.maxTokensProAnfrage,
                ["messages"] = messages
            };
            return body.ToString(Formatting.None);
        }

        private float BerechneKosten(int inputTokens, int outputTokens)
        {
            if (config.llmAnbieter == LLMAnbieter.Anthropic)
                return (inputTokens * 3f + outputTokens * 15f) / 1_000_000f;
            // OpenAI-kompatibel: Kosten nicht berechenbar ohne Preisinfo, nur Tokens zaehlen
            return 0f;
        }

        public bool IstVerfuegbar()
        {
            return !string.IsNullOrEmpty(config.llmApiKey) &&
                   fehlgeschlageneAnfragen < 5; // Einfacher Health-Check
        }

        public float GetGesamtKosten() => gesamtKosten;
        public int GetAnzahlCalls() => gesamtAnfragen;

        /// <summary>
        /// Hot-Swap: Wechselt das aktive Modell zur Laufzeit (z.B. nach Fine-Tuning).
        /// </summary>
        public void WechsleModell(string neuesModell)
        {
            if (string.IsNullOrEmpty(neuesModell)) return;
            string altes = aktuellesModell ?? config.llmModel;
            aktuellesModell = neuesModell;
            cache.Clear(); // Cache invalidieren bei Modellwechsel
            Debug.Log($"[LLMAdapter] Modell gewechselt: {altes} → {neuesModell}");
        }

        /// <summary>
        /// Gibt das aktuell aktive Modell zurueck (ggf. nach Fine-Tuning gewechselt).
        /// </summary>
        public string GetAktuellesModell() => aktuellesModell ?? config.llmModel;

        public string KostenReport()
        {
            return $"Anfragen: {gesamtAnfragen}, Tokens: {gesamtTokens}, " +
                   $"Kosten: ${gesamtKosten:F4}, Fehler: {fehlgeschlageneAnfragen}";
        }

        public void ResetKosten()
        {
            gesamtTokens = 0;
            gesamtKosten = 0;
            gesamtAnfragen = 0;
            fehlgeschlageneAnfragen = 0;
        }
    }
}
