using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Kern
{
    // =====================================================================
    // Kurrikulum-gesteuertes Training: 6 Phasen mit aufsteigender Komplexitaet
    // Generiert synthetische Inputs + Ziele fuer den AGI-Zyklus
    // =====================================================================

    public enum TrainingsPhase
    {
        Beobachten,     // Phase 0: Umgebung wahrnehmen, Objekte benennen
        Navigieren,     // Phase 1: Zu Objekten/Orten bewegen
        Interagieren,   // Phase 2: Objekte manipulieren (greifen, oeffnen, ...)
        Sozial,         // Phase 3: Mit NPCs interagieren
        Planen,         // Phase 4: Multi-Schritt-Ziele verfolgen
        Frei            // Phase 5: Neugier-getrieben, eigene Hypothesen
    }

    [Serializable]
    public class TrainingsStatistik
    {
        public TrainingsPhase phase;
        public int zyklenGesamt;
        public int zyklenInPhase;
        public int erfahrungenGesamt;
        public int zieleErreicht;
        public int zieleGescheitert;
        public float durchschnittsBelohnung;
        public float explorationRate;
        public float dqnUpdates;
        public float weltModellTransitionen;
        public string startZeit;
        public string letzterTick;
        public List<float> belohnungsHistorie = new List<float>();
    }

    public class TrainingsKurrikulum
    {
        private TrainingsPhase aktuellePhase;
        private readonly System.Random rng = new System.Random();
        private int phaseZyklen;
        private float phaseBelohnungsSumme;
        private int phaseErfolge;
        private int phaseVersuche;

        // Pro Phase: min. Zyklen bevor Aufstieg moeglich + Erfolgsquote-Schwelle
        private static readonly Dictionary<TrainingsPhase, (int minZyklen, float erfolgsSchwelle)> Schwellen
            = new Dictionary<TrainingsPhase, (int, float)>
        {
            { TrainingsPhase.Beobachten,   (15,  0.5f) },
            { TrainingsPhase.Navigieren,   (20,  0.4f) },
            { TrainingsPhase.Interagieren, (25,  0.35f) },
            { TrainingsPhase.Sozial,       (20,  0.3f) },
            { TrainingsPhase.Planen,       (30,  0.25f) },
            { TrainingsPhase.Frei,         (999999, 0f) } // bleibt hier
        };

        public TrainingsPhase AktuellePhase => aktuellePhase;
        public int PhaseZyklen => phaseZyklen;

        public TrainingsKurrikulum(TrainingsPhase startPhase = TrainingsPhase.Beobachten)
        {
            aktuellePhase = startPhase;
        }

        // ---- Phasen-Aufstieg pruefen ----
        public bool PruefeAufstieg()
        {
            if (aktuellePhase == TrainingsPhase.Frei) return false;
            var (minZ, schwelle) = Schwellen[aktuellePhase];
            if (phaseZyklen < minZ) return false;

            float quote = phaseVersuche > 0 ? phaseErfolge / (float)phaseVersuche : 0f;
            if (quote >= schwelle)
            {
                aktuellePhase = (TrainingsPhase)((int)aktuellePhase + 1);
                Debug.Log($"[Kurrikulum] AUFSTIEG → {aktuellePhase} (Quote: {quote:P0}, Zyklen: {phaseZyklen})");
                phaseZyklen = 0;
                phaseBelohnungsSumme = 0;
                phaseErfolge = 0;
                phaseVersuche = 0;
                return true;
            }
            return false;
        }

        public void RegistriereZyklus(float belohnung, bool erfolg)
        {
            phaseZyklen++;
            phaseBelohnungsSumme += belohnung;
            phaseVersuche++;
            if (erfolg) phaseErfolge++;
        }

        // ---- Synthetischen Input generieren (pro Phase) ----
        public string GeneriereInput(WeltZustand welt, List<NPCInfo> npcs)
        {
            switch (aktuellePhase)
            {
                case TrainingsPhase.Beobachten:
                    return GeneriereBeobachtungsInput(welt);
                case TrainingsPhase.Navigieren:
                    return GeneriereNavigationsInput(welt);
                case TrainingsPhase.Interagieren:
                    return GeneriereInteraktionsInput(welt);
                case TrainingsPhase.Sozial:
                    return GeneriereSozialInput(npcs);
                case TrainingsPhase.Planen:
                    return GenerierePlanungsInput(welt, npcs);
                case TrainingsPhase.Frei:
                    return null; // Kein synthetischer Input — Neugier uebernimmt
                default:
                    return null;
            }
        }

        // ---- Ziel fuer aktuelle Phase generieren ----
        public (string beschreibung, ZielTyp typ)? GeneriereZiel(WeltZustand welt, List<NPCInfo> npcs)
        {
            switch (aktuellePhase)
            {
                case TrainingsPhase.Beobachten:
                    return ("Alle sichtbaren Objekte identifizieren und beschreiben", ZielTyp.EXPLORATION);

                case TrainingsPhase.Navigieren:
                    var obj = ZufaelligesObjekt(welt);
                    return obj != null
                        ? ($"Navigiere zu {obj.name} und beschreibe seine Umgebung", ZielTyp.EXPLORATION)
                        : ("Erkunde die Umgebung systematisch", ZielTyp.EXPLORATION);

                case TrainingsPhase.Interagieren:
                    var target = ZufaelligesInteragierbaresObjekt(welt);
                    return target != null
                        ? ($"Interagiere mit {target.name}: versuche zu greifen, oeffnen oder aktivieren", ZielTyp.EXPERIMENT)
                        : ("Finde ein Objekt und experimentiere damit", ZielTyp.EXPERIMENT);

                case TrainingsPhase.Sozial:
                    if (npcs != null && npcs.Count > 0)
                    {
                        var npc = npcs[rng.Next(npcs.Count)];
                        return ($"Sprich mit {npc.name} und finde heraus was sie/er tut", ZielTyp.SOZIAL);
                    }
                    return ("Suche nach anderen Wesen in der Umgebung", ZielTyp.SOZIAL);

                case TrainingsPhase.Planen:
                    return GeneriereKomplexesZiel(welt, npcs);

                case TrainingsPhase.Frei:
                    return null; // Neugier generiert eigene Ziele

                default:
                    return null;
            }
        }

        // ---- Interne Generatoren ----

        private string GeneriereBeobachtungsInput(WeltZustand welt)
        {
            string[] templates = {
                "Was siehst du um dich herum?",
                "Beschreibe deine Umgebung.",
                "Welche Objekte sind in deiner Naehe?",
                "Wie ist das Wetter gerade?",
                "Was kannst du hoeren und sehen?",
                "Schau dich um. Was faellt dir auf?",
                "Welche Tageszeit ist es und wie sieht die Landschaft aus?",
                "Gibt es etwas Ungewoehnliches in deiner Umgebung?",
                "Beschreibe die Farben und Formen um dich herum.",
                "Was riechst du? Was fuehlst du unter deinen Fuessen?"
            };
            return templates[rng.Next(templates.Length)];
        }

        private string GeneriereNavigationsInput(WeltZustand welt)
        {
            var obj = ZufaelligesObjekt(welt);
            if (obj != null)
            {
                string[] templates = {
                    $"Geh zu {obj.name}.",
                    $"Kannst du {obj.name} finden und dorthin laufen?",
                    $"Bewege dich zum {obj.typ} namens {obj.name}.",
                    $"Suche {obj.name} und naehere dich vorsichtig."
                };
                return templates[rng.Next(templates.Length)];
            }

            string[] fallback = {
                "Lauf in eine zufaellige Richtung und schau was du findest.",
                "Erkunde den Bereich noerdlich von dir.",
                "Geh so weit du kannst in eine Richtung."
            };
            return fallback[rng.Next(fallback.Length)];
        }

        private string GeneriereInteraktionsInput(WeltZustand welt)
        {
            var obj = ZufaelligesInteragierbaresObjekt(welt);
            if (obj != null)
            {
                string[] templates = {
                    $"Greife {obj.name} und hebe es auf.",
                    $"Was passiert wenn du {obj.name} oeffnest?",
                    $"Versuche {obj.name} zu aktivieren.",
                    $"Schiebe {obj.name} in eine andere Richtung.",
                    $"Untersuche {obj.name} genau. Kannst du es benutzen?",
                    $"Experimentiere mit {obj.name}. Was kannst du damit machen?"
                };
                return templates[rng.Next(templates.Length)];
            }

            string[] fallback = {
                "Gibt es hier etwas das du aufheben koenntest?",
                "Suche ein Objekt und versuche es zu benutzen.",
                "Finde etwas Interessantes und experimentiere damit."
            };
            return fallback[rng.Next(fallback.Length)];
        }

        private string GeneriereSozialInput(List<NPCInfo> npcs)
        {
            if (npcs != null && npcs.Count > 0)
            {
                var npc = npcs[rng.Next(npcs.Count)];
                string[] templates = {
                    $"Sprich mit {npc.name}.",
                    $"Frage {npc.name} was sie/er gerade macht.",
                    $"Was denkst du ueber {npc.name}? Wie wirkt sie/er auf dich?",
                    $"Beobachte {npc.name} und beschreibe sein/ihr Verhalten.",
                    $"Versuche herauszufinden was {npc.name} als naechstes tun wird.",
                    $"Gruesse {npc.name} und beginne ein Gespraech."
                };
                return templates[rng.Next(templates.Length)];
            }

            return "Gibt es jemanden in deiner Naehe mit dem du sprechen koenntest?";
        }

        private string GenerierePlanungsInput(WeltZustand welt, List<NPCInfo> npcs)
        {
            string[] templates = {
                "Sammle drei verschiedene Objekte und lege sie an einem Ort ab.",
                "Finde den hoechsten Punkt in der Umgebung und beschreibe was du von dort siehst.",
                "Erkunde systematisch alle Objekte und erstelle eine Liste.",
                "Plane eine Route die an allen interessanten Punkten vorbeifuehrt.",
                "Finde ein Problem in der Umgebung und loese es.",
                "Baue etwas Nuetzliches aus Objekten die du findest.",
                "Untersuche welche Objekte zusammen gehoeren oder zusammenwirken."
            };

            if (npcs != null && npcs.Count > 0)
            {
                var npc = npcs[rng.Next(npcs.Count)];
                string[] sozialPlan = {
                    $"Finde heraus was {npc.name} braucht und hilf dabei.",
                    $"Bringe {npc.name} ein Objekt und beobachte die Reaktion.",
                };
                // 30% Chance fuer sozialen Plan
                if (rng.NextDouble() < 0.3)
                    return sozialPlan[rng.Next(sozialPlan.Length)];
            }

            return templates[rng.Next(templates.Length)];
        }

        private (string beschreibung, ZielTyp typ)? GeneriereKomplexesZiel(WeltZustand welt, List<NPCInfo> npcs)
        {
            string[] ziele = {
                "Alle Objekte in der Umgebung katalogisieren und klassifizieren",
                "Die Kausalzusammenhaenge zwischen Objekten herausfinden",
                "Einen effizienten Weg zu allen Objekten planen und abgehen",
                "Physikalische Regeln durch Experimente entdecken"
            };

            return (ziele[rng.Next(ziele.Length)], ZielTyp.AUFGABE);
        }

        // ---- Helfer ----

        private WeltObjekt ZufaelligesObjekt(WeltZustand welt)
        {
            if (welt?.objekte == null || welt.objekte.Count == 0) return null;
            var liste = welt.objekte.Values.ToList();
            return liste[rng.Next(liste.Count)];
        }

        private WeltObjekt ZufaelligesInteragierbaresObjekt(WeltZustand welt)
        {
            if (welt?.objekte == null) return null;
            var interagierbar = welt.objekte.Values
                .Where(o => o.zustand != null || o.tags.Count > 0)
                .ToList();
            if (interagierbar.Count == 0)
                return ZufaelligesObjekt(welt);
            return interagierbar[rng.Next(interagierbar.Count)];
        }
    }

    // Leichtgewichtiges NPC-Info-Struct (ohne Unity-Abhaengigkeit im Kurrikulum)
    [Serializable]
    public class NPCInfo
    {
        public string id;
        public string name;
        public string rolle;
        public string aktuelleAktion;
    }
}
