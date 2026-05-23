# Mini-Tutorial — Spezifikation

Subtiles Steuerungs-Tutorial für Screenreader-Spieler, angedockt an das
vorhandene Spiel-Tutorial (`TutorialTips-DarkAge.lua`).

Status: Planung. Wortlaut wird Stufe für Stufe nach Abnahme durch die
Userin in Abschnitt 8 eingetragen.

## 1. Ziel

Ein neuer blinder Spieler lernt die Mod-Steuerung beiläufig, während das
Spiel seine eigenen Tutorial-Karten zeigt. Zwei Aufgaben:

- **Übersetzen** — die Maus-/Touch-Formulierungen der Spiel-Tutorialkarten
  ("click", "tap", "slide", "drag") in Tastatur-Äquivalente überführen.
  Aktuell liest die Mod diese Karten wörtlich vor, sagt blinden Spielern
  also, sie sollen Dinge anklicken.
- **Ergänzen** — Mod-Konzepte, die das Spiel gar nicht kennt
  (Tab-Navigation, D zum Vorlesen, F1).

## 2. Architektur-Entscheidungen (festgelegt)

- **Erststart:** einmalige, klare Willkommensansage. Nicht subtil
  angedockt, damit ein Spieler ohne Doku nicht in der Henne-Ei-Falle sitzt.
- **Danach:** pro Kontext genau ein Hinweis, beiläufig ans Ende der
  Header-/Tutorial-Ansage gehängt — Kerninhalt zuerst, Hinweis zuletzt
  (siehe `feedback_review_keys_interrupt`, `feedback_no_repeated_hints`).
- **Reset:** über den vorhandenen "Reset Tutorial"-Knopf in
  `ControlsOverlay`. Ein Harmony-Postfix auf `ControlsOverlay.ResetTutorial`
  leert zusätzlich die Onboarding-Datei der Mod. Ein Knopf, beide Resets.
- **Nur Interaktions-Karten** bekommen einen Hinweis; reine Lore-/
  Strategie-Karten nicht (die liest die Mod schon korrekt vor).
- **F5/F6 sind NICHT Teil des Lehrplans** — Alt-Tasten aus der frühen
  Entwicklung.

## 3. Hook-Punkte (bereits vorhanden, getestet)

- **Vollbild-Tutorial:** Postfix auf `TutorialController.UpdateToCurrentTopic`
  → `MenuPatches.FlushPendingTutorialAnnounce`. Hängt heute schon eine
  Steuerungszeile an.
- **Hinweis-Karten:** Postfix auf `TutorialView.Init`
  → `MenuPatches.TutorialCard_Shown`.
- **Reset:** Postfix auf `ControlsOverlay.ResetTutorial` (neu anzulegen).

## 4. Kontext-Schlüssel

- **Karten-ID:** `TutorialCard.name` (= `PluginImport.TutorialView_Name()`).
  Jede Lua-Karte hat ein eindeutiges `id`-Feld ("b1", "intro3", "s1" …).
  VERIFIKATION nötig: ob `TutorialView_Name()` die `id` oder den
  Trigger-`name` liefert (Abschnitt 7).
- **Fallback:** `Tutorial.topicScreen` (GameScreen-Enum) für Vollbild-Themen,
  `TutorialCard.callout` zur Disambiguierung von Hinweis-Karten.

## 5. Persistenz

- Textdatei im Plugin-Ordner, eine gesehene Hinweis-ID pro Zeile
  (Mono-2.0-sicher, kein JSON).
- Reset = Datei leeren.
- **Konzept-Ebene:** Mehrere Lua-Karten-IDs zeigen auf denselben
  Lehrplan-Hinweis. Gespeichert wird der Lehrplan-Hinweis, nicht die
  Karte — so fällt z. B. der Saison-Hinweis genau einmal, egal ob
  `intro5` oder `u3` zuerst kommt.

## 6. Karten-Zuordnung

Quelle: `Content\TutorialTips-DarkAge.lua` im Spielverzeichnis.
Auf Windows greift jeweils die `textComputer`-Variante.

- **Stufe 0 — Begrüßung.** Anker: `b1` (erste Hauptmenü-Karte) bzw. erster
  Hauptmenü-Aufruf. Mod-eigene Willkommensansage davor.
- **Stufe 1 — Hinweis-Karten bedienen.** `b1` (Teil), `b5`, `i5`, `s2`,
  `g7` — alle erklären Maus-Box-Mechanik → ersetzt durch "H wiederholt".
- **Stufe 2 — Entscheidungen.** `q1`, `q2`, `i10`, `i10continued`, `i12`.
  Pfeiltasten bewegen, **Enter wählt aus — keine Leertaste** bei
  Szene-Antworten.
- **Stufe 3 — Anführer & Klan-Ring.** `u1` (ChooseLeader), `rd1`
  (Reorganize), `rd2`, `rd3`.
- **Stufe 4 — Bildschirmwechsel.** `intro7`, `u4`.
- **Stufe 5 — Hilfe.** `u5` ("click the ? button") → "Hilfe mit F1".
- **Stufe 6 — Verwaltungs-Bildschirme, je Erstbesuch.** `intro3` Klan ·
  `m4` Magie · `ma1` Karte · `re1` Beziehungen · `wa1` Krieg · `we1`
  Wohlstand · `lo1` Lore · `sa1` Saga · `st1` Heilige Zeit.
- **Stufe 7 — Detail-Interaktionen.** Saison: `intro5` + `u3` · Aktion
  bestätigen: `u7` · Berater: `s1` · Magie-Rune: `intro6` + `u6` ·
  Spielstände: `b2` · Quester-Fähigkeiten: `hq5`.

Notierte Funde:

- `vo1` — Karte "VoiceOver": reservierter Screenreader-Slot des Spiels.
  Feuert auf Windows mangels VoiceOver vermutlich nicht; `Tutorial.ShowNote`
  ist aber von der Mod aufrufbar (Option für später).
- `cs1` — Karte "TutorialReset": feuert beim Reset-Knopf. Bestätigt den
  Reset-Pfad.

## 7. Identifier-Strategie (Update Session 46, 2026-05-23)

**Ursprüngliche Annahme (Session 41/44):** `PluginImport.TutorialView_Name()`
liefert die Lua-`id`. Verifikation auf den Playtest verschoben.

**Was Session 46 im Spiel-Log gefunden hat:** Native gibt das optionale
`name`-Trigger-Feld zurück, nicht die `id`. Im Lua ist `name` nur bei einer
Handvoll Karten gesetzt (z. B. `"TutorialBottom"`, `"Replayable"`,
`"intro_1GoldenEmpire"`); bei allen anderen ist es `null`. Es gibt **keine**
`Tutorial_TopicId`-Native — die Lua-`id` ist aus dem nativen Plugin nicht
erreichbar.

**Neue Strategie (umgesetzt Session 46):** Karten werden per Substring-
Fingerprint im `TutorialCard.currentText` identifiziert. Pro Curriculum-Karte
liegen zwei Fingerprints in der Map — einer aus dem englischen Lua-Text, einer
aus dem deutschen Korpus-Eintrag — weil der DE-Translator den `currentText`
postfixt und je nach Sprache eine der beiden Sprachen sichtbar ist. Matching
über `IndexOf(prefix, Ordinal) >= 0` (Contains, nicht StartsWith), damit
Apostroph-Encoding-Unterschiede zwischen Lua-Quelltext und Runtime keinen
Match abreißen.

Die Fingerprints werden einmalig aus `Content\TutorialTips-DarkAge.lua` und
`tools\extract-corpus\corpus_de.json` mit `tools/extract-corpus/dump_curriculum_cards.py`
extrahiert. Erweiterung um eine neue Karte = Eintrag in der `BuildCurriculum`-
Methode mit zwei `Add()`-Zeilen (EN + DE).

Memory-Eintrag: `project_tutorial_id_unreachable` dokumentiert die Falle für
zukünftige Sessions.

## 8. Wortlaut

DE + EN, alle Strings über `Loc.Get()`. Stufe 0–7 abgenommen (2026-05-22).

**Stufe 0 — Willkommensansage (Erststart, einmalig)**

- DE: „Willkommen bei Six Ages 2. Diese Mod macht das Spiel mit Tastatur
  und Screenreader spielbar. Mit den Pfeiltasten bewegst du dich durch die
  Anzeige, mit Enter aktivierst du etwas. Umschalt F1 nennt dir jederzeit
  die Tasten für den Bildschirm, auf dem du gerade bist. Diese Hinweise
  erscheinen nur beim ersten Mal."
- EN: „Welcome to Six Ages 2. This mod makes the game playable with a
  keyboard and screen reader. Use the arrow keys to move through the
  display, and Enter to activate. Shift F1 always tells you the keys for
  the screen you are on. These hints appear only once."

**Stufe 1 — Hinweis-Karten (erste Karte)**

- DE: „Das Spiel zeigt manchmal Hinweis-Karten. Die Mod liest sie
  automatisch vor. Mit der Taste H hörst du den aktuellen Hinweis noch
  einmal."
- EN: „The game sometimes shows hint cards. The mod reads them aloud
  automatically. Press H to hear the current hint again."

**Stufe 2 — Entscheidungen (erste Szene-Antwortauswahl)**

- DE: „In einer Entscheidung gehst du die Antwortmöglichkeiten mit den
  Pfeiltasten durch. Enter wählt die aktuelle Antwort aus."
- EN: „In a decision, move through the response options with the arrow
  keys. Enter selects the current response."

**Stufe 3 — Anführer & Klan-Ring** (zwei Hinweise — zwei Bildschirme)

_3a Anführer-Wahl (Karte `u1`):_

- DE: „Eine Person für eine Aufgabe wählst du so: mit den Pfeiltasten durch
  die Kandidaten gehen, Leertaste wählt die Person aus, Enter bestätigt.
  D liest die Lebensbeschreibung."
- EN: „To choose a person for a task, move through the candidates with the
  arrow keys. Space selects the person, Enter confirms. D reads their life
  story."

_3b Klan-Ring (Karten `rd1`, `rd2`, `rd3`):_

- DE: „Im Klan-Ring wechselst du mit Tab zwischen Personenliste und Ring.
  In der Liste setzt die Leertaste eine Person in den Ring oder nimmt sie
  heraus, C macht sie zum Häuptling. Enter wendet die neue Ordnung an."
- EN: „In the clan ring, Tab switches between the list of people and the
  ring. In the list, Space puts a person into the ring or removes them,
  C makes them chieftain. Enter applies the new arrangement."

**Stufe 4 — Bildschirmwechsel** (Karten `intro7`, `u4`)

- DE: „Zwischen den Verwaltungs-Bildschirmen wechselst du mit Strg und einer
  Zahl von 1 bis 9. Strg mit Tab wechselt der Reihe nach."
- EN: „Switch between the management screens with Ctrl and a number from
  1 to 9. Ctrl with Tab moves through them in order."

**Stufe 5 — Hilfe** (Karte `u5`)

- DE: „Die Hilfe zu einem Bildschirm öffnest du mit F1 und gehst sie mit
  den Pfeiltasten Abschnitt für Abschnitt durch. Shift F1 nennt dir die
  Tasten der Mod."
- EN: „Open a screen's help with F1 and move through it section by section
  with the arrow keys. Shift F1 lists the mod's keys."

**Stufe 6 — Verwaltungs-Bildschirme** (Karten `intro3`, `m4`, `ma1`, `re1`,
`wa1`, `we1`, `lo1`, `sa1`, `st1` — EIN Hinweis, beim ersten
Verwaltungs-Bildschirm; Entscheidung 2026-05-22, nicht neun)

- DE: „Verwaltungs-Bildschirme zeigen Werte, Listen und Knöpfe. Mit Tab
  wechselst du zwischen den Bereichen. Welche Tasten der Bildschirm hat,
  nennt dir Shift F1."
- EN: „Management screens show figures, lists and buttons. Tab switches
  between the areas. Shift F1 lists the keys this screen has."

**Stufe 7 — Detail-Interaktionen**

_7a Saison (Karten `intro5`, `u3`, `u7`):_

- DE: „Die Zeit rückst du mit der Taste S vor — das ist der ‚Saison'-Knopf
  des Spiels. Eine Aktion kostet eine halbe Saison."
- EN: „Advance time with the S key — that is the game's Season button. An
  action costs half a season."

_7b Berater (Karte `s1`):_

- DE: „Den Rat deiner Berater zu einer Szene hörst du mit F3. Jeder Berater
  liest dir auch die Antworten vor, die er für sinnvoll hält — das können
  keine, eine, mehrere oder alle sein, je nach Berater und Lage. Shift F3
  nennt dir Infos zum Berater."
- EN: „Press F3 to hear your advisors' take on a scene. Each advisor also
  reads out the responses they consider sensible — that can be none, one,
  several or all of them, depending on the advisor and the situation. Shift
  F3 gives you information about the advisor."
- Erweitert Session 47 (2026-05-23, Userin-Entscheidung): die Zahl der vom
  Berater empfohlenen Antworten (Bitmaske aus
  `PluginImport.Script_SuggestionsForAdvisor`) ist nicht immer ≥ 1 — der
  Hinweis räumt das ausdrücklich ein, damit ein Berater ohne Vorschlag nicht
  als Fehler missverstanden wird.

_7c Magie-Anzeige (Karten `intro6`, `u6`):_

- DE: „Die Magie deines Klans hörst du auf dem Magie-Bildschirm. Mit F4
  gehst du außerdem die Belange und die aktive Magie durch."
- EN: „You hear your clan's magic on the Magic screen. F4 also steps
  through the concerns and the active magic."

_7d Spielstände (Karte `b2`):_

- DE: „Einen Spielstand löschst du in der Spielstand-Liste mit der
  Entfernen-Taste — zweimal binnen drei Sekunden bestätigt das Löschen."
- EN: „Delete a save in the saved-games list with the Delete key —
  pressing it twice within three seconds confirms."

_7e Quester-Fähigkeiten (Karte `hq5`):_

- DE: „In einer Heldenqueste steuerst du die questende Person mit den
  Pfeiltasten an und hörst mit D ihre Fähigkeiten."
- EN: „In a hero quest, focus the questing person with the arrow keys and
  press D to hear their skills."
- ⚠ NICHT VERDRAHTET (Session 44, Userin-Entscheidung 2026-05-22): `hq5` ist
  bewusst aus der Kurrikulum-Map ausgelassen, bis im Spiel geprüft ist, ob die
  Quester-Person ein pfeil-ansteuerbares, mit D lesbares Element ist — sonst
  lügt die Ansage (`feedback_keyhints_live_in_other_files`). Der Wortlaut oben
  bleibt für das spätere Nachrüsten stehen. Nachverdrahten siehe Abschnitt 10,
  Punkt 7.

## 9. Abhängigkeit: zentrale Tastenhinweis-Quelle

Mini-Tutorial, F1-Hilfe (`AnnounceScreenShortcuts`) und die Auto-Ansagen
müssen ihre Tastentexte aus EINER Quelle ziehen (`Hints`-Klasse oder feste
`Loc`-Keys), sonst driften sie auseinander (`feedback_keyhints_live_in_other_files`).

**Erledigt (Session 42):** `src/SixAgesAccessibility/Hints.cs` angelegt — 17
Atom-Properties, jedes ein kurzer lokalisierter Satz über `Loc.Get()` (Stil
„kurze Sätze", Userin-Entscheidung). Zentralisiert nur die Mehrfach-/Global-
Tasten; bildschirm-einzigartige Hotkeys bleiben Literale. F5/F6/L bewusst nicht
enthalten. Die Tastenhilfe (`AnnounceScreenShortcuts`, jetzt auf Shift+F1)
komponiert aus diesen Atomen.

F1-Hilfe-Sidequest (Entscheidung 2026-05-22):

- `AnnounceScreenShortcuts` wird echt kontextabhängig: pro Bildschirm nur
  die dort relevanten Tasten, Wichtiges zuerst, kurz.
- **F5/F6/L raus aus ALLEN Hilfetexten** (F1-Hilfe und sonstige
  Tasten-Ansagen). Die Tasten bleiben funktional im Code — sie werden nur
  nicht mehr beworben.
- Fehlende Tasten ergänzen: Tastenmodell Leertaste/Enter/D, H für Hinweis.

## 10. Nächste Schritte

1. ~~Wortlaut Stufe 0–7 abnehmen~~ (erledigt, 2026-05-22 — Abschnitt 8).
2. ~~`TutorialView_Name()` verifizieren~~ — in den Playtest gefaltet (Session 44).
   `OnboardingHintHandler.GetCardHint` loggt bei F12 jede Karten-`id`; die
   Kurrikulum-Map ist auf `id` verdrahtet (Begründung: der `id` ist laut Lua der
   einzige eindeutige Karten-Identifier, `name` ist nicht eindeutig — kommt z. B.
   doppelt als „CloseCombat" vor). Der erste Playtest bestätigt es.
3. ~~Zentrale Tastenhinweis-Quelle anlegen~~ (erledigt, Session 42 — `Hints.cs`).
4. ~~F1-Hilfe (`AnnounceScreenShortcuts`) auf die `Hints`-Atome umstellen~~
   (erledigt, Session 42 — 24 Kontexte, F5/F6/L raus, lokalisiert).
5. ~~`OnboardingHintHandler` + Persistenz bauen, an die Hooks verdrahten~~
   (erledigt, Session 44 — `OnboardingHintHandler.cs`, `MenuPatches`
   `TutorialCard_Shown` + `MainMenu_Activated`, Persistenz `onboarding-progress.txt`).
6. ~~`ControlsOverlay.ResetTutorial`-Postfix ergänzen~~ (erledigt, Session 44 —
   `MenuPatches.ControlsOverlay_ResetTutorial`, mit kurzer Reset-Bestätigung).
7. **Offen — Stufe 7e (`hq5`/Quester):** bewusst NICHT verdrahtet. Vor dem
   Nachrüsten im Spiel prüfen, ob der Heldenqueste-Bildschirm die questende
   Person als pfeil-ansteuerbares, mit D lesbares Element anbietet (im Code liegt
   sie in der `AdvisorsView`, eher F3-Bereich). Nachverdrahten = Eintrag
   `Map(map, "<konzept>", "hq5")` + Hinweistext in `OnboardingHintHandler` und
   `LocSetup`.
