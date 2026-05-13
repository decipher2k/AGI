using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Kern
{
    [Serializable]
    public class PromptSzenarioObjekt
    {
        public string name;
        public string typ;
        public Vector3 position;
        public Vector3 geschwindigkeit;
        public float masseKg = 1f;
        public float reibung = 0.5f;
        public float luftWiderstand = 0.05f;
        public float elastizitaet = 0.2f;
        public float bruchSchwelle = 10f;
        public bool gebrochen;
        public string notiz;
    }

    [Serializable]
    public class PromptSzenarioSchritt
    {
        public float zeit;
        public string beschreibung;
    }

    [Serializable]
    public class PromptSzenarioAnalyse
    {
        public bool aktiv;
        public string frage;
        public string szenenBeschreibung;
        public List<PromptSzenarioObjekt> objekte = new();
        public List<PromptSzenarioSchritt> schritte = new();
        public List<string> einsichten = new();
        public float konfidenz;

        public string AlsKontextBlock()
        {
            if (!aktiv) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("[Automatisch aufgebaute Frage-Szene / mentale Physik-Simulation]");
            sb.AppendLine(szenenBeschreibung);
            if (objekte.Count > 0)
            {
                sb.AppendLine("Objekte und Parameter:");
                foreach (var obj in objekte.Take(8))
                {
                    sb.AppendLine($"- {obj.name}: m={obj.masseKg:F1}kg, reibung={obj.reibung:F2}, drag={obj.luftWiderstand:F2}, elastizitaet={obj.elastizitaet:F2}, bruch={obj.bruchSchwelle:F1}N, v=({obj.geschwindigkeit.x:F1},{obj.geschwindigkeit.y:F1},{obj.geschwindigkeit.z:F1})");
                }
            }
            if (schritte.Count > 0)
            {
                sb.AppendLine("Durchgespielte Schritte:");
                foreach (var schritt in schritte.Take(10))
                    sb.AppendLine($"- t={schritt.zeit:F1}s: {schritt.beschreibung}");
            }
            if (einsichten.Count > 0)
            {
                sb.AppendLine("Nutzbare Einsichten fuer die Antwort:");
                foreach (var einsicht in einsichten.Take(8))
                    sb.AppendLine($"- {einsicht}");
            }
            sb.AppendLine($"Simulations-Konfidenz: {konfidenz:F2}. Nutze diese Einsichten, aber kennzeichne Unsicherheit, wenn Promptdetails fehlen.");
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Baut aus einer natuerlichsprachlichen Frage eine kleine, interne Szene auf
    /// und spielt sie mit promptabhaengigen Physikparametern durch. Das ist kein
    /// Ersatz fuer Unity-PhysX, sondern ein schneller "Theater-im-Kopf"-Rollout,
    /// damit Antworten auf konkrete Szenarien nicht nur abstrakt formuliert werden.
    /// </summary>
    public class PromptSzenarioSimulator
    {
        private const float Gravitation = 9.81f;
        private const float DeltaZeit = 0.25f;
        private const int Schritte = 16;

        private static readonly Regex FrageRegex = new(@"\?|\b(was|wie|warum|wann|wohin|welch|passiert|geschieht|landet|faellt|fällt|rutscht|rollt|bricht|beschleunigt|bremst|kollidiert)\b", RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, (float masse, float reibung, float drag, float elast, float bruch)> Profile = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ball", (0.45f, 0.35f, 0.04f, 0.75f, 80f) },
            { "stein", (2.5f, 0.65f, 0.08f, 0.10f, 200f) },
            { "glas", (0.3f, 0.45f, 0.03f, 0.05f, 8f) },
            { "kiste", (5f, 0.7f, 0.10f, 0.15f, 120f) },
            { "auto", (1200f, 0.8f, 0.32f, 0.05f, 40000f) },
            { "fahrrad", (14f, 0.55f, 0.8f, 0.1f, 2000f) },
            { "papier", (0.01f, 0.8f, 1.8f, 0.05f, 2f) },
            { "feder", (0.005f, 0.6f, 2.5f, 0.05f, 1f) },
            { "block", (3f, 0.7f, 0.08f, 0.15f, 150f) },
            { "rampe", (20f, 0.55f, 0.02f, 0.1f, 1000f) },
            { "boden", (1000f, 0.6f, 0.0f, 0.2f, 100000f) }
        };

        public PromptSzenarioAnalyse AnalysiereFrage(string frage, WeltZustand aktuelleWelt = null)
        {
            var analyse = new PromptSzenarioAnalyse { frage = frage, aktiv = SollSimulieren(frage) };
            if (!analyse.aktiv) return analyse;

            string lower = frage.ToLowerInvariant();
            var objekte = ExtrahiereObjekte(lower, aktuelleWelt);
            WendePromptPhysikAn(objekte, lower);
            InitialisiereBewegung(objekte, lower);

            analyse.objekte.AddRange(objekte);
            analyse.szenenBeschreibung = BaueSzenenBeschreibung(lower, objekte);
            SpieleDurch(analyse, lower);
            analyse.konfidenz = BerechneKonfidenz(lower, objekte, analyse.einsichten.Count);
            return analyse;
        }

        private bool SollSimulieren(string frage)
        {
            if (string.IsNullOrWhiteSpace(frage)) return false;
            return FrageRegex.IsMatch(frage);
        }

        private List<PromptSzenarioObjekt> ExtrahiereObjekte(string lower, WeltZustand aktuelleWelt)
        {
            var objekte = new List<PromptSzenarioObjekt>();
            foreach (var profil in Profile)
            {
                if (lower.Contains(profil.Key))
                    objekte.Add(ErzeugeObjekt(profil.Key, profil.Key));
            }

            if (aktuelleWelt?.objekte != null)
            {
                foreach (var weltObj in aktuelleWelt.objekte.Values.Take(20))
                {
                    if (string.IsNullOrWhiteSpace(weltObj.name)) continue;
                    if (!lower.Contains(weltObj.name.ToLowerInvariant())) continue;
                    var obj = ErzeugeObjekt(weltObj.name, weltObj.typ);
                    if (weltObj.position != null && weltObj.position.Length >= 3)
                        obj.position = new Vector3(weltObj.position[0], weltObj.position[1], weltObj.position[2]);
                    if (weltObj.geschwindigkeit != null && weltObj.geschwindigkeit.Length >= 3)
                        obj.geschwindigkeit = new Vector3(weltObj.geschwindigkeit[0], weltObj.geschwindigkeit[1], weltObj.geschwindigkeit[2]);
                    obj.masseKg = Mathf.Max(0.001f, weltObj.masseKg);
                    obj.reibung = Mathf.Clamp01(weltObj.reibung);
                    obj.luftWiderstand = Mathf.Max(0f, weltObj.luftWiderstand);
                    obj.elastizitaet = Mathf.Clamp01(weltObj.elastizitaet);
                    obj.bruchSchwelle = Mathf.Max(0.1f, weltObj.bruchSchwelle);
                    objekte.Add(obj);
                }
            }

            if (objekte.Count == 0)
                objekte.Add(ErzeugeObjekt("objekt", "generisch"));
            if (!objekte.Any(o => o.typ == "boden"))
                objekte.Add(ErzeugeObjekt("boden", "boden"));

            for (int i = 0; i < objekte.Count; i++)
                objekte[i].position = new Vector3(i * 1.2f, objekte[i].typ == "boden" ? 0f : 1f, 0f);
            return objekte;
        }

        private PromptSzenarioObjekt ErzeugeObjekt(string name, string typ)
        {
            string key = Profile.Keys.FirstOrDefault(k => name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0 || (typ ?? string.Empty).IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) ?? "block";
            var p = Profile[key];
            return new PromptSzenarioObjekt
            {
                name = name,
                typ = key,
                masseKg = p.masse,
                reibung = p.reibung,
                luftWiderstand = p.drag,
                elastizitaet = p.elast,
                bruchSchwelle = p.bruch
            };
        }

        private void WendePromptPhysikAn(List<PromptSzenarioObjekt> objekte, string lower)
        {
            foreach (var obj in objekte)
            {
                if (lower.Contains("glatt") || lower.Contains("eis") || lower.Contains("rutschig")) obj.reibung *= 0.25f;
                if (lower.Contains("rau") || lower.Contains("sand") || lower.Contains("teppich")) obj.reibung = Mathf.Min(1f, obj.reibung + 0.3f);
                if (lower.Contains("aerodynamisch") || lower.Contains("stromlinien")) obj.luftWiderstand *= 0.35f;
                if (lower.Contains("wind") || lower.Contains("luftwiderstand")) obj.luftWiderstand = Mathf.Max(obj.luftWiderstand, 0.8f);
                if (lower.Contains("zerbrechlich") || lower.Contains("duenn") || lower.Contains("dünn")) obj.bruchSchwelle *= 0.35f;
                if (lower.Contains("gummi") || lower.Contains("springt") || lower.Contains("prallt")) obj.elastizitaet = Mathf.Max(obj.elastizitaet, 0.75f);
                if (lower.Contains("schwer")) obj.masseKg *= 2f;
                if (lower.Contains("leicht")) obj.masseKg *= 0.5f;
            }
        }

        private void InitialisiereBewegung(List<PromptSzenarioObjekt> objekte, string lower)
        {
            var bewegliches = objekte.FirstOrDefault(o => o.typ != "boden" && o.typ != "rampe") ?? objekte[0];
            float speed = lower.Contains("schnell") ? 12f : lower.Contains("langsam") ? 2f : 5f;
            if (lower.Contains("beschleunigt") || lower.Contains("speed up")) speed *= 1.5f;
            if (lower.Contains("bremst") || lower.Contains("brake") || lower.Contains("abbremst")) speed *= 0.6f;

            bewegliches.geschwindigkeit = new Vector3(speed, lower.Contains("wirft") || lower.Contains("fliegt") ? speed * 0.5f : 0f, 0f);
            if (lower.Contains("faellt") || lower.Contains("fällt") || lower.Contains("fallen"))
            {
                bewegliches.position = new Vector3(0f, 3f, 0f);
                bewegliches.geschwindigkeit = new Vector3(0f, 0f, 0f);
            }
        }

        private string BaueSzenenBeschreibung(string lower, List<PromptSzenarioObjekt> objekte)
        {
            var eigenschaften = new List<string>();
            if (lower.Contains("rampe")) eigenschaften.Add("geneigte Flaeche/Rampe");
            if (lower.Contains("glatt") || lower.Contains("eis")) eigenschaften.Add("niedrige Reibung");
            if (lower.Contains("rau") || lower.Contains("sand")) eigenschaften.Add("hohe Reibung");
            if (lower.Contains("wind") || lower.Contains("aerodynamisch")) eigenschaften.Add("Aerodynamik relevant");
            if (lower.Contains("bricht") || lower.Contains("zerbrechlich")) eigenschaften.Add("Bruchgrenzen relevant");
            return $"Interne Szene mit {objekte.Count} Objekten ({string.Join(", ", objekte.Select(o => o.name).Take(6))}); Eigenschaften: {(eigenschaften.Count == 0 ? "Standardgravitation und Kontaktreibung" : string.Join(", ", eigenschaften))}.";
        }

        private void SpieleDurch(PromptSzenarioAnalyse analyse, string lower)
        {
            var bewegliche = analyse.objekte.Where(o => o.typ != "boden" && o.typ != "rampe").ToList();
            if (bewegliche.Count == 0) return;

            for (int i = 0; i < Schritte; i++)
            {
                float zeit = (i + 1) * DeltaZeit;
                foreach (var obj in bewegliche)
                {
                    Vector3 vorher = obj.position;
                    Vector3 v = obj.geschwindigkeit;

                    bool inLuft = obj.position.y > 0.05f || Mathf.Abs(v.y) > 0.01f;
                    if (inLuft)
                        v.y -= Gravitation * DeltaZeit;

                    float dragFaktor = Mathf.Clamp01(1f - (obj.luftWiderstand / Mathf.Max(0.05f, obj.masseKg)) * DeltaZeit);
                    v.x *= dragFaktor;
                    v.z *= dragFaktor;

                    if (!inLuft || obj.position.y <= 0.05f)
                    {
                        float reibFaktor = Mathf.Clamp01(1f - obj.reibung * DeltaZeit);
                        v.x *= reibFaktor;
                        v.z *= reibFaktor;
                    }

                    if (lower.Contains("beschleunigt") || lower.Contains("speed up"))
                        v.x += 1.5f * DeltaZeit;
                    if (lower.Contains("bremst") || lower.Contains("brake") || lower.Contains("abbremst"))
                        v.x *= Mathf.Clamp01(1f - 1.2f * DeltaZeit);

                    obj.position += v * DeltaZeit;
                    if (obj.position.y <= 0f)
                    {
                        float impact = Mathf.Abs(v.y) * obj.masseKg;
                        obj.position = new Vector3(obj.position.x, 0f, obj.position.z);
                        if (impact > obj.bruchSchwelle)
                        {
                            obj.gebrochen = true;
                            analyse.einsichten.Add($"{obj.name} ueberschreitet beim Aufprall die Bruchschwelle ({impact:F1}N > {obj.bruchSchwelle:F1}N).");
                        }
                        v.y = Mathf.Abs(v.y) * obj.elastizitaet;
                        if (v.y < 0.2f) v.y = 0f;
                    }

                    obj.geschwindigkeit = v;
                    if (i % 4 == 0)
                        analyse.schritte.Add(new PromptSzenarioSchritt { zeit = zeit, beschreibung = $"{obj.name} bewegt sich von {Format(vorher)} nach {Format(obj.position)} mit Tempo {v.magnitude:F1} m/s." });
                }
            }

            foreach (var obj in bewegliche)
            {
                float horizontalSpeed = new Vector2(obj.geschwindigkeit.x, obj.geschwindigkeit.z).magnitude;
                if (horizontalSpeed < 0.2f)
                    analyse.einsichten.Add($"{obj.name} kommt durch Reibung/Luftwiderstand nahezu zum Stillstand.");
                else if (obj.reibung < 0.2f)
                    analyse.einsichten.Add($"Niedrige Reibung laesst {obj.name} weiter gleiten; Bremsweg ist lang.");
                else if (obj.luftWiderstand > 0.7f)
                    analyse.einsichten.Add($"Hoher Luftwiderstand reduziert die Geschwindigkeit von {obj.name} deutlich.");

                if (!obj.gebrochen && (lower.Contains("bricht") || lower.Contains("zerbrechlich")))
                    analyse.einsichten.Add($"{obj.name} bricht in dieser vereinfachten Simulation nicht, solange die Belastung unter {obj.bruchSchwelle:F1}N bleibt.");
            }
        }

        private float BerechneKonfidenz(string lower, List<PromptSzenarioObjekt> objekte, int einsichten)
        {
            float konf = 0.45f;
            if (objekte.Count > 1) konf += 0.15f;
            if (lower.Contains("glatt") || lower.Contains("rau") || lower.Contains("schnell") || lower.Contains("bremst") || lower.Contains("aerodynamisch") || lower.Contains("zerbrechlich")) konf += 0.25f;
            if (einsichten > 0) konf += 0.1f;
            return Mathf.Clamp01(konf);
        }

        private string Format(Vector3 v) => $"({v.x:F1},{v.y:F1},{v.z:F1})";
    }
}
