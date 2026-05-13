using System.Collections.Generic;
using System.Linq;
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
        public AutoTrainer autoTrainer;

        private List<string> chatVerlauf = new List<string>();
        private const int MAX_VERLAUF = 200;

        private void Start()
        {
            // Auto-discover from inspector-wired or existing child hierarchy
            if (inputField == null) inputField = GetComponentInChildren<InputField>(true);
            if (scrollRect == null) scrollRect = GetComponentInChildren<ScrollRect>(true);
            if (sendenButton == null) sendenButton = GetComponentInChildren<Button>(true);
            if (chatText == null && scrollRect?.content != null)
                chatText = scrollRect.content.GetComponentInChildren<Text>(true);

            // If still missing, build the entire UI programmatically
            if (GetComponent<RectTransform>() != null)
                EnsureUIElemente();

            // Ensure scroll content auto-resizes with text
            if (scrollRect?.content != null)
            {
                var fitter = scrollRect.content.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = scrollRect.content.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }

            if (sendenButton != null) sendenButton.onClick.AddListener(Senden);
            if (inputField != null)
            {
                inputField.onSubmit.AddListener(OnSubmit);
                inputField.onEndEdit.AddListener(OnEndEdit);
            }
            if (agiKern != null) agiKern.OnAntwort += OnAGIAntwort;

            ZeigeNachricht("[System] Billig-AGI Chat bereit. Tippe /hilfe fuer Befehle.");
        }

        private void EnsureUIElemente()
        {
            // Panel background
            if (GetComponent<Image>() == null)
                gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            // ScrollView
            if (scrollRect == null)
            {
                var scrollGo = UIKind("ScrollView");
                AnchorRect(scrollGo, new Vector2(0.02f, 0.2f), new Vector2(0.98f, 0.98f));
                scrollGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);
                scrollRect = scrollGo.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;

                var viewport = UIKind("Viewport", scrollGo);
                AnchorRect(viewport, Vector2.zero, Vector2.one);
                viewport.AddComponent<Image>().color = Color.clear;
                viewport.AddComponent<Mask>().showMaskGraphic = false;

                var content = UIKind("Content", viewport);
                var contentRT = content.GetComponent<RectTransform>();
                contentRT.anchorMin = new Vector2(0f, 1f);
                contentRT.anchorMax = new Vector2(1f, 1f);
                contentRT.pivot   = new Vector2(0.5f, 1f);
                contentRT.sizeDelta = new Vector2(0f, 0f);

                chatText = content.AddComponent<Text>();
                chatText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                chatText.fontSize = 16;
                chatText.supportRichText = true;
                chatText.alignment = TextAnchor.UpperLeft;
                chatText.horizontalOverflow = HorizontalWrapMode.Wrap;
                chatText.verticalOverflow   = VerticalWrapMode.Overflow;
                chatText.color = Color.white;

                var fitter = content.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scrollRect.viewport = viewport.GetComponent<RectTransform>();
                scrollRect.content  = contentRT;
            }

            // InputField
            if (inputField == null)
            {
                var inputGo = UIKind("InputField");
                AnchorRect(inputGo, new Vector2(0.02f, 0.02f), new Vector2(0.78f, 0.18f));
                inputGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
                inputField = inputGo.AddComponent<InputField>();

                var textGo = UIKind("Text", inputGo);
                AnchorRect(textGo, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
                var inputText = textGo.AddComponent<Text>();
                inputText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                inputText.fontSize  = 16;
                inputText.color     = Color.white;
                inputText.alignment = TextAnchor.MiddleLeft;
                inputText.supportRichText = false;

                var phGo = UIKind("Placeholder", inputGo);
                AnchorRect(phGo, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
                var ph = phGo.AddComponent<Text>();
                ph.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                ph.fontSize  = 16;
                ph.color     = new Color(1f, 1f, 1f, 0.4f);
                ph.fontStyle = FontStyle.Italic;
                ph.text      = "Nachricht oder /befehl...";
                ph.alignment = TextAnchor.MiddleLeft;

                inputField.textComponent = inputText;
                inputField.placeholder   = ph;
            }

            // Send button
            if (sendenButton == null)
            {
                var btnGo = UIKind("SendenButton");
                AnchorRect(btnGo, new Vector2(0.8f, 0.02f), new Vector2(0.98f, 0.18f));
                btnGo.AddComponent<Image>().color = new Color(0.15f, 0.45f, 0.8f, 0.85f);
                sendenButton = btnGo.AddComponent<Button>();

                var lblGo = UIKind("Text", btnGo);
                AnchorRect(lblGo, Vector2.zero, Vector2.one);
                var lbl = lblGo.AddComponent<Text>();
                lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lbl.text      = "Senden";
                lbl.fontSize  = 16;
                lbl.color     = Color.white;
                lbl.alignment = TextAnchor.MiddleCenter;
            }
        }

        private GameObject UIKind(string name, GameObject parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent != null ? parent.transform : transform, false);
            return go;
        }

        private static void AnchorRect(GameObject go, Vector2 min, Vector2 max,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private void OnDestroy()
        {
            if (agiKern != null) agiKern.OnAntwort -= OnAGIAntwort;
        }

        private void OnAGIAntwort(string antwort)
        {
            ZeigeNachricht($"[AGI] {antwort}");
        }

        private void OnSubmit(string text)
        {
            Senden();
        }

        private void OnEndEdit(string text)
        {
            // Submit when Enter was pressed; OnEndEdit also fires on focus loss
            if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
                Senden();
        }

        public void Senden()
        {
            if (inputField == null) return;
            string input = inputField.text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            inputField.text = "";
            inputField.ActivateInputField();
            ZeigeNachricht($"[Du] {input}");

            // Befehle verarbeiten
            if (input.StartsWith("/"))
            {
                VerarbeiteBefehl(input);
                return;
            }

            // An AGI-Kern senden
            if (agiKern == null)
            {
                ZeigeNachricht("[Fehler] AGI-Kern nicht verbunden.");
                return;
            }
            if (!agiKern.IstBereit())
            {
                ZeigeNachricht("[System] AGI initialisiert noch... bitte warten.");
                return;
            }
            agiKern.VerarbeiteInput(input);
        }

        private void VerarbeiteBefehl(string befehl)
        {
            string cmd = befehl.ToLowerInvariant();

            if (cmd == "/hilfe" || cmd == "/help")
            {
                ZeigeNachricht("[System] Befehle: /ziele /plan /welt /stats /kompetenz /hypothesen " +
                    "/autonom an|aus /konsolidiere /kosten /konzepte /emotionen /geschichte " +
                    "/tom <name> /kreativ <ziel> /llmquote /modus /bench run|report /revision <konzept> " +
                    "/training start|stop|status|reset|phase <N> " +
                    "/finetuning status|start|rollback " +
                    "/szene erstelle|spawn|entferne|wetter|zeit " +
                    "/transfer status|mining|schemata " +
                    "/konzeptbildung status|jetzt " +
                    "/kausal warum|intervention|status " +
                    "/hypothese status|generiere|liste " +
                    "/ewc status|snapshot " +
                    "/konzeptbaum status|baum|reorganisiere " +
                    "/metaziel status|generiere|quellen " +
                    "/grounding status|wort <X>|top " +
                    "/physiksim status|wo <Objekt>|stabilitaet " +
                    "/simulation status|waswenn <Aktion>|beste|kontrafaktisch " +
                    "/langzeitplan status|meilensteine|historie " +
                    "/curriculum status|ziele|statistik " +
                    "/sprache status|erklaere <Wort>|warum " +
                    "/perf status " +
                    "/mission status|an|aus|start <Text>|startauto|empfehlung|stop|historie " +
                    "/arc2 run [N]|status|report");
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
                float quote = agiKern.GetSemantik()?.BerechneLokalQuote() ?? 0f;
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
            else if (cmd.StartsWith("/training"))
            {
                VerarbeiteTrainingBefehl(cmd);
            }
            else if (cmd.StartsWith("/finetuning") || cmd.StartsWith("/finetune"))
            {
                VerarbeiteFineTuningBefehl(cmd);
            }
            else if (cmd.StartsWith("/szene"))
            {
                VerarbeiteSzeneBefehl(cmd);
            }
            else if (cmd.StartsWith("/transfer"))
            {
                VerarbeiteTransferBefehl(cmd);
            }
            else if (cmd.StartsWith("/konzeptbildung"))
            {
                VerarbeiteKonzeptBildungBefehl(cmd);
            }
            else if (cmd.StartsWith("/kausal"))
            {
                VerarbeiteKausalBefehl(cmd);
            }
            else if (cmd.StartsWith("/hypothese"))
            {
                VerarbeiteHypothesenBefehl(cmd);
            }
            else if (cmd.StartsWith("/ewc"))
            {
                VerarbeiteEWCBefehl(cmd);
            }
            else if (cmd.StartsWith("/konzeptbaum"))
            {
                VerarbeiteKonzeptBaumBefehl(cmd);
            }
            else if (cmd.StartsWith("/metaziel"))
            {
                VerarbeiteMetaZielBefehl(cmd);
            }
            else if (cmd.StartsWith("/grounding"))
            {
                VerarbeiteGroundingBefehl(cmd);
            }
            else if (cmd.StartsWith("/physiksim"))
            {
                VerarbeitePhysikSimBefehl(cmd);
            }
            else if (cmd.StartsWith("/simulation"))
            {
                VerarbeiteSimulationBefehl(cmd);
            }
            else if (cmd.StartsWith("/langzeitplan"))
            {
                VerarbeiteLangzeitPlanBefehl(cmd);
            }
            else if (cmd.StartsWith("/curriculum"))
            {
                VerarbeiteCurriculumBefehl(cmd);
            }
            else if (cmd.StartsWith("/sprache"))
            {
                VerarbeiteSpracheBefehl(cmd);
            }
            else if (cmd.StartsWith("/perf"))
            {
                VerarbeitePerfBefehl(cmd);
            }
            else if (cmd.StartsWith("/mission"))
            {
                VerarbeiteMissionBefehl(cmd);
            }
            else if (cmd.StartsWith("/arc2"))
            {
                VerarbeiteArc2Befehl(cmd);
            }
            else
            {
                ZeigeNachricht($"[System] Unbekannter Befehl: {befehl}");
            }
        }

        private void VerarbeiteTrainingBefehl(string cmd)
        {
            if (autoTrainer == null) { ZeigeNachricht("[Training] Kein AutoTrainer zugewiesen."); return; }

            if (cmd.Contains("start"))
            {
                autoTrainer.StartTraining();
                ZeigeNachricht("[Training] Automatisches Training gestartet.");
            }
            else if (cmd.Contains("stop") || cmd.Contains("pause"))
            {
                autoTrainer.PauseTraining();
                ZeigeNachricht("[Training] Training pausiert.");
            }
            else if (cmd.Contains("reset"))
            {
                autoTrainer.ResetTraining();
                ZeigeNachricht("[Training] Training zurueckgesetzt.");
            }
            else if (cmd.Contains("phase"))
            {
                // /training phase 3 → Planen
                string[] teile = cmd.Split(' ');
                for (int i = 0; i < teile.Length; i++)
                {
                    if (teile[i] == "phase" && i + 1 < teile.Length && int.TryParse(teile[i + 1], out int p))
                    {
                        if (p >= 0 && p <= 5)
                        {
                            autoTrainer.SetzePhase((TrainingsPhase)p);
                            ZeigeNachricht($"[Training] Phase gesetzt: {(TrainingsPhase)p}");
                        }
                        else
                            ZeigeNachricht("[Training] Phase 0-5: Beobachten/Navigieren/Interagieren/Sozial/Planen/Frei");
                        return;
                    }
                }
                ZeigeNachricht("[Training] /training phase <0-5>");
            }
            else // status
            {
                var stats = autoTrainer.GetStatistik();
                var phase = autoTrainer.GetPhase();
                ZeigeNachricht(
                    $"[Training] Phase: {phase} | Zyklen: {stats?.zyklenGesamt ?? 0} | " +
                    $"Erfahrungen: {stats?.erfahrungenGesamt ?? 0} | " +
                    $"Ziele: {stats?.zieleErreicht ?? 0}\u2713 {stats?.zieleGescheitert ?? 0}\u2717 | " +
                    $"\u2205 Belohnung: {stats?.durchschnittsBelohnung ?? 0:F3}");
            }
        }

        private void VerarbeiteSzeneBefehl(string cmd)
        {
            var wm = agiKern?.GetWeltManipulator();
            if (wm == null) { ZeigeNachricht("[Szene] Kein WeltManipulator verfuegbar."); return; }

            // "/szene" allein → Hilfe
            string args = cmd.Length > 6 ? cmd.Substring(6).Trim() : "";
            if (string.IsNullOrEmpty(args))
            {
                ZeigeNachricht("[Szene] Befehle:\n" +
                    "  /szene erstelle wald|garten|teich|wiese — Szenario erstellen\n" +
                    "  /szene spawn <prefab> [x,y,z] — Objekt platzieren\n" +
                    "  /szene entferne <name> — Objekt entfernen\n" +
                    "  /szene bewege <name> x,y,z — Objekt bewegen\n" +
                    "  /szene wetter regen|schnee|nebel|sturm|klar [0-1]\n" +
                    "  /szene zeit <0-24> — Tageszeit setzen\n" +
                    "  /szene event explosion [x,y,z]");
                return;
            }

            var ergebnis = wm.FuehreDirektBefehlAus(args);
            ZeigeNachricht($"[Szene] {ergebnis.beschreibung}");
        }

        private void VerarbeiteFineTuningBefehl(string cmd)
        {
            var so = agiKern?.GetSelbstOptimierung();
            if (so == null) { ZeigeNachricht("[FineTuning] Keine SelbstOptimierung verfuegbar."); return; }

            if (cmd.Contains("start"))
            {
                so.ErzwingeFineTuning();
                ZeigeNachricht("[FineTuning] Fine-Tuning manuell gestartet.");
            }
            else if (cmd.Contains("rollback"))
            {
                so.ManuellRollback();
                ZeigeNachricht("[FineTuning] Rollback durchgefuehrt.");
            }
            else if (cmd.Contains("an") || cmd.Contains("on"))
            {
                so.StarteOptimierung();
                ZeigeNachricht("[FineTuning] Selbstoptimierung aktiviert.");
            }
            else if (cmd.Contains("aus") || cmd.Contains("off"))
            {
                so.PauseOptimierung();
                ZeigeNachricht("[FineTuning] Selbstoptimierung pausiert.");
            }
            else // status
            {
                ZeigeNachricht($"[FineTuning] {so.GetStatusText()}");
                var ft = agiKern.GetFineTuningManager();
                if (ft != null)
                    ZeigeNachricht($"[FineTuning] {ft.GetZusammenfassung()}");
            }
        }

        private async void VerarbeiteTransferBefehl(string cmd)
        {
            var tl = agiKern.GetTransferLerner();
            if (tl == null)
            {
                ZeigeNachricht("[Transfer] TransferLerner nicht initialisiert.");
                return;
            }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "";

            if (sub == "status")
            {
                ZeigeNachricht($"[Transfer] {tl.GetStatusText()}");
            }
            else if (sub == "mining")
            {
                ZeigeNachricht("[Transfer] Starte Schema-Mining...");
                var neue = await tl.SchemaMining();
                ZeigeNachricht($"[Transfer] {neue.Count} neue Schemata extrahiert. Gesamt: {tl.SchemaAnzahl}");
            }
            else if (sub == "schemata")
            {
                var alle = tl.GetAlleSchemata();
                if (alle.Count == 0)
                {
                    ZeigeNachricht("[Transfer] Keine Schemata vorhanden.");
                    return;
                }
                foreach (var s in alle)
                {
                    ZeigeNachricht($"[Schema] {s.name} [{s.konfidenz:F2}] — {s.abstrakteRegel}");
                    ZeigeNachricht($"  Domaene: {s.quellDomaene}, Anwendungen: {s.anwendungen}, Erfolg: {s.erfolgsRate:P0}");
                }
            }
            else
            {
                ZeigeNachricht("[Transfer] Syntax: /transfer status|mining|schemata");
            }
        }

        private async void VerarbeiteKonzeptBildungBefehl(string cmd)
        {
            var kb = agiKern.GetKonzeptBildung();
            if (kb == null)
            {
                ZeigeNachricht("[KonzeptBildung] Nicht initialisiert.");
                return;
            }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "";

            if (sub == "status")
            {
                ZeigeNachricht($"[KonzeptBildung] {kb.GetStatusText()}");
            }
            else if (sub == "jetzt")
            {
                ZeigeNachricht("[KonzeptBildung] Starte Analyse...");
                var ergebnis = await kb.ErzwingeKonzeptbildung();
                if (ergebnis != null && ergebnis.neuesKonzeptEntdeckt)
                    ZeigeNachricht($"[KonzeptBildung] {ergebnis.zusammenfassung}");
                else
                    ZeigeNachricht("[KonzeptBildung] Keine neuen Muster gefunden.");
            }
            else
            {
                ZeigeNachricht("[KonzeptBildung] Syntax: /konzeptbildung status|jetzt");
            }
        }

        private void VerarbeiteEWCBefehl(string cmd)
        {
            var dqn = agiKern?.GetDQN();
            if (dqn == null) { ZeigeNachricht("[EWC] DQN nicht aktiv."); return; }

            var ewc = dqn.GetEWC();
            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[EWC] {ewc.GetStatusText()}");
            }
            else if (sub == "snapshot")
            {
                string name = teile.Length > 2 ? teile[2] : $"manuell_{System.DateTime.Now:HHmmss}";
                dqn.KonsolidiereWissen(name);
                ZeigeNachricht($"[EWC] Snapshot '{name}' erstellt. Aktuelle Gewichte sind jetzt geschützt.");
            }
            else
            {
                ZeigeNachricht("[EWC] Syntax: /ewc status|snapshot [name]");
            }
        }

        private async void VerarbeiteKonzeptBaumBefehl(string cmd)
        {
            var kb = agiKern?.GetKonzeptBaum();
            if (kb == null) { ZeigeNachricht("[KonzeptBaum] Nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[KonzeptBaum] {kb.GetStatusText()}");
            }
            else if (sub == "baum")
            {
                string baum = kb.GetBaumText();
                if (string.IsNullOrWhiteSpace(baum))
                    ZeigeNachricht("[KonzeptBaum] Noch keine Hierarchie aufgebaut.");
                else
                    ZeigeNachricht($"[KonzeptBaum]\n{baum}");
            }
            else if (sub == "reorganisiere")
            {
                ZeigeNachricht("[KonzeptBaum] Starte Reorganisation...");
                var erg = await kb.ErzwingeReorganisation();
                if (erg != null)
                    ZeigeNachricht($"[KonzeptBaum] {erg}");
                else
                    ZeigeNachricht("[KonzeptBaum] Keine Reorganisation nötig.");
            }
            else
            {
                ZeigeNachricht("[KonzeptBaum] Syntax: /konzeptbaum status|baum|reorganisiere");
            }
        }

        private async void VerarbeiteKausalBefehl(string cmd)
        {
            var kr = agiKern?.GetKausalesReasoning();
            if (kr == null) { ZeigeNachricht("[Kausal] KausalesReasoning nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "";

            if (sub == "status")
            {
                ZeigeNachricht($"[Kausal] {kr.GetStatusText()}");
            }
            else if (sub == "warum" && teile.Length > 2)
            {
                string wirkung = string.Join(" ", teile, 2, teile.Length - 2);
                ZeigeNachricht($"[Kausal] Analysiere: '{wirkung}'...");
                var analyse = await kr.WarumAnalyse(wirkung);
                ZeigeNachricht($"[Kausal] Ebene: {analyse.ebene}\n{analyse.erklaerung}");
                if (analyse.hypothesen?.Count > 0)
                {
                    foreach (var h in analyse.hypothesen)
                        ZeigeNachricht($"  → {h.ursache} → {h.wirkung} (Konfidenz: {h.konfidenz:F2})");
                }
            }
            else if (sub == "intervention" && teile.Length > 2)
            {
                string aktionStr = teile[2];
                if (System.Enum.TryParse<AktionsTyp>(aktionStr, true, out var aktionsTyp))
                {
                    var zustand = agiKern.GetLetzterZustandsVektor();
                    if (zustand != null)
                    {
                        var erg = kr.SimuliereIntervention(zustand, aktionsTyp);
                        ZeigeNachricht($"[Kausal] Intervention '{aktionsTyp}': Δ Belohnung = {erg.deltaBelohnung:+0.00;-0.00} " +
                            $"(Erwartung: {erg.erwarteteBelohnung:F2})");
                    }
                    else
                        ZeigeNachricht("[Kausal] Kein Zustandsvektor verfuegbar.");
                }
                else
                    ZeigeNachricht($"[Kausal] Unbekannte Aktion. Verfuegbar: {string.Join(", ", System.Enum.GetNames(typeof(AktionsTyp)))}");
            }
            else
            {
                ZeigeNachricht("[Kausal] Syntax: /kausal status | /kausal warum <Wirkung> | /kausal intervention <Aktion>");
            }
        }

        private async void VerarbeiteHypothesenBefehl(string cmd)
        {
            var he = agiKern?.GetHypothesenEngine();
            if (he == null) { ZeigeNachricht("[Hypothesen] HypothesenEngine nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "";

            if (sub == "status")
            {
                ZeigeNachricht($"[Hypothesen] {he.GetStatusText()}");
            }
            else if (sub == "generiere")
            {
                ZeigeNachricht("[Hypothesen] Generiere neue Hypothese...");
                var erg = await he.ErzwingeHypothesenbildung();
                if (erg != null && erg.neueHypothese)
                    ZeigeNachricht($"[Hypothesen] {erg.zusammenfassung}");
                else
                    ZeigeNachricht("[Hypothesen] Keine neue Hypothese generiert.");
            }
            else if (sub == "liste")
            {
                var alle = he.GetAlle();
                if (alle.Count == 0) { ZeigeNachricht("[Hypothesen] Keine Hypothesen vorhanden."); return; }
                foreach (var h in alle)
                {
                    ZeigeNachricht($"[{h.status}] {h.beschreibung} (Konfidenz: {h.konfidenz:F2}, " +
                        $"Stuetzend: {h.stuetzendeErfahrungen.Count}, Widerspruch: {h.widersprechendeErfahrungen.Count})");
                }
            }
            else
            {
                ZeigeNachricht("[Hypothesen] Syntax: /hypothese status|generiere|liste");
            }
        }

        private void VerarbeiteMetaZielBefehl(string cmd)
        {
            var mz = agiKern?.GetMetaZielSystem();
            if (mz == null) { ZeigeNachricht("[MetaZiel] MetaZielSystem nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[MetaZiel] {mz.GetStatusText()}");
            }
            else if (sub == "generiere")
            {
                ZeigeNachricht("[MetaZiel] Erzwinge Zielgenerierung...");
                var erg = mz.ErzwingeGenerierung();
                if (erg != null && erg.zieleGeneriert > 0)
                {
                    ZeigeNachricht($"[MetaZiel] {erg.zusammenfassung}");
                    foreach (var b in erg.generierteBeschreibungen)
                        ZeigeNachricht($"  → {b}");
                }
                else
                    ZeigeNachricht("[MetaZiel] Keine neuen Ziele generiert (alle Slots belegt oder keine Quellen).");
            }
            else if (sub == "quellen")
            {
                var quellen = mz.GetLetzteQuellen();
                if (quellen.Count == 0) { ZeigeNachricht("[MetaZiel] Keine Quellen im letzten Zyklus."); return; }
                foreach (var q in quellen)
                    ZeigeNachricht($"  [{q.name}] {q.beschreibung} (Dringlichkeit: {q.dringlichkeit:F2})");
                ZeigeNachricht($"[MetaZiel] Quellenverteilung: {mz.GetQuellenVerteilungText()}");
            }
            else
            {
                ZeigeNachricht("[MetaZiel] Syntax: /metaziel status|generiere|quellen");
            }
        }

        private void VerarbeiteGroundingBefehl(string cmd)
        {
            var gb = agiKern?.GetGroundingBruecke();
            if (gb == null) { ZeigeNachricht("[Grounding] GroundingBruecke nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[Grounding] {gb.GetStatusText()}");
            }
            else if (sub == "wort" && teile.Length > 2)
            {
                string wort = teile[2].ToLowerInvariant();
                var eintrag = gb.GetGroundingFuerWort(wort);
                if (eintrag != null)
                {
                    ZeigeNachricht($"[Grounding] '{wort}': Staerke={eintrag.groundingStaerke:F2}, " +
                        $"Erfahrungen={eintrag.erfahrungsAnzahl}, " +
                        $"Letztes Update: {eintrag.letztesUpdate}");
                }
                else
                {
                    ZeigeNachricht($"[Grounding] '{wort}' ist nicht erfahrungsgeerdet (nur LLM oder unbekannt).");
                }
            }
            else if (sub == "top")
            {
                var top = gb.GetTopGeerdeteWoerter(10);
                if (top.Count == 0) { ZeigeNachricht("[Grounding] Noch keine geerdeten Woerter."); return; }
                ZeigeNachricht("[Grounding] Top geerdete Woerter:");
                foreach (var e in top)
                    ZeigeNachricht($"  '{e.wort}': Staerke={e.groundingStaerke:F2} ({e.erfahrungsAnzahl}x erlebt)");
            }
            else
            {
                ZeigeNachricht("[Grounding] Syntax: /grounding status|wort <Wort>|top");
            }
        }

        private void VerarbeitePhysikSimBefehl(string cmd)
        {
            var ps = agiKern?.GetPhysikSimulator();
            if (ps == null) { ZeigeNachricht("[PhysikSim] Nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[PhysikSim] {ps.GetStatusText()}");
            }
            else if (sub == "wo" && teile.Length > 2)
            {
                string objName = string.Join(" ", teile, 2, teile.Length - 2);
                var spur = ps.WoIstObjekt(objName);
                if (spur != null)
                {
                    string pos = spur.geschaetztePosition != null
                        ? $"[{spur.geschaetztePosition[0]:F1}, {spur.geschaetztePosition[1]:F1}, {spur.geschaetztePosition[2]:F1}]"
                        : "unbekannt";
                    ZeigeNachricht($"[PhysikSim] '{spur.name}': Position≈{pos}, " +
                        $"Sichtbar: {spur.sichtbar}, Konfidenz: {spur.konfidenz:F2}");
                }
                else
                    ZeigeNachricht($"[PhysikSim] Objekt '{objName}' nicht getrackt.");
            }
            else if (sub == "stabilitaet")
            {
                var intuition = ps.GetLetzteIntuition();
                if (intuition?.stabilitaet != null)
                {
                    var stab = intuition.stabilitaet;
                    ZeigeNachricht($"[PhysikSim] Stabilitaet: {stab.stabilitaet:F2} — {stab.beschreibung}");
                    if (stab.risiken.Count > 0)
                        foreach (var r in stab.risiken)
                            ZeigeNachricht($"  ⚠ {r}");
                }
                else
                    ZeigeNachricht("[PhysikSim] Noch keine Stabilitaetsanalyse.");
            }
            else
            {
                ZeigeNachricht("[PhysikSim] Syntax: /physiksim status|wo <Objekt>|stabilitaet");
            }
        }

        private void VerarbeiteSimulationBefehl(string cmd)
        {
            var ms = agiKern?.GetMentaleSimulation();
            if (ms == null) { ZeigeNachricht("[Simulation] Nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[Simulation] {ms.GetStatusText()}");
            }
            else if (sub == "waswenn" && teile.Length > 2)
            {
                string aktionStr = teile[2];
                var zustand = agiKern.GetLetzterZustandsVektor();
                if (zustand == null) { ZeigeNachricht("[Simulation] Kein Zustandsvektor verfuegbar."); return; }

                if (System.Enum.TryParse<AktionsTyp>(aktionStr, true, out var aktionsTyp))
                {
                    var erg = ms.WasPassiertWenn(zustand, aktionsTyp);
                    if (erg != null)
                        ZeigeNachricht($"[Simulation] {erg.beschreibung}");
                    else
                        ZeigeNachricht("[Simulation] Simulation fehlgeschlagen (Weltmodell inaktiv?).");
                }
                else
                    ZeigeNachricht($"[Simulation] Unbekannte Aktion. Verfuegbar: {string.Join(", ", System.Enum.GetNames(typeof(AktionsTyp)))}");
            }
            else if (sub == "beste")
            {
                var zustand = agiKern.GetLetzterZustandsVektor();
                if (zustand == null) { ZeigeNachricht("[Simulation] Kein Zustandsvektor verfuegbar."); return; }

                var pfad = ms.FindeBesteSequenz(zustand);
                if (pfad != null)
                {
                    var aktionen = string.Join(" → ", pfad.aktionen.Select(a => a.ToString()));
                    ZeigeNachricht($"[Simulation] Beste Sequenz: {aktionen}");
                    ZeigeNachricht($"  Erwartete Belohnung: {pfad.kumulativeBelohnung:F2}, Konfidenz: {pfad.konfidenz:F2}");
                }
                else
                    ZeigeNachricht("[Simulation] Keine Sequenz simulierbar.");
            }
            else if (sub == "kontrafaktisch")
            {
                var historie = ms.GetKontrafaktischeHistorie();
                if (historie.Count == 0) { ZeigeNachricht("[Simulation] Noch keine kontrafaktischen Analysen."); return; }
                foreach (var k in historie.TakeLast(5))
                    ZeigeNachricht($"  {k.tatsaechlicheAktion}→{k.tatsaechlicheBelohnung:F2} vs. " +
                        $"{k.alternativeAktion}→{k.kontrafaktischeBelohnung:F2} (Δ={k.differenz:+0.00;-0.00}) — {k.bewertung}");
            }
            else
            {
                ZeigeNachricht("[Simulation] Syntax: /simulation status|waswenn <Aktion>|beste|kontrafaktisch");
            }
        }

        private void VerarbeiteLangzeitPlanBefehl(string cmd)
        {
            var lp = agiKern?.GetLangzeitPlaner();
            if (lp == null) { ZeigeNachricht("[LangzeitPlan] Nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[LangzeitPlan] {lp.GetStatusText()}");
            }
            else if (sub == "meilensteine")
            {
                ZeigeNachricht($"[LangzeitPlan] Meilensteine:\n{lp.GetMeilensteineText()}");
            }
            else if (sub == "historie")
            {
                var historie = lp.GetHistorie();
                if (historie.Count == 0) { ZeigeNachricht("[LangzeitPlan] Keine Plan-Historie."); return; }
                foreach (var p in historie.TakeLast(5))
                    ZeigeNachricht($"  [{p.status}] '{p.beschreibung}' — " +
                        $"{p.meilensteine.Count(m => m.status == MeilensteinStatus.ABGESCHLOSSEN)}/" +
                        $"{p.meilensteine.Count} Meilensteine, {p.umplanungen} Umplanungen");
            }
            else
            {
                ZeigeNachricht("[LangzeitPlan] Syntax: /langzeitplan status|meilensteine|historie");
            }
        }

        private void VerarbeiteCurriculumBefehl(string cmd)
        {
            var sc = agiKern?.GetSelbstCurriculum();
            if (sc == null) { ZeigeNachricht("[Curriculum] Nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[Curriculum] {sc.GetStatusText()}");
            }
            else if (sub == "ziele")
            {
                ZeigeNachricht($"[Curriculum] Lernziele:\n{sc.GetLernZieleText()}");
            }
            else if (sub == "statistik")
            {
                var stats = sc.GetStatistik();
                ZeigeNachricht($"[Curriculum] Statistik:\n" +
                    $"  Lernziele: {stats.lernZieleErstellt} erstellt, {stats.lernZieleAbgeschlossen} abgeschlossen\n" +
                    $"  Uebungen: {stats.uebungenGesamt} gesamt, {stats.uebungenErfolgreich} erfolgreich\n" +
                    $"  ∅ Kompetenz-Zuwachs: {stats.durchschnittlicherKompetenzZuwachs:F3}\n" +
                    $"  Strategie-Wechsel: {stats.strategieWechsel}");
            }
            else
            {
                ZeigeNachricht("[Curriculum] Syntax: /curriculum status|ziele|statistik");
            }
        }

        private void VerarbeiteSpracheBefehl(string cmd)
        {
            var gs = agiKern?.GetGroundedSprache();
            if (gs == null) { ZeigeNachricht("[Sprache] GroundedSprachproduktion nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[Sprache] {gs.GetStatusText()}");
            }
            else if (sub == "erklaere" && teile.Length > 2)
            {
                string wort = string.Join(" ", teile, 2, teile.Length - 2);
                ZeigeNachricht($"[Sprache] {gs.ErklaereWort(wort)}");
            }
            else if (sub == "warum")
            {
                var zustand = agiKern.GetLetzterZustandsVektor();
                ZeigeNachricht($"[Sprache] {gs.ErklaereEntscheidung(zustand)}");
            }
            else
            {
                ZeigeNachricht("[Sprache] Syntax: /sprache status|erklaere <Wort>|warum");
            }
        }

        private void VerarbeitePerfBefehl(string cmd)
        {
            var perf = agiKern?.GetZyklusStabilisator();
            if (perf == null) { ZeigeNachricht("[Perf] ZyklusStabilisator nicht initialisiert."); return; }

            ZeigeNachricht($"[Perf] {perf.GetStatusText()}");
        }

        private void VerarbeiteMissionBefehl(string cmd)
        {
            var mission = agiKern?.GetAutonomieMissionen();
            if (mission == null) { ZeigeNachricht("[Mission] AutonomieMissionen nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[Mission] {mission.GetStatusText()}");
            }
            else if (sub == "an")
            {
                mission.SetAktiviert(true);
                ZeigeNachricht("[Mission] Auto-Missionen aktiviert.");
            }
            else if (sub == "aus")
            {
                mission.SetAktiviert(false);
                ZeigeNachricht("[Mission] Auto-Missionen deaktiviert.");
            }
            else if (sub == "start")
            {
                string beschreibung = teile.Length > 2
                    ? string.Join(" ", teile, 2, teile.Length - 2)
                    : "Selbststaendige Explorationsmission";
                ZeigeNachricht($"[Mission] {mission.StarteMission(beschreibung)}");
            }
            else if (sub == "startauto")
            {
                ZeigeNachricht($"[Mission] {mission.StarteEmpfohleneMission()}");
            }
            else if (sub == "empfehlung")
            {
                ZeigeNachricht($"[Mission] {mission.GetEmpfehlungText()}");
            }
            else if (sub == "stop")
            {
                ZeigeNachricht($"[Mission] {mission.StoppeMission()}");
            }
            else if (sub == "historie")
            {
                var hist = mission.GetHistorie();
                if (hist.Count == 0) { ZeigeNachricht("[Mission] Keine Historie."); return; }
                foreach (var m in hist.TakeLast(5))
                    ZeigeNachricht($"  [{m.status}] {m.beschreibung} | Schritte={m.schritte}, ØR={m.durchschnittBelohnung:F2}");
            }
            else
            {
                ZeigeNachricht("[Mission] Syntax: /mission status|an|aus|start <Text>|startauto|empfehlung|stop|historie");
            }
        }

        private async void VerarbeiteArc2Befehl(string cmd)
        {
            var arc = agiKern?.GetArc2Evaluator();
            if (arc == null) { ZeigeNachricht("[ARC2] Arc2Evaluator nicht initialisiert."); return; }

            var teile = cmd.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string sub = teile.Length > 1 ? teile[1] : "status";

            if (sub == "status")
            {
                ZeigeNachricht($"[ARC2] {arc.GetStatusText()}");
            }
            else if (sub == "run")
            {
                int n = 20;
                if (teile.Length > 2)
                    int.TryParse(teile[2], out n);
                n = Mathf.Clamp(n, 1, 200);

                ZeigeNachricht($"[ARC2] Starte Lauf mit max {n} Tasks...");
                var report = await arc.FuehreAus(n);
                ZeigeNachricht($"[ARC2] Exakt: {report.exaktQuote:P1} ({report.exaktRichtig}/{report.ausgewertet}), " +
                              $"JSON: {report.jsonParseQuote:P1}, Avg: {report.durchschnittMs}ms, LLMCalls: {report.llmCallsGesamt}, " +
                              $"Copy-Baseline: {report.baselineCopyQuote:P1}");
            }
            else if (sub == "report")
            {
                var report = arc.GetLetzterReport();
                if (report == null || report.ausgewertet <= 0)
                {
                    ZeigeNachricht("[ARC2] Noch kein Report vorhanden.");
                    return;
                }

                ZeigeNachricht($"[ARC2] Letzter Report: Exakt {report.exaktQuote:P1} ({report.exaktRichtig}/{report.ausgewertet}), " +
                              $"JSON {report.jsonParseQuote:P1}, Avg {report.durchschnittMs}ms, LLMCalls {report.llmCallsGesamt}");

                foreach (var e in report.ergebnisse.TakeLast(5))
                    ZeigeNachricht($"  {e.taskId}: exakt={e.exaktRichtig}, json={e.jsonParsebar}, {e.dauerMs}ms");
            }
            else
            {
                ZeigeNachricht("[ARC2] Syntax: /arc2 run [N]|status|report");
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

            // Auto-Scroll to bottom
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
