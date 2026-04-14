using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BilligAGI.Daten;
using BilligAGI.Kern;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BilligAGI.Evaluation
{
    [Serializable]
    public class Arc2Pair
    {
        public int[][] input;
        public int[][] output;
    }

    [Serializable]
    public class Arc2Task
    {
        public string id;
        public List<Arc2Pair> train = new();
        public List<Arc2Pair> test = new();
    }

    [Serializable]
    public class Arc2TaskSet
    {
        public List<Arc2Task> tasks = new();
    }

    [Serializable]
    public class Arc2TaskErgebnis
    {
        public string taskId;
        public bool exaktRichtig;
        public bool jsonParsebar;
        public long dauerMs;
        public string fehler;
    }

    [Serializable]
    public class Arc2Report
    {
        public string zeitstempel;
        public int anzahlTasks;
        public int ausgewertet;
        public int exaktRichtig;
        public float exaktQuote;
        public float jsonParseQuote;
        public long durchschnittMs;
        public int llmCallsGesamt;
        public float baselineCopyQuote;
        public List<Arc2TaskErgebnis> ergebnisse = new();
    }

    // ARC-2 Baseline-Evaluator fuer Billig-AGI.
    // Erwartet Tasks in Assets/StreamingAssets/Data/arc2_tasks.json
    // oder als einzelne Dateien unter Assets/StreamingAssets/Data/arc2/*.json
    public class Arc2Evaluator
    {
        private readonly AGIKern kern;
        private readonly AGIConfig config;

        private Arc2Report letzterReport;

        private const string TASKS_DATEI = "arc2_tasks.json";
        private const string TASKS_ORDNER = "arc2";
        private const string REPORT_DATEI = "arc2_report_last.json";

        public Arc2Evaluator(AGIKern kern, AGIConfig config)
        {
            this.kern = kern;
            this.config = config;
        }

        public async Task<Arc2Report> FuehreAus(int maxTasks = 20)
        {
            var tasks = LadeTasks();
            if (tasks.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[ARC2] Keine Tasks gefunden. Erwartet Data/arc2_tasks.json oder Data/arc2/*.json");
                letzterReport = new Arc2Report
                {
                    zeitstempel = DateTime.UtcNow.ToString("o"),
                    anzahlTasks = 0,
                    ausgewertet = 0
                };
                return letzterReport;
            }

            int limit = Mathf.Clamp(maxTasks, 1, tasks.Count);
            var subset = tasks.Take(limit).ToList();

            var report = new Arc2Report
            {
                zeitstempel = DateTime.UtcNow.ToString("o"),
                anzahlTasks = tasks.Count,
                ausgewertet = limit
            };

            long msGesamt = 0;
            int parsebar = 0;
            int korrekt = 0;
            int baselineKorrekt = 0;

            int llmVorher = kern.GetLLM()?.GetAnzahlCalls() ?? 0;

            foreach (var task in subset)
            {
                var erg = await EvaluiereTask(task);
                report.ergebnisse.Add(erg);
                msGesamt += erg.dauerMs;

                if (erg.jsonParsebar) parsebar++;
                if (erg.exaktRichtig) korrekt++;

                if (IstCopyBaselineRichtig(task))
                    baselineKorrekt++;
            }

            int llmNachher = kern.GetLLM()?.GetAnzahlCalls() ?? llmVorher;

            report.exaktRichtig = korrekt;
            report.exaktQuote = limit > 0 ? korrekt / (float)limit : 0f;
            report.jsonParseQuote = limit > 0 ? parsebar / (float)limit : 0f;
            report.durchschnittMs = limit > 0 ? msGesamt / limit : 0;
            report.llmCallsGesamt = Mathf.Max(0, llmNachher - llmVorher);
            report.baselineCopyQuote = limit > 0 ? baselineKorrekt / (float)limit : 0f;

            letzterReport = report;
            DatenLader.Speichere(REPORT_DATEI, report);

            UnityEngine.Debug.Log($"[ARC2] Fertig: Exakt={report.exaktQuote:P1}, Parse={report.jsonParseQuote:P1}, " +
                                  $"Avg={report.durchschnittMs}ms, LLMCalls={report.llmCallsGesamt}");

            return report;
        }

        public Arc2Report GetLetzterReport()
        {
            return letzterReport ?? DatenLader.Lade<Arc2Report>(REPORT_DATEI);
        }

        public string GetStatusText()
        {
            var r = GetLetzterReport();
            if (r == null || r.ausgewertet <= 0)
                return "Noch kein ARC-2 Lauf.";

            return $"ARC2: Exakt {r.exaktQuote:P1} ({r.exaktRichtig}/{r.ausgewertet}) | " +
                   $"JSON {r.jsonParseQuote:P1} | Avg {r.durchschnittMs}ms | " +
                   $"LLMCalls {r.llmCallsGesamt} | Copy-Baseline {r.baselineCopyQuote:P1}";
        }

        private async Task<Arc2TaskErgebnis> EvaluiereTask(Arc2Task task)
        {
            var sw = Stopwatch.StartNew();
            var result = new Arc2TaskErgebnis { taskId = task.id };

            try
            {
                if (task.test == null || task.test.Count == 0 || task.test[0]?.output == null)
                {
                    result.fehler = "Task ohne test/output";
                    result.jsonParsebar = false;
                    result.exaktRichtig = false;
                    return result;
                }

                string prompt = BauePrompt(task);
                string system = "Du loest ARC-2 Rasteraufgaben. Gib AUSSCHLIESSLICH ein JSON 2D-Array mit Zahlen 0-9 zurueck.";

                var llm = kern.GetLLM();
                if (llm == null)
                {
                    result.fehler = "LLM nicht verfuegbar";
                    result.jsonParsebar = false;
                    result.exaktRichtig = false;
                    return result;
                }

                var llmAntwort = await llm.FreieAnfrage(prompt, system);
                string antwort = llmAntwort?.text;
                int[][] vorhersage = ParseGridAusAntwort(antwort);
                int[][] ziel = task.test[0].output;

                result.jsonParsebar = vorhersage != null;
                result.exaktRichtig = vorhersage != null && GridsIdentisch(vorhersage, ziel);
            }
            catch (Exception ex)
            {
                result.fehler = ex.Message;
                result.jsonParsebar = false;
                result.exaktRichtig = false;
            }
            finally
            {
                sw.Stop();
                result.dauerMs = sw.ElapsedMilliseconds;
            }

            return result;
        }

        private static string BauePrompt(Arc2Task task)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Lerne die Transformation aus den Beispielen und gib nur das Output-Grid fuer den Test zurueck.");
            sb.AppendLine();

            for (int i = 0; i < task.train.Count; i++)
            {
                sb.AppendLine($"Train {i + 1} Input:");
                sb.AppendLine(JsonConvert.SerializeObject(task.train[i].input));
                sb.AppendLine($"Train {i + 1} Output:");
                sb.AppendLine(JsonConvert.SerializeObject(task.train[i].output));
                sb.AppendLine();
            }

            sb.AppendLine("Test Input:");
            sb.AppendLine(JsonConvert.SerializeObject(task.test[0].input));
            sb.AppendLine();
            sb.AppendLine("Antworte nur mit dem JSON 2D-Array des Test-Outputs.");
            return sb.ToString();
        }

        private static int[][] ParseGridAusAntwort(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            string normalized = text.Trim();
            normalized = normalized.Replace("```json", "").Replace("```", "").Trim();

            // 1) Fast path: gesamte Antwort ist bereits das Grid.
            if (TryParseGrid(normalized, out var fullGrid))
                return fullGrid;

            // 2) Fallback: Bei Antworten mit mehreren Arrays (z.B. Erklaerung + Beispiele)
            // den letzten gueltigen 2D-Grid-Block verwenden.
            var bloecke = ExtrahiereJsonArrayBloecke(normalized);
            for (int i = bloecke.Count - 1; i >= 0; i--)
            {
                if (TryParseGrid(bloecke[i], out var kandidat))
                    return kandidat;
            }

            return null;
        }

        private static bool TryParseGrid(string text, out int[][] grid)
        {
            grid = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            try
            {
                var token = JToken.Parse(text);
                if (token is not JArray outer || outer.Count == 0) return false;

                var rows = new List<int[]>();
                int breite = -1;

                foreach (var rowToken in outer)
                {
                    if (rowToken is not JArray rowArr || rowArr.Count == 0) return false;

                    var row = new int[rowArr.Count];
                    for (int i = 0; i < rowArr.Count; i++)
                    {
                        if (rowArr[i] == null || rowArr[i].Type != JTokenType.Integer)
                            return false;

                        int value = rowArr[i].Value<int>();
                        if (value < 0 || value > 9)
                            return false;
                        row[i] = value;
                    }

                    if (breite < 0) breite = row.Length;
                    if (row.Length != breite) return false;

                    rows.Add(row);
                }

                grid = rows.ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static List<string> ExtrahiereJsonArrayBloecke(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            int start = -1;
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '[')
                {
                    if (depth == 0)
                        start = i;
                    depth++;
                }
                else if (c == ']')
                {
                    if (depth == 0)
                        continue;

                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        result.Add(text.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return result;
        }

        private static bool GridsIdentisch(int[][] a, int[][] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == null || b[i] == null) return false;
                if (a[i].Length != b[i].Length) return false;

                for (int j = 0; j < a[i].Length; j++)
                {
                    if (a[i][j] != b[i][j]) return false;
                }
            }

            return true;
        }

        private static bool IstCopyBaselineRichtig(Arc2Task task)
        {
            if (task?.test == null || task.test.Count == 0) return false;
            var t = task.test[0];
            if (t?.input == null || t.output == null) return false;
            return GridsIdentisch(t.input, t.output);
        }

        private static List<Arc2Task> LadeTasks()
        {
            var result = new List<Arc2Task>();

            // 1) Sammeldatei
            var set = DatenLader.Lade<Arc2TaskSet>(TASKS_DATEI);
            if (set?.tasks != null && set.tasks.Count > 0)
                result.AddRange(set.tasks.Where(IstTaskValid));

            // 2) Einzeldateien in Data/arc2/*.json
            string dataRoot = Path.Combine(Application.streamingAssetsPath, "Data");
            string ordner = Path.Combine(dataRoot, TASKS_ORDNER);

            if (Directory.Exists(ordner))
            {
                foreach (var file in Directory.GetFiles(ordner, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var task = JsonConvert.DeserializeObject<Arc2Task>(json);
                        if (task != null && IstTaskValid(task))
                        {
                            if (string.IsNullOrWhiteSpace(task.id))
                                task.id = Path.GetFileNameWithoutExtension(file);
                            result.Add(task);
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"[ARC2] Ueberspringe defekte Task-Datei {file}: {ex.Message}");
                    }
                }
            }

            // Doppelte IDs entfernen (Datei-Variante bevorzugt spaeter geladen)
            return result
                .GroupBy(t => string.IsNullOrWhiteSpace(t.id) ? Guid.NewGuid().ToString("N") : t.id)
                .Select(g => g.Last())
                .ToList();
        }

        private static bool IstTaskValid(Arc2Task t)
        {
            return t != null &&
                   t.train != null && t.train.Count > 0 &&
                   t.test != null && t.test.Count > 0 &&
                   t.train.All(p => p?.input != null && p.output != null) &&
                   t.test.All(p => p?.input != null && p.output != null);
        }
    }
}
