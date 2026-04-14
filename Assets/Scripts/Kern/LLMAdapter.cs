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
                    var responseObj = JObject.Parse(responseJson);

                    string antwortText;
                    int inputTokens, outputTokens;

                    if (config.llmAnbieter == LLMAnbieter.Anthropic)
                    {
                        antwortText = responseObj["content"]?[0]?["text"]?.ToString() ?? "";
                        inputTokens = responseObj["usage"]?["input_tokens"]?.Value<int>() ?? 0;
                        outputTokens = responseObj["usage"]?["output_tokens"]?.Value<int>() ?? 0;
                    }
                    else
                    {
                        antwortText = responseObj["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
                        inputTokens = responseObj["usage"]?["prompt_tokens"]?.Value<int>() ?? 0;
                        outputTokens = responseObj["usage"]?["completion_tokens"]?.Value<int>() ?? 0;
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

        private string BaueAnthropicBody(string prompt, string systemPrompt)
        {
            var body = new JObject
            {
                ["model"] = config.llmModel,
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
                ["model"] = config.llmModel,
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
