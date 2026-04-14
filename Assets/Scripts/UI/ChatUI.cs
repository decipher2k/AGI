using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BilligAGI.Kern;
using BilligAGI.Modelle;

namespace BilligAGI.UI
{
    public class ChatUI : MonoBehaviour
    {
        [Header("UI-Elemente")]
        public InputField inputField;
        public ScrollRect scrollRect;
        public Text chatText;
        public Button sendenButton;

        [Header("Referenzen")]
        public AGIKern agiKern;

        private List<string> chatVerlauf = new List<string>();
        private const int MAX_VERLAUF = 200;

        private void Start()
        {
            if (sendenButton != null)
                sendenButton.onClick.AddListener(Senden);
            if (inputField != null)
                inputField.onEndEdit.AddListener(OnEndEdit);

            ZeigeNachricht("[System] Billig-AGI Chat bereit. Tippe /hilfe fuer Befehle.");
        }

        private void OnEndEdit(string text)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Senden();
        }

        public void Senden()
        {
            if (inputField == null) return;
            string input = inputField.text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            inputField.text = "";
            ZeigeNachricht($"[Du] {input}");

            // Befehle verarbeiten
            if (input.StartsWith("/"))
            {
                VerarbeiteBefehl(input);
                return;
            }

            // An AGI-Kern senden
            if (agiKern != null)
                agiKern.VerarbeiteInput(input);
        }

        private void VerarbeiteBefehl(string befehl)
        {
            string cmd = befehl.ToLowerInvariant();

            if (cmd == "/hilfe" || cmd == "/help")
            {
                ZeigeNachricht("[System] Befehle: /ziele /plan /welt /stats /kompetenz /hypothesen " +
                    "/autonom an|aus /konsolidiere /kosten /konzepte /emotionen /geschichte " +
                    "/tom <name> /kreativ <ziel> /llmquote /modus /bench run|report /revision <konzept>");
                return;
            }

            if (agiKern == null) { ZeigeNachricht("[Fehler] Kein AGI-Kern."); return; }

            if (cmd == "/ziele")
            {
                var ziele = agiKern.GetZielManager()?.GetAlleAktiven();
                if (ziele == null || ziele.Count == 0) { ZeigeNachricht("[ZIELE] Keine aktiven Ziele."); return; }
                foreach (var z in ziele)
                    ZeigeNachricht($"[ZIELE] {z.beschreibung} (Prio: {z.effektivePrioritaet:F2}, Status: {z.status})");
            }
            else if (cmd == "/emotionen")
            {
                var emo = agiKern.GetEmotionen()?.zustand;
                if (emo == null) { ZeigeNachricht("[EMO] Kein EmotionsSystem."); return; }
                ZeigeNachricht($"[EMO] Angst:{emo.angst:F2} Neugier:{emo.neugier:F2} Frust:{emo.frustration:F2} " +
                    $"Zufrieden:{emo.zufriedenheit:F2} Ueberraschung:{emo.ueberraschung:F2}");
            }
            else if (cmd == "/kompetenz")
            {
                var sm = agiKern.GetSelbstModell();
                if (sm == null) return;
                ZeigeNachricht($"[KOMPETENZ] {sm.GetSelbstbeschreibung()}");
            }
            else if (cmd == "/modus")
            {
                ZeigeNachricht($"[MODUS] {agiKern.GetModus()} | Robustheit: {agiKern.GetRobustheit()?.GetAktuellerModus()}");
            }
            else if (cmd == "/llmquote")
            {
                float quote = agiKern.GetSemantik()?.BerechneQuote() ?? 0f;
                ZeigeNachricht($"[LLM-QUOTE] Lokal: {quote:P0}");
            }
            else if (cmd == "/kosten")
            {
                float kosten = agiKern.GetLLM()?.GetGesamtKosten() ?? 0f;
                ZeigeNachricht($"[KOSTEN] ${kosten:F4}");
            }
            else if (cmd == "/geschichte")
            {
                var kapitel = agiKern.GetNarativ()?.GetAutobiographie()?.kapitel;
                if (kapitel == null || kapitel.Count == 0) { ZeigeNachricht("[NARRATIV] Noch keine Geschichte."); return; }
                foreach (var k in kapitel)
                    ZeigeNachricht($"[NARRATIV] Kap.{k.nummer} ({k.alchemischePhase}): {k.zusammenfassung}");
            }
            else if (cmd == "/konsolidiere")
            {
                agiKern.GetKonsolidierung()?.Konsolidiere();
                ZeigeNachricht("[GEDAECHTNIS] Konsolidierung durchgefuehrt.");
            }
            else if (cmd.StartsWith("/autonom"))
            {
                bool an = cmd.Contains("an") || cmd.Contains("on");
                agiKern.SetzeAutonom(an);
                ZeigeNachricht($"[MODUS] Autonomer Modus: {(an ? "AN" : "AUS")}");
            }
            else if (cmd == "/welt")
            {
                var welt = agiKern.GetWeltModell()?.zustand;
                if (welt == null) return;
                ZeigeNachricht($"[WELT] {welt.objekte.Count} Objekte, Wetter: {welt.wetter}, Tageszeit: {welt.tageszeit:F1}h");
            }
            else if (cmd == "/stats")
            {
                int erfAnz = agiKern.GetErfahrungen()?.Anzahl() ?? 0;
                ZeigeNachricht($"[STATS] Erfahrungen: {erfAnz}");
            }
            else
            {
                ZeigeNachricht($"[System] Unbekannter Befehl: {befehl}");
            }
        }

        public void ZeigeNachricht(string nachricht)
        {
            chatVerlauf.Add(nachricht);
            if (chatVerlauf.Count > MAX_VERLAUF)
                chatVerlauf.RemoveAt(0);

            if (chatText != null)
            {
                chatText.text = string.Join("\n", chatVerlauf);
            }

            // Auto-Scroll
            if (scrollRect != null)
                Canvas.ForceUpdateCanvases();
        }
    }
}
