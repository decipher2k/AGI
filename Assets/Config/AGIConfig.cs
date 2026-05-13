using UnityEngine;
 
namespace BilligAGI
{
    public enum LLMAnbieter { Anthropic, OpenAI }

    [CreateAssetMenu(fileName = "AGIConfig", menuName = "BilligAGI/Config")]
    public class AGIConfig : ScriptableObject
    {
        [Header("LLM-Anbindung")]
        public LLMAnbieter llmAnbieter = LLMAnbieter.Anthropic;
        public string llmApiKey = "";
        public string llmModel = "claude-sonnet-4-20250514";
        public string llmApiUrl = "https://api.anthropic.com/v1/messages";
        public int maxTokensProAnfrage = 4096;

        [Header("Vektor-DB")]
        public string vektorDbUrl = "http://localhost:8000";
        public string vektorDbTyp = "chroma"; // "chroma" oder "qdrant"

        [Header("Wikipedia-RAG")]
        public bool wikipediaRagAktiv = true;
        public string wikipediaSprache = "de";
        public string wikipediaApiUrl = "https://de.wikipedia.org/w/api.php";
        public string wikipediaArtikelBasisUrl = "https://de.wikipedia.org/wiki/";
        public int wikipediaTopK = 3;
        public int wikipediaMaxArtikel = 3;
        public int wikipediaChunkZeichen = 900;
        public float wikipediaTimeoutSekunden = 5f;
        public int wikipediaCacheMinuten = 60;

        [Header("Physik & Sozial")]
        [Range(0f, 1f)] public float physikKonfidenzSchwelle = 0.7f;
        [Range(0f, 1f)] public float sozialKonfidenzSchwelle = 0.6f;

        [Header("Autonomer Modus")]
        public bool autonomerModus = false;
        public float zyklusIntervall = 2f;
        public float autonomModusTickRate = 2f;
        public int maxAutonomeSchritte = 100;

        [Header("VAKOG")]
        [Range(0f, 1f)] public float vakogSchwellwerte = 0.3f;

        [Header("KonzeptRevision (Hermeneutischer Zirkel)")]
        public int konzeptRevisionNachNAnwendungen = 10;
        public int konzeptRevisionSchwelle = 10;
        public int konzeptRevisionMaxPasses = 7;
        [Range(0f, 1f)] public float konzeptDriftSchwelle = 0.3f;
        public int maxRueckpropagationsTiefe = 3;

        [Header("Emotionen")]
        [Range(0f, 1f)] public float emotionsDecayRate = 0.05f;
        [Range(0f, 1f)] public float emotionsSchwelle = 0.3f;
        [Range(0f, 1f)] public float emotionsBaseline = 0.2f;

        [Header("One-Shot-Lernen")]
        [Range(0f, 1f)] public float oneShotSchwelle = 0.8f;

        [Header("Narratives Selbst")]
        public int autobiographieKapitelLaenge = 20;

        [Header("Theory of Mind")]
        public int tomMaxEntitaeten = 10;

        [Header("LLM-Unabhaengigkeit")]
        public bool llmFallbackModusAktiv = true;
        [Range(0f, 1f)] public float llmUnabhaengigkeitsZielquote = 0.6f;

        [Header("Subsymbolik")]
        public bool subsymbolikAktiv = true;
        public int subsymbolikDim = 128;

        [Header("Kreativitaet")]
        [Range(0f, 1f)] public float kreativitaetNoveltySchwelle = 0.65f;
        [Range(0f, 1f)] public float kreativitaetUtilitySchwelle = 0.55f;
        public bool kreativitaetABTestAktiv = true;
        public int kreativitaetMaxVarianten = 5;

        [Header("Langzeitlernen")]
        [Range(0f, 0.1f)] public float forgettingRate = 0.01f;
        public int langzeitMaxErfahrungen = 5000;
        [Range(0f, 1f)] public float langzeitDriftSchwelle = 0.15f;
        public int langzeitStabilisierungsSchwelle = 5;

        [Header("Konsistenzpruefung")]
        public int konsistenzPruefIntervall = 10;

        [Header("Robustheit")]
        public float apiRecoveryMaxSekunden = 120f;

        [Header("Sicherheit")]
        public int maxAutonomeSchritteProSitzung = 500;
        public bool notbremseAktiv = true;

        [Header("Iteratives Reasoning (A)")]
        public bool iterativesReasoningAktiv = true;
        [Range(2, 5)] public int reasoningIterationen = 3;

        [Header("DQN (B)")]
        public bool dqnStattTabular = true;

        [Header("Prediktives Weltmodell (C)")]
        public bool weltModellAktiv = false;

        [Header("Arbeitsgedaechtnis (D)")]
        public bool arbeitsGedaechtnisAktiv = true;
        public int arbeitsGedaechtnisMaxInteraktionen = 10;
        public int arbeitsGedaechtnisTokenBudget = 3000;

        [Header("Fine-Tuning / Selbstoptimierung")]
        public bool fineTuningAktiv = false;
        public string fineTuningApiUrl = "";    // Leer = leite von llmApiUrl ab
        public int fineTuningEpochen = 3;
        [Range(0.1f, 10f)] public float fineTuningLernrate = 1.0f;
        public int minErfahrungenFuerFineTuning = 500;
        public int fineTuningIntervallZyklen = 1000;
        public int evaluierungsZyklen = 50;

        [Header("Transfer-Learning")]
        public int transferMiningIntervall = 100;       // Alle N Zyklen Schema-Mining
        public int transferMiningSampleGroesse = 50;    // Letzte N Erfahrungen analysieren
    }
}
