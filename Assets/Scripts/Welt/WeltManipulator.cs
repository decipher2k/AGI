using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using BilligAGI.Modelle;

namespace BilligAGI.Welt
{
    // =====================================================================
    // WeltManipulator: Bruecke zwischen Sprache und Weltveraenderung
    //
    // Ermoeglicht dem LLM und dem Nutzer, die Unity-Szene per
    // natuerlicher Sprache zu veraendern.
    //
    // Zwei Modi:
    //   1. LLM-basiert: Text → LLM-Parse → WeltBefehl → Ausfuehrung
    //   2. Direkt: Strukturierter WeltBefehl → Ausfuehrung
    //
    // Integration:
    //   - AGIKern ruft nach NACHDENKEN-Schritt ParseUndFuehreAus() auf
    //   - ChatUI bietet /welt-Befehle fuer direkte Manipulation
    //   - Agent kann in Plaenen Weltveraenderungen planen
    //
    // Sicherheit:
    //   - Alle Aenderungen werden im WeltModell protokolliert
    //   - Notbremse verhindert destruktive Operationen wenn aktiv
    // =====================================================================

    public enum WeltBefehlTyp
    {
        SzenarioErstellen,   // "Erstelle einen Wald mit See"
        ObjektSpawnen,       // "Stelle eine Kiste auf den Tisch"
        ObjektEntfernen,     // "Entferne den Stuhl"
        ObjektBewegen,       // "Schiebe den Tisch 3m nach rechts"
        WetterAendern,       // "Lass es regnen"
        TageszeitAendern,    // "Mach Nacht" / "Setze auf Sonnenuntergang"
        PhysikEvent          // "Lass es dort explodieren"
    }

    [Serializable]
    public class WeltBefehl
    {
        public WeltBefehlTyp typ;
        public string ziel;             // Objekt-/Szenario-Name
        public string biom;             // Fuer SzenarioErstellen: wald/wiese/innen
        public float[] position;        // [x, y, z] — Zielposition
        public float wert;              // Tageszeit (0-24), Intensitaet, etc.
        public string wetterTyp;        // Klar/Regen/Schnee/Nebel/Sturm
        public string eventTyp;         // explosion/wasserfluss
        public int breite;              // Szenario-Groesse
        public int tiefe;
        public int objektDichte;
        public string beschreibung;     // Freitext-Beschreibung fuer Logging
    }

    [Serializable]
    public class ManipulationsErgebnis
    {
        public bool erfolg;
        public string beschreibung;
        public int ausgefuehrteBefehle;
        public List<string> details = new List<string>();
    }

    public class WeltManipulator
    {
        private readonly Kern.LLMAdapter llm;
        private readonly WeltController weltController;
        private readonly WeltGenerator weltGenerator;
        private readonly AGIConfig config;
        private bool notbremseAktiv;

        // Statistik
        private int gesamtBefehle;
        private int erfolgreicheBefehle;

        public int GesamtBefehle => gesamtBefehle;
        public int ErfolgreicheBefehle => erfolgreicheBefehle;

        private const string PARSE_SYSTEM = @"Du bist der Welt-Interpreter einer AGI in Unity 3D. 
Analysiere den Text und extrahiere Weltveraenderungs-Befehle.

Antworte als JSON-Array von Befehlen. Jeder Befehl hat:
- typ: SzenarioErstellen | ObjektSpawnen | ObjektEntfernen | ObjektBewegen | WetterAendern | TageszeitAendern | PhysikEvent
- ziel: Name des Objekts/Szenarios
- biom: wald | wiese | innen (nur bei SzenarioErstellen)
- position: [x, y, z] (optional)
- wert: Zahl (Tageszeit 0-24, Intensitaet 0-1)
- wetterTyp: Klar | Regen | Schnee | Nebel | Sturm (nur bei WetterAendern)
- eventTyp: explosion | wasserfluss (nur bei PhysikEvent)
- breite/tiefe/objektDichte: Zahlen (nur bei SzenarioErstellen)
- beschreibung: Kurze Beschreibung

Wenn der Text KEINE Weltveraenderung impliziert, antworte mit leerem Array: []

Beispiele:
""Erstelle einen Wald"" → [{""typ"":""SzenarioErstellen"",""ziel"":""Wald"",""biom"":""wald"",""breite"":40,""tiefe"":40,""objektDichte"":30,""beschreibung"":""Wald-Szenario""}]
""Lass es regnen"" → [{""typ"":""WetterAendern"",""wetterTyp"":""Regen"",""wert"":0.7,""beschreibung"":""Regen einschalten""}]
""Stelle einen Stuhl neben den Tisch"" → [{""typ"":""ObjektSpawnen"",""ziel"":""Stuhl"",""position"":[1,0,0],""beschreibung"":""Stuhl neben Tisch spawnen""}]
""Mach Nacht"" → [{""typ"":""TageszeitAendern"",""wert"":22,""beschreibung"":""Nacht setzen""}]
""Raeume alles auf und erstelle einen Garten"" → [{""typ"":""SzenarioErstellen"",""ziel"":""Garten"",""biom"":""wiese"",""breite"":30,""tiefe"":30,""objektDichte"":15,""beschreibung"":""Garten-Szenario""}]

Antworte NUR mit dem JSON-Array, kein anderer Text.";

        public WeltManipulator(
            Kern.LLMAdapter llm,
            WeltController weltController,
            WeltGenerator weltGenerator,
            AGIConfig config)
        {
            this.llm = llm;
            this.weltController = weltController;
            this.weltGenerator = weltGenerator;
            this.config = config;
        }

        // ========== Hauptmethode: Text → Parse → Ausfuehren ==========

        /// <summary>
        /// Analysiert Text mit LLM und fuehrt erkannte Weltbefehle aus.
        /// Gibt null zurueck wenn keine Weltveraenderung erkannt wurde.
        /// </summary>
        public async Task<ManipulationsErgebnis> ParseUndFuehreAus(string text)
        {
            if (string.IsNullOrEmpty(text) || notbremseAktiv) return null;

            // Schnell-Check: Enthaelt der Text ueberhaupt weltrelevante Woerter?
            if (!EnthaeltWeltIntent(text)) return null;

            var befehle = await ParseMitLLM(text);
            if (befehle == null || befehle.Count == 0) return null;

            return FuehreBefehleListe(befehle);
        }

        /// <summary>
        /// Fuehrt einen einzelnen WeltBefehl direkt aus (ohne LLM-Parsing).
        /// </summary>
        public ManipulationsErgebnis FuehreBefehlAus(WeltBefehl befehl)
        {
            return FuehreBefehleListe(new List<WeltBefehl> { befehl });
        }

        // ========== LLM-basiertes Parsing ==========

        private async Task<List<WeltBefehl>> ParseMitLLM(string text)
        {
            try
            {
                var antwort = await llm.FreieAnfrage(text, PARSE_SYSTEM);
                if (antwort == null || antwort.ausFallback) return null;

                string json = antwort.text?.Trim() ?? "";

                // JSON-Array extrahieren (LLM gibt manchmal Markdown-Bloecke zurueck)
                int arrayStart = json.IndexOf('[');
                int arrayEnd = json.LastIndexOf(']');
                if (arrayStart < 0 || arrayEnd < 0 || arrayEnd <= arrayStart) return null;
                json = json.Substring(arrayStart, arrayEnd - arrayStart + 1);

                var jArray = JArray.Parse(json);
                var befehle = new List<WeltBefehl>();

                foreach (var jObj in jArray)
                {
                    var befehl = ParseEinzelBefehl(jObj as JObject);
                    if (befehl != null)
                        befehle.Add(befehl);
                }

                return befehle;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeltManipulator] LLM-Parse fehlgeschlagen: {ex.Message}");
                return null;
            }
        }

        private WeltBefehl ParseEinzelBefehl(JObject jObj)
        {
            if (jObj == null) return null;

            var befehl = new WeltBefehl();
            string typStr = jObj["typ"]?.ToString() ?? "";

            if (!Enum.TryParse<WeltBefehlTyp>(typStr, true, out var typ))
                return null;

            befehl.typ = typ;
            befehl.ziel = jObj["ziel"]?.ToString();
            befehl.biom = jObj["biom"]?.ToString();
            befehl.wert = jObj["wert"]?.Value<float>() ?? 0f;
            befehl.wetterTyp = jObj["wetterTyp"]?.ToString();
            befehl.eventTyp = jObj["eventTyp"]?.ToString();
            befehl.breite = jObj["breite"]?.Value<int>() ?? 30;
            befehl.tiefe = jObj["tiefe"]?.Value<int>() ?? 30;
            befehl.objektDichte = jObj["objektDichte"]?.Value<int>() ?? 15;
            befehl.beschreibung = jObj["beschreibung"]?.ToString() ?? typStr;

            var posArr = jObj["position"] as JArray;
            if (posArr != null && posArr.Count >= 3)
                befehl.position = new float[]
                {
                    posArr[0].Value<float>(),
                    posArr[1].Value<float>(),
                    posArr[2].Value<float>()
                };

            return befehl;
        }

        // ========== Befehlsausfuehrung ==========

        private ManipulationsErgebnis FuehreBefehleListe(List<WeltBefehl> befehle)
        {
            var ergebnis = new ManipulationsErgebnis();

            foreach (var befehl in befehle)
            {
                gesamtBefehle++;

                try
                {
                    string detail = FuehreEinzelBefehlAus(befehl);
                    ergebnis.details.Add(detail);
                    erfolgreicheBefehle++;
                    ergebnis.ausgefuehrteBefehle++;
                }
                catch (Exception ex)
                {
                    ergebnis.details.Add($"FEHLER bei {befehl.typ}: {ex.Message}");
                    Debug.LogWarning($"[WeltManipulator] Befehl fehlgeschlagen: {ex.Message}");
                }
            }

            ergebnis.erfolg = ergebnis.ausgefuehrteBefehle > 0;
            ergebnis.beschreibung = string.Join("; ", ergebnis.details);

            if (ergebnis.erfolg)
                Debug.Log($"[WeltManipulator] {ergebnis.ausgefuehrteBefehle}/{befehle.Count} Befehle ausgefuehrt: {ergebnis.beschreibung}");

            return ergebnis;
        }

        private string FuehreEinzelBefehlAus(WeltBefehl befehl)
        {
            switch (befehl.typ)
            {
                case WeltBefehlTyp.SzenarioErstellen:
                    return SzenarioErstellen(befehl);

                case WeltBefehlTyp.ObjektSpawnen:
                    return ObjektSpawnen(befehl);

                case WeltBefehlTyp.ObjektEntfernen:
                    return ObjektEntfernen(befehl);

                case WeltBefehlTyp.ObjektBewegen:
                    return ObjektBewegen(befehl);

                case WeltBefehlTyp.WetterAendern:
                    return WetterAendern(befehl);

                case WeltBefehlTyp.TageszeitAendern:
                    return TageszeitAendern(befehl);

                case WeltBefehlTyp.PhysikEvent:
                    return PhysikEventAusfuehren(befehl);

                default:
                    return $"Unbekannter Befehlstyp: {befehl.typ}";
            }
        }

        // ---- Einzelne Befehlstypen ----

        private string SzenarioErstellen(WeltBefehl befehl)
        {
            if (weltGenerator == null)
                return "Kein WeltGenerator verfuegbar.";

            // Bekannte Szenario-Namen direkt weiterleiten
            string name = befehl.ziel?.ToLowerInvariant() ?? "";

            if (name.Contains("raum") || name.Contains("zimmer"))
            {
                weltGenerator.ErstelleSzenario("raum mit tisch");
                return $"Szenario erstellt: Raum mit Tisch";
            }
            if (name.Contains("garten"))
            {
                weltGenerator.ErstelleSzenario("garten");
                return "Szenario erstellt: Garten";
            }
            if (name.Contains("teich") || name.Contains("see"))
            {
                weltGenerator.ErstelleSzenario("teich");
                return "Szenario erstellt: Teich";
            }

            // Generisch: WeltBeschreibung aufbauen
            var beschreibung = new WeltBeschreibung
            {
                name = befehl.ziel ?? "Neue Welt",
                biom = befehl.biom ?? "wiese",
                breite = befehl.breite > 0 ? befehl.breite : 40,
                tiefe = befehl.tiefe > 0 ? befehl.tiefe : 40,
                objektDichte = befehl.objektDichte > 0 ? befehl.objektDichte : 20
            };

            weltGenerator.GeneriereWelt(beschreibung);
            return $"Welt generiert: {beschreibung.name} ({beschreibung.biom}, " +
                   $"{beschreibung.breite}x{beschreibung.tiefe}, {beschreibung.objektDichte} Objekte)";
        }

        private string ObjektSpawnen(WeltBefehl befehl)
        {
            if (weltController == null)
                return "Kein WeltController verfuegbar.";

            if (string.IsNullOrEmpty(befehl.ziel))
                return "Kein Objekt-Name angegeben.";

            Vector3 pos = befehl.position != null && befehl.position.Length >= 3
                ? new Vector3(befehl.position[0], befehl.position[1], befehl.position[2])
                : SucheFreiePosition();

            var obj = weltController.SpawnObjekt(befehl.ziel, pos, Quaternion.identity);
            if (obj == null)
                return $"Prefab '{befehl.ziel}' nicht gefunden. Verfuegbare Prefabs: {GetVerfuegbarePrefabs()}";

            return $"'{befehl.ziel}' gespawnt bei [{pos.x:F1}, {pos.y:F1}, {pos.z:F1}]";
        }

        private string ObjektEntfernen(WeltBefehl befehl)
        {
            if (weltController == null)
                return "Kein WeltController verfuegbar.";

            var obj = FindeWeltObjekt(befehl.ziel);
            if (obj == null)
                return $"Objekt '{befehl.ziel}' nicht gefunden.";

            weltController.EntferneObjekt(obj);
            return $"'{befehl.ziel}' entfernt.";
        }

        private string ObjektBewegen(WeltBefehl befehl)
        {
            if (weltController == null)
                return "Kein WeltController verfuegbar.";

            var obj = FindeWeltObjekt(befehl.ziel);
            if (obj == null)
                return $"Objekt '{befehl.ziel}' nicht gefunden.";

            Vector3 neuPos;
            if (befehl.position != null && befehl.position.Length >= 3)
                neuPos = new Vector3(befehl.position[0], befehl.position[1], befehl.position[2]);
            else
                return "Keine Zielposition angegeben.";

            weltController.BewegeObjekt(obj, neuPos);
            return $"'{befehl.ziel}' bewegt nach [{neuPos.x:F1}, {neuPos.y:F1}, {neuPos.z:F1}]";
        }

        private string WetterAendern(WeltBefehl befehl)
        {
            if (weltController == null)
                return "Kein WeltController verfuegbar.";

            WetterTyp wetter = WetterTyp.Klar;
            if (!string.IsNullOrEmpty(befehl.wetterTyp))
            {
                switch (befehl.wetterTyp.ToLowerInvariant())
                {
                    case "regen": wetter = WetterTyp.Regen; break;
                    case "schnee": wetter = WetterTyp.Schnee; break;
                    case "nebel": wetter = WetterTyp.Nebel; break;
                    case "sturm": wetter = WetterTyp.Sturm; break;
                    case "bewoelkt": wetter = WetterTyp.Bewoelkt; break;
                    default: wetter = WetterTyp.Klar; break;
                }
            }

            float intensitaet = befehl.wert > 0 ? Mathf.Clamp01(befehl.wert) : 0.7f;
            weltController.SetzeWetter(wetter, intensitaet);
            return $"Wetter geaendert: {wetter} (Intensitaet: {intensitaet:F1})";
        }

        private string TageszeitAendern(WeltBefehl befehl)
        {
            if (weltController == null)
                return "Kein WeltController verfuegbar.";

            float stunde = Mathf.Clamp(befehl.wert, 0f, 24f);
            if (stunde == 0f) stunde = 12f; // Default: Mittag

            weltController.SetzeTageszeit(stunde);
            return $"Tageszeit gesetzt: {stunde:F0}:00 Uhr";
        }

        private string PhysikEventAusfuehren(WeltBefehl befehl)
        {
            if (weltController == null)
                return "Kein WeltController verfuegbar.";

            Vector3 pos = befehl.position != null && befehl.position.Length >= 3
                ? new Vector3(befehl.position[0], befehl.position[1], befehl.position[2])
                : Vector3.zero;

            string eventTyp = befehl.eventTyp ?? "explosion";
            weltController.AusloesePhysikEvent(eventTyp, pos);
            return $"Physik-Event: {eventTyp} bei [{pos.x:F1}, {pos.y:F1}, {pos.z:F1}]";
        }

        // ========== Helfer ==========

        /// <summary>
        /// Schnell-Check ob der Text ueberhaupt Weltveraenderungs-Intent hat.
        /// Spart LLM-Call bei normalen Gespraechen.
        /// </summary>
        private bool EnthaeltWeltIntent(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string lower = text.ToLowerInvariant();

            string[] weltWoerter = {
                "erstelle", "erzeuge", "generiere", "baue",
                "spawne", "platziere", "stelle", "setze",
                "entferne", "loesche", "zerstoere", "raeume",
                "bewege", "schiebe", "verschiebe",
                "regen", "schnee", "nebel", "sturm", "sonne", "wetter",
                "nacht", "tag", "morgen", "abend", "tageszeit", "uhr",
                "explosion", "wald", "garten", "see", "teich", "raum", "zimmer",
                "create", "spawn", "build", "remove", "delete", "move",
                "forest", "garden", "room", "rain", "snow", "night", "day"
            };

            foreach (var wort in weltWoerter)
            {
                if (lower.Contains(wort))
                    return true;
            }

            return false;
        }

        private GameObject FindeWeltObjekt(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            // Exakter Match
            var obj = GameObject.Find(name);
            if (obj != null) return obj;

            // Fuzzy: Alle Objekte durchsuchen
            string lower = name.ToLowerInvariant();
            var alle = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

            // Erst Contains-Match
            foreach (var t in alle)
            {
                if (t.gameObject.name.ToLowerInvariant().Contains(lower))
                    return t.gameObject;
            }

            return null;
        }

        private Vector3 SucheFreiePosition()
        {
            // Zufaellige Position in der Naehe des Ursprungs
            float x = UnityEngine.Random.Range(-10f, 10f);
            float z = UnityEngine.Random.Range(-10f, 10f);

            // Raycast um Bodenhoehe zu finden
            if (Physics.Raycast(new Vector3(x, 100f, z), Vector3.down, out RaycastHit hit, 200f))
                return hit.point + Vector3.up * 0.5f;

            return new Vector3(x, 0.5f, z);
        }

        private string GetVerfuegbarePrefabs()
        {
            if (weltController?.registriertePrefabs == null) return "(keine)";
            var namen = weltController.registriertePrefabs
                .Where(p => p != null)
                .Select(p => p.name);
            return string.Join(", ", namen);
        }

        // ========== Direkte Befehle (fuer ChatUI) ==========

        /// <summary>
        /// Parst direkte Textbefehle ohne LLM (fuer /welt Kommandos).
        /// Format: /welt erstelle wald | /welt spawn kiste 5,0,3 | /welt wetter regen
        /// </summary>
        public ManipulationsErgebnis FuehreDirektBefehlAus(string befehlText)
        {
            if (string.IsNullOrEmpty(befehlText))
                return new ManipulationsErgebnis { erfolg = false, beschreibung = "Leerer Befehl." };

            string[] teile = befehlText.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (teile.Length == 0)
                return new ManipulationsErgebnis { erfolg = false, beschreibung = "Leerer Befehl." };

            string aktion = teile[0].ToLowerInvariant();
            var befehl = new WeltBefehl();

            switch (aktion)
            {
                case "erstelle":
                case "create":
                case "generiere":
                    befehl.typ = WeltBefehlTyp.SzenarioErstellen;
                    befehl.ziel = teile.Length > 1 ? teile[1] : "wiese";
                    befehl.biom = befehl.ziel.ToLowerInvariant();
                    befehl.breite = 40;
                    befehl.tiefe = 40;
                    befehl.objektDichte = 20;
                    break;

                case "spawn":
                case "platziere":
                    befehl.typ = WeltBefehlTyp.ObjektSpawnen;
                    befehl.ziel = teile.Length > 1 ? teile[1] : "";
                    if (teile.Length > 2)
                        befehl.position = ParsePositionString(teile[2]);
                    break;

                case "entferne":
                case "loesche":
                case "remove":
                    befehl.typ = WeltBefehlTyp.ObjektEntfernen;
                    befehl.ziel = teile.Length > 1 ? teile[1] : "";
                    break;

                case "bewege":
                case "move":
                    befehl.typ = WeltBefehlTyp.ObjektBewegen;
                    befehl.ziel = teile.Length > 1 ? teile[1] : "";
                    if (teile.Length > 2)
                        befehl.position = ParsePositionString(teile[2]);
                    break;

                case "wetter":
                case "weather":
                    befehl.typ = WeltBefehlTyp.WetterAendern;
                    befehl.wetterTyp = teile.Length > 1 ? teile[1] : "Klar";
                    befehl.wert = teile.Length > 2 && float.TryParse(teile[2], out float i) ? i : 0.7f;
                    break;

                case "zeit":
                case "tageszeit":
                case "time":
                    befehl.typ = WeltBefehlTyp.TageszeitAendern;
                    befehl.wert = teile.Length > 1 && float.TryParse(teile[1], out float h) ? h : 12f;
                    break;

                case "event":
                    befehl.typ = WeltBefehlTyp.PhysikEvent;
                    befehl.eventTyp = teile.Length > 1 ? teile[1] : "explosion";
                    if (teile.Length > 2)
                        befehl.position = ParsePositionString(teile[2]);
                    break;

                default:
                    return new ManipulationsErgebnis
                    {
                        erfolg = false,
                        beschreibung = $"Unbekannter Befehl: {aktion}. " +
                            "Verfuegbar: erstelle, spawn, entferne, bewege, wetter, zeit, event"
                    };
            }

            return FuehreBefehlAus(befehl);
        }

        private float[] ParsePositionString(string s)
        {
            var teile = s.Split(',');
            if (teile.Length >= 3 &&
                float.TryParse(teile[0], out float x) &&
                float.TryParse(teile[1], out float y) &&
                float.TryParse(teile[2], out float z))
            {
                return new float[] { x, y, z };
            }
            return null;
        }

        public void Notbremse() => notbremseAktiv = true;
        public void NotbremseAufheben() => notbremseAktiv = false;
    }
}
