using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    /// <summary>
    /// Deep Q-Network (DQN) mit 3-Layer MLP. Kein LLM. Kein externer ML-Framework.
    /// Reines C#-Netz: 20 → 64 → 32 → 17 (AktionsTypen).
    ///
    /// Ersetzt tabular Q-Learning: Generalisiert ueber aehnliche Zustaende.
    /// Experience Replay + Target Network fuer stabiles Lernen.
    ///
    /// Gleiche oeffentliche API wie der alte ReinforcementLerner.
    /// </summary>
    public class DQNLerner
    {
        // Netzwerk-Architektur
        private const int INPUT_DIM = ZustandsEncoder.VEKTOR_GROESSE; // 20
        private const int HIDDEN1 = 64;
        private const int HIDDEN2 = 32;
        private int outputDim; // AktionsTypen (17)

        // Online-Netzwerk (wird trainiert)
        private float[,] w1, w2, w3;
        private float[] b1, b2, b3;

        // Target-Netzwerk (wird periodisch kopiert, stabilisiert Training)
        private float[,] tw1, tw2, tw3;
        private float[] tb1, tb2, tb3;

        private ZustandsEncoder encoder;
        private float lernrate;
        private float diskontierung;
        private float exploration;
        private float explorationDecay;
        private float minExploration;
        private int gesamtUpdates;
        private int targetUpdateIntervall;

        // Experience Replay
        private List<RLTransition> replayBuffer;
        private const int MAX_REPLAY = 2000;
        private const int BATCH_GROESSE = 32;
        private const int MIN_REPLAY_FUER_TRAINING = 64;

        private System.Random rng;

        private const string PERSISTENZ_DATEI = "dqn_gewichte.json";

        // EWC-Schutz gegen Catastrophic Forgetting
        private EWCSchutz ewc;

        public DQNLerner(ZustandsEncoder encoder, AGIConfig config = null)
        {
            this.encoder = encoder;
            outputDim = Enum.GetValues(typeof(AktionsTyp)).Length;

            lernrate = 0.001f;
            diskontierung = 0.95f;
            exploration = 0.3f;
            explorationDecay = 0.9995f;
            minExploration = 0.05f;
            gesamtUpdates = 0;
            targetUpdateIntervall = 100;

            replayBuffer = new List<RLTransition>();
            rng = new System.Random();
            ewc = new EWCSchutz(400f);

            // Gewichte laden oder initialisieren
            var gespeichert = DatenLader.Lade<DQNGewichte>(PERSISTENZ_DATEI);
            if (gespeichert != null && gespeichert.w1 != null)
            {
                LadeGewichte(gespeichert);
                Debug.Log($"[DQN] Gewichte geladen, {gesamtUpdates} Updates.");
            }
            else
            {
                InitialisiereGewichte();
                Debug.Log("[DQN] Neue Gewichte initialisiert (Xavier).");
            }

            // Target = Kopie des Online-Netzwerks
            KopiereZuTarget();
        }

        // ===== Oeffentliche API (gleich wie ReinforcementLerner) =====

        public (AktionsTyp? aktion, float konfidenz) WaehleAktion(float[] zustand)
        {
            if (zustand == null || zustand.Length != INPUT_DIM)
                return (null, 0f);

            // Epsilon-Greedy
            if (rng.NextDouble() < exploration)
            {
                int zufaellig = rng.Next(outputDim);
                return ((AktionsTyp)zufaellig, 0.1f);
            }

            float[] qWerte = Forward(zustand, w1, b1, w2, b2, w3, b3);

            int besteAktion = 0;
            float besterWert = qWerte[0];
            for (int i = 1; i < outputDim; i++)
            {
                if (qWerte[i] > besterWert)
                {
                    besterWert = qWerte[i];
                    besteAktion = i;
                }
            }

            float durchschnitt = qWerte.Average();
            float spreizung = besterWert - durchschnitt;
            float konfidenz = Clamp01(spreizung * 2f + 0.3f);

            return ((AktionsTyp)besteAktion, konfidenz);
        }

        public void Lerne(float[] zustandVorher, AktionsTyp aktion, float belohnung, float[] zustandNachher)
        {
            replayBuffer.Add(new RLTransition
            {
                zustandVorher = zustandVorher,
                aktion = aktion,
                belohnung = belohnung,
                zustandNachher = zustandNachher
            });

            while (replayBuffer.Count > MAX_REPLAY)
                replayBuffer.RemoveAt(0);

            // Erst trainieren wenn genug Erfahrungen vorhanden
            if (replayBuffer.Count < MIN_REPLAY_FUER_TRAINING)
                return;

            // Mini-Batch Training
            TrainiereBatch();

            exploration = Math.Max(minExploration, exploration * explorationDecay);
            gesamtUpdates++;

            // Target-Netzwerk periodisch synchronisieren
            if (gesamtUpdates % targetUpdateIntervall == 0)
                KopiereZuTarget();

            if (gesamtUpdates % 100 == 0)
                Persistiere();
        }

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

                AktionsTyp aktionsTyp = AktionsTyp.Beobachten;
                if (e.aktionenListe != null && e.aktionenListe.Count > 0)
                    aktionsTyp = e.aktionenListe[0].typ;

                Lerne(zustand, aktionsTyp, e.belohnung, zustandNachher);
            }

            Persistiere();
            Debug.Log($"[DQN] Batch-Training: {erfahrungen.Count} Erfahrungen verarbeitet.");
        }

        public float GetExplorationRate() => exploration;
        public int GetGesamtUpdates() => gesamtUpdates;
        public int GetBekannteZustaende() => replayBuffer.Count;
        public EWCSchutz GetEWC() => ewc;

        /// <summary>
        /// Erstellt einen EWC-Snapshot (nach Trainingsphase / manuell).
        /// Schuetzt die aktuellen Gewichte vor zukuenftigem Vergessen.
        /// </summary>
        public void KonsolidiereWissen(string phasenName)
        {
            ewc.ErstelleSnapshot(phasenName, w1, b1, w2, b2, w3, b3,
                replayBuffer, INPUT_DIM, HIDDEN1, HIDDEN2, outputDim);
        }

        public float GetVertrautheit(float[] zustand)
        {
            if (zustand == null) return 0f;
            float[] qWerte = Forward(zustand, w1, b1, w2, b2, w3, b3);
            float max = qWerte.Max();
            float min = qWerte.Min();
            return Clamp01((max - min) * 3f);
        }

        public Dictionary<AktionsTyp, float> GetGlobalePolicyTendenz()
        {
            var tendenz = new Dictionary<AktionsTyp, float>();
            if (replayBuffer.Count == 0) return tendenz;

            // Durchschnitt der Q-Werte ueber Replay-Buffer
            float[] summe = new float[outputDim];
            int count = Math.Min(100, replayBuffer.Count);
            for (int i = replayBuffer.Count - count; i < replayBuffer.Count; i++)
            {
                float[] q = Forward(replayBuffer[i].zustandVorher, w1, b1, w2, b2, w3, b3);
                for (int j = 0; j < outputDim; j++)
                    summe[j] += q[j];
            }

            for (int i = 0; i < outputDim; i++)
                tendenz[(AktionsTyp)i] = summe[i] / count;

            return tendenz;
        }

        // ===== Neuronales Netz: Forward Pass =====

        private float[] Forward(float[] input,
            float[,] layer1W, float[] layer1B,
            float[,] layer2W, float[] layer2B,
            float[,] layer3W, float[] layer3B)
        {
            // Hidden 1: ReLU(W1 * input + b1)
            float[] h1 = new float[HIDDEN1];
            for (int i = 0; i < HIDDEN1; i++)
            {
                float sum = layer1B[i];
                for (int j = 0; j < INPUT_DIM; j++)
                    sum += layer1W[i, j] * input[j];
                h1[i] = sum > 0f ? sum : 0f; // ReLU
            }

            // Hidden 2: ReLU(W2 * h1 + b2)
            float[] h2 = new float[HIDDEN2];
            for (int i = 0; i < HIDDEN2; i++)
            {
                float sum = layer2B[i];
                for (int j = 0; j < HIDDEN1; j++)
                    sum += layer2W[i, j] * h1[j];
                h2[i] = sum > 0f ? sum : 0f; // ReLU
            }

            // Output: W3 * h2 + b3 (linear, Q-Werte)
            float[] output = new float[outputDim];
            for (int i = 0; i < outputDim; i++)
            {
                float sum = layer3B[i];
                for (int j = 0; j < HIDDEN2; j++)
                    sum += layer3W[i, j] * h2[j];
                output[i] = sum;
            }

            return output;
        }

        // ===== Training: Backpropagation mit MSE Loss =====

        private void TrainiereBatch()
        {
            // Zufaelligen Mini-Batch aus Replay-Buffer
            var batch = new List<RLTransition>();
            for (int i = 0; i < BATCH_GROESSE; i++)
                batch.Add(replayBuffer[rng.Next(replayBuffer.Count)]);

            foreach (var t in batch)
            {
                // Target Q-Wert berechnen (mit Target-Netzwerk)
                float[] qNachher = Forward(t.zustandNachher, tw1, tb1, tw2, tb2, tw3, tb3);
                float maxQNachher = qNachher.Max();
                float target = t.belohnung + diskontierung * maxQNachher;

                // Forward Pass (Online-Netzwerk) mit Zwischenwerten
                float[] h1 = new float[HIDDEN1];
                float[] h1_pre = new float[HIDDEN1]; // vor ReLU
                for (int i = 0; i < HIDDEN1; i++)
                {
                    float sum = b1[i];
                    for (int j = 0; j < INPUT_DIM; j++)
                        sum += w1[i, j] * t.zustandVorher[j];
                    h1_pre[i] = sum;
                    h1[i] = sum > 0f ? sum : 0f;
                }

                float[] h2 = new float[HIDDEN2];
                float[] h2_pre = new float[HIDDEN2];
                for (int i = 0; i < HIDDEN2; i++)
                {
                    float sum = b2[i];
                    for (int j = 0; j < HIDDEN1; j++)
                        sum += w2[i, j] * h1[j];
                    h2_pre[i] = sum;
                    h2[i] = sum > 0f ? sum : 0f;
                }

                float[] output = new float[outputDim];
                for (int i = 0; i < outputDim; i++)
                {
                    float sum = b3[i];
                    for (int j = 0; j < HIDDEN2; j++)
                        sum += w3[i, j] * h2[j];
                    output[i] = sum;
                }

                // Nur den Q-Wert der gewaehlten Aktion updaten
                int aktionsIndex = (int)t.aktion;
                float fehler = target - output[aktionsIndex];

                // Gradient Clipping (Huber-Loss-Approx)
                fehler = Math.Max(-1f, Math.Min(1f, fehler));

                // Backprop: Output → Hidden2
                float[] dOut = new float[outputDim];
                dOut[aktionsIndex] = fehler;

                float[] dH2 = new float[HIDDEN2];
                for (int j = 0; j < HIDDEN2; j++)
                {
                    float grad = 0f;
                    for (int i = 0; i < outputDim; i++)
                        grad += dOut[i] * w3[i, j];
                    dH2[j] = h2_pre[j] > 0f ? grad : 0f; // ReLU derivative
                }

                // Backprop: Hidden2 → Hidden1
                float[] dH1 = new float[HIDDEN1];
                for (int j = 0; j < HIDDEN1; j++)
                {
                    float grad = 0f;
                    for (int i = 0; i < HIDDEN2; i++)
                        grad += dH2[i] * w2[i, j];
                    dH1[j] = h1_pre[j] > 0f ? grad : 0f;
                }

                // Gewichte aktualisieren (SGD + EWC-Penalty)
                // EWC-Penalty berechnen (schuetzt wichtige Gewichte alter Tasks)
                ewc.BerechneSchichtPenalties(w1, b1, w2, b2, w3, b3,
                    out var pw1, out var pb1, out var pw2, out var pb2, out var pw3, out var pb3);

                // Layer 3
                for (int i = 0; i < outputDim; i++)
                {
                    for (int j = 0; j < HIDDEN2; j++)
                        w3[i, j] += lernrate * (dOut[i] * h2[j] - pw3[i, j]);
                    b3[i] += lernrate * (dOut[i] - pb3[i]);
                }

                // Layer 2
                for (int i = 0; i < HIDDEN2; i++)
                {
                    for (int j = 0; j < HIDDEN1; j++)
                        w2[i, j] += lernrate * (dH2[i] * h1[j] - pw2[i, j]);
                    b2[i] += lernrate * (dH2[i] - pb2[i]);
                }

                // Layer 1
                for (int i = 0; i < HIDDEN1; i++)
                {
                    for (int j = 0; j < INPUT_DIM; j++)
                        w1[i, j] += lernrate * (dH1[i] * t.zustandVorher[j] - pw1[i, j]);
                    b1[i] += lernrate * (dH1[i] - pb1[i]);
                }
            }
        }

        // ===== Gewichts-Initialisierung (Xavier) =====

        private void InitialisiereGewichte()
        {
            w1 = XavierInit(HIDDEN1, INPUT_DIM);
            b1 = new float[HIDDEN1];
            w2 = XavierInit(HIDDEN2, HIDDEN1);
            b2 = new float[HIDDEN2];
            w3 = XavierInit(outputDim, HIDDEN2);
            b3 = new float[outputDim];
        }

        private float[,] XavierInit(int rows, int cols)
        {
            float limit = (float)Math.Sqrt(6.0 / (rows + cols));
            var m = new float[rows, cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    m[i, j] = (float)(rng.NextDouble() * 2 * limit - limit);
            return m;
        }

        private void KopiereZuTarget()
        {
            tw1 = (float[,])w1.Clone();
            tb1 = (float[])b1.Clone();
            tw2 = (float[,])w2.Clone();
            tb2 = (float[])b2.Clone();
            tw3 = (float[,])w3.Clone();
            tb3 = (float[])b3.Clone();
        }

        // ===== Persistenz =====

        public void Persistiere()
        {
            var data = new DQNGewichte
            {
                w1 = Flatten(w1), w1Rows = HIDDEN1, w1Cols = INPUT_DIM, b1 = b1,
                w2 = Flatten(w2), w2Rows = HIDDEN2, w2Cols = HIDDEN1, b2 = b2,
                w3 = Flatten(w3), w3Rows = outputDim, w3Cols = HIDDEN2, b3 = b3,
                gesamtUpdates = gesamtUpdates,
                exploration = exploration
            };
            DatenLader.Speichere(PERSISTENZ_DATEI, data);
        }

        private void LadeGewichte(DQNGewichte g)
        {
            w1 = Unflatten(g.w1, g.w1Rows, g.w1Cols);
            b1 = g.b1;
            w2 = Unflatten(g.w2, g.w2Rows, g.w2Cols);
            b2 = g.b2;
            w3 = Unflatten(g.w3, g.w3Rows, g.w3Cols);
            b3 = g.b3;
            gesamtUpdates = g.gesamtUpdates;
            exploration = g.exploration;
        }

        private static float[] Flatten(float[,] m)
        {
            int rows = m.GetLength(0), cols = m.GetLength(1);
            float[] flat = new float[rows * cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    flat[i * cols + j] = m[i, j];
            return flat;
        }

        private static float[,] Unflatten(float[] flat, int rows, int cols)
        {
            var m = new float[rows, cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    m[i, j] = flat[i * cols + j];
            return m;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    // ===== Persistenz-Modell =====

    [Serializable]
    public class DQNGewichte
    {
        public float[] w1; public int w1Rows, w1Cols; public float[] b1;
        public float[] w2; public int w2Rows, w2Cols; public float[] b2;
        public float[] w3; public int w3Rows, w3Cols; public float[] b3;
        public int gesamtUpdates;
        public float exploration;
    }
}
