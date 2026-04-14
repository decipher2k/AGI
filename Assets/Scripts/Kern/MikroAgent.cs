using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace BilligAGI.Kern
{
    /// <summary>
    /// Basis-Klasse fuer alle Mikroagenten.
    ///
    /// Ein Mikroagent ist eine autonome Einheit mit eigenem Aktivierungslevel,
    /// eigenem Zustand, und eigener Tick-Logik. Nicht jeder Agent laeuft
    /// in jedem Zyklus — Aktivierung haengt von Relevanz ab.
    ///
    /// Kommunikation geschieht ueber das AgentNetzwerk (Blackboard + Nachrichten).
    /// </summary>
    public abstract class MikroAgent
    {
        public string Name { get; protected set; }
        public float Aktivierung { get; set; }          // 0-1, wie dringend will dieser Agent laufen?
        public float Energie { get; set; }               // 0-1, nimmt ab bei jedem Tick, regeneriert passiv
        public bool Aktiv => Aktivierung > 0.2f && Energie > 0.05f;
        public int TicksSeitLetztemLauf { get; private set; }
        public float LetzteAusfuehrungsDauer { get; private set; }

        private const float ENERGIE_PRO_TICK = 0.02f;
        private const float ENERGIE_REGENERATION = 0.01f;

        protected MikroAgent(string name)
        {
            Name = name;
            Aktivierung = 0.5f;
            Energie = 1f;
            TicksSeitLetztemLauf = 0;
        }

        /// <summary>
        /// Hauptlogik des Agenten. Liest vom Blackboard, schreibt Ergebnisse zurueck.
        /// </summary>
        public async Task<AgentErgebnis> Ausfuehren(Blackboard blackboard)
        {
            if (!Aktiv)
                return new AgentErgebnis { agent = Name, ausgefuehrt = false };

            float startZeit = Time.realtimeSinceStartup;

            Energie = Math.Max(0f, Energie - ENERGIE_PRO_TICK);
            TicksSeitLetztemLauf = 0;

            try
            {
                var ergebnis = await Tick(blackboard);
                LetzteAusfuehrungsDauer = Time.realtimeSinceStartup - startZeit;
                return ergebnis;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MikroAgent:{Name}] Fehler: {e.Message}");
                return new AgentErgebnis { agent = Name, ausgefuehrt = false, fehler = e.Message };
            }
        }

        /// <summary>
        /// Passive Updates: Energie regenerieren, Aktivierung anpassen.
        /// Wird JEDEN Tick aufgerufen, auch wenn der Agent nicht laeuft.
        /// </summary>
        public virtual void PassiverTick(Blackboard blackboard)
        {
            TicksSeitLetztemLauf++;
            Energie = Math.Min(1f, Energie + ENERGIE_REGENERATION);

            // Aktivierung neu berechnen
            Aktivierung = BerechneAktivierung(blackboard);
        }

        /// <summary>
        /// Von Subklassen implementiert: Die eigentliche Logik.
        /// </summary>
        protected abstract Task<AgentErgebnis> Tick(Blackboard blackboard);

        /// <summary>
        /// Von Subklassen implementiert: Wie dringend ist dieser Agent gerade?
        /// </summary>
        protected abstract float BerechneAktivierung(Blackboard blackboard);
    }

    /// <summary>
    /// Ergebnis eines Mikroagenten-Ticks.
    /// </summary>
    public class AgentErgebnis
    {
        public string agent;
        public bool ausgefuehrt;
        public string fehler;
        public Dictionary<string, object> erzeugteDaten = new Dictionary<string, object>();
        public List<AgentNachricht> nachrichten = new List<AgentNachricht>();
    }

    /// <summary>
    /// Nachricht zwischen Agenten.
    /// </summary>
    [Serializable]
    public class AgentNachricht
    {
        public string von;
        public string an;              // Ziel-Agent oder "*" fuer Broadcast
        public string typ;             // z.B. "WARNUNG", "VORSCHLAG", "DATEN"
        public string inhalt;
        public float prioritaet;
        public float zeitstempel;
    }

    /// <summary>
    /// Blackboard: Geteilter Zustand fuer alle Agenten.
    /// Kein Agent besitzt Daten — alle lesen/schreiben auf dem Blackboard.
    /// Das ermoeglicht emergente Interaktion ohne explizite Verdrahtung.
    /// </summary>
    public class Blackboard
    {
        private Dictionary<string, object> daten = new Dictionary<string, object>();
        private List<AgentNachricht> nachrichten = new List<AgentNachricht>();
        private Dictionary<string, float> letzteAktualisierung = new Dictionary<string, float>();

        /// <summary>
        /// Schreibt einen Wert aufs Blackboard. Ueberschreibt vorherigen Wert.
        /// </summary>
        public void Schreibe(string schluessel, object wert)
        {
            daten[schluessel] = wert;
            letzteAktualisierung[schluessel] = Time.time;
        }

        /// <summary>
        /// Liest einen Wert vom Blackboard.
        /// </summary>
        public T Lies<T>(string schluessel, T fallback = default)
        {
            if (daten.TryGetValue(schluessel, out var wert) && wert is T typisiertValue)
                return typisiertValue;
            return fallback;
        }

        public bool Hat(string schluessel) => daten.ContainsKey(schluessel);

        public float LetzteAenderung(string schluessel)
        {
            return letzteAktualisierung.TryGetValue(schluessel, out float t) ? t : 0f;
        }

        /// <summary>
        /// Sendet eine Nachricht an einen bestimmten Agenten oder Broadcast.
        /// </summary>
        public void SendeNachricht(AgentNachricht nachricht)
        {
            nachricht.zeitstempel = Time.time;
            nachrichten.Add(nachricht);
        }

        /// <summary>
        /// Holt alle Nachrichten fuer einen bestimmten Agenten.
        /// Konsumiert die Nachrichten (jede wird nur einmal gelesen).
        /// </summary>
        public List<AgentNachricht> HoleNachrichten(string agentName)
        {
            var relevant = nachrichten.Where(n =>
                n.an == agentName || n.an == "*").ToList();
            nachrichten.RemoveAll(n => relevant.Contains(n));
            return relevant;
        }

        /// <summary>
        /// Alle Schluessel auf dem Blackboard (fuer Meta-Kognition).
        /// </summary>
        public List<string> AlleSchluessel() => daten.Keys.ToList();
    }

    /// <summary>
    /// Das AgentNetzwerk: Verwaltet alle Mikroagenten und orchestriert sie.
    ///
    /// NICHT linear (Schritt 1, 2, 3...) sondern AKTIVIERUNGSBASIERT:
    /// - Jeder Agent berechnet seine Aktivierung
    /// - Die Top-N aktivsten Agenten laufen in diesem Tick
    /// - Agenten interagieren ueber das Blackboard
    /// - Komplexes Verhalten entsteht aus einfachen Regeln
    ///
    /// Der AGIKern benutzt das Netzwerk PARALLEL zum bisherigen Zyklus
    /// (nicht als Ersatz — sondern als emergente Ergaenzung).
    /// </summary>
    public class AgentNetzwerk
    {
        private List<MikroAgent> agenten = new List<MikroAgent>();
        private Blackboard blackboard;
        private int maxParallelAgenten;
        private List<NetzwerkTick> historie = new List<NetzwerkTick>();

        public Blackboard Blackboard => blackboard;

        public AgentNetzwerk(int maxParallel = 4)
        {
            blackboard = new Blackboard();
            maxParallelAgenten = maxParallel;
        }

        public void RegistriereAgent(MikroAgent agent)
        {
            agenten.Add(agent);
            Debug.Log($"[AgentNetzwerk] Agent registriert: {agent.Name}");
        }

        /// <summary>
        /// Ein Netzwerk-Tick:
        /// 1. Alle Agenten berechnen Aktivierung
        /// 2. Top-N werden ausgewaehlt
        /// 3. Parallele Ausfuehrung
        /// 4. Ergebnisse auf Blackboard geschrieben
        /// </summary>
        public async Task<NetzwerkTick> Tick()
        {
            var tick = new NetzwerkTick { zeitstempel = Time.time };

            // 1. Passive Updates fuer alle
            foreach (var agent in agenten)
                agent.PassiverTick(blackboard);

            // 2. Auswahl nach Aktivierung (hoechste zuerst)
            var aktiveListe = agenten
                .Where(a => a.Aktiv)
                .OrderByDescending(a => a.Aktivierung)
                .Take(maxParallelAgenten)
                .ToList();

            tick.aktiveAgenten = aktiveListe.Select(a => a.Name).ToList();

            // 3. Parallel ausfuehren
            var tasks = aktiveListe.Select(a => a.Ausfuehren(blackboard)).ToList();
            var ergebnisse = await Task.WhenAll(tasks);

            // 4. Ergebnisse verarbeiten
            foreach (var ergebnis in ergebnisse)
            {
                tick.ergebnisse.Add(ergebnis);

                // Erzeugte Daten aufs Blackboard
                foreach (var kvp in ergebnis.erzeugteDaten)
                    blackboard.Schreibe(kvp.Key, kvp.Value);

                // Nachrichten weiterleiten
                foreach (var nachricht in ergebnis.nachrichten)
                    blackboard.SendeNachricht(nachricht);
            }

            historie.Add(tick);
            if (historie.Count > 200) historie.RemoveAt(0);

            return tick;
        }

        // --- Statistik ---

        public List<(string name, float aktivierung, float energie)> GetAgentStatus()
        {
            return agenten.Select(a => (a.Name, a.Aktivierung, a.Energie)).ToList();
        }

        public List<NetzwerkTick> GetHistorie(int n = 20)
        {
            return historie.TakeLast(n).ToList();
        }

        public MikroAgent GetAgent(string name)
        {
            return agenten.FirstOrDefault(a => a.Name == name);
        }
    }

    /// <summary>
    /// Aufzeichnung eines Netzwerk-Ticks fuer Meta-Kognition.
    /// </summary>
    public class NetzwerkTick
    {
        public float zeitstempel;
        public List<string> aktiveAgenten = new List<string>();
        public List<AgentErgebnis> ergebnisse = new List<AgentErgebnis>();
    }
}
