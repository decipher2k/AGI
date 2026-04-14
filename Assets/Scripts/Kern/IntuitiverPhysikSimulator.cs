using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using BilligAGI.Physik;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    // ============================================================
    //  IntuitiverPhysikSimulator — "Bauchgefuehl fuer Physik"
    //
    //  Geht ueber PhysikEngine (Regelabgleich) und
    //  PrediktivesWeltModell (reine MLP-Vorhersage) hinaus:
    //
    //  1. Objektpermanenz: Verdeckte Objekte weiter tracken
    //  2. Trajektorien: Wohin faellt/fliegt ein Objekt?
    //  3. Stabilitaet: Faellt dieser Stapel um?
    //  4. Containment: Was ist in was drin?
    //  5. Kollision: Werden sich diese Objekte treffen?
    //
    //  Ohne LLM — rein auf gelernten Regeln + Heuristiken.
    //  Nutzt PhysikEngine fuer bekannte Regeln und
    //  PrediktivesWeltModell fuer Forward-Simulation.
    // ============================================================

    [Serializable]
    public class ObjektSpur
    {
        public string objektId;
        public string name;
        public float[] letztePosition;          // [x,y,z]
        public float[] geschaetztGeschwindigkeit; // [vx,vy,vz]
        public bool sichtbar;
        public float seitUnsichtbar;            // Sekunden seit verdeckt
        public float[] geschaetztePosition;      // Wo glauben wir ist es jetzt?
        public float konfidenz;                  // Sinkt mit Zeit
        public string zeitstempel;
    }

    [Serializable]
    public class TrajektorienVorhersage
    {
        public string objektId;
        public float[] startPosition;
        public float[] endPosition;             // Vorhergesagter Aufschlagpunkt
        public float flugzeit;                  // Geschaetzte Sekunden
        public float konfidenz;
        public string beschreibung;
    }

    [Serializable]
    public class StabilitaetsAnalyse
    {
        public string beschreibung;
        public float stabilitaet;               // 0=instabil, 1=stabil
        public List<string> risiken = new();    // "Objekt X hat keinen Untergrund"
        public bool kippGefahr;
    }

    [Serializable]
    public class ContainmentRelation
    {
        public string containerId;
        public string containerName;
        public List<string> inhaltIds = new();
        public List<string> inhaltNamen = new();
    }

    [Serializable]
    public class KollisionsVorhersage
    {
        public string objektA;
        public string objektB;
        public float wahrscheinlichkeit;
        public float geschaetzteZeit;           // Sekunden bis Kollision
        public float[] geschaetzterPunkt;        // [x,y,z]
    }

    [Serializable]
    public class PhysikIntuition
    {
        public List<ObjektSpur> verdeckteObjekte = new();
        public List<TrajektorienVorhersage> trajektorien = new();
        public StabilitaetsAnalyse stabilitaet;
        public List<ContainmentRelation> containments = new();
        public List<KollisionsVorhersage> kollisionen = new();
        public int zyklusNummer;
    }

    [Serializable]
    public class PhysikSimStatistik
    {
        public int vorhersagenGesamt;
        public int vorhersagenKorrekt;
        public int permanenzTracking;
        public int trajektorienBerechnet;
    }

    public class IntuitiverPhysikSimulator
    {
        private readonly PhysikEngine physikEngine;
        private readonly PrediktivesWeltModell weltModell;
        private readonly AGIConfig config;

        private Dictionary<string, ObjektSpur> objektSpuren = new();
        private List<ContainmentRelation> containments = new();
        private PhysikSimStatistik statistik;
        private PhysikIntuition letzteIntuition;
        private int zyklusZaehler;

        // Physik-Konstanten (vereinfacht)
        private const float GRAVITATION = 9.81f;
        private const float PERMANENZ_TIMEOUT = 30f;     // Sekunden bevor wir aufgeben
        private const float KONFIDENZ_ZERFALL = 0.02f;   // Pro Sekunde
        private const float STABIL_SCHWELLE = 0.3f;      // Unter diesem Wert: instabil
        private const float NAEHERADIUS = 2f;             // Fuer Containment-Check
        private const float KOLLISIONS_HORIZONT = 5f;     // Sekunden voraus schauen
        private const string PERSISTENZ_DATEI = "physik_sim_statistik.json";

        public IntuitiverPhysikSimulator(
            PhysikEngine physikEngine,
            PrediktivesWeltModell weltModell,
            AGIConfig config)
        {
            this.physikEngine = physikEngine;
            this.weltModell = weltModell;
            this.config = config;

            statistik = DatenLader.Lade<PhysikSimStatistik>(PERSISTENZ_DATEI) ?? new PhysikSimStatistik();
            Debug.Log($"[PhysikSim] Initialisiert. {statistik.vorhersagenGesamt} bisherige Vorhersagen.");
        }

        // ======== Haupt-Tick ========

        public PhysikIntuition ZyklusTick(WeltZustand welt, SensorDaten sensor, float deltaZeit)
        {
            zyklusZaehler++;
            if (welt == null) return null;

            var intuition = new PhysikIntuition { zyklusNummer = zyklusZaehler };

            // 1. Objektpermanenz aktualisieren
            AktualisiereObjektSpuren(welt, deltaZeit);
            intuition.verdeckteObjekte = objektSpuren.Values
                .Where(s => !s.sichtbar && s.konfidenz > 0.1f).ToList();

            // 2. Trajektorien fuer sich bewegende Objekte
            intuition.trajektorien = BerechneAktiveTrajektorien(welt);

            // 3. Stabilitaetsanalyse
            intuition.stabilitaet = AnalysiereStabilitaet(welt);

            // 4. Containment-Relationen
            AktualisiereContainment(welt);
            intuition.containments = new List<ContainmentRelation>(containments);

            // 5. Kollisionsvorhersagen
            intuition.kollisionen = VorhersageKollisionen(welt);

            letzteIntuition = intuition;
            return intuition;
        }

        // ======== 1. Objektpermanenz ========

        private void AktualisiereObjektSpuren(WeltZustand welt, float deltaZeit)
        {
            if (welt.objekte == null) return;

            var sichtbareIds = new HashSet<string>(welt.objekte.Keys);

            // Sichtbare Objekte: Spur aktualisieren
            foreach (var kvp in welt.objekte)
            {
                var obj = kvp.Value;
                if (obj.position == null || obj.position.Length < 3) continue;

                if (objektSpuren.TryGetValue(obj.id, out var spur))
                {
                    // Geschwindigkeit schaetzen aus Positionsdifferenz
                    if (spur.letztePosition != null && deltaZeit > 0.001f)
                    {
                        spur.geschaetztGeschwindigkeit = new float[]
                        {
                            (obj.position[0] - spur.letztePosition[0]) / deltaZeit,
                            (obj.position[1] - spur.letztePosition[1]) / deltaZeit,
                            (obj.position[2] - spur.letztePosition[2]) / deltaZeit
                        };
                    }
                    spur.letztePosition = (float[])obj.position.Clone();
                    spur.geschaetztePosition = (float[])obj.position.Clone();
                    spur.sichtbar = true;
                    spur.konfidenz = 1f;
                    spur.seitUnsichtbar = 0f;
                }
                else
                {
                    // Neues Objekt
                    objektSpuren[obj.id] = new ObjektSpur
                    {
                        objektId = obj.id,
                        name = obj.name,
                        letztePosition = (float[])obj.position.Clone(),
                        geschaetztGeschwindigkeit = new float[] { 0, 0, 0 },
                        sichtbar = true,
                        konfidenz = 1f,
                        geschaetztePosition = (float[])obj.position.Clone(),
                        zeitstempel = DateTime.UtcNow.ToString("o")
                    };
                }
            }

            // Nicht mehr sichtbare Objekte: Extrapolieren
            var zuEntfernen = new List<string>();
            foreach (var kvp in objektSpuren)
            {
                if (sichtbareIds.Contains(kvp.Key)) continue;

                var spur = kvp.Value;
                spur.sichtbar = false;
                spur.seitUnsichtbar += deltaZeit;
                spur.konfidenz = Mathf.Max(0f, spur.konfidenz - KONFIDENZ_ZERFALL * deltaZeit);

                // Geschaetzte Position extrapolieren (mit Gravitation)
                if (spur.geschaetztePosition != null && spur.geschaetztGeschwindigkeit != null)
                {
                    spur.geschaetztePosition[0] += spur.geschaetztGeschwindigkeit[0] * deltaZeit;
                    spur.geschaetztePosition[1] += spur.geschaetztGeschwindigkeit[1] * deltaZeit
                        - 0.5f * GRAVITATION * deltaZeit * deltaZeit;
                    spur.geschaetztePosition[2] += spur.geschaetztGeschwindigkeit[2] * deltaZeit;

                    // Boden-Check: Nicht unter y=0 fallen
                    if (spur.geschaetztePosition[1] < 0f)
                    {
                        spur.geschaetztePosition[1] = 0f;
                        spur.geschaetztGeschwindigkeit[1] = 0f;
                    }

                    // Geschwindigkeit: Gravitation anwenden
                    spur.geschaetztGeschwindigkeit[1] -= GRAVITATION * deltaZeit;
                }

                // Timeout: Spur aufgeben
                if (spur.seitUnsichtbar > PERMANENZ_TIMEOUT || spur.konfidenz <= 0f)
                    zuEntfernen.Add(kvp.Key);
                else
                    statistik.permanenzTracking++;
            }

            foreach (var id in zuEntfernen)
                objektSpuren.Remove(id);
        }

        /// <summary>
        /// Wo ist ein verdecktes Objekt? (Objektpermanenz-Abfrage)
        /// </summary>
        public ObjektSpur WoIstObjekt(string objektIdOderName)
        {
            // Direkt per ID
            if (objektSpuren.TryGetValue(objektIdOderName, out var spur))
                return spur;

            // Per Name suchen
            return objektSpuren.Values.FirstOrDefault(s =>
                s.name != null && s.name.IndexOf(objektIdOderName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ======== 2. Trajektorien ========

        private List<TrajektorienVorhersage> BerechneAktiveTrajektorien(WeltZustand welt)
        {
            var trajektorien = new List<TrajektorienVorhersage>();

            foreach (var spur in objektSpuren.Values)
            {
                if (!spur.sichtbar || spur.geschaetztGeschwindigkeit == null) continue;

                float vx = spur.geschaetztGeschwindigkeit[0];
                float vy = spur.geschaetztGeschwindigkeit[1];
                float vz = spur.geschaetztGeschwindigkeit[2];
                float speed = Mathf.Sqrt(vx * vx + vy * vy + vz * vz);

                // Nur fuer Objekte die sich signifikant bewegen
                if (speed < 0.5f) continue;

                // In der Luft? (vy > 1 oder y > 0.5 und steigend)
                bool inDerLuft = vy > 1f ||
                    (spur.letztePosition != null && spur.letztePosition[1] > 0.5f && vy > 0);

                if (inDerLuft)
                {
                    // Parabel-Berechnung: Wann trifft es den Boden?
                    float y0 = spur.letztePosition?[1] ?? 0f;
                    float tBoden = BerechneBodenAufprall(y0, vy);

                    if (tBoden > 0f && tBoden < 30f)
                    {
                        float endX = spur.letztePosition[0] + vx * tBoden;
                        float endZ = spur.letztePosition[2] + vz * tBoden;

                        trajektorien.Add(new TrajektorienVorhersage
                        {
                            objektId = spur.objektId,
                            startPosition = (float[])spur.letztePosition.Clone(),
                            endPosition = new float[] { endX, 0f, endZ },
                            flugzeit = tBoden,
                            konfidenz = Mathf.Clamp01(0.8f - tBoden * 0.05f), // Weiter = unsicherer
                            beschreibung = $"'{spur.name}' wird in ~{tBoden:F1}s bei " +
                                $"[{endX:F1}, 0, {endZ:F1}] aufschlagen."
                        });

                        statistik.trajektorienBerechnet++;
                    }
                }
            }

            return trajektorien;
        }

        /// <summary>
        /// Berechne Aufprallzeit eines Objekts auf y=0 (Parabelwurf).
        /// </summary>
        public TrajektorienVorhersage VorhersageTrajektorie(float[] position, float[] geschwindigkeit)
        {
            if (position == null || geschwindigkeit == null) return null;

            float y0 = position[1];
            float vy = geschwindigkeit[1];
            float tBoden = BerechneBodenAufprall(y0, vy);

            if (tBoden <= 0f) return null;

            return new TrajektorienVorhersage
            {
                startPosition = (float[])position.Clone(),
                endPosition = new float[]
                {
                    position[0] + geschwindigkeit[0] * tBoden,
                    0f,
                    position[2] + geschwindigkeit[2] * tBoden
                },
                flugzeit = tBoden,
                konfidenz = Mathf.Clamp01(0.8f - tBoden * 0.05f),
                beschreibung = $"Aufschlag in ~{tBoden:F1}s"
            };
        }

        private float BerechneBodenAufprall(float y0, float vy)
        {
            // y(t) = y0 + vy*t - 0.5*g*t² = 0
            // → 0.5*g*t² - vy*t - y0 = 0
            // → t = (vy + sqrt(vy² + 2*g*y0)) / g
            float diskriminante = vy * vy + 2f * GRAVITATION * y0;
            if (diskriminante < 0f) return -1f;
            float t = (vy + Mathf.Sqrt(diskriminante)) / GRAVITATION;
            return t > 0.01f ? t : -1f;
        }

        // ======== 3. Stabilitaet ========

        private StabilitaetsAnalyse AnalysiereStabilitaet(WeltZustand welt)
        {
            if (welt.objekte == null || welt.objekte.Count < 2)
                return new StabilitaetsAnalyse { stabilitaet = 1f, beschreibung = "Wenige Objekte — stabil." };

            var analyse = new StabilitaetsAnalyse();
            float gesamtStabilitaet = 1f;

            // Sortiere Objekte nach Hoehe (y)
            var nachHoehe = welt.objekte.Values
                .Where(o => o.position != null && o.position.Length >= 3)
                .OrderBy(o => o.position[1])
                .ToList();

            for (int i = 1; i < nachHoehe.Count; i++)
            {
                var oben = nachHoehe[i];
                float obenY = oben.position[1];

                if (obenY < 0.3f) continue; // Auf dem Boden → stabil

                // Hat es etwas darunter?
                bool hatUnterstuetzung = false;
                float besteUnterstuetzung = float.MaxValue;

                for (int j = 0; j < i; j++)
                {
                    var unten = nachHoehe[j];
                    float dx = oben.position[0] - unten.position[0];
                    float dz = oben.position[2] - unten.position[2];
                    float horizontalDist = Mathf.Sqrt(dx * dx + dz * dz);
                    float vertikalDist = obenY - unten.position[1];

                    // Innerhalb von 1m horizontal und 0.1–2m vertikal → Unterstuetzung
                    if (horizontalDist < 1f && vertikalDist > 0.1f && vertikalDist < 2f)
                    {
                        hatUnterstuetzung = true;
                        besteUnterstuetzung = Mathf.Min(besteUnterstuetzung, horizontalDist);
                    }
                }

                if (!hatUnterstuetzung && obenY > 0.5f)
                {
                    analyse.risiken.Add($"'{oben.name}' schwebt bei Y={obenY:F1} ohne Unterstuetzung");
                    gesamtStabilitaet -= 0.3f;
                }
                else if (hatUnterstuetzung && besteUnterstuetzung > 0.6f)
                {
                    analyse.risiken.Add($"'{oben.name}' steht am Rand seiner Unterstuetzung (Offset: {besteUnterstuetzung:F1}m)");
                    gesamtStabilitaet -= 0.15f;
                    analyse.kippGefahr = true;
                }
            }

            // Hohe Stapel sind generell instabiler
            float maxHoehe = nachHoehe.Count > 0 ? nachHoehe.Last().position[1] : 0f;
            if (maxHoehe > 3f)
            {
                gesamtStabilitaet -= (maxHoehe - 3f) * 0.1f;
                analyse.risiken.Add($"Hoher Stapel ({maxHoehe:F1}m) ist generell instabiler.");
            }

            analyse.stabilitaet = Mathf.Clamp01(gesamtStabilitaet);
            analyse.beschreibung = analyse.stabilitaet > 0.7f
                ? "Stabile Konfiguration."
                : analyse.stabilitaet > 0.4f
                    ? $"Eingeschraenkt stabil ({analyse.risiken.Count} Risiken)."
                    : $"Instabil! {analyse.risiken.Count} Risiken erkannt.";

            return analyse;
        }

        // ======== 4. Containment ========

        private void AktualisiereContainment(WeltZustand welt)
        {
            containments.Clear();
            if (welt.objekte == null) return;

            var objekte = welt.objekte.Values
                .Where(o => o.position != null && o.position.Length >= 3)
                .ToList();

            // Finde potentielle Container (Objekte mit "offen" Zustand oder Container-Tags)
            var container = objekte.Where(o =>
                o.zustand == "offen" ||
                (o.tags != null && o.tags.Any(t =>
                    t == "Container" || t == "Kiste" || t == "Truhe" ||
                    t == "Korb" || t == "Topf" || t == "Regal"))).ToList();

            foreach (var cont in container)
            {
                var relation = new ContainmentRelation
                {
                    containerId = cont.id,
                    containerName = cont.name
                };

                foreach (var obj in objekte)
                {
                    if (obj.id == cont.id) continue;

                    float dx = obj.position[0] - cont.position[0];
                    float dy = obj.position[1] - cont.position[1];
                    float dz = obj.position[2] - cont.position[2];
                    float dist = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);

                    // Innerhalb des Containers: nah horizontal, leicht ueber oder drin
                    if (dist < NAEHERADIUS && dy >= -0.5f && dy <= 1.5f)
                    {
                        // Horizontale Naehe entscheidend
                        float hDist = Mathf.Sqrt(dx * dx + dz * dz);
                        if (hDist < 0.8f)
                        {
                            relation.inhaltIds.Add(obj.id);
                            relation.inhaltNamen.Add(obj.name);
                        }
                    }
                }

                if (relation.inhaltIds.Count > 0)
                    containments.Add(relation);
            }
        }

        // ======== 5. Kollisionsvorhersage ========

        private List<KollisionsVorhersage> VorhersageKollisionen(WeltZustand welt)
        {
            var vorhersagen = new List<KollisionsVorhersage>();

            var bewegteObjekte = objektSpuren.Values
                .Where(s => s.sichtbar && s.geschaetztGeschwindigkeit != null)
                .Where(s =>
                {
                    var v = s.geschaetztGeschwindigkeit;
                    return Mathf.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]) > 0.5f;
                })
                .ToList();

            // Paarweise Kollisionscheck (nur bewegte → alle)
            foreach (var bewegt in bewegteObjekte)
            {
                foreach (var andere in objektSpuren.Values)
                {
                    if (andere.objektId == bewegt.objektId) continue;
                    if (bewegt.letztePosition == null || andere.letztePosition == null) continue;

                    // Vereinfachte Naehungsberechnung:
                    // Linearer Abstandsverlauf pruefen
                    float minZeit = BerechneNaechsteAnnaeherung(
                        bewegt.letztePosition, bewegt.geschaetztGeschwindigkeit,
                        andere.letztePosition, andere.geschaetztGeschwindigkeit ?? new float[] { 0, 0, 0 });

                    if (minZeit < 0f || minZeit > KOLLISIONS_HORIZONT) continue;

                    // Position zur Kollisionszeit berechnen
                    float[] punktA = ExtrapolierePosition(bewegt.letztePosition, bewegt.geschaetztGeschwindigkeit, minZeit);
                    float[] punktB = ExtrapolierePosition(andere.letztePosition, andere.geschaetztGeschwindigkeit ?? new float[] { 0, 0, 0 }, minZeit);

                    float distBeiKollision = Distanz3D(punktA, punktB);

                    if (distBeiKollision < 1.5f) // Kollisionsradius
                    {
                        vorhersagen.Add(new KollisionsVorhersage
                        {
                            objektA = bewegt.name ?? bewegt.objektId,
                            objektB = andere.name ?? andere.objektId,
                            wahrscheinlichkeit = Mathf.Clamp01(1f - distBeiKollision / 1.5f),
                            geschaetzteZeit = minZeit,
                            geschaetzterPunkt = new float[]
                            {
                                (punktA[0] + punktB[0]) / 2f,
                                (punktA[1] + punktB[1]) / 2f,
                                (punktA[2] + punktB[2]) / 2f
                            }
                        });
                    }
                }
            }

            statistik.vorhersagenGesamt += vorhersagen.Count;
            return vorhersagen;
        }

        // ======== Vorhersage validieren ========

        public void ValidiereVorhersage(string objektId, float[] tatsaechlichePosition)
        {
            if (!objektSpuren.TryGetValue(objektId, out var spur)) return;
            if (spur.geschaetztePosition == null || tatsaechlichePosition == null) return;

            float fehler = Distanz3D(spur.geschaetztePosition, tatsaechlichePosition);
            if (fehler < 1f)
                statistik.vorhersagenKorrekt++;
        }

        // ======== Status ========

        public string GetStatusText()
        {
            int verdeckt = objektSpuren.Values.Count(s => !s.sichtbar && s.konfidenz > 0.1f);
            int gesamt = objektSpuren.Count;
            float genauigkeit = statistik.vorhersagenGesamt > 0
                ? statistik.vorhersagenKorrekt / (float)statistik.vorhersagenGesamt
                : 0f;

            return $"Getrackt: {gesamt} Objekte ({verdeckt} verdeckt) | " +
                $"Trajektorien: {statistik.trajektorienBerechnet} | " +
                $"Vorhersagen: {statistik.vorhersagenGesamt} (Genauigkeit: {genauigkeit:P0}) | " +
                $"Containments: {containments.Count}";
        }

        public PhysikIntuition GetLetzteIntuition() => letzteIntuition;

        public List<ObjektSpur> GetVerdeckteObjekte()
        {
            return objektSpuren.Values
                .Where(s => !s.sichtbar && s.konfidenz > 0.1f)
                .OrderByDescending(s => s.konfidenz)
                .ToList();
        }

        public List<ContainmentRelation> GetContainments() => containments;

        public PhysikSimStatistik GetStatistik() => statistik;

        public void Persistiere()
        {
            DatenLader.Speichere(PERSISTENZ_DATEI, statistik);
        }

        // ======== Hilfsfunktionen ========

        private float BerechneNaechsteAnnaeherung(
            float[] posA, float[] velA, float[] posB, float[] velB)
        {
            // Relative Position und Geschwindigkeit
            float dx = posB[0] - posA[0];
            float dy = posB[1] - posA[1];
            float dz = posB[2] - posA[2];
            float dvx = velB[0] - velA[0];
            float dvy = velB[1] - velA[1];
            float dvz = velB[2] - velA[2];

            // t_min = -(dp · dv) / (dv · dv)
            float dotPosVel = dx * dvx + dy * dvy + dz * dvz;
            float dotVelVel = dvx * dvx + dvy * dvy + dvz * dvz;

            if (dotVelVel < 0.001f) return -1f; // Keine relative Bewegung
            float t = -dotPosVel / dotVelVel;
            return t;
        }

        private float[] ExtrapolierePosition(float[] pos, float[] vel, float t)
        {
            return new float[]
            {
                pos[0] + vel[0] * t,
                Mathf.Max(0f, pos[1] + vel[1] * t - 0.5f * GRAVITATION * t * t),
                pos[2] + vel[2] * t
            };
        }

        private float Distanz3D(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length < 3 || b.Length < 3) return float.MaxValue;
            float dx = a[0] - b[0];
            float dy = a[1] - b[1];
            float dz = a[2] - b[2];
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
