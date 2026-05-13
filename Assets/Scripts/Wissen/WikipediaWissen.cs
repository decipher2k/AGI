using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BilligAGI.Wissen
{
    /// <summary>
    /// Kleine, optionale Wikipedia-RAG-Schicht: sucht relevante Artikel live ueber die MediaWiki API,
    /// extrahiert kurze Text-Chunks und liefert sie getrennt vom autobiographischen Gedaechtnis.
    /// </summary>
    public class WikipediaWissen
    {
        private readonly AGIConfig config;
        private readonly HttpClient client;
        private readonly Dictionary<string, CacheEintrag> cache = new Dictionary<string, CacheEintrag>();

        public WikipediaWissen(AGIConfig config)
        {
            this.config = config;
            client = new HttpClient { Timeout = TimeSpan.FromSeconds(Mathf.Max(1f, config.wikipediaTimeoutSekunden)) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BilligAGI/0.1 (Wikipedia-RAG; Unity)");
        }

        public async Task<List<WissensDokument>> Suche(string frage)
        {
            if (!config.wikipediaRagAktiv || string.IsNullOrWhiteSpace(frage))
                return new List<WissensDokument>();

            string query = NormalisiereQuery(frage);
            if (string.IsNullOrWhiteSpace(query))
                return new List<WissensDokument>();

            if (cache.TryGetValue(query, out var cached) && cached.ablauf > DateTime.UtcNow)
                return cached.dokumente;

            try
            {
                var treffer = await SucheArtikel(query);
                var dokumente = new List<WissensDokument>();

                foreach (var trefferInfo in treffer.Take(Mathf.Max(1, config.wikipediaMaxArtikel)))
                {
                    string extrakt = await LadeArtikelExtrakt(trefferInfo.pageId);
                    dokumente.AddRange(ChunkArtikel(trefferInfo, extrakt, query));
                }

                var top = dokumente
                    .OrderByDescending(d => d.score)
                    .Take(Mathf.Max(1, config.wikipediaTopK))
                    .ToList();

                cache[query] = new CacheEintrag
                {
                    ablauf = DateTime.UtcNow.AddMinutes(Mathf.Max(1, config.wikipediaCacheMinuten)),
                    dokumente = top
                };

                return top;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WikipediaWissen] Suche fehlgeschlagen: {ex.Message}");
                return new List<WissensDokument>();
            }
        }

        private async Task<List<ArtikelTreffer>> SucheArtikel(string query)
        {
            string url = $"{config.wikipediaApiUrl}?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&srlimit={Mathf.Max(1, config.wikipediaMaxArtikel)}&utf8=1&format=json&origin=*";
            string json = await client.GetStringAsync(url);
            var root = JObject.Parse(json);
            var results = root["query"]?["search"] as JArray;
            var treffer = new List<ArtikelTreffer>();
            if (results == null) return treffer;

            foreach (var item in results)
            {
                treffer.Add(new ArtikelTreffer
                {
                    pageId = item.Value<int?>("pageid") ?? 0,
                    titel = item.Value<string>("title") ?? "Unbekannt",
                    snippet = EntferneHtml(item.Value<string>("snippet") ?? ""),
                    rangBonus = 1f / (treffer.Count + 1)
                });
            }
            return treffer.Where(t => t.pageId > 0).ToList();
        }

        private async Task<string> LadeArtikelExtrakt(int pageId)
        {
            string url = $"{config.wikipediaApiUrl}?action=query&prop=extracts&explaintext=1&exsectionformat=plain&pageids={pageId}&format=json&origin=*";
            string json = await client.GetStringAsync(url);
            var root = JObject.Parse(json);
            var page = root["query"]?["pages"]?[pageId.ToString()];
            return page?.Value<string>("extract") ?? "";
        }

        private IEnumerable<WissensDokument> ChunkArtikel(ArtikelTreffer artikel, string extrakt, string query)
        {
            if (string.IsNullOrWhiteSpace(extrakt)) yield break;

            string[] abschnitte = extrakt
                .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            int index = 0;
            foreach (string roh in abschnitte)
            {
                string text = roh.Trim();
                if (text.Length < 80) continue;

                foreach (string chunk in SplitteChunk(text, Mathf.Max(300, config.wikipediaChunkZeichen)))
                {
                    float score = Score(query, artikel.titel, chunk) + artikel.rangBonus;
                    yield return new WissensDokument
                    {
                        id = $"wiki:{artikel.pageId}:{index}",
                        quelle = "wikipedia",
                        titel = artikel.titel,
                        abschnitt = $"Abschnitt {index + 1}",
                        text = chunk,
                        url = $"{config.wikipediaArtikelBasisUrl}{Uri.EscapeDataString(artikel.titel.Replace(' ', '_'))}",
                        revision = "live-mediawiki-api",
                        sprache = config.wikipediaSprache,
                        zeitstempel = DateTime.UtcNow.ToString("o"),
                        score = score
                    };
                    index++;
                }
            }
        }

        private static IEnumerable<string> SplitteChunk(string text, int maxZeichen)
        {
            if (text.Length <= maxZeichen)
            {
                yield return text;
                yield break;
            }

            for (int start = 0; start < text.Length; start += maxZeichen)
            {
                int len = Math.Min(maxZeichen, text.Length - start);
                yield return text.Substring(start, len).Trim();
            }
        }

        private static float Score(string query, string titel, string text)
        {
            var tokens = Tokenisiere(query);
            if (tokens.Count == 0) return 0f;

            string haystack = ((titel ?? "") + " " + (text ?? "")).ToLowerInvariant();
            int hits = tokens.Count(token => haystack.Contains(token));
            return hits / (float)tokens.Count;
        }

        private static List<string> Tokenisiere(string text)
        {
            char[] trennzeichen = { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '?', '!', '(', ')', '[', ']', '"', '\'', '/', '\\', '-' };
            return (text ?? "")
                .ToLowerInvariant()
                .Split(trennzeichen, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2 && !IstStopwort(t))
                .Distinct()
                .ToList();
        }

        private static bool IstStopwort(string token)
        {
            string[] stop = { "was", "wer", "wie", "wo", "wann", "warum", "ist", "sind", "der", "die", "das", "ein", "eine", "und", "oder", "mit", "von", "ueber", "über", "bitte", "erklaer", "erklär" };
            return stop.Contains(token);
        }

        private static string NormalisiereQuery(string frage)
        {
            string q = frage.Trim();
            string[] prefixe = { "was ist", "was sind", "wer ist", "wer war", "erklaer", "erklär", "definiere", "wie funktioniert" };
            foreach (string prefix in prefixe)
            {
                if (q.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    q = q.Substring(prefix.Length).Trim(' ', '?', '.', ':');
                    break;
                }
            }
            return string.IsNullOrWhiteSpace(q) ? frage.Trim() : q;
        }

        private static string EntferneHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("<span class=\"searchmatch\">", "").Replace("</span>", "");
        }

        private class ArtikelTreffer
        {
            public int pageId;
            public string titel;
            public string snippet;
            public float rangBonus;
        }

        private class CacheEintrag
        {
            public DateTime ablauf;
            public List<WissensDokument> dokumente;
        }
    }
}
