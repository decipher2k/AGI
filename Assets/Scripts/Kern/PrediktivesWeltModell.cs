using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    /// <summary>
    /// Predictive World Model — lernt Zustand + Aktion → naechster Zustand + Belohnung.
    /// Erlaubt dem Agenten "im Kopf zu simulieren" bevor er handelt.
    ///
    /// Architektur: MLP 37 → 64 → 32 → 21 (20D Zustand + 1 Belohnung)
    /// Input: 20D Zustand + 17D Aktion (one-hot)
    /// Output: 20D vorhergesagter Zustand + 1 vorhergesagte Belohnung
    ///
    /// Zuschaltbar via AGIConfig.weltModellAktiv
    /// </summary>
    public class PrediktivesWeltModell
    {
        private const int ZUSTAND_DIM = ZustandsEncoder.VEKTOR_GROESSE; // 20
        private int aktionDim; // 17 (AktionsTypen)
        private int inputDim;  // 37
        private const int HIDDEN1 = 64;
        private const int HIDDEN2 = 32;
        private int outputDim; // 21 (20 Zustand + 1 Belohnung)

        // Netzwerk-Gewichte
        private float[,] w1, w2, w3;
        private float[] b1, b2, b3;

        // Trainingsdaten
        private List<WeltModellTransition> trainingBuffer;
        private const int MAX_BUFFER = 3000;
        private const int BATCH_GROESSE = 32;
        private const int MIN_FUER_TRAINING = 100;

        private float lernrate;
        private int gesamtUpdates;
        private float letzterVerlust;
        private System.Random rng;

        private const string PERSISTENZ_DATEI = "weltmodell_gewichte.json";

        public bool Aktiv { get; set; }
        public float LetzterVerlust => letzterVerlust;
        public int GesamtUpdates => gesamtUpdates;
        public int GetAnzahlTransitionen() => trainingBuffer?.Count ?? 0;

        public PrediktivesWeltModell(bool aktiv = true)
        {
            aktionDim = Enum.GetValues(typeof(AktionsTyp)).Length;
            inputDim = ZUSTAND_DIM + aktionDim;
            outputDim = ZUSTAND_DIM + 1;
            Aktiv = aktiv;

            lernrate = 0.0005f;
            gesamtUpdates = 0;
            letzterVerlust = float.MaxValue;
            rng = new System.Random();

            trainingBuffer = new List<WeltModellTransition>();

            var gespeichert = DatenLader.Lade<DQNGewichte>(PERSISTENZ_DATEI);
            if (gespeichert != null && gespeichert.w1 != null)
            {
                LadeGewichte(gespeichert);
                Debug.Log($"[WeltModell] Gewichte geladen, {gesamtUpdates} Updates, Verlust: {letzterVerlust:F4}");
            }
            else
            {
                InitialisiereGewichte();
                Debug.Log("[WeltModell] Neue Gewichte initialisiert.");
            }
        }

        // ===== Vorhersage (Imagination) =====

        /// <summary>
        /// Sagt vorher: Was passiert wenn ich in diesem Zustand diese Aktion ausfuehre?
        /// </summary>
        public WeltVorhersage Vorhersage(float[] zustand, AktionsTyp aktion)
        {
            if (!Aktiv || zustand == null || zustand.Length != ZUSTAND_DIM)
                return null;

            float[] input = BaueInput(zustand, aktion);
            float[] output = Forward(input);

            float[] vorhergesagterZustand = new float[ZUSTAND_DIM];
            Array.Copy(output, 0, vorhergesagterZustand, 0, ZUSTAND_DIM);

            // Zustand auf [0,1] clampen
            for (int i = 0; i < ZUSTAND_DIM; i++)
                vorhergesagterZustand[i] = Clamp01(vorhergesagterZustand[i]);

            return new WeltVorhersage
            {
                vorhergesagterZustand = vorhergesagterZustand,
                vorhergesagteBelohnung = output[ZUSTAND_DIM],
                konfidenz = BerechneKonfidenz()
            };
        }

        /// <summary>
        /// Simuliert N Schritte in die Zukunft ("Imagination Rollout").
        /// Gibt die kumulative Belohnung zurueck.
        /// </summary>
        public float SimuliereRollout(float[] startZustand, AktionsTyp[] aktionsSequenz)
        {
            if (!Aktiv || startZustand == null) return 0f;

            float kumulativeBelohnung = 0f;
            float[] aktuell = (float[])startZustand.Clone();
            float diskont = 1f;

            foreach (var aktion in aktionsSequenz)
            {
                var vorhersage = Vorhersage(aktuell, aktion);
                if (vorhersage == null) break;

                kumulativeBelohnung += diskont * vorhersage.vorhergesagteBelohnung;
                diskont *= 0.95f;
                aktuell = vorhersage.vorhergesagterZustand;
            }

            return kumulativeBelohnung;
        }

        /// <summary>
        /// Bewertet alle moeglichen Aktionen und gibt die beste zurueck.
        /// Model-based Planning: "Was waere wenn?"
        /// </summary>
        public (AktionsTyp besteAktion, float erwarteteBelohunung) PlaneMitModell(float[] zustand)
        {
            if (!Aktiv || zustand == null || gesamtUpdates < MIN_FUER_TRAINING)
                return (AktionsTyp.Beobachten, 0f);

            AktionsTyp besteAktion = AktionsTyp.Beobachten;
            float besteBelohnung = float.MinValue;

            foreach (AktionsTyp aktion in Enum.GetValues(typeof(AktionsTyp)))
            {
                var vorhersage = Vorhersage(zustand, aktion);
                if (vorhersage != null && vorhersage.vorhergesagteBelohnung > besteBelohnung)
                {
                    besteBelohnung = vorhersage.vorhergesagteBelohnung;
                    besteAktion = aktion;
                }
            }

            return (besteAktion, besteBelohnung);
        }

        // ===== Lernen =====

        /// <summary>
        /// Registriert eine tatsaechliche Transition (Zustand + Aktion → Ergebnis).
        /// </summary>
        public void RegistriereTransition(float[] zustandVorher, AktionsTyp aktion,
            float[] zustandNachher, float belohnung)
        {
            if (!Aktiv) return;

            trainingBuffer.Add(new WeltModellTransition
            {
                zustandVorher = zustandVorher,
                aktion = aktion,
                zustandNachher = zustandNachher,
                belohnung = belohnung
            });

            while (trainingBuffer.Count > MAX_BUFFER)
                trainingBuffer.RemoveAt(0);

            if (trainingBuffer.Count >= MIN_FUER_TRAINING && trainingBuffer.Count % 10 == 0)
                TrainiereBatch();
        }

        private void TrainiereBatch()
        {
            float verlustSumme = 0f;

            for (int b = 0; b < BATCH_GROESSE; b++)
            {
                var t = trainingBuffer[rng.Next(trainingBuffer.Count)];

                float[] input = BaueInput(t.zustandVorher, t.aktion);

                // Target: tatsaechlicher naechster Zustand + Belohnung
                float[] target = new float[outputDim];
                Array.Copy(t.zustandNachher, 0, target, 0, ZUSTAND_DIM);
                target[ZUSTAND_DIM] = t.belohnung;

                // Forward mit Zwischenwerten
                float[] h1_pre = new float[HIDDEN1];
                float[] h1 = new float[HIDDEN1];
                for (int i = 0; i < HIDDEN1; i++)
                {
                    float sum = b1[i];
                    for (int j = 0; j < inputDim; j++)
                        sum += w1[i, j] * input[j];
                    h1_pre[i] = sum;
                    h1[i] = sum > 0f ? sum : 0f;
                }

                float[] h2_pre = new float[HIDDEN2];
                float[] h2 = new float[HIDDEN2];
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

                // MSE Loss + Backprop
                float[] dOut = new float[outputDim];
                for (int i = 0; i < outputDim; i++)
                {
                    float fehler = target[i] - output[i];
                    fehler = Math.Max(-1f, Math.Min(1f, fehler)); // Gradient Clipping
                    dOut[i] = fehler;
                    verlustSumme += fehler * fehler;
                }

                float[] dH2 = new float[HIDDEN2];
                for (int j = 0; j < HIDDEN2; j++)
                {
                    float grad = 0f;
                    for (int i = 0; i < outputDim; i++)
                        grad += dOut[i] * w3[i, j];
                    dH2[j] = h2_pre[j] > 0f ? grad : 0f;
                }

                float[] dH1 = new float[HIDDEN1];
                for (int j = 0; j < HIDDEN1; j++)
                {
                    float grad = 0f;
                    for (int i = 0; i < HIDDEN2; i++)
                        grad += dH2[i] * w2[i, j];
                    dH1[j] = h1_pre[j] > 0f ? grad : 0f;
                }

                // SGD Update
                for (int i = 0; i < outputDim; i++)
                {
                    for (int j = 0; j < HIDDEN2; j++)
                        w3[i, j] += lernrate * dOut[i] * h2[j];
                    b3[i] += lernrate * dOut[i];
                }
                for (int i = 0; i < HIDDEN2; i++)
                {
                    for (int j = 0; j < HIDDEN1; j++)
                        w2[i, j] += lernrate * dH2[i] * h1[j];
                    b2[i] += lernrate * dH2[i];
                }
                for (int i = 0; i < HIDDEN1; i++)
                {
                    for (int j = 0; j < inputDim; j++)
                        w1[i, j] += lernrate * dH1[i] * input[j];
                    b1[i] += lernrate * dH1[i];
                }
            }

            letzterVerlust = verlustSumme / (BATCH_GROESSE * outputDim);
            gesamtUpdates++;

            if (gesamtUpdates % 100 == 0)
            {
                Persistiere();
                Debug.Log($"[WeltModell] Update {gesamtUpdates}, Verlust: {letzterVerlust:F4}, Buffer: {trainingBuffer.Count}");
            }
        }

        // ===== Hilfsmethoden =====

        private float[] BaueInput(float[] zustand, AktionsTyp aktion)
        {
            float[] input = new float[inputDim];
            Array.Copy(zustand, 0, input, 0, ZUSTAND_DIM);
            input[ZUSTAND_DIM + (int)aktion] = 1f; // One-hot
            return input;
        }

        private float[] Forward(float[] input)
        {
            float[] h1 = new float[HIDDEN1];
            for (int i = 0; i < HIDDEN1; i++)
            {
                float sum = b1[i];
                for (int j = 0; j < inputDim; j++)
                    sum += w1[i, j] * input[j];
                h1[i] = sum > 0f ? sum : 0f;
            }

            float[] h2 = new float[HIDDEN2];
            for (int i = 0; i < HIDDEN2; i++)
            {
                float sum = b2[i];
                for (int j = 0; j < HIDDEN1; j++)
                    sum += w2[i, j] * h1[j];
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

            return output;
        }

        private float BerechneKonfidenz()
        {
            if (gesamtUpdates < MIN_FUER_TRAINING) return 0f;
            // Inverse Loss als Konfidenz (niedriger Verlust = hoehere Konfidenz)
            return Clamp01(1f - letzterVerlust * 5f);
        }

        // ===== Initialisierung =====

        private void InitialisiereGewichte()
        {
            w1 = XavierInit(HIDDEN1, inputDim);
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

        // ===== Persistenz =====

        public void Persistiere()
        {
            var data = new DQNGewichte
            {
                w1 = Flatten(w1), w1Rows = HIDDEN1, w1Cols = inputDim, b1 = b1,
                w2 = Flatten(w2), w2Rows = HIDDEN2, w2Cols = HIDDEN1, b2 = b2,
                w3 = Flatten(w3), w3Rows = outputDim, w3Cols = HIDDEN2, b3 = b3,
                gesamtUpdates = gesamtUpdates,
                exploration = letzterVerlust
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
            letzterVerlust = g.exploration;
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

    // ===== Modelle =====

    [Serializable]
    public class WeltVorhersage
    {
        public float[] vorhergesagterZustand;
        public float vorhergesagteBelohnung;
        public float konfidenz;
    }

    [Serializable]
    public class WeltModellTransition
    {
        public float[] zustandVorher;
        public AktionsTyp aktion;
        public float[] zustandNachher;
        public float belohnung;
    }
}
