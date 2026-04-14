using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    /// <summary>
    /// Tabular Q-Learning. Kein LLM. Kein neuronales Netz.
    /// Lernt aus Erfahrung.belohnung welche AktionsTypen in welchen
    /// Zustaenden gute Ergebnisse bringen.
    ///
    /// Wird NICHT als Ersatz fuer den Planer benutzt, sondern als ERGAENZUNG:
    /// Der Planer plant (LLM), der RL-Lerner bewertet die Aktionen im Nachhinein
    /// und schlaegt Alternativen vor wenn er genuegend Erfahrung hat.
    ///
    /// Zusaetzlich: Policy-Gewichtung — haeufig erfolgreiche Aktionen in
    /// aehnlichen Zustaenden bekommen hoehere Prioritaet.
    /// </summary>
    public class ReinforcementLerner
    {
        private Dictionary<long, float[]> qTable;         // state_hash → Q-Werte pro AktionsTyp
        private ZustandsEncoder encoder;
        private int aktionsAnzahl;
        private float lernrate;
        private float diskontierung;
        private float exploration;                         // Epsilon fuer epsilon-greedy
        private float explorationDecay;
        private float minExploration;
        private int gesamtUpdates;

        private const string PERSISTENZ_DATEI = "rl_qtable.json";

        // Erfahrungs-Replay-Buffer fuer stabileres Lernen
        private List<RLTransition> replayBuffer;
        private const int MAX_REPLAY = 500;
        private const int BATCH_GROESSE = 16;

        private System.Random rng;

        public ReinforcementLerner(ZustandsEncoder encoder, AGIConfig config = null)
        {
            this.encoder = encoder;
            aktionsAnzahl = Enum.GetValues(typeof(AktionsTyp)).Length; // 17

            lernrate = 0.1f;
            diskontierung = 0.95f;
            exploration = 0.3f;       // 30% zufaellige Exploration am Anfang
            explorationDecay = 0.999f;
            minExploration = 0.05f;
            gesamtUpdates = 0;

            replayBuffer = new List<RLTransition>();
            rng = new System.Random();

            // Q-Table laden
            var gespeichert = DatenLader.LadeListe<QTableEintrag>(PERSISTENZ_DATEI);
            qTable = new Dictionary<long, float[]>();
            if (gespeichert != null)
            {
                foreach (var e in gespeichert)
                    qTable[e.zustandHash] = e.qWerte;
                Debug.Log($"[RL] {qTable.Count} Zustaende geladen, {gesamtUpdates} Updates.");
            }
        }

        /// <summary>
        /// Schlaegt eine Aktion vor (epsilon-greedy).
        /// Gibt den AktionsTyp und den Q-Wert zurueck.
        /// Wenn nicht genuegend Erfahrung: gibt null zurueck → Planer entscheidet.
        /// </summary>
        public (AktionsTyp? aktion, float konfidenz) WaehleAktion(float[] zustand)
        {
            long hash = encoder.Diskretisiere(zustand);

            if (!qTable.TryGetValue(hash, out float[] qWerte))
                return (null, 0f); // Unbekannter Zustand → kein Vorschlag

            // Epsilon-greedy
            if (rng.NextDouble() < exploration)
            {
                int zufaellig = rng.Next(aktionsAnzahl);
                return ((AktionsTyp)zufaellig, 0.1f); // Niedrige Konfidenz = Exploration
            }

            // Greedy: Beste Aktion
            int besteAktion = 0;
            float besterWert = qWerte[0];
            for (int i = 1; i < aktionsAnzahl; i++)
            {
                if (qWerte[i] > besterWert)
                {
                    besterWert = qWerte[i];
                    besteAktion = i;
                }
            }

            // Konfidenz basiert auf Spreizung der Q-Werte
            float durchschnitt = qWerte.Average();
            float spreizung = besterWert - durchschnitt;
            float konfidenz = Mathf.Clamp01(spreizung * 2f + 0.3f);

            return ((AktionsTyp)besteAktion, konfidenz);
        }

        /// <summary>
        /// Lernt aus einer Erfahrung: Was war der Zustand, welche Aktion, was war die Belohnung?
        /// </summary>
        public void Lerne(float[] zustandVorher, AktionsTyp aktion, float belohnung, float[] zustandNachher)
        {
            // Transition im Replay-Buffer speichern
            replayBuffer.Add(new RLTransition
            {
                zustandVorher = zustandVorher,
                aktion = aktion,
                belohnung = belohnung,
                zustandNachher = zustandNachher
            });

            // Buffer begrenzen
            while (replayBuffer.Count > MAX_REPLAY)
                replayBuffer.RemoveAt(0);

            // Q-Update fuer aktuelle Transition
            QUpdate(zustandVorher, aktion, belohnung, zustandNachher);

            // Mini-Batch Replay (stabileres Lernen)
            if (replayBuffer.Count >= BATCH_GROESSE)
            {
                for (int i = 0; i < BATCH_GROESSE; i++)
                {
                    var t = replayBuffer[rng.Next(replayBuffer.Count)];
                    QUpdate(t.zustandVorher, t.aktion, t.belohnung, t.zustandNachher);
                }
            }

            // Exploration-Rate absenken
            exploration = Math.Max(minExploration, exploration * explorationDecay);
            gesamtUpdates++;

            // Periodisch persistieren
            if (gesamtUpdates % 50 == 0)
                Persistiere();
        }

        /// <summary>
        /// Batch-Lernen aus mehreren Erfahrungen (z.B. abends beim Konsolidieren).
        /// </summary>
        public void LerneAusErfahrungen(List<Erfahrung> erfahrungen, ZustandsEncoder enc)
        {
            for (int i = 0; i < erfahrungen.Count - 1; i++)
            {
                var e = erfahrungen[i];
                var eNaechste = erfahrungen[i + 1];

                var zustand = enc.Kodiere(
                    e.vakog, e.emotionalerZustand, e.weltKontext,
                    e.belohnung, 0.5f, 0, 0, 0f, 0f);
                var zustandNachher = enc.Kodiere(
                    eNaechste.vakog, eNaechste.emotionalerZustand, eNaechste.weltKontext,
                    eNaechste.belohnung, 0.5f, 0, 0, 0f, 0f);

                // AktionsTyp aus der Erfahrung extrahieren
                AktionsTyp aktionsTyp = AktionsTyp.Beobachten; // Default
                if (e.aktionenListe != null && e.aktionenListe.Count > 0)
                    aktionsTyp = e.aktionenListe[0].typ;

                Lerne(zustand, aktionsTyp, e.belohnung, zustandNachher);
            }

            Persistiere();
            Debug.Log($"[RL] Batch-Training: {erfahrungen.Count} Erfahrungen verarbeitet.");
        }

        private void QUpdate(float[] zustandVorher, AktionsTyp aktion, float belohnung, float[] zustandNachher)
        {
            long hashVorher = encoder.Diskretisiere(zustandVorher);
            long hashNachher = encoder.Diskretisiere(zustandNachher);

            if (!qTable.TryGetValue(hashVorher, out float[] qVorher))
            {
                qVorher = new float[aktionsAnzahl];
                qTable[hashVorher] = qVorher;
            }

            // Max Q-Wert des Folgezustands
            float maxQNachher = 0f;
            if (qTable.TryGetValue(hashNachher, out float[] qNachher))
                maxQNachher = qNachher.Max();

            int aktionsIndex = (int)aktion;

            // Q-Learning Update: Q(s,a) ← Q(s,a) + α[r + γ·max(Q(s',a')) - Q(s,a)]
            float alter = qVorher[aktionsIndex];
            float neuer = alter + lernrate * (belohnung + diskontierung * maxQNachher - alter);
            qVorher[aktionsIndex] = neuer;
        }

        // --- Statistik fuer Meta-Kognition ---

        public float GetExplorationRate() => exploration;
        public int GetGesamtUpdates() => gesamtUpdates;
        public int GetBekannteZustaende() => qTable.Count;

        /// <summary>
        /// Wie gut kennt sich das System in der Naehe dieses Zustands aus?
        /// 0 = voellig unbekannt, 1 = viele aehnliche Zustaende mit Spreizung.
        /// </summary>
        public float GetVertrautheit(float[] zustand)
        {
            long hash = encoder.Diskretisiere(zustand);
            if (!qTable.TryGetValue(hash, out float[] qWerte))
                return 0f;

            // Vertrautheit = Spreizung der Q-Werte (wenn klar ist welche Aktion besser)
            float max = qWerte.Max();
            float min = qWerte.Min();
            return Mathf.Clamp01((max - min) * 3f);
        }

        /// <summary>
        /// Durchschnittliche Q-Werte pro AktionsTyp (globale Policy-Tendenz).
        /// </summary>
        public Dictionary<AktionsTyp, float> GetGlobalePolicyTendenz()
        {
            var tendenz = new Dictionary<AktionsTyp, float>();
            if (qTable.Count == 0) return tendenz;

            float[] summe = new float[aktionsAnzahl];
            foreach (var q in qTable.Values)
                for (int i = 0; i < aktionsAnzahl; i++)
                    summe[i] += q[i];

            for (int i = 0; i < aktionsAnzahl; i++)
                tendenz[(AktionsTyp)i] = summe[i] / qTable.Count;

            return tendenz;
        }

        public void Persistiere()
        {
            var liste = qTable.Select(kvp => new QTableEintrag
            {
                zustandHash = kvp.Key,
                qWerte = kvp.Value
            }).ToList();
            DatenLader.Speichere(PERSISTENZ_DATEI, liste);
        }
    }

    // --- Hilfsklassen ---

    [Serializable]
    public class RLTransition
    {
        public float[] zustandVorher;
        public AktionsTyp aktion;
        public float belohnung;
        public float[] zustandNachher;
    }

    [Serializable]
    public class QTableEintrag
    {
        public long zustandHash;
        public float[] qWerte;
    }
}
