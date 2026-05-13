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
            public TaskCompletionSource<bool> fertig;
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

            // OpenAI-kompatibler Stream-Modus: die AGI erzeugt intern weiterhin
            // eine vollstaendige Antwort und liefert sie danach als SSE-Delta-Chunks aus.
            bool stream = requestObj["stream"]?.Value<bool>() ?? false;

            // Messages extrahieren
            var messages = requestObj["messages"] as JArray;
            if (messages == null || messages.Count == 0)
            {
                SendeFehler(ctx, 400, "invalid_request", "messages-Array fehlt oder leer");
                return;
            }

            string systemPrompt = null;
            var userParts = new StringBuilder();

            foreach (var msg in messages)
            {
                string role = msg["role"]?.ToString() ?? "";
                string content = msg["content"]?.ToString() ?? "";

                if (role == "system")
                    systemPrompt = content;
                else if (role == "user")
                {
                    if (userParts.Length > 0) userParts.AppendLine();
                    userParts.Append(content);
                }
                else if (role == "assistant")
                {
                    // Konversationsverlauf: vorherige Antwort als Kontext mitgeben
                    if (userParts.Length > 0) userParts.AppendLine();
                    userParts.Append($"[Vorherige Antwort: {content}]");
                }
            }

            string prompt = userParts.ToString();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                SendeFehler(ctx, 400, "invalid_request", "Kein User-Content in messages");
                return;
            }

            string model = requestObj["model"]?.ToString() ?? "billig-agi";

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
            agiKern.RegistriereWartendeApiAnfrage();
            warteschlange.Enqueue(new AnfrageItem
            {
                context = ctx,
                prompt = prompt,
                systemPrompt = systemPrompt,
                model = model,
                stream = stream,
                fertig = tcs
            });

            // Blockierend auf Main-Thread-Verarbeitung warten (Listener-Thread bleibt hier stehen)
            tcs.Task.Wait();
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

                string completionId = $"chatcmpl-agi-{Guid.NewGuid():N}";
                long created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string model = string.IsNullOrWhiteSpace(item.model) ? "billig-agi" : item.model;
                string modus = agiKern.GetModus();
                float llmKosten = agiKern.GetLLM()?.GesamtKosten ?? 0f;

                if (item.stream)
                {
                    SendeStreamingAntwort(item.context, completionId, created, model, antwort ?? "",
                        promptTokens, completionTokens, dauerMs, modus, llmKosten);
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

                SendeJsonAntwort(item.context, 200, response.ToString());
                Debug.Log($"[AGIApi] Antwort gesendet ({dauerMs:F0}ms, ~{promptTokens + completionTokens} tokens)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AGIApi] Verarbeitungsfehler: {ex.Message}");
                SendeFehler(item.context, 500, "processing_error", ex.Message);
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

        private static void SendeStreamingAntwort(HttpListenerContext ctx, string completionId, long created,
            string model, string antwort, int promptTokens, int completionTokens, float dauerMs,
            string modus, float llmKosten)
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

        private static void SendeSseData(HttpListenerContext ctx, string data)
        {
            byte[] buffer = Encoding.UTF8.GetBytes($"data: {data}\n\n");
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
            catch { }
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
