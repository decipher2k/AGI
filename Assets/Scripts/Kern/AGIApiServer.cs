using System;
using System.Collections.Concurrent;
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
                            ["status"] = agiKern?.IstBereit() == true ? "ready" : "initializing",
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

            // Stream-Modus pruefen
            bool stream = requestObj["stream"]?.Value<bool>() ?? false;
            if (stream)
            {
                SendeFehler(ctx, 400, "streaming_not_supported",
                    "Billig-AGI unterstuetzt kein Streaming. Setze stream=false.");
                return;
            }

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

            // AGI ist nicht bereit?
            if (agiKern == null || !agiKern.IstBereit())
            {
                SendeFehler(ctx, 503, "model_not_ready", "AGI initialisiert noch oder verarbeitet eine Anfrage");
                return;
            }

            // Anfrage in Queue fuer Main-Thread einreihen
            var tcs = new TaskCompletionSource<bool>();
            warteschlange.Enqueue(new AnfrageItem
            {
                context = ctx,
                prompt = prompt,
                systemPrompt = systemPrompt,
                model = model,
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
                Debug.Log($"[AGIApi] Anfrage empfangen: {item.prompt.Substring(0, Math.Min(item.prompt.Length, 100))}...");

                // AGI-Zyklus durchlaufen
                string antwort = await agiKern.VerarbeiteAnfrageAsync(item.prompt, item.systemPrompt);
                float dauerMs = (float)(DateTime.UtcNow - startTime).TotalMilliseconds;

                // Token-Schaetzung (grob: 4 Zeichen pro Token)
                int promptTokens = item.prompt.Length / 4;
                int completionTokens = (antwort?.Length ?? 0) / 4;

                // OpenAI-kompatible Antwort bauen
                var response = new JObject
                {
                    ["id"] = $"chatcmpl-agi-{Guid.NewGuid():N}",
                    ["object"] = "chat.completion",
                    ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ["model"] = "billig-agi",
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
                        ["modus"] = agiKern.GetModus(),
                        ["llm_kosten"] = agiKern.GetLLM()?.GesamtKosten ?? 0f
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
