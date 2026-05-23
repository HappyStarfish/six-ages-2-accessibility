# Tastenbelegung — Referenz

Vollständiges Inventar der aktiven Tastenbelegungen der Accessibility-Mod
(Stand 2026-05-22). Grundlage für die zentrale Tastenhinweis-Quelle, die
überarbeitete F1-Hilfe und den Mini-Tutorial-Wortlaut.

## Dispatch-Architektur

`KeyboardNavigationHandler.Update()` prüft zuerst die globalen Hotkeys,
dann übergibt es an den Navigator des aktuellen Bildschirms. Ein Navigator,
der nach `HandleInput` `return`t, besitzt die Eingabe vollständig —
globale Tasten, die NACH der Übergabe stehen, sind dort nicht erreichbar.
Wirklich global sind nur die Tasten, die VOR der Übergabe geprüft werden.
`AnyModifier()` = Strg/Alt/Shift beliebig gedrückt.

## Globale Tasten (vor Navigator-Dispatch)

- **F12** — Debug-Modus umschalten
- **Shift+F11** — letzte Debug-Einträge vorlesen
- **F1** — Spiel-Hilfe-Overlay öffnen (`HelpController`) — universelle Hilfe-Taste
- **Shift+F1** — Mod-Tasten für den aktuellen Bildschirm ansagen (`AnnounceScreenShortcuts`)
- **F4 / Shift+F4 / Ctrl+F4** — Dashboard-Sorgen vor / zurück / einmalig (nur Management)
- **F5** — Inhalt wiederholen — *bleibt als Taste, raus aus Hilfetexten*
- **F6** — Antwortoptionen / Slider-Übersicht wiederholen — *bleibt als Taste, raus aus Hilfetexten*
- **F3 / Shift+F3** — Berater-Rat / Berater-Info
- **F2** — Statuspanel-Kategorien durchschalten
- **F10** — Kampfstatus (nur Kämpfe)
- **S** — Saison-Vorrücken (nur Management, ohne Modifier)
- **I** — Szene-Info umschalten (nur `InteractiveController`-Szenen)
- **P** — Szene-Bild/Text umschalten (nur `InteractiveController`-Szenen)
- **Ctrl+1 … Ctrl+9** — Management-Bildschirm 1–9 wechseln
- **Ctrl+Tab / Ctrl+Shift+Tab** — Management-Bildschirm vor/zurück
- **H** — aktuellen Tutorial-Hinweis vorlesen

## Generische Bildschirme (ohne eigenen Navigator)

Intro, ChooseGame, Battle, Management generisch, Szenen, die vier
ManagementDialogs Fortify/Build/Ritual/Venture.

- **Tab / Shift+Tab** — Schaltflächen vor/zurück
- **Pfeil hoch/runter** — Fokus zwischen Buttons/Toggles/Antworten/Slidern
- **Pfeil links/rechts** — fokussierten Slider verstellen (Shift = 5× Schritt, Strg = Min/Max)
- **L** — Listenmodus betreten
- **Ctrl+R** — Bildschirm erneut ansagen
- **Enter** — fokussiertes Element aktivieren
- **Leertaste** — fokussierten Toggle umschalten
- **Escape** — zurück / Dialog schließen
- Listenmodus: Pfeil hoch/runter, Leertaste wählt Zeile (nur ManagementDialog),
  Enter aktiviert, **D** liest Klan-Beschreibung (erneut D = Absätze), Tab/Escape verlassen
- ManagementDialog (Fortify/Build/Ritual/Venture): Enter = Hauptaktion, Leertaste = Button, Escape = schließen
- Saison-Panel offen: Pfeil hoch/runter/Tab = 3 Slots, Enter = aktivieren, Escape = abbrechen, D = Saison-Details, F5 = Saison-Status
- **ChooseGame** zusätzlich: **Entf** — Spielstand löschen (zweimal binnen 3 s = bestätigen)

## Pro Bildschirm (eigener Navigator — besitzt die Eingabe)

### Relations (Beziehungen)
F = Filter vor, Shift+F = Filter zurück, Tab = Klanliste betreten, dann generische Listenmodus-Tasten.

### Magic (Magie)
Tab/Shift+Tab = Zonen (Filter/Liste/Segen/Buttons), L = Listenzone, H = Hinweis,
Ctrl+R = Volldetails, D = Beschreibung, Escape = Orientierungshinweis.
Filterzone: links/hoch = vorher, rechts/runter = nächster.
Liste/Segen: Pfeil hoch/runter; Segen: Leertaste schaltet Segen.
Buttons: Pfeil hoch/runter, Leertaste/Enter feuert.

### Reorganize (Klan-Ring)
Enter = reorganisieren, Escape = verwerfen, Tab/Shift+Tab = Liste/Ring,
F = Sortierung vor, Shift+F = zurück, L = Listenzone.
Liste: Pfeil hoch/runter, Leertaste = Ring-Mitgliedschaft, **C** = Häuptling, D = Detail-Facetten.
Ring: Pfeil hoch/runter (7 Slots), Leertaste = zum Insassen springen, D = Position erneut lesen.

### ChooseLeader (Anführer wählen)
Enter = wählen, Escape = schließen (nur View-only), F = Filter vor, Shift+F = zurück,
Pfeil hoch/runter = Kandidat, Leertaste = Person auswählen, D = vollständige Bio.

### War (Krieg)
F = Fortify, R = Raid, O = Honor-Raid, C = Cattle-Raid, W = Warriors. (Bricht ab, wenn ein Modifier gedrückt ist.)

### Wealth (Wohlstand)
Enter/C = Caravan-Dialog, Tab/Shift+Tab = Zonen, L = Schatzliste, D = Beschreibung,
Escape = Orientierung, Pfeil hoch/runter = Elemente, Leertaste = aktivieren. (F5 global.)

### Lore
Tab/Shift+Tab = Zonen (Geschichte/Mythen), L = Geschichtsliste, M = Handbuch,
D = Beschreibung, Escape = Orientierung, Pfeil hoch/runter = Einträge, Enter/Leertaste = öffnen. (F5 global.)

### Map (Karte / Foray)
Enter = Mission senden, Escape = abbrechen, Tab/Shift+Tab = Zonen, F5 = Vollstatus,
G = Zielzone, K = Ziel-Liste, X = Foray-Panel.
Zonen: Ziele (Pfeil/Leertaste/D), Slider (Pfeil/links-rechts), Anführer (Leertaste/D),
Ziel-Liste (Pfeil/Leertaste/D, **F** Filter, **O** Sortierung), Hex-Cursor (Pfeile, Shift = 5 Hex, Home, Leertaste, D).
Hinweis: S ist auf der Karte unbrauchbar (globaler S-Handler schluckt sie) — darum O statt S für Sortierung.

### SacredTime (Heilige Zeit)
Enter = fortfahren, G = Saga-Chronik, Tab/Shift+Tab = Forecast/Allocation,
Escape = Orientierung, F5 = Vollstatus.
Forecast: D = Jahres-Kopf, Pfeil hoch/runter = Absätze.
Allocation: Pfeil hoch/runter = Zeilen, links/rechts = anpassen, D = Zeile erneut.

### Saga
Enter = Wiederherstellen, Pfeil hoch/runter = Jahr, D = Volltext. (F5 global.)

### GameOver
Nur bei offenem Saga-Overlay: Enter = Wiederherstellen, Escape = schließen,
Pfeil hoch/runter = Jahr, D = Volltext, F5 = Vollstatus. Sonst generische Tab-Navigation.

### Caravan
Enter = senden, Escape = schließen, Tab/Shift+Tab = 6 Zonen, F5 = Vollstatus,
L = Klanliste, **R** = aktive Handelsrouten. Zonen mit Pfeil/Leertaste/D je nach Zone.

### Emissary
Enter = senden, Escape = schließen, Tab/Shift+Tab = Zonen, F5 = Vollstatus,
L = Klanliste. Listen-/Slider-/Anführer-Zone mit Pfeil/Leertaste/D.

### Raid (Raid + CattleRaid)
Enter = Raid, Escape = schließen, Tab/Shift+Tab = Zonen, F5 = Vollstatus,
L = Raid-Liste. Helfer-Zone: F = Filter (Shift+F zurück).

### Sacrifice / Spirit / Warriors
Enter = Hauptaktion, Escape = schließen, Tab/Shift+Tab = Zonen, F5 = Vollstatus.
Zonen mit Pfeil/Leertaste/D bzw. Slidern.

### Tutorial-Vollbild
Nur Pfeil hoch/runter = Tutorial-Absätze. Enter (Weiter) läuft über generische Navigation.

### Tutorial-Hinweis-Karte
Kein eigener Input — nur globales **H**.

### Spiel-Hilfe-Overlay (`HelpController`, mit F1 geöffnet)
Pfeil hoch/runter = Hilfe-Abschnitte durchblättern (Bildschirmname, Beschreibung,
Hinweis links/rechts, Tastenliste). Escape = schließen, F5 = erneut vorlesen.
Nur die Pfeiltasten werden vom `HelpScreenNavigator` verbraucht.

### Lore-Leser (Myth / Info / Manual-Dialog)
F5 = Vollstatus, Escape = schließen, Pfeil hoch/runter = Absatz (Strg = Überschrift),
Home/End, Tab/Shift+Tab = Links, Enter = Link folgen, M = ganzes Dokument, L = Links auflisten, D = Detail/Kompakt.

## F5 / F6 / L — Sonderbehandlung

**Bleiben als funktionale Tasten** (Entscheidung 2026-05-22), werden nur aus
allen Hilfetexten entfernt.

- **F5** gebunden: global `KeyboardNavigationHandler.cs:483` (+ Saison-Panel `:3196`);
  zusätzlich redundant in Sacrifice/Spirit/Emissary/Caravan/GameOver/SacredTime/Warriors/Raid/LoreReader-Navigatoren.
- **F6** gebunden: nur global `KeyboardNavigationHandler.cs:568`.
- **L** gebunden: generisch `KeyboardNavigationHandler.cs:2013` (Listenmodus);
  je Navigator als Sprung in eine Zone: Magic `:60`, Reorganize `:119`, Emissary `:80`,
  Caravan `:102`, Raid `:92`, Wealth `:66`, Lore `:49`; LoreReader `:231` (Links auflisten).

### F5/F6/L in Hilfetexten — Streichliste

- F5: `KeyboardNavigationHandler.cs:3689,3701,3726`, `HelpScreenReader.cs:78`,
  `LoreScreenNavigator.cs:287`, `ManagementScreenReader.cs:239,556,622`,
  `MapScreenNavigator.cs:132`, `WealthScreenNavigator.cs:336`, `DialogContentReader.cs:249`,
  + DE-Entsprechungen in `LocSetup.cs`.
- F6: `KeyboardNavigationHandler.cs:3701,3726` (nur diese zwei; nicht über `Loc.Get` → siehe Probleme).
- L: `KeyboardNavigationHandler.cs:3691,3703,3728,3876`, `ManagementScreenReader.cs:476`,
  `CombatScreenReader.cs:231`, `DialogContentReader.cs:286,355,397,732,819,870`,
  `RaidNavigator.cs:544`, `LoreReader.cs:374` + DE-Entsprechungen in `LocSetup.cs`.

## Tasten-Strings im Code — Zentralisierungs-Ziele

Tasten werden derzeit in vielen Dateien als String-Literale beschrieben.
Die zentrale Tastenhinweis-Quelle muss diese ablösen. Hauptfundorte:

- `KeyboardNavigationHandler.cs` — `AnnounceScreenShortcuts` (`:3669–3756`, F1-Hilfe) sowie viele Einzel-Hinweise.
- `DialogContentReader.cs` — Hilfetexte je Dialogtyp (`:186–870`).
- `ManagementScreenReader.cs` — Hilfetexte je Management-Bildschirm (`:215–622`).
- Einzelne Navigatoren — Orientierungs-/Disabled-Hinweise (Reorganize, Map, SacredTime, Warriors, Raid, Lore, Wealth …).
- `LocSetup.cs` — die deutschen Übersetzungen all dieser Strings (muss mitgezogen werden).

## Bekannte Probleme (Hilfetexte, die lügen)

1. **Phantom-Taste Y/N.** `ScenePatches.cs:420` sagt „Yes or No. Press Y or N.",
   aber es gibt KEINE `KeyCode.Y`/`KeyCode.N`-Bindung. Ja/Nein-Szenen laufen über
   die Antwort-Buttons (Tab/Pfeile/Enter). Text muss korrigiert werden.
2. **F6 nicht lokalisiert.** Die beiden F6-Erwähnungen werden mit rohem englischem
   `sb.Append` gebaut, nicht über `Loc.Get` — im DE-Build also immer englisch. Die
   F1-Hilfe-Blöcke (`:3676–3755`) sind generell überwiegend nicht über `Loc.Get`.
3. **„L" ist nicht eine Taste.** Generisch = Listenmodus betreten; in Zonen-Navigatoren
   = Sprung in eine bestimmte Zone; im Lore-Leser = Links auflisten. Ein pauschaler
   Hilfesatz „L betritt Listenmodus" stimmt nur auf generischen Bildschirmen.
4. **Buchstaben-Hotkeys kollidieren bewusst, sind aber bildschirm-gated.** F, C, R, O, D
   bedeuten je nach Bildschirm Verschiedenes — die kontextabhängige F1-Hilfe muss das spiegeln.
