using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BilligAGI.Kern
{
    /// <summary>
    /// OpenAI-kompatibler API-Server fuer Billig-AGI.
    /// Empfaengt Chat-Completion-Anfragen, verarbeitet sie durch den AGI-Zyklus,
    /// und liefert Antworten im OpenAI-Format zurueck.
    /// Nutzung: z.B. ARC-Benchmark laeuft gegen http://localhost:8741/v1/chat/completions
    /// </summary>
    public class AGIApiServer : MonoBehaviour
    {
        [Header("Server")]
        public int port = 8741;
        public bool autoStart = true;
        [Tooltip("Maximale Wartezeit, bevor der HTTP-Client eine JSON-Fehlerantwort statt einer leeren/abgebrochenen Verbindung bekommt.")]
        public float requestTimeoutSekunden = 25f;

        [Header("Referenzen")]
        public AGIKern agiKern;

        private HttpListener listener;
        private Thread listenerThread;
        private bool running;

        private readonly ConcurrentQueue<AnfrageItem> warteschlange
            = new ConcurrentQueue<AnfrageItem>();

        private class AnfrageItem
        {
            public HttpListenerContext context;
            public string prompt;
            public string systemPrompt;
            public string model;
            public bool stream;
            public string completionId;
            public long created;
            public bool streamingGestartet;
            public TaskCompletionSource<bool> fertig;
            public int mainThreadGestartet;
            private int antwortReserviert;

            public bool ReserviereAntwort()
            {
                return Interlocked.Exchange(ref antwortReserviert, 1) == 0;
            }
        }

        private class ChatRequest
        {
            public string prompt;
            public string systemPrompt;
            public string model;
            public bool stream;
        }

        private void Start()
        {
            if (autoStart && agiKern != null)
                StartServer();
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void Update()
        {
            if (!running) return;

            // Pro Frame eine Anfrage verarbeiten (Main-Thread fuer Unity-APIs)
            if (warteschlange.TryDequeue(out var item))
            {
                if (Interlocked.CompareExchange(ref item.mainThreadGestartet, 1, 0) != 0)
                {
                    item.fertig.TrySetResult(true);
                    return;
                }

                _ = VerarbeiteAufMainThread(item);
            }
        }

        public void StartServer()
        {
            if (running) return;

            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();
                running = true;

                listenerThread = new Thread(ListenerLoop)
                {
                    IsBackground = true,
                    Name = "AGIApiListener"
                };
                listenerThread.Start();

                Debug.Log($"[AGIApi] Server gestartet: http://localhost:{port}/v1/chat/completions");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AGIApi] Server-Start fehlgeschlagen: {ex.Message}");
            }
        }

        public void StopServer()
        {
            running = false;
            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch { }
            Debug.Log("[AGIApi] Server gestoppt.");
        }

        // ===== Hintergrund-Thread: HTTP-Anfragen annehmen =====

        private void ListenerLoop()
        {
            while (running)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break; // Server wurde gestoppt
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AGIApi] Listener-Fehler: {ex.Message}");
                    continue;
                }

                if (ctx == null) continue;

                try
                {
                    string path = ctx.Request.Url.AbsolutePath.TrimEnd('/');
                    string method = ctx.Request.HttpMethod;

                    // CORS
                    ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                    ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");

                    if (method == "OPTIONS")
                    {
                        SendeJsonAntwort(ctx, 200, "{}");
                        continue;
                    }

                    if (path == "/v1/models" && method == "GET")
                    {
                        HandleModels(ctx);
                        continue;
                    }

                    if (path == "/v1/chat/completions" && method == "POST")
                    {
                        HandleChatCompletions(ctx);
                        continue;
                    }

                    // Health-Check
                    if (path == "/health" || path == "/")
                    {
                        var health = new JObject
                        {
                            ["status"] = agiKern?.IstInitialisiert() == true
                                ? (agiKern.IstBeschaeftigt() ? "busy" : "ready")
                                : "initializing",
                            ["model"] = "billig-agi",
                            ["version"] = "1.0"
                        };
                        SendeJsonAntwort(ctx, 200, health.ToString());
                        continue;
                    }

                    SendeFehler(ctx, 404, "not_found", $"Endpunkt nicht gefunden: {path}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AGIApi] Request-Fehler: {ex.Message}");
                    try { SendeFehler(ctx, 500, "internal_error", ex.Message); }
                    catch { }
                }
            }
        }

        // ===== /v1/models =====

        private void HandleModels(HttpListenerContext ctx)
        {
            var response = new JObject
            {
                ["object"] = "list",
                ["data"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "billig-agi",
                        ["object"] = "model",
                        ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        ["owned_by"] = "billig-agi"
                    }
                }
            };
            SendeJsonAntwort(ctx, 200, response.ToString());
        }

        // ===== /v1/chat/completions =====

        private void HandleChatCompletions(HttpListenerContext ctx)
        {
            // Request-Body lesen
            string body;
            using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            JObject requestObj;
            try
            {
                requestObj = JObject.Parse(body);
            }
            catch
            {
                SendeFehler(ctx, 400, "invalid_json", "Ungültiger JSON-Body");
                return;
            }

            ChatRequest request;
            try
            {
                request = ExtrahiereChatRequest(requestObj);
            }
            catch (ArgumentException ex)
            {
                SendeFehler(ctx, 400, "invalid_request", ex.Message);
                return;
            }

            // AGI ist noch nicht initialisiert? Beschaeftigte Zyklen sind kein
            // Ablehnungsgrund: Die Anfrage wird angenommen und mit Vorrang vor
            // weiteren AutoTrainer-Schritten abgearbeitet.
            if (agiKern == null || !agiKern.KannApiAnfrageAnnehmen())
            {
                SendeFehler(ctx, 503, "model_not_ready", "AGI initialisiert noch");
                return;
            }

            // Anfrage in Queue fuer Main-Thread einreihen
            var tcs = new TaskCompletionSource<bool>();
            var item = new AnfrageItem
            {
                context = ctx,
                prompt = request.prompt,
                systemPrompt = request.systemPrompt,
                model = request.model,
                stream = request.stream,
                completionId = $"chatcmpl-agi-{Guid.NewGuid():N}",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                fertig = tcs
            };

            agiKern.RegistriereWartendeApiAnfrage();
            warteschlange.Enqueue(item);

            if (item.stream)
                item.streamingGestartet = SendeStreamingStart(item.context, item.completionId, item.created, item.model);

            // Blockierend warten, aber nur begrenzt: ohne Timeout schliessen viele
            // Clients die Verbindung selbst und melden dann "empty response from server".
            var timeout = TimeSpan.FromSeconds(Mathf.Max(1f, requestTimeoutSekunden));
            if (!tcs.Task.Wait(timeout))
            {
                bool nochNichtGestartet = Interlocked.CompareExchange(ref item.mainThreadGestartet, 2, 0) == 0;
                if (nochNichtGestartet)
                    agiKern.EntferneWartendeApiAnfrage();

                string message = nochNichtGestartet
                    ? "API-Anfrage wartete zu lange auf den Unity-Main-Thread."
                    : "API-Verarbeitung dauerte zu lange; pruefe LLM-Server, Autonomie-/Training-Last oder erhoehe requestTimeoutSekunden.";
                SendeFehlerEinmal(item, 504, "request_timeout", message);
            }
        }

        // ===== Main-Thread Verarbeitung =====

        private async Task VerarbeiteAufMainThread(AnfrageItem item)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                agiKern?.EntferneWartendeApiAnfrage();
                Debug.Log($"[AGIApi] Anfrage empfangen: {item.prompt.Substring(0, Math.Min(item.prompt.Length, 100))}...");

                // AGI-Zyklus durchlaufen
                string antwort = await agiKern.VerarbeiteAnfrageAsync(item.prompt, item.systemPrompt);
                antwort = NormalisiereAntworttext(antwort);
                float dauerMs = (float)(DateTime.UtcNow - startTime).TotalMilliseconds;

                // Token-Schaetzung (grob: 4 Zeichen pro Token)
                int promptTokens = item.prompt.Length / 4;
                int completionTokens = (antwort?.Length ?? 0) / 4;

                string completionId = item.completionId;
                long created = item.created;
                string model = string.IsNullOrWhiteSpace(item.model) ? "billig-agi" : item.model;
                string modus = agiKern.GetModus();
                float llmKosten = agiKern.GetLLM()?.GesamtKosten ?? 0f;

                if (item.stream)
                {
                    if (item.ReserviereAntwort())
                        SendeStreamingAntwort(item.context, completionId, created, model, antwort ?? "",
                            promptTokens, completionTokens, dauerMs, modus, llmKosten, !item.streamingGestartet);
                    Debug.Log($"[AGIApi] Streaming-Antwort gesendet ({dauerMs:F0}ms, ~{promptTokens + completionTokens} tokens)");
                    return;
                }

                // OpenAI-kompatible Antwort bauen
                var response = new JObject
                {
                    ["id"] = completionId,
                    ["object"] = "chat.completion",
                    ["created"] = created,
                    ["model"] = model,
                    ["choices"] = new JArray
                    {
                        new JObject
                        {
                            ["index"] = 0,
                            ["message"] = new JObject
                            {
                                ["role"] = "assistant",
                                ["content"] = antwort ?? ""
                            },
                            ["finish_reason"] = "stop"
                        }
                    },
                    ["usage"] = new JObject
                    {
                        ["prompt_tokens"] = promptTokens,
                        ["completion_tokens"] = completionTokens,
                        ["total_tokens"] = promptTokens + completionTokens
                    },
                    ["system_fingerprint"] = "billig-agi-v1",
                    ["x_agi_metadata"] = new JObject
                    {
                        ["dauer_ms"] = dauerMs,
                        ["modus"] = modus,
                        ["llm_kosten"] = llmKosten
                    }
                };

                if (item.ReserviereAntwort())
                    SendeJsonAntwort(item.context, 200, response.ToString());
                Debug.Log($"[AGIApi] Antwort gesendet ({dauerMs:F0}ms, ~{promptTokens + completionTokens} tokens)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AGIApi] Verarbeitungsfehler: {ex.Message}");
                SendeFehlerEinmal(item, 500, "processing_error", ex.Message);
            }
            finally
            {
                item.fertig.TrySetResult(true);
            }
        }

        // ===== Hilfsmethoden =====


        private static string NormalisiereAntworttext(string antwort)
        {
            return string.IsNullOrWhiteSpace(antwort)
                ? "Ich konnte gerade keine Antwort generieren. Bitte versuche es erneut oder pruefe die LLM-Server-Konfiguration."
                : antwort;
        }

        private static ChatRequest ExtrahiereChatRequest(JObject requestObj)
        {
            string model = requestObj.Value<string>("model");
            bool stream = requestObj.Value<bool?>("stream") ?? false;
            var messages = requestObj["messages"] as JArray;
            if (messages == null || messages.Count == 0)
                throw new ArgumentException("messages muss ein nicht-leeres Array sein.");

            var systemBuilder = new StringBuilder();
            var promptBuilder = new StringBuilder();

            foreach (var token in messages)
            {
                var message = token as JObject;
                if (message == null) continue;

                string role = message.Value<string>("role") ?? "user";
                string content = ExtrahiereMessageContent(message["content"]);
                if (string.IsNullOrWhiteSpace(content)) continue;

                if (role == "system" || role == "developer")
                {
                    if (systemBuilder.Length > 0) systemBuilder.AppendLine();
                    systemBuilder.AppendLine(content);
                }
                else
                {
                    if (promptBuilder.Length > 0) promptBuilder.AppendLine();
                    promptBuilder.Append(role).Append(": ").AppendLine(content);
                }
            }

            string prompt = promptBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Keine nutzbare User-/Assistant-Nachricht in messages gefunden.");

            return new ChatRequest
            {
                prompt = prompt,
                systemPrompt = systemBuilder.ToString().Trim(),
                model = string.IsNullOrWhiteSpace(model) ? "billig-agi" : model,
                stream = stream
            };
        }

        private static string ExtrahiereMessageContent(JToken contentToken)
        {
            if (contentToken == null || contentToken.Type == JTokenType.Null)
                return string.Empty;

            if (contentToken.Type == JTokenType.String)
                return contentToken.Value<string>() ?? string.Empty;

            if (contentToken is JArray parts)
            {
                var sb = new StringBuilder();
                foreach (var partToken in parts)
                {
                    if (partToken is JObject part)
                    {
                        string type = part.Value<string>("type");
                        if (type == "text")
                        {
                            string text = part.Value<string>("text");
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                if (sb.Length > 0) sb.AppendLine();
                                sb.Append(text);
                            }
                        }
                    }
                    else if (partToken.Type == JTokenType.String)
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(partToken.Value<string>());
                    }
                }
                return sb.ToString();
            }

            return contentToken.ToString(Formatting.None);
        }

        private static bool SendeStreamingStart(HttpListenerContext ctx, string completionId, long created, string model)
        {
            try
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/event-stream; charset=utf-8";
                ctx.Response.SendChunked = true;
                ctx.Response.Headers.Add("Cache-Control", "no-cache");
                ctx.Response.Headers.Add("X-Accel-Buffering", "no");

                var roleChunk = NeuerStreamChunk(completionId, created, model,
                    new JObject { ["role"] = "assistant" }, null);
                SendeSseData(ctx, roleChunk.ToString(Formatting.None));
                SendeSseKommentar(ctx, "billig-agi request accepted");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AGIApi] Konnte Streaming-Antwort nicht starten: {ex.Message}");
                return false;
            }
        }

        private static void SendeStreamingAntwort(HttpListenerContext ctx, string completionId, long created,
            string model, string antwort, int promptTokens, int completionTokens, float dauerMs,
            string modus, float llmKosten, bool includeRoleChunk = true)
        {
            try
            {
                if (includeRoleChunk)
                    SendeStreamingStart(ctx, completionId, created, model);

                foreach (string teil in TeileTextInStreamChunks(antwort))
                {
                    var contentChunk = NeuerStreamChunk(completionId, created, model,
                        new JObject { ["content"] = teil }, null);
                    SendeSseData(ctx, contentChunk.ToString(Formatting.None));
                }

                var finishChunk = NeuerStreamChunk(completionId, created, model, new JObject(), "stop");
                finishChunk["usage"] = new JObject
                {
                    ["prompt_tokens"] = promptTokens,
                    ["completion_tokens"] = completionTokens,
                    ["total_tokens"] = promptTokens + completionTokens
                };
                finishChunk["x_agi_metadata"] = new JObject
                {
                    ["dauer_ms"] = dauerMs,
                    ["modus"] = modus,
                    ["llm_kosten"] = llmKosten
                };
                SendeSseData(ctx, finishChunk.ToString(Formatting.None));
                SendeSseData(ctx, "[DONE]");
                ctx.Response.OutputStream.Close();
            }
            catch
            {
                try { ctx.Response.OutputStream.Close(); }
                catch { }
            }
        }

        private static JObject NeuerStreamChunk(string completionId, long created, string model,
            JObject delta, string finishReason)
        {
            return new JObject
            {
                ["id"] = completionId,
                ["object"] = "chat.completion.chunk",
                ["created"] = created,
                ["model"] = model,
                ["choices"] = new JArray
                {
                    new JObject
                    {
                        ["index"] = 0,
                        ["delta"] = delta,
                        ["finish_reason"] = finishReason == null ? JValue.CreateNull() : new JValue(finishReason)
                    }
                },
                ["system_fingerprint"] = "billig-agi-v1"
            };
        }

        private static IEnumerable<string> TeileTextInStreamChunks(string text, int maxZeichen = 48)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            int start = 0;
            while (start < text.Length)
            {
                int laenge = Math.Min(maxZeichen, text.Length - start);
                int ende = start + laenge;

                if (ende < text.Length)
                {
                    int letzterTrenner = -1;
                    for (int i = ende - 1; i > start; i--)
                    {
                        if (char.IsWhiteSpace(text[i]))
                        {
                            letzterTrenner = i + 1;
                            break;
                        }
                    }

                    if (letzterTrenner > start)
                        ende = letzterTrenner;
                }

                yield return text.Substring(start, ende - start);
                start = ende;
            }
        }

        private static void SendeFehlerEinmal(AnfrageItem item, int statusCode, string errorType, string message)
        {
            if (item == null || !item.ReserviereAntwort())
                return;

            string model = string.IsNullOrWhiteSpace(item.model) ? "billig-agi" : item.model;
            if (item.stream && item.streamingGestartet)
            {
                SendeStreamingFehler(item.context, item.completionId, item.created, model, errorType, message);
                return;
            }

            SendeFehler(item.context, statusCode, errorType, message);
        }

        private static void SendeStreamingFehler(HttpListenerContext ctx, string completionId, long created,
            string model, string errorType, string message)
        {
            try
            {
                var errorText = $"[FEHLER] {errorType}: {message}";
                foreach (string teil in TeileTextInStreamChunks(errorText))
                {
                    var contentChunk = NeuerStreamChunk(completionId, created, model,
                        new JObject { ["content"] = teil }, null);
                    SendeSseData(ctx, contentChunk.ToString(Formatting.None));
                }

                var finishChunk = NeuerStreamChunk(completionId, created, model, new JObject(), "stop");
                finishChunk["x_agi_metadata"] = new JObject
                {
                    ["error"] = errorType,
                    ["message"] = message
                };
                SendeSseData(ctx, finishChunk.ToString(Formatting.None));
                SendeSseData(ctx, "[DONE]");
                ctx.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AGIApi] Konnte Streaming-Fehler nicht senden: {ex.Message}");
                try { ctx.Response.OutputStream.Close(); }
                catch { }
            }
        }

        private static void SendeSseData(HttpListenerContext ctx, string data)
        {
            byte[] buffer = Encoding.UTF8.GetBytes($"data: {data}\n\n");
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.OutputStream.Flush();
        }

        private static void SendeSseKommentar(HttpListenerContext ctx, string kommentar)
        {
            byte[] buffer = Encoding.UTF8.GetBytes($": {kommentar}\n\n");
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.OutputStream.Flush();
        }

        private static void SendeJsonAntwort(HttpListenerContext ctx, int statusCode, string json)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                ctx.Response.StatusCode = statusCode;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.ContentLength64 = buffer.Length;
                ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                ctx.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AGIApi] Konnte JSON-Antwort nicht senden: {ex.Message}");
            }
        }

        private static void SendeFehler(HttpListenerContext ctx, int statusCode,
            string errorType, string message)
        {
            var error = new JObject
            {
                ["error"] = new JObject
                {
                    ["message"] = message,
                    ["type"] = errorType,
                    ["code"] = statusCode
                }
            };
            SendeJsonAntwort(ctx, statusCode, error.ToString());
        }
    }
}
