namespace SixAgesAccessibility
{
    /// <summary>
    /// Registers German values into <see cref="Loc"/>. Called from <c>Plugin.Awake</c>
    /// <b>only when the German translation mod is detected</b> — if it is absent, this
    /// method never runs and every <c>Loc.Get</c> call falls back to its English key.
    /// That is the whole language switch: English by default, German when the
    /// translation mod is installed.
    ///
    /// Key convention: the English source string IS the key (matches existing
    /// callers like <c>TutorialHintHandler</c>). Adding a new German string
    /// here makes any caller that already passes that English literal through
    /// <c>Loc.Get</c> immediately speak the German value.
    /// </summary>
    internal static class LocSetup
    {
        public static void RegisterGerman()
        {
            // Intro extra-controls composite (ScenePatches.GetIntroExtraControlsHint)
            Loc.Register("a clan name field", "ein Eingabefeld für den Klan-Namen");
            Loc.Register("restore mode toggles None, One, Unlimited",
                "Wiederherstellungs-Schalter Keine, Eins, Unbegrenzt");
            Loc.Register("a Start button", "ein Start-Knopf");
            Loc.Register("Also on this screen: ", "Auch auf diesem Bildschirm: ");
            Loc.Register(". Use Tab to reach them; press Enter on Start to begin. ",
                ". Mit Tab erreicht ihr sie; drückt Eingabe auf Start zum Beginnen. ");

            // Tutorial hint handler
            Loc.Register("No hint on this screen.", "Kein Hinweis auf diesem Bildschirm.");
            Loc.Register("Hint: ", "Hinweis: ");
            Loc.Register("Page {0} of {1}.", "Seite {0} von {1}.");

            // Full-screen tutorial: Up/Down paragraph review (TutorialScreenNavigator)
            Loc.Register("No tutorial text.", "Kein Tutorialtext.");
            Loc.Register("Beginning of tutorial.", "Anfang des Tutorials.");
            Loc.Register("End of tutorial. Press Enter to continue.",
                "Ende des Tutorials. Weiter mit Eingabe.");
            Loc.Register("Use Up and Down to re-read the text. Press Enter to continue.",
                "Mit Pfeil hoch und runter den Text erneut lesen. Eingabe zum Fortfahren.");

            // Mini-tutorial (OnboardingHintHandler) — welcome + per-concept hints +
            // reset confirmation. Wording approved 2026-05-22, docs/mini-tutorial-spec.md
            // section 8. Keys must match the English strings in OnboardingHintHandler.cs
            // and MenuPatches.ControlsOverlay_ResetTutorial exactly.
            Loc.Register("Welcome to Six Ages 2. This mod makes the game playable "
                + "with a keyboard and screen reader. Use the arrow keys to move through "
                + "the display, and Enter to activate. Shift F1 always tells you the keys "
                + "for the screen you are on. These hints appear only once.",
                "Willkommen bei Six Ages 2. Diese Mod macht das Spiel mit Tastatur und "
                + "Screenreader spielbar. Mit den Pfeiltasten bewegst du dich durch die "
                + "Anzeige, mit Enter aktivierst du etwas. Umschalt F1 nennt dir jederzeit "
                + "die Tasten für den Bildschirm, auf dem du gerade bist. Diese Hinweise "
                + "erscheinen nur beim ersten Mal.");
            Loc.Register("The game sometimes shows hint cards. The mod reads "
                + "them aloud automatically. Press H to hear the current hint again.",
                "Das Spiel zeigt manchmal Hinweis-Karten. Die Mod liest sie automatisch "
                + "vor. Mit der Taste H hörst du den aktuellen Hinweis noch einmal.");
            Loc.Register("In a decision, move through the response options with "
                + "the arrow keys. Enter selects the current response.",
                "In einer Entscheidung gehst du die Antwortmöglichkeiten mit den "
                + "Pfeiltasten durch. Enter wählt die aktuelle Antwort aus.");
            Loc.Register("To choose a person for a task, move through the candidates "
                + "with the arrow keys. Space selects the person, Enter confirms. D reads "
                + "their life story.",
                "Eine Person für eine Aufgabe wählst du so: mit den Pfeiltasten durch die "
                + "Kandidaten gehen, Leertaste wählt die Person aus, Enter bestätigt. "
                + "D liest die Lebensbeschreibung.");
            Loc.Register("In the clan ring, Tab switches between the list of "
                + "people and the ring. In the list, Space puts a person into the ring or "
                + "removes them, C makes them chieftain. Enter applies the new arrangement.",
                "Im Klan-Ring wechselst du mit Tab zwischen Personenliste und Ring. In der "
                + "Liste setzt die Leertaste eine Person in den Ring oder nimmt sie heraus, "
                + "C macht sie zum Häuptling. Enter wendet die neue Ordnung an.");
            Loc.Register("Switch between the management screens with Ctrl and "
                + "a number from 1 to 9. Ctrl with Tab moves through them in order.",
                "Zwischen den Verwaltungs-Bildschirmen wechselst du mit Strg und einer "
                + "Zahl von 1 bis 9. Strg mit Tab wechselt der Reihe nach.");
            Loc.Register("Open a screen's help with F1 and move through it section by "
                + "section with the arrow keys. Shift F1 lists the mod's keys.",
                "Die Hilfe zu einem Bildschirm öffnest du mit F1 und gehst sie mit den "
                + "Pfeiltasten Abschnitt für Abschnitt durch. Shift F1 nennt dir die "
                + "Tasten der Mod.");
            Loc.Register("Management screens show figures, lists and buttons. "
                + "Tab switches between the areas. Shift F1 lists the keys this screen has.",
                "Verwaltungs-Bildschirme zeigen Werte, Listen und Knöpfe. Mit Tab "
                + "wechselst du zwischen den Bereichen. Welche Tasten der Bildschirm hat, "
                + "nennt dir Shift F1.");
            Loc.Register("Advance time with the S key — that is the game's Season "
                + "button. An action costs half a season.",
                "Die Zeit rückst du mit der Taste S vor — das ist der ‚Saison'-Knopf des "
                + "Spiels. Eine Aktion kostet eine halbe Saison.");
            Loc.Register("Press F3 to hear your advisors' take on a scene. Each "
                + "advisor also reads out the responses they consider sensible — that "
                + "can be none, one, several or all of them, depending on the advisor "
                + "and the situation. Shift F3 gives you information about the advisor.",
                "Den Rat deiner Berater zu einer Szene hörst du mit F3. Jeder Berater "
                + "liest dir auch die Antworten vor, die er für sinnvoll hält — das "
                + "können keine, eine, mehrere oder alle sein, je nach Berater und "
                + "Lage. Shift F3 nennt dir Infos zum Berater.");
            Loc.Register("You hear your clan's magic on the Magic screen. F4 also "
                + "steps through the concerns and the active magic.",
                "Die Magie deines Klans hörst du auf dem Magie-Bildschirm. Mit F4 gehst "
                + "du außerdem die Belange und die aktive Magie durch.");
            Loc.Register("Delete a save in the saved-games list with the Delete key — "
                + "pressing it twice within three seconds confirms.",
                "Einen Spielstand löschst du in der Spielstand-Liste mit der "
                + "Entfernen-Taste — zweimal binnen drei Sekunden bestätigt das Löschen.");
            Loc.Register("Tutorial reset. The mod's hints will appear again.",
                "Tutorial zurückgesetzt. Die Hinweise der Mod erscheinen wieder.");

            // Static Unity button labels (do not flow through PluginImport, so the
            // German translation mod can't reach them). Looked up at announcement
            // time via Loc.Get(label) — see KeyboardNavigationHandler.AnnounceFocusedButton
            // and ScenePatches button-announce sites.
            Loc.Register("PLAY", "SPIELEN");
            Loc.Register("RESUME", "FORTSETZEN");
            Loc.Register("NEW GAME", "NEUES SPIEL");
            Loc.Register("TUTORIAL", "TUTORIAL");
            Loc.Register("CONTROLS", "EINSTELLUNGEN");
            Loc.Register("START", "START");
            Loc.Register("SKIP", "ÜBERSPRINGEN");
            Loc.Register("BACK", "ZURÜCK");
            Loc.Register("QUIT", "BEENDEN");
            Loc.Register("OPTIONS", "OPTIONEN");
            Loc.Register("Proceed", "Weiter");
            Loc.Register("Done", "Fertig");
            Loc.Register("OK", "OK");
            Loc.Register("Cancel", "Abbrechen");
            Loc.Register("Close", "Schließen");

            // " button, Enter." composite — appended after the focused button label
            // by ScenePatches.SceneFinal_Postfix and AnnounceCurrentResponses.
            Loc.Register(" button, Enter.", " Knopf, Eingabe.");
            Loc.Register(" button, Enter. ", " Knopf, Eingabe. ");

            // Clan name input prompt
            Loc.Register("empty", "leer");
            Loc.Register("on", "ein");
            Loc.Register("off", "aus");

            // Toggle labels (intro restore-mode group)
            Loc.Register("None", "Keine");
            Loc.Register("One", "Eins");
            Loc.Register("Unlimited", "Unbegrenzt");

            // Screen / menu headers and composites (MenuPatches)
            Loc.Register("Main Menu.", "Hauptmenü.");
            Loc.Register("Choose Game. ", "Spiel wählen. ");
            Loc.Register(" playable options: ", " spielbare Optionen: ");
            // ChooseGame screen section labels — used by MenuPatches.ChooseGame_Shown.
            Loc.Register("Chapter ", "Kapitel ");
            Loc.Register(" (not available)", " (nicht verfügbar)");
            // Compact summary — "Chapter X: N entries, M not available."
            Loc.Register("entry", "Eintrag");
            Loc.Register("entries", "Einträge");
            Loc.Register(" not available", " nicht verfügbar");
            Loc.Register("not available", "nicht verfügbar");

            // ManagementScreenReader — clan summary composite
            Loc.Register(". Population: ", ". Bevölkerung: ");
            Loc.Register(" healthy", " gesund");
            Loc.Register(" sick", " krank");
            Loc.Register(" wounded", " verwundet");
            Loc.Register(". Mood: ", ". Stimmung: ");
            Loc.Register("Commoners: ", "Gemeine: ");
            Loc.Register("Children: ", "Kinder: ");
            Loc.Register("Warriors: ", "Krieger: ");
            Loc.Register("Nobles: ", "Adelige: ");
            Loc.Register("Total: ", "Gesamt: ");
            Loc.Register("Mood: ", "Stimmung: ");
            Loc.Register("Venture: ", "Unternehmung: ");
            // AppendIfNonZero suffixes (the literal descriptor argument)
            Loc.Register("sick", "krank");
            Loc.Register("wounded", "verwundet");

            // SacredTimeNavigator — Forecast + Magic allocation
            Loc.Register("Forecast zone. Up and Down read paragraphs.",
                "Vorhersage-Bereich. Hoch und Runter lesen Absätze.");
            Loc.Register("Magic allocation zone. ", "Magie-Zuteilungs-Bereich. ");
            Loc.Register(" Right and Left to adjust, Up and Down to switch lines.",
                " Rechts und Links zum Anpassen, Hoch und Runter wechseln Zeilen.");
            Loc.Register("Year of ", "Jahr ");
            Loc.Register("the unknown", "des Unbekannten");
            Loc.Register(". Magic reserve ", ". Magie-Reserve ");
            Loc.Register(" remaining.", " verbleibend.");
            Loc.Register("No allocation lines available.", "Keine Zuteilungs-Zeilen verfügbar.");
            Loc.Register("No line.", "Keine Zeile.");
            Loc.Register(" unavailable.", " nicht verfügbar.");
            Loc.Register(" of ", " von ");
            Loc.Register("Reserve ", "Reserve ");
            Loc.Register("Magic reserve empty. Reduce another allocation first.",
                "Magie-Reserve leer. Reduziert zuerst eine andere Zuteilung.");
            Loc.Register("Sacred Time. No data available.", "Heilige Zeit. Keine Daten verfügbar.");
            Loc.Register("Sacred Time, year of ", "Heilige Zeit, Jahr ");
            Loc.Register("Magic reserve ", "Magie-Reserve ");
            Loc.Register("Up and Down read forecast paragraphs. Tab switches to Magic allocation. G for Saga, Enter to continue.",
                "Hoch und Runter lesen Vorhersage-Absätze. Tab wechselt zur Magie-Zuteilung. G für Sage, Eingabe zum Fortfahren.");
            Loc.Register("Sacred Time has no back. Enter to continue, G for Saga, Tab to switch between Forecast and Allocation.",
                "Heilige Zeit hat kein Zurück. Eingabe zum Fortfahren, G für Sage, Tab wechselt zwischen Vorhersage und Zuteilung.");
            Loc.Register("Sacred Time. Year of ", "Heilige Zeit. Jahr ");
            Loc.Register("Magic ", "Magie ");
            Loc.Register(" used of ", " genutzt von ");
            Loc.Register("Forecast: ", "Vorhersage: ");
            Loc.Register(" paragraphs. ", " Absätze. ");
            Loc.Register(" Up and Down for more.", " Hoch und Runter für mehr.");

            // Magic allocation labels (used both in SacredTime and Magic screens)
            Loc.Register("Fields", "Felder");
            Loc.Register("Pastures", "Weiden");
            Loc.Register("Wilds", "Wildlande");
            Loc.Register("Crafts", "Handwerk");
            Loc.Register("Harmony", "Harmonie");
            Loc.Register("Health", "Gesundheit");
            Loc.Register("Exploring", "Erkundung");
            Loc.Register("Ritual", "Ritual");
            Loc.Register("Diplomacy", "Diplomatie");
            Loc.Register("War", "Krieg");

            // Button labels not in MainMenu list
            Loc.Register("Reorganize", "Umorganisieren");
            Loc.Register("VENTURE", "UNTERNEHMUNG");
            Loc.Register("EMISSARY", "GESANDTER");
            Loc.Register("RECRUIT", "REKRUTIEREN");
            Loc.Register("DISMISS", "ENTLASSEN");
            Loc.Register("FORTIFY", "BEFESTIGEN");
            Loc.Register("WARRIORS", "KRIEGER");
            Loc.Register("RAID", "ÜBERFALL");
            Loc.Register("CARAVAN", "KARAWANE");
            Loc.Register("BUILD", "BAUEN");
            Loc.Register("SACRIFICE", "OPFERN");

            // Yes/No response buttons (via GetResponseButtonLabel → Loc.Get)
            Loc.Register("Yes", "Ja");
            Loc.Register("No", "Nein");

            // ScenePatches choice dialogs
            Loc.Register(" Yes or No. Press Y or N.", " Ja oder Nein. Drückt J oder N.");
            Loc.Register("Choose a ", "Wählt einen ");
            Loc.Register(" Use arrow keys to navigate the list, Enter to select, then Tab and Enter for Proceed.",
                " Mit Pfeiltasten in der Liste navigieren, Eingabe zum Auswählen, dann Tab und Eingabe für Weiter.");
            Loc.Register("Choose an amount.", "Wählt eine Menge.");
            Loc.Register(" sliders: ", " Schieber: ");
            Loc.Register("Amount", "Menge");
            Loc.Register(" to ", " bis ");
            Loc.Register(" Use Up/Down to switch sliders, Left/Right to change value, Enter for Proceed.",
                " Hoch/Runter wechselt zwischen den Schiebern, Links/Rechts ändert den Wert, Eingabe für Weiter.");

            // Slider value announcements (AnnounceFocusedSlider, AnnounceSliderValue, AnnounceAllSliders)
            Loc.Register(". Left and Right to change.", ". Links und Rechts zum Ändern.");
            Loc.Register("No sliders.", "Keine Schieber.");
            Loc.Register("Goods", "Güter");
            Loc.Register("Herds", "Herden");
            Loc.Register("Swords", "Schwerter");
            Loc.Register("Horses", "Pferde");

            // ConcernReader
            Loc.Register("No concerns and no active magic.", "Keine Belange und keine aktive Magie.");

            // Advance season dialog
            Loc.Register(" Use Up and Down to choose, Enter to confirm, Escape to cancel, D for season details.",
                " Mit Hoch und Runter wählen, Eingabe bestätigt, Escape bricht ab, D für Saison-Details.");
            Loc.Register("Advance season? Current: ", "Saison vorrücken? Aktuell: ");
            Loc.Register(" Advance.", " Vorrücken.");
            Loc.Register("Advance.", "Vorrücken.");
            Loc.Register("Cancel.", "Abbrechen.");
            Loc.Register("Advance", "Vorrücken");
            Loc.Register("question", "Frage");
            Loc.Register(". Options: Advance, Cancel. Currently on ",
                ". Optionen: Vorrücken, Abbrechen. Aktuell auf ");
            Loc.Register(". Enter to confirm, Escape to cancel, D for details.",
                ". Eingabe bestätigt, Escape bricht ab, D für Details.");

            // Map screen Mission planning composite
            Loc.Register("Map. Mission planning. ", "Karte. Missions-Planung. ");
            Loc.Register("Goal: ", "Ziel: ");
            Loc.Register("Tab cycles zones: goals, sliders, leader, destination list, hex cursor. ",
                "Tab wechselt Bereiche: Ziele, Schieber, Anführer, Zielliste, Hex-Cursor. ");
            Loc.Register("G jumps to goals, K to destination list. ",
                "G springt zu Zielen, K zur Zielliste. ");
            Loc.Register("In the list: F filters, O orders. ",
                "In der Liste: F filtert, O sortiert. ");
            Loc.Register("Space selects or sets the focused element, Enter sends, Escape cancels. F5 reads full status.",
                "Leertaste wählt oder setzt das fokussierte Element, Eingabe sendet, Escape bricht ab. F5 liest den vollen Status.");
            Loc.Register("A meteor crater is visible on the map. ",
                "Ein Meteoritenkrater ist auf der Karte sichtbar. ");
            Loc.Register("A starfall impact is visible on the map. ",
                "Ein Sternfall-Einschlag ist auf der Karte sichtbar. ");
            Loc.Register("A glacier is visible to the north, not yet explored. ",
                "Ein Gletscher ist im Norden sichtbar, noch nicht erkundet. ");

            // Emissary dialog composite
            Loc.Register("Emissary. ", "Gesandter. ");
            Loc.Register(" clans", " Klans");
            Loc.Register("Tab cycles zones: clan list, gifts and escort, leader. ",
                "Tab wechselt Bereiche: Klanliste, Geschenke und Eskorte, Anführer. ");
            Loc.Register("L jumps to the clan list. Up and Down navigate, Left and Right adjust sliders. ",
                "L springt zur Klanliste. Hoch und Runter navigieren, Links und Rechts stellen Schieber ein. ");
            Loc.Register("Space selects the focused clan, Enter sends, D reads details, Escape closes.",
                "Leertaste wählt den fokussierten Klan, Eingabe sendet, D liest Details, Escape schließt.");
            Loc.Register("Send emissary to ", "Gesandten senden zu ");
            Loc.Register(". Press Enter to open the emissary dialog.",
                ". Eingabe öffnet den Gesandten-Dialog.");
            Loc.Register("Send emissary, disabled. Select a clan first.",
                "Gesandten senden, deaktiviert. Wählt zuerst einen Klan.");
            Loc.Register(" selected as recipient.", " als Empfänger gewählt.");

            // Filter composite (Relations, Magic, RaidNav, MapScreenNav)
            Loc.Register("Filter: ", "Filter: ");
            Loc.Register(" clans listed. ", " Klans gelistet. ");
            Loc.Register("Up and Down cycle clans, D reads description. ",
                "Hoch und Runter wechseln Klans, D liest Beschreibung. ");
            Loc.Register("F cycles filter, Enter opens the emissary dialog",
                "F wechselt Filter, Eingabe öffnet den Gesandten-Dialog");
            Loc.Register(". Tab leaves the list and cycles ",
                ". Tab verlässt die Liste und wechselt zwischen ");
            Loc.Register(" mission marker", " Missions-Markierung");
            Loc.Register(" mission markers", " Missions-Markierungen");
            Loc.Register(" for clans with available emissary missions",
                " für Klans mit verfügbaren Gesandtschafts-Missionen");
            Loc.Register(" clans: ", " Klans: ");

            // Mood prefix stripped from MoodDisplay.label.text (see ManagementScreenReader.SafeMood)
            Loc.Register("The clan mood is ", "Die Klanstimmung ist ");
            Loc.Register("unknown", "unbekannt");

            // Magic screen
            Loc.Register("Selected: ", "Ausgewählt: ");
            Loc.Register("rewards", "Belohnungen");
            Loc.Register("entries", "Einträge");
            Loc.Register(" in list.", " in der Liste.");
            Loc.Register("No rewards earned yet.", "Noch keine Belohnungen erhalten.");

            // Magic screen — blessing slots (MagicScreenNavigator). The state phrases map
            // 1:1 to the game's BlessingLevel enum; see docs/game-api.md. "Wirkt" = the
            // blessing is in effect; "gewählt/Tempelplatz" = it occupies a permanent slot.
            Loc.Register("Blessings", "Segen");
            Loc.Register("Blessings: ", "Segen: ");
            Loc.Register("Blessing", "Segen");
            Loc.Register(" entries. ", " Einträge. ");
            Loc.Register("{0} of {1} permanent temple slots in use",
                "{0} von {1} dauerhaften Tempelplätzen belegt");
            Loc.Register(" (not yet learned)", " (noch nicht erlernt)");
            Loc.Register(" (learned, not in effect)", " (erlernt, wirkt nicht)");
            Loc.Register(" (in effect temporarily, from a sacrifice)",
                " (wirkt vorübergehend, durch ein Opfer)");
            Loc.Register(" (in effect temporarily, from a spirit bargain)",
                " (wirkt vorübergehend, durch einen Geisterhandel)");
            Loc.Register(" (in effect permanently, from the temple)",
                " (wirkt dauerhaft, durch den Tempel)");
            Loc.Register(" (deity is dead, blessings have no effect)",
                " (Gottheit ist tot, Segen wirken nicht)");
            Loc.Register(" — now in effect permanently", " — wirkt jetzt dauerhaft");
            Loc.Register(" — no longer in effect", " — wirkt nicht mehr");
            Loc.Register(", cannot be changed.", ", kann nicht geändert werden.");
            Loc.Register(", cannot toggle. Use the Bargain action instead.",
                ", nicht umschaltbar. Nutzt stattdessen die Aktion Handeln.");
            Loc.Register("No temple built. Build a shrine first to activate blessings.",
                "Kein Tempel gebaut. Baut zuerst einen Schrein, um Segen zu aktivieren.");
            Loc.Register("All {0} blessing slots are in use. Build a larger temple for more.",
                "Alle {0} Segensplätze sind belegt. Baut einen größeren Tempel für mehr.");
            Loc.Register("This is a description, not a toggle.",
                "Das ist eine Beschreibung, kein Schalter.");

            // Magic screen — zone navigation, list, buttons, help (MagicScreenNavigator).
            Loc.Register("Use Ctrl+1 through 9 to switch screens, or Shift+F1 for shortcuts.",
                "Mit Strg+1 bis 9 wechselt ihr Bildschirme, Umschalt+F1 zeigt die Tastenkürzel.");
            Loc.Register("Empty list.", "Leere Liste.");
            Loc.Register("List is empty.", "Liste ist leer.");
            Loc.Register("List, ", "Liste, ");
            Loc.Register(" items. ", " Einträge. ");
            Loc.Register("Actions. ", "Aktionen. ");
            Loc.Register(" is not available.", " ist nicht verfügbar.");
            Loc.Register(", not available", ", nicht verfügbar");
            Loc.Register("No details available.", "Keine Details verfügbar.");
            Loc.Register("No description available.", "Keine Beschreibung verfügbar.");
            Loc.Register("No blessing selected.", "Kein Segen ausgewählt.");
            Loc.Register("No actions available.", "Keine Aktionen verfügbar.");
            Loc.Register(" (deity dead)", " (Gottheit tot)");
            Loc.Register(" (recently sacrificed)", " (kürzlich geopfert)");
            Loc.Register(" (recently bargained)", " (kürzlich gehandelt)");
            Loc.Register(" (temple damaged)", " (Tempel beschädigt)");
            Loc.Register(" (temple seriously damaged)", " (Tempel schwer beschädigt)");
            Loc.Register("Temple damaged. ", "Tempel beschädigt. ");
            Loc.Register("Temple seriously damaged. ", "Tempel schwer beschädigt. ");
            Loc.Register("Gods: shows deities your clan knows. Select one to see blessings and temple status. You can sacrifice, build temples, or perform rituals.",
                "Götter: zeigt die Gottheiten, die euer Klan kennt. Wählt eine, um Segen und Tempelstatus zu sehen. Ihr könnt opfern, Tempel bauen oder Rituale durchführen.");
            Loc.Register("Spirits: shows spirits your clan knows. You can bargain with well-known spirits for temporary blessings.",
                "Geister: zeigt die Geister, die euer Klan kennt. Mit gut bekannten Geistern könnt ihr um vorübergehende Segen handeln.");
            Loc.Register("Sacred Time: shows magic categories for the next Sacred Time allocation.",
                "Heilige Zeit: zeigt die Magie-Kategorien für die nächste Zuteilung der Heiligen Zeit.");
            Loc.Register("Other: shows other magical effects currently active.",
                "Sonstiges: zeigt andere derzeit aktive magische Effekte.");

            // --- Filter caption labels (game UI text read raw from filter.captionText;
            // routed through Loc.Get in MagicScreenNavigator.AnnounceFilter and
            // ManagementScreenReader Magic/Relations summaries). Unregistered captions
            // pass through unchanged. ---
            Loc.Register("Gods", "Götter");
            Loc.Register("Spirits", "Geister");
            Loc.Register("Sacred Time", "Heilige Zeit");
            Loc.Register("Other", "Sonstiges");
            Loc.Register("Known Clans", "Bekannte Klans");
            // --- Season advance / leader chosen announcements ---
            Loc.Register("Advancing season...", "Saison wird vorgerückt...");
            Loc.Register(" chosen as leader.", " als Anführer gewählt.");

            // --- Phase / screen header composite (ScreenChangePatches.GetPhaseHeaderIfNew,
            // also drives ScenePatches scene headers). "<phase> phase. <screen> screen." ---
            Loc.Register(" phase. ", " Phase. ");
            Loc.Register(" screen.", " Bildschirm.");
            // PhaseLabel() returns
            Loc.Register("Management", "Verwaltung");
            Loc.Register("Story", "Geschichte");
            Loc.Register("Battle", "Schlacht");
            Loc.Register("Sacred time", "Heilige Zeit");
            Loc.Register("Game over", "Spielende");
            // GameOverNavigator hint texts (Game over screen + REVIEW SAGA hint).
            // Button-Wortlaut „SAGA ANSEHEN" in den Hints muss zum Loc-Mapping
            // von „REVIEW SAGA" unten passen, sonst hört der User Anweisungen,
            // die er auf dem Bildschirm nicht findet.
            Loc.Register(" Press REVIEW SAGA to read your chronicle and pick a year to restore.",
                " Drückt SAGA ANSEHEN, um eure Chronik zu lesen und ein Jahr zur Wiederherstellung zu wählen.");
            Loc.Register(" Press REVIEW SAGA to read your chronicle.",
                " Drückt SAGA ANSEHEN, um eure Chronik zu lesen.");
            Loc.Register("Game over. ", "Spielende. ");
            Loc.Register("Restores used ", "Wiederherstellungen verwendet ");
            Loc.Register("Restore available. Open REVIEW SAGA, pick an earlier year, then Enter.",
                "Wiederherstellung verfügbar. Öffnet SAGA ANSEHEN, wählt ein früheres Jahr und drückt Eingabe.");
            Loc.Register("No restores remaining.", "Keine Wiederherstellungen mehr übrig.");
            Loc.Register("Restore is not available right now.",
                "Wiederherstellung ist im Moment nicht verfügbar.");
            // Static Unity button labels on the GameOver screen (not routed
            // through PluginImport, never reach the DE translation mod).
            Loc.Register("MAIN MENU", "HAUPTMENÜ");
            Loc.Register("REVIEW SAGA", "SAGA ANSEHEN");
            Loc.Register("Unknown", "Unbekannt");
            // "Menu" / "Dialog" also serve as PhaseLabel returns
            Loc.Register("Menu", "Menü");
            Loc.Register("Dialog", "Dialog");
            // GameScreens.NameOf / ScreenLabelFor screen labels
            Loc.Register("Clan", "Klan");
            Loc.Register("Magic", "Magie");
            Loc.Register("Map", "Karte");
            Loc.Register("Relations", "Beziehungen");
            Loc.Register("War", "Krieg");
            Loc.Register("Wealth", "Wohlstand");
            Loc.Register("Lore", "Wissen");
            Loc.Register("Saga", "Sage");
            // The game's enum is screen_Controls, but the screen is the audio/video/tutorial
            // settings overlay — German users expect "Einstellungen", not "Steuerung".
            Loc.Register("Controls", "Einstellungen");
            Loc.Register("Venture", "Unternehmung");
            Loc.Register("Emissary", "Gesandter");
            Loc.Register("Foray", "Vorstoß");
            Loc.Register("Sacrifice", "Opfer");
            Loc.Register("Spirit", "Geist");
            Loc.Register("Temple", "Tempel");
            Loc.Register("Cattle raid", "Viehraub");
            Loc.Register("Honor raid", "Ehrenraub");
            Loc.Register("Fortify", "Befestigen");
            Loc.Register("Raid", "Überfall");
            Loc.Register("Warriors", "Krieger");
            Loc.Register("Caravan", "Karawane");
            Loc.Register("Main menu", "Hauptmenü");
            Loc.Register("Choose game", "Spielauswahl");
            Loc.Register("Marketing", "Marketing");
            Loc.Register("Thanks", "Danksagung");
            Loc.Register("Intro", "Intro");
            Loc.Register("Intro recap", "Intro-Rückblick");
            Loc.Register("Scene", "Szene");
            Loc.Register("Hero quest", "Heldenfahrt");
            Loc.Register("Myth", "Mythos");
            Loc.Register("Prolog", "Prolog");
            Loc.Register("Prolog intro", "Prolog-Intro");
            Loc.Register("Scene end", "Szenen-Ende");
            Loc.Register("Quest end", "Fahrt-Ende");
            Loc.Register("Final scene", "Schluss-Szene");
            Loc.Register("Final scene prolog", "Schluss-Szenen-Prolog");
            Loc.Register("Battle results", "Schlacht-Ergebnis");
            Loc.Register("Heroic combat", "Heldenkampf");
            Loc.Register("Choose leader", "Anführer wählen");
            Loc.Register("Sacrifice details", "Opfer-Details");
            Loc.Register("Victory", "Sieg");
            Loc.Register("Game won", "Spiel gewonnen");
            Loc.Register("Manual", "Handbuch");
            Loc.Register("First of year", "Jahresbeginn");
            Loc.Register("All screens", "Alle Bildschirme");
            Loc.Register("Settings", "Einstellungen");

            // Settings overlay (ControlsOverlay) — exact UILabel.text values from the
            // prefab, as captured by KeyboardNavigationHandler.GetElementLabel after
            // NormalizeLabel collapses embedded newlines. Toggle labels are mixed case
            // ("Background Music"), button labels are upper case ("CHOOSE", "RESET TIPS").
            // "Volume" is the volume slider's UILabel.text (read by the slider-focus path).
            Loc.Register("Background Music", "Hintergrundmusik");
            Loc.Register("Adventure Music", "Abenteuermusik");
            Loc.Register("Sound Effects", "Soundeffekte");
            Loc.Register("Show Tips", "Tipps anzeigen");
            Loc.Register("RESET TIPS", "Tipps zurücksetzen");
            // The "CHOOSE" button in ControlsOverlay opens a file browser for the
            // StormAge folder from Six Ages 1 (Ride Like the Wind) — picking it lets
            // this game continue from a RLTW victory save. The bare label "Auswählen"
            // gives no hint of that, so spell it out for the screen reader.
            Loc.Register("CHOOSE", "Ordner für Vorgängerspiel wählen");
            Loc.Register("Volume", "Lautstärke");

            // StormAge folder picker bypass — replaces SimpleFileBrowser, see
            // MenuPatches.PickContinuationFolder_Prefix for the rationale.
            Loc.Register(
                "To set the previous-game folder, copy the full path of your StormAge folder to the clipboard and activate this button again. The folder must be named StormAge.",
                "Um den Vorgängerspiel-Ordner zu setzen, kopiere den vollständigen Pfad deines StormAge-Ordners in die Zwischenablage und drücke diesen Knopf nochmal. Der Ordner muß StormAge heißen.");
            Loc.Register("The folder must be named StormAge, but got: ",
                "Der Ordner muß StormAge heißen, ist aber: ");
            Loc.Register("Folder not found: ", "Ordner nicht gefunden: ");
            Loc.Register("Previous-game folder set to ", "Vorgängerspiel-Ordner gesetzt auf ");

            // Scene/News/Battle prefixes (KeyboardNavigationHandler, CombatScreenReader)
            Loc.Register("Result: ", "Ergebnis: ");
            Loc.Register("Result shown. Press Escape or Close to continue.",
                "Ergebnis angezeigt. Drückt Escape oder Schließen zum Fortfahren.");

            // Resource-change deltas (ScenePatches.ShowDeltas_Postfix / FormatDeltaText)
            Loc.Register("Resource changes: ", "Ressourcen-Änderungen: ");
            Loc.Register(" options: ", " Optionen: ");
            Loc.Register(" up", " hoch");
            Loc.Register(" down", " runter");
            Loc.Register("plus ", "plus ");
            Loc.Register("minus ", "minus ");
            // Absolute state appended to the vague Food/Mood deltas (VagueAbsoluteSuffix).
            Loc.Register("now ", "jetzt ");
            // Delta resource names (game UILabel text, not routed through PluginImport here).
            // The full set is InteractiveController.deltaInfo_'s "user" values; the
            // diplomacy/reputation deltas Sick/Like/Fear/Hate/Mock were missing.
            // German for Fear/Hate/Mock/sick confirmed against the translation corpus.
            Loc.Register("Mood", "Stimmung");
            Loc.Register("Food", "Nahrung");
            Loc.Register("Cattle", "Vieh");
            Loc.Register("Treasures", "Schätze");
            Loc.Register("Population", "Bevölkerung");
            Loc.Register("Sick", "Kranke");
            Loc.Register("Like", "Sympathie");
            Loc.Register("Fear", "Furcht");
            Loc.Register("Hate", "Hass");
            Loc.Register("Mock", "Spott");

            // Advisor / dialog speaker wrapper (AdvisorPatches, DialogPatches)
            Loc.Register("Advisor ", "Berater ");
            Loc.Register(" says: ", " sagt: ");
            Loc.Register("Advisor: ", "Berater: ");
            // News / battle-result speaker name prefix (ScenePatches.AnnounceSceneInit)
            Loc.Register("Speaker: ", "Sprecher: ");

            // ---- AdvisorReader (F3/Shift+F3 ring advice + info) ----
            Loc.Register("Advisor advice is available during scenes and management.",
                "Beraterrat ist in Szenen und in der Verwaltung verfügbar.");
            Loc.Register("Advisor info is available during scenes and management.",
                "Berater-Infos sind in Szenen und in der Verwaltung verfügbar.");
            Loc.Register("No advisor advice available.", "Kein Beraterrat verfügbar.");
            Loc.Register("No advisor info available.", "Keine Berater-Infos verfügbar.");
            Loc.Register("No advice available.", "Kein Rat verfügbar.");
            Loc.Register("Could not read advisor advice.", "Beraterrat konnte nicht gelesen werden.");
            Loc.Register("Could not read advisor info.", "Berater-Infos konnten nicht gelesen werden.");
            // Fallback name when the expedition-leader index is missing.
            Loc.Register("Expedition leader", "Expeditions-Anführer");
            // Empty-advice blocker reasons surfaced for ring chairs without advice this turn.
            Loc.Register("absent this season", "diese Saison abwesend");
            Loc.Register("sulking, not advising", "schmollt, berät nicht");
            // Prefix appended after an advisor's name when their advice contains *highlight* segments.
            Loc.Register(", highlighted: ", ", hervorgehoben: ");
            // Prefix for the per-advisor suggested-response list ("Suggests: 1) ..., 2) ...").
            Loc.Register(" Suggests: ", " empfiehlt: ");

            // Sacred Time forecast paragraph navigation (SacredTimeNavigator)
            Loc.Register("Beginning of forecast.", "Anfang der Vorhersage.");
            Loc.Register("End of forecast.", "Ende der Vorhersage.");

            // ---- WarriorsNavigator (recruit/dismiss dialog) ----
            // "Warriors: " already registered above (clan summary composite).
            Loc.Register(". Recruiting ", ". Rekrutiere ");
            Loc.Register(". Dismissing ", ". Entlasse ");
            // "Krieger" is identical in singular and plural — both keys map to it.
            Loc.Register(" warrior.", " Krieger.");
            Loc.Register(" warriors.", " Krieger.");
            Loc.Register(". No change.", ". Keine Änderung.");
            Loc.Register(", missing.", ", fehlt.");
            Loc.Register(", not available in current mode", ", im aktuellen Modus nicht verfügbar");
            Loc.Register(" is not available in the current mode. Use the slider to switch between recruiting and dismissing.",
                " ist im aktuellen Modus nicht verfügbar. Nutzt den Schieber, um zwischen Rekrutieren und Entlassen zu wechseln.");
            Loc.Register("Offer gifts (raises recruit cost)",
                "Geschenke anbieten (erhöht die Rekrutierungskosten)");
            Loc.Register("Recruit from clan", "Aus dem Klan rekrutieren");
            Loc.Register("Recruit outsiders", "Außenstehende rekrutieren");
            Loc.Register("Severance pay (softens dismissal)", "Abfindung (mildert die Entlassung)");
            Loc.Register("Toggle", "Schalter");
            Loc.Register("Slider zone.", "Schieber-Bereich.");
            Loc.Register("Toggle zone.", "Schalter-Bereich.");
            Loc.Register("No change to warriors. Move the slider first to recruit or dismiss.",
                "Keine Änderung an den Kriegern. Bewegt zuerst den Schieber, um zu rekrutieren oder zu entlassen.");
            Loc.Register("Action is not available right now.", "Aktion ist gerade nicht verfügbar.");
            Loc.Register("Confirming", "Bestätige");
            Loc.Register("Warriors dialog status. ", "Krieger-Dialog Status. ");
            Loc.Register("Slider: ", "Schieber: ");
            Loc.Register(", recruiting ", ", rekrutiere ");
            Loc.Register(", dismissing ", ", entlasse ");
            Loc.Register(", no change. ", ", keine Änderung. ");
            Loc.Register(" (locked)", " (gesperrt)");
            Loc.Register("Action", "Aktion");
            Loc.Register(" available. ", " verfügbar. ");
            Loc.Register(" disabled. ", " deaktiviert. ");
            Loc.Register("Tab switches zones. Left and Right adjust the slider, Up and Down cycle toggles, Space flips the focused toggle, Enter confirms, Escape closes.",
                "Tab wechselt Bereiche. Links und Rechts stellen den Schieber ein, Hoch und Runter wechseln die Schalter, Leertaste schaltet den gewählten Schalter um, Eingabe bestätigt, Escape schließt.");

            // ---- SagaNavigator (year list + Restore) ----
            Loc.Register("No years available.", "Keine Jahre verfügbar.");
            Loc.Register("No year selected.", "Kein Jahr ausgewählt.");
            Loc.Register("Ancient lore. ", "Alte Sage. ");
            Loc.Register("Year ", "Jahr ");
            Loc.Register("Ruler ", "Herrscher ");
            Loc.Register("... Press D for full text.", "... Drückt D für den vollen Text.");
            Loc.Register("Select a year first using the arrow keys.",
                "Wählt zuerst ein Jahr mit den Pfeiltasten.");
            Loc.Register("Cannot restore to the current year. Pick an earlier year first.",
                "Wiederherstellung ins aktuelle Jahr nicht möglich. Wählt zuerst ein früheres Jahr.");
            Loc.Register("Restore is not available on this view.",
                "Wiederherstellung ist in dieser Ansicht nicht verfügbar.");
            Loc.Register("No restores remaining. Used ", "Keine Wiederherstellungen übrig. Genutzt ");
            Loc.Register("Restore is not available right now.",
                "Wiederherstellung ist gerade nicht verfügbar.");
            Loc.Register("No saga text available for this year.",
                "Kein Sagentext für dieses Jahr verfügbar.");
            Loc.Register("Saga screen. No data available.", "Sagen-Bildschirm. Keine Daten verfügbar.");
            Loc.Register("Saga. ", "Sage. ");
            Loc.Register(" years. ", " Jahre. ");
            Loc.Register("Viewing ancient lore. ", "Zeige alte Sage. ");
            Loc.Register("Viewing current year. ", "Zeige aktuelles Jahr. ");
            Loc.Register("Viewing year ", "Zeige Jahr ");
            Loc.Register("Restores used ", "Wiederherstellungen genutzt ");
            Loc.Register("Restores: ", "Wiederherstellungen: ");
            Loc.Register(" used. ", " genutzt. ");
            Loc.Register("Restore available. Use arrows to pick a year, then Enter.",
                "Wiederherstellung verfügbar. Wählt mit den Pfeiltasten ein Jahr, dann Eingabe.");
            Loc.Register("No restores remaining.", "Keine Wiederherstellungen übrig.");
            Loc.Register("Current year — cannot restore.",
                "Aktuelles Jahr — Wiederherstellung nicht möglich.");
            Loc.Register("Restore not available here.", "Wiederherstellung hier nicht verfügbar.");
            Loc.Register("Enter to restore.", "Eingabe zum Wiederherstellen.");
            Loc.Register("No restores remaining (", "Keine Wiederherstellungen übrig (");
            Loc.Register(" used).", " genutzt).");
            Loc.Register("Restore disabled.", "Wiederherstellung deaktiviert.");

            // ---- ChooseLeaderNavigator (leader picker dialog) ----
            Loc.Register("Sort by ", "Sortiert nach ");
            Loc.Register(" candidates.", " Kandidaten.");
            Loc.Register(" candidates. ", " Kandidaten. ");
            Loc.Register("No candidates available.", "Keine Kandidaten verfügbar.");
            Loc.Register("Empty slot.", "Leerer Platz.");
            Loc.Register(" family", "-Familie");
            Loc.Register(", devotee of ", ", Anhänger von ");
            Loc.Register(", selected", ", ausgewählt");
            Loc.Register("ring leader", "Ring-Anführer");
            Loc.Register("ring member", "Ring-Mitglied");
            Loc.Register(" is highlighted. View only — leader is chosen automatically. Escape to return.",
                " ist hervorgehoben. Nur Ansicht — der Anführer wird automatisch gewählt. Escape kehrt zurück.");
            Loc.Register(" is already selected. Press Enter to choose.",
                " ist bereits ausgewählt. Eingabe zum Bestätigen.");
            Loc.Register(" highlighted. View only — leader is chosen automatically. Escape to return.",
                " hervorgehoben. Nur Ansicht — der Anführer wird automatisch gewählt. Escape kehrt zurück.");
            Loc.Register(" selected. Press Enter to choose.",
                " ausgewählt. Eingabe zum Bestätigen.");
            Loc.Register("View only — the leader is chosen automatically. Press Escape to return.",
                "Nur Ansicht — der Anführer wird automatisch gewählt. Escape kehrt zurück.");
            Loc.Register("Choose is not available in this dialog.",
                "Wählen ist in diesem Dialog nicht verfügbar.");
            Loc.Register("Choose is not available yet. Select a candidate first.",
                "Wählen ist noch nicht verfügbar. Wählt zuerst einen Kandidaten.");
            Loc.Register("This dialog requires a leader. Pick one and press Enter.",
                "Dieser Dialog erfordert einen Anführer. Wählt einen und drückt Eingabe.");
            Loc.Register("Choose Leader. ", "Anführer wählen. ");
            Loc.Register("View only — leader is chosen automatically. ",
                "Nur Ansicht — der Anführer wird automatisch gewählt. ");
            Loc.Register("No leader chosen. ", "Kein Anführer gewählt. ");
            Loc.Register("Press Escape to return.", "Escape kehrt zurück.");
            Loc.Register("Press Enter to choose.", "Eingabe zum Bestätigen.");
            Loc.Register("Choose is disabled — select a candidate first.",
                "Wählen ist deaktiviert — wählt zuerst einen Kandidaten.");
            // Skill filter names (FilterNames array) — Diplomacy/Food/Lore/Magic already registered.
            Loc.Register("Bargaining", "Verhandeln");
            Loc.Register("Combat", "Kampf");
            Loc.Register("Leadership", "Anführerschaft");
            // Skill rating adjectives — wording matches the game's own scale
            // ("Fair — Good — Very Good — Excellent — Renowned — Heroic", DE corpus).
            Loc.Register("Fair", "Annehmbar");
            Loc.Register("Good", "Gut");
            Loc.Register("Very Good", "Sehr gut");
            Loc.Register("Excellent", "Vortrefflich");
            Loc.Register("Renowned", "Berühmt");
            Loc.Register("Heroic", "Heroisch");

            // ---- LoreScreenNavigator (history + myth lists) ----
            Loc.Register("History zone. ", "Geschichts-Bereich. ");
            Loc.Register(" entry.", " Eintrag.");
            Loc.Register(" entries.", " Einträge.");
            Loc.Register(" Up and Down to cycle, Enter opens it.",
                " Hoch und Runter wechseln, Eingabe öffnet.");
            Loc.Register("Myths zone. ", "Mythen-Bereich. ");
            Loc.Register(" myth.", " Mythos.");
            Loc.Register(" myths.", " Mythen.");
            Loc.Register("(empty row)", "(leere Zeile)");
            Loc.Register("Use Up and Down to pick an entry first.",
                "Wählt zuerst einen Eintrag mit Hoch und Runter.");
            Loc.Register("entry", "Eintrag");
            Loc.Register("Opening ", "Öffne ");
            Loc.Register("No entries in list.", "Keine Einträge in der Liste.");
            Loc.Register("(unnamed entry)", "(unbenannter Eintrag)");
            Loc.Register(". Press Enter to open.", ". Eingabe zum Öffnen.");
            Loc.Register("Opening Manual.", "Öffne Handbuch.");
            Loc.Register("Lore status. ", "Wissens-Status. ");
            Loc.Register(" history entry, ", " Geschichts-Eintrag, ");
            Loc.Register(" history entries, ", " Geschichts-Einträge, ");
            Loc.Register(" myth. ", " Mythos. ");
            Loc.Register(" myths. ", " Mythen. ");
            Loc.Register("History zone active", "Geschichts-Bereich aktiv");
            Loc.Register("Myths zone active", "Mythen-Bereich aktiv");
            Loc.Register(", focused: ", ", Fokus: ");
            Loc.Register(". Tab cycles zones, Up and Down cycle entries, Enter opens, ",
                ". Tab wechselt Bereiche, Hoch und Runter wechseln Einträge, Eingabe öffnet, ");
            Loc.Register("M opens Manual, F5 for status.", "M öffnet das Handbuch, F5 für den Status.");

            // ---- EmissaryNavigator (send-emissary dialog) ----
            // Reused keys (registered above, not repeated here): ", selected",
            // " selected as recipient.", " of ", " (locked)", "No leader chosen. ".
            Loc.Register("Clan list.", "Klanliste.");
            Loc.Register("Gifts and escort.", "Geschenke und Eskorte.");
            Loc.Register("Leader.", "Anführer.");
            Loc.Register("No clans available.", "Keine Klans verfügbar.");
            Loc.Register("Unknown clan", "Unbekannter Klan");
            Loc.Register(", feud", ", Fehde");
            Loc.Register(", trade partner", ", Handelspartner");
            Loc.Register(" slider not available.", " Schieber nicht verfügbar.");
            Loc.Register(". Left and Right to adjust, Shift for larger steps.",
                ". Links und Rechts zum Anpassen, Umschalt für größere Schritte.");
            // Emissary slider labels (the navigator's own hardcoded labels).
            Loc.Register("Elite warriors", "Elite-Krieger");
            Loc.Register("Regular warriors", "Reguläre Krieger");
            Loc.Register("Goods gift", "Güter-Geschenk");
            Loc.Register("Herds gift", "Herden-Geschenk");
            Loc.Register("Horses gift", "Pferde-Geschenk");
            Loc.Register("Slider", "Schieber");
            Loc.Register("Choose Leader is not available.", "Anführerwahl ist nicht verfügbar.");
            Loc.Register("No leader chosen yet. Press Space to pick one.",
                "Noch kein Anführer gewählt. Leertaste zum Auswählen.");
            Loc.Register("Leader: ", "Anführer: ");
            Loc.Register(". Press Space to change.", ". Leertaste zum Wechseln.");
            // Send-button gating reasons (joined into a "Send needs: ..." sentence).
            Loc.Register("pick a clan", "einen Klan wählen");
            Loc.Register("pick a leader", "einen Anführer wählen");
            Loc.Register("send at least two warriors", "mindestens zwei Krieger senden");
            Loc.Register("Send needs: ", "Zum Senden noch nötig: ");
            Loc.Register("Send is disabled, but the game-mechanic conditions appear to be met. The game may be waiting for an internal state update.",
                "Senden ist deaktiviert, aber die Spielmechanik-Bedingungen scheinen erfüllt. Das Spiel wartet vielleicht auf eine interne Statusaktualisierung.");
            Loc.Register("Close button not found.", "Schließen-Knopf nicht gefunden.");
            // F5 full status
            Loc.Register("Emissary dialog. ", "Gesandten-Dialog. ");
            Loc.Register("Recipient: ", "Empfänger: ");
            Loc.Register("No recipient chosen. ", "Kein Empfänger gewählt. ");
            Loc.Register("Press Enter to send. Escape to leave.",
                "Eingabe zum Senden. Escape zum Verlassen.");

            // ---- RelationsScreen clan list (KeyboardNavigationHandler +
            //      ManagementScreenReader.ReadRelations*). Model Y: Space selects,
            //      Enter opens the emissary dialog. ----
            // Reused keys (registered above): "Clan list.", "No clans available.",
            // "Filter: ", " clans listed. ", " clans: ", " mission marker(s)",
            // ". Tab leaves the list and cycles ", "Send emissary to ".
            Loc.Register("selected", "ausgewählt");
            Loc.Register("No filter available on this screen.",
                "Auf diesem Bildschirm ist kein Filter verfügbar.");
            Loc.Register("Tutorial expects you to use: ", "Das Tutorial erwartet: ");
            Loc.Register(". The emissary action is locked until then.",
                ". Die Gesandten-Aktion ist bis dahin gesperrt.");
            // Clan-item summary parts (TryFormatClanItem + StringHelpers.AttitudeLabel)
            Loc.Register("hostile", "feindlich");
            Loc.Register("unfriendly", "unfreundlich");
            Loc.Register("neutral", "neutral");
            Loc.Register("friendly", "freundlich");
            Loc.Register("allied", "verbündet");
            Loc.Register("tribe member", "Stammesmitglied");
            Loc.Register("our clan", "unser Klan");
            Loc.Register("relations unknown", "Beziehung unbekannt");
            Loc.Register(", in our tribe", ", in unserem Stamm");
            // Clan cultures — Hyaloring/Orlanthi are proper names, kept as-is.
            Loc.Register("Chariot", "Wagenfahrer");
            Loc.Register("Hyaloring", "Hyaloring");
            Loc.Register("Orlanthi", "Orlanthi");
            Loc.Register("near", "in der Nähe");
            Loc.Register("feud", "Fehde");
            Loc.Register("trading", "Handelspartner");
            Loc.Register("visited by emissary", "von Gesandtem besucht");
            Loc.Register("visited by caravan", "von Karawane besucht");
            // Relations full status (ManagementScreenReader.ReadRelationsFull)
            Loc.Register(" and ", " und ");
            Loc.Register(" more", " weitere");
            Loc.Register("No clans listed. ", "Keine Klans gelistet. ");
            Loc.Register("Up and Down cycle clans (clan list is active by default). ",
                "Hoch und Runter wechseln Klans (die Klanliste ist standardmäßig aktiv). ");
            Loc.Register("D reads a clan's description; press D again to cycle paragraphs. ",
                "D liest die Beschreibung eines Klans; nochmal D blättert durch die Absätze. ");
            Loc.Register("F cycles the filter. ", "F wechselt den Filter. ");
            Loc.Register("Enter opens the emissary dialog. ",
                "Eingabe öffnet den Gesandten-Dialog. ");
            Loc.Register("Tab leaves the list and cycles ",
                "Tab verlässt die Liste und wechselt durch ");
            Loc.Register(" on the map. ", " auf der Karte. ");
            Loc.Register("No mission markers on the map right now. ",
                "Gerade keine Missions-Markierungen auf der Karte. ");
            // F1 shortcut overlay — Relations block
            Loc.Register("On Relations: F cycles the clan filter, ",
                "Auf Beziehungen: F wechselt den Klan-Filter, ");
            Loc.Register("Tab cycles map markers for clans with available missions, ",
                "Tab wechselt durch Karten-Markierungen für Klans mit verfügbaren Missionen, ");
            Loc.Register("Enter opens the emissary dialog for the focused clan. ",
                "Eingabe öffnet den Gesandten-Dialog für den fokussierten Klan. ");

            // ---- ReorganizeNavigator (clan ring dialog) ----
            // Reused keys (registered above, not repeated here): "No candidates
            // available.", "Empty slot.", "Sort by ", " candidates.", " family",
            // ", devotee of ", "empty", "Close button not found.", the skill
            // adjectives, and the filter names Bargaining/Combat/Diplomacy/Food/
            // Leadership/Lore/Magic.
            Loc.Register("Candidate list.", "Kandidatenliste.");
            Loc.Register("Ring positions.", "Ring-Positionen.");
            // Filter segments not already registered as skill names.
            Loc.Register("A to Z", "A bis Z");
            Loc.Register("Deity", "Gottheit");
            // Per-candidate working-ring + eligibility markers (right after the name).
            Loc.Register(", chief", ", Häuptling");
            Loc.Register(", in ring", ", im Ring");
            Loc.Register(", ineligible", ", nicht wählbar");
            Loc.Register(", cannot be chief", ", kann nicht Häuptling werden");
            // Ring-toggle (Space) feedback.
            Loc.Register("This person is ineligible for the ring.",
                "Diese Person ist nicht für den Ring wählbar.");
            Loc.Register("Ring is full. Remove someone else first.",
                "Der Ring ist voll. Entfernt zuerst jemand anderen.");
            Loc.Register(" added to ring.", " in den Ring aufgenommen.");
            Loc.Register(" removed from ring.", " aus dem Ring entfernt.");
            // Chief-toggle (C) feedback.
            Loc.Register("This person cannot be chief.",
                "Diese Person kann nicht Häuptling werden.");
            Loc.Register("Another person is already chief. Remove the current chief first.",
                "Eine andere Person ist bereits Häuptling. Entfernt zuerst den jetzigen Häuptling.");
            Loc.Register(" is now chief.", " ist jetzt Häuptling.");
            Loc.Register(" is no longer chief.", " ist nicht mehr Häuptling.");
            // Detail cycle (D) — advice facet.
            Loc.Register(" is away — no advice available.",
                " ist fort — kein Rat verfügbar.");
            Loc.Register(" has no advice to give.", " hat keinen Rat zu geben.");
            // Ring zone.
            Loc.Register("Chief slot", "Häuptlings-Platz");
            Loc.Register("Ring slot ", "Ring-Platz ");
            Loc.Register(". Press Space to find them in the list.",
                ". Leertaste, um sie in der Liste zu finden.");
            Loc.Register(": empty.", ": leer.");
            Loc.Register("Slot is empty.", "Der Platz ist leer.");
            // Primary action (Enter) + F5 full status.
            Loc.Register("Reorganize is not available yet. Ring is incomplete or unchanged.",
                "Umorganisieren ist noch nicht möglich. Der Ring ist unvollständig oder unverändert.");
            Loc.Register("Reorganize. ", "Umorganisieren. ");
            Loc.Register("Working ring: ", "Arbeits-Ring: ");
            Loc.Register(" (chief)", " (Häuptling)");
            Loc.Register("Press Enter to reorganize. Escape to discard.",
                "Eingabe zum Umorganisieren. Escape verwirft.");
            Loc.Register("Reorganize disabled. Ring is incomplete or unchanged.",
                "Umorganisieren deaktiviert. Der Ring ist unvollständig oder unverändert.");
            // DialogContentReader.ReadReorganize entry announcement (Model Y).
            Loc.Register("Current ring: ", "Aktueller Ring: ");
            Loc.Register(" candidates", " Kandidaten");
            Loc.Register("Up and Down cycle candidates. F changes sort, Space toggles ring membership, C toggles chief, D reads advice and full information, Tab switches to ring slots, F5 reads ring status. Enter reorganizes, Escape closes.",
                "Hoch und Runter wechseln Kandidaten. F ändert die Sortierung, Leertaste schaltet die Ring-Zugehörigkeit um, C schaltet den Häuptling um, D liest Rat und volle Informationen, Tab wechselt zu den Ring-Plätzen, F5 liest den Ring-Status. Eingabe organisiert um, Escape schließt.");
            // ReorganizeNavigator.BuildReorganizeSummary — two-step Enter confirmation.
            Loc.Register("You choose {0} as chief, with {1} in the ring.",
                "Du wählst {0} als Häuptling, dazu {1} im Ring.");
            Loc.Register("You choose {0} as chief.", "Du wählst {0} als Häuptling.");
            Loc.Register("You put {0} in the ring.", "Du nimmst {0} in den Ring.");
            Loc.Register("You change nothing.", "Du änderst nichts.");

            // ---- PersonBio (localized port of Person.AttributedTextFor) ----
            // Shared by Reorganize / ChooseLeader / Caravan / Map / AdvisorReader.
            // Skill names (Bargaining…Magic) and rating adjectives (Fair…Heroic) are
            // already registered above. "Sick" is NOT registered here on purpose —
            // the key "Sick" already means the diplomacy delta "Kranke"; the
            // person-health "Krank" is handled by a language branch in PersonBio.
            Loc.Register("Age: ", "Alter: ");
            Loc.Register(", age ", ", Alter ");
            Loc.Register("Worships: ", "Verehrt: ");
            Loc.Register("Worshiped: ", "Verehrte: ");
            // Location states (PersonBio.AwayStatus + at-home flags).
            Loc.Register("(Married outside the clan)", "(Außerhalb des Klans verheiratet)");
            Loc.Register("(In the Otherworld)", "(Im Jenseits)");
            Loc.Register("(Exploring)", "(Erkundung)");
            Loc.Register("(Searching for horses)", "(Suche nach Pferden)");
            Loc.Register("(Foraging)", "(Nahrungssuche)");
            Loc.Register("(Searching for spirits)", "(Suche nach Geistern)");
            Loc.Register("(Vanished)", "(Verschwunden)");
            Loc.Register("(Departed)", "(Fortgegangen)");
            Loc.Register("(Away from the clan)", "(Vom Klan entfernt)");
            Loc.Register("(Drumming)", "(Trommelt)");
            Loc.Register("Absent", "Abwesend");
            Loc.Register("Sulking", "Schmollt");
            // Health states.
            Loc.Register("Dead", "Tot");
            Loc.Register("Wounded", "Verwundet");
            Loc.Register("Unliving", "Unlebend");

            // ---- RaidNavigator (raid / cattle-raid dialog) ----
            // Reused keys (registered above): ", selected", "Filter: ", " entry.",
            // " entries.", " of ", " (locked)", " slider not available.",
            // ". Left and Right to adjust, Shift for larger steps.", "Swords",
            // "Slider", "Choose Leader is not available.",
            // "No leader chosen yet. Press Space to pick one.", "Leader: ",
            // ". Press Space to change.", "Leader.", "Unknown clan", ", feud",
            // ", trade partner", "Clan", "No leader chosen. ", " available. ",
            // " disabled. ", "Raid".
            Loc.Register("No raidable clans available.", "Keine überfallbaren Klans verfügbar.");
            Loc.Register(" selected as target. Press Enter to raid.",
                " als Ziel gewählt. Eingabe zum Überfallen.");
            Loc.Register("Target selected.", "Ziel gewählt.");
            Loc.Register("No filter available.", "Kein Filter verfügbar.");
            Loc.Register(" helper.", " Helfer.");
            Loc.Register(" helpers.", " Helfer.");
            Loc.Register("No helper clans available.", "Keine helfenden Klans verfügbar.");
            Loc.Register(", selected as helper", ", als Helfer gewählt");
            Loc.Register(" will help. Press Space again to remove.",
                " hilft mit. Leertaste erneut zum Entfernen.");
            Loc.Register("Helper removed.", "Helfer entfernt.");
            Loc.Register("Helper toggled.", "Helfer umgeschaltet.");
            Loc.Register("Bows", "Bögen");
            Loc.Register("Raidable clans, ", "Überfallbare Klans, ");
            Loc.Register("Warriors.", "Krieger.");
            Loc.Register("Helpers", "Helfer");
            Loc.Register(" entry. F cycles filter.", " Eintrag. F wechselt den Filter.");
            Loc.Register(" entries. F cycles filter.", " Einträge. F wechselt den Filter.");
            Loc.Register("Raiding", "Überfall");
            Loc.Register("Raid is disabled. Move the Swords or Bows slider above zero.",
                "Überfall deaktiviert. Bewegt den Schwerter- oder Bögen-Schieber über null.");
            Loc.Register("Raid is disabled. Pick a target from the raidable list first.",
                "Überfall deaktiviert. Wählt zuerst ein Ziel aus der Liste der überfallbaren Klans.");
            Loc.Register("Raid is disabled. The helper and the target are the same clan — pick another helper or remove it.",
                "Überfall deaktiviert. Helfer und Ziel sind derselbe Klan — wählt einen anderen Helfer oder entfernt ihn.");
            Loc.Register("Raid is not available right now.", "Überfall ist gerade nicht verfügbar.");
            Loc.Register(" status. ", " Status. ");
            Loc.Register("Target: ", "Ziel: ");
            Loc.Register("Target chosen. ", "Ziel gewählt. ");
            Loc.Register("No target chosen. ", "Kein Ziel gewählt. ");
            Loc.Register("Helper: ", "Helfer: ");
            Loc.Register("Helper chosen. ", "Helfer gewählt. ");
            Loc.Register("No helper chosen. ", "Kein Helfer gewählt. ");
            Loc.Register("Tab cycles zones. ", "Tab wechselt Bereiche. ");
            Loc.Register("F cycles the helper filter. ", "F wechselt den Helfer-Filter. ");
            Loc.Register("L returns to the raidable list. Up and Down navigate, Left and Right adjust sliders, D describes the focused clan. Enter raids, Escape closes.",
                "L kehrt zur Liste der überfallbaren Klans zurück. Hoch und Runter navigieren, Links und Rechts stellen Schieber ein, D beschreibt den fokussierten Klan. Eingabe überfällt, Escape schließt.");
            // DialogContentReader.ReadRaid entry announcement (Model Y).
            Loc.Register("Raid. ", "Überfall. ");
            Loc.Register(" raidable clans", " überfallbare Klans");
            Loc.Register(" helper clans. ", " helfende Klans. ");
            Loc.Register("Up and Down navigate the raidable clan list, L returns to list. Tab cycles to sliders and helpers. Space selects a target or toggles a helper, D describes a clan. Enter raids, Escape closes.",
                "Hoch und Runter navigieren die Liste der überfallbaren Klans, L kehrt zur Liste zurück. Tab wechselt zu Schiebern und Helfern. Leertaste wählt ein Ziel oder schaltet einen Helfer um, D beschreibt einen Klan. Eingabe überfällt, Escape schließt.");

            // ---- ConfirmGate (two-step Enter confirmation on commit screens) ----
            // Shared by SacrificeNavigator and ReorganizeNavigator. First Enter speaks
            // a per-screen summary plus this hint; second Enter commits.
            Loc.Register("Press Enter again to confirm.", "Noch mal Enter zum Bestätigen.");
            // Shared list-joiner conjunction used by StringHelpers.JoinList.
            Loc.Register("and", "und");

            // ---- SacrificeNavigator (sacrifice dialog) ----
            // Reused keys (registered above): "selected", "Close button not found.",
            // " slider not available.", " of ",
            // ". Left and Right to adjust, Shift for larger steps.", "Goods", "Herds",
            // "Slider", "Unknown", "Blessing".
            Loc.Register("Blessings.", "Segen.");
            Loc.Register("Offerings.", "Opfergaben.");
            Loc.Register("No blessings available.", "Keine Segen verfügbar.");
            // Blessing toggle states (announced right after the name).
            Loc.Register("unavailable", "nicht verfügbar");
            Loc.Register("hidden", "verborgen");
            Loc.Register("locked, not yet learned", "gesperrt, noch nicht erlernt");
            Loc.Register("available", "verfügbar");
            Loc.Register(", no description available.", ", keine Beschreibung verfügbar.");
            // Space (select) feedback.
            Loc.Register("Blessing not available.", "Segen nicht verfügbar.");
            Loc.Register("Blessing not yet learned.", "Segen noch nicht erlernt.");
            Loc.Register(" already selected.", " bereits ausgewählt.");
            Loc.Register(" selected. ", " ausgewählt. ");
            Loc.Register(" selected.", " ausgewählt.");
            // Primary action (Enter) + F5 full status.
            Loc.Register("Cannot sacrifice: ", "Kann nicht opfern: ");
            Loc.Register("Sacrifice dialog. ", "Opfer-Dialog. ");
            Loc.Register("Selected blessing: ", "Gewählter Segen: ");
            Loc.Register("No blessing selected. ", "Kein Segen ausgewählt. ");
            Loc.Register("Goods: ", "Güter: ");
            Loc.Register("Herds: ", "Herden: ");
            Loc.Register("Press Enter to sacrifice. Escape to leave.",
                "Eingabe zum Opfern. Escape zum Verlassen.");
            Loc.Register("Sacrifice disabled: ", "Opfern deaktiviert: ");
            // Disabled-reason fragments.
            Loc.Register("no blessing selected", "kein Segen ausgewählt");
            Loc.Register("no goods or herds pledged yet", "noch keine Güter oder Herden zugesagt");
            Loc.Register("unknown reason", "unbekannter Grund");
            Loc.Register("no one is wounded — this blessing only helps wounded clan members",
                "niemand ist verwundet — dieser Segen hilft nur verwundeten Klan-Mitgliedern");
            Loc.Register("no one is sick — this blessing only helps sick clan members",
                "niemand ist krank — dieser Segen hilft nur kranken Klan-Mitgliedern");
            // DialogContentReader.ReadSacrifice entry announcement (Model Y).
            Loc.Register("Sacrifice. ", "Opfer. ");
            Loc.Register("Tab cycles zones: blessings, sliders. Up and Down navigate the active zone, Space selects a blessing, Left and Right adjust sliders, D reads details. Enter sacrifices, Escape closes.",
                "Tab wechselt Bereiche: Segen, Schieber. Hoch und Runter navigieren den aktiven Bereich, Leertaste wählt einen Segen, Links und Rechts stellen Schieber ein, D liest Details. Eingabe opfert, Escape schließt.");
            // SacrificeNavigator.BuildSacrificeSummary — two-step Enter confirmation.
            Loc.Register("You sacrifice to {0}: {1}", "Du opferst an {0}: {1}");
            Loc.Register("You sacrifice {0}", "Du opferst {0}");
            Loc.Register(" for {0} goods and {1} herds.", " für {0} Güter und {1} Herden.");
            Loc.Register(" for {0} goods.", " für {0} Güter.");
            Loc.Register(" for {0} herds.", " für {0} Herden.");
            Loc.Register("no blessing", "keinen Segen");

            // ---- SpiritNavigator (spirit-bargain dialog) ----
            // Reused keys (registered above): "Blessings.", "No blessings available.",
            // "Blessing not available.", "Blessing not yet learned.",
            // " already selected.", " selected.", "unavailable", "hidden",
            // "selected", "available", "Selected: ", "Blessings: ",
            // "Close button not found.", "Unknown".
            Loc.Register("Bargain approach.", "Handels-Ansatz.");
            Loc.Register("No bargain approaches available.", "Keine Handels-Ansätze verfügbar.");
            Loc.Register(", no description.", ", keine Beschreibung.");
            Loc.Register("Approach not available.", "Ansatz nicht verfügbar.");
            Loc.Register(" is not available right now.", " ist gerade nicht verfügbar.");
            // Blessing / approach states not already registered.
            Loc.Register("not yet learned", "noch nicht erlernt");
            Loc.Register("not available", "nicht verfügbar");
            // Primary action (Enter) + F5 full status.
            Loc.Register("Cannot bargain: ", "Kann nicht handeln: ");
            Loc.Register("Spirit Bargain. ", "Geisterhandel. ");
            Loc.Register("Approaches: ", "Ansätze: ");
            Loc.Register("Press Enter to bargain. Escape to leave.",
                "Eingabe zum Handeln. Escape zum Verlassen.");
            Loc.Register("Bargain disabled: ", "Handeln deaktiviert: ");
            Loc.Register("no bargain approach selected", "kein Handels-Ansatz gewählt");
            Loc.Register("the bargain is not available right now",
                "der Handel ist gerade nicht verfügbar");
            // DialogContentReader.ReadSpirit entry announcement (Model Y).
            Loc.Register("Bargain approaches: ", "Handels-Ansätze: ");
            Loc.Register(" (selected)", " (ausgewählt)");
            Loc.Register(" (not available)", " (nicht verfügbar)");
            Loc.Register("Tab switches between approaches and blessings. Up and Down navigate, Space selects, D reads details. Enter bargains, Escape closes.",
                "Tab wechselt zwischen Ansätzen und Segen. Hoch und Runter navigieren, Leertaste wählt, D liest Details. Eingabe handelt, Escape schließt.");

            // ---- WealthScreenNavigator (wealth management screen) ----
            // Reused keys (registered above): "List is empty.", "(empty row)",
            // "Use Up and Down to pick an item first.", "No items in list.",
            // "Use Ctrl+1 through 9 to switch screens, or Shift+F1 for shortcuts.",
            // ", focused: ", "Chariot", "Hyaloring", "Orlanthi", "near", ", feud",
            // "trading", "visited by emissary", "visited by caravan".
            Loc.Register("Treasures zone. ", "Schätze-Bereich. ");
            Loc.Register(" treasure.", " Schatz.");
            Loc.Register(" treasures.", " Schätze.");
            Loc.Register(" Up and Down to cycle, D for description, Space to view.",
                " Hoch und Runter wechseln, D für Beschreibung, Leertaste zum Ansehen.");
            Loc.Register("Trade partners zone. ", "Handelspartner-Bereich. ");
            Loc.Register(" clan.", " Klan.");
            Loc.Register(" clans.", " Klans.");
            Loc.Register(" Up and Down to cycle, D for clan info, Space selects.",
                " Hoch und Runter wechseln, D für Klan-Info, Leertaste wählt aus.");
            Loc.Register("Selected treasure.", "Schatz ausgewählt.");
            Loc.Register("Selected clan.", "Klan ausgewählt.");
            Loc.Register("No description available for this treasure.",
                "Keine Beschreibung für diesen Schatz verfügbar.");
            Loc.Register("No description available for this clan.",
                "Keine Beschreibung für diesen Klan verfügbar.");
            Loc.Register("Opening Caravan dialog.", "Öffne Karawanen-Dialog.");
            // F5 full status.
            Loc.Register("Wealth status. ", "Wohlstand-Status. ");
            Loc.Register(" treasure, ", " Schatz, ");
            Loc.Register(" treasures, ", " Schätze, ");
            Loc.Register(" trade partner. ", " Handelspartner. ");
            Loc.Register(" trade partners. ", " Handelspartner. ");
            Loc.Register("Treasures zone active", "Schätze-Bereich aktiv");
            Loc.Register("Trade partners zone active", "Handelspartner-Bereich aktiv");
            Loc.Register(". Tab cycles zones, Up and Down cycle items, D for description, ",
                ". Tab wechselt Bereiche, Hoch und Runter wechseln Einträge, D für Beschreibung, ");
            Loc.Register("Space selects, Enter opens Caravan, F5 for status.",
                "Leertaste wählt, Eingabe öffnet die Karawane, F5 für den Status.");
            // F1 shortcut overlay — Wealth block.
            Loc.Register("On Wealth: Tab cycles treasures and trade partners zones, ",
                "Auf Wohlstand: Tab wechselt zwischen Schätze- und Handelspartner-Bereich, ");
            Loc.Register("Up and Down cycle items, D for description, Space activates, ",
                "Hoch und Runter wechseln Einträge, D für Beschreibung, Leertaste aktiviert, ");
            Loc.Register("Enter opens the Caravan dialog. ", "Eingabe öffnet den Karawanen-Dialog. ");

            // ---- Venture dialog (DialogContentReader.ReadVenture + venture-list nav) ----
            // Model Y: Leertaste wählt, Eingabe startet — kein Strg+Eingabe mehr.
            Loc.Register("Venture. ", "Unternehmung. ");
            Loc.Register(" ventures", " Unternehmungen");
            Loc.Register("No ventures available. ", "Keine Unternehmungen verfügbar. ");
            Loc.Register("No ventures available.", "Keine Unternehmungen verfügbar.");
            Loc.Register("Select a venture first.", "Wählt zuerst eine Unternehmung.");
            Loc.Register("No description available.", "Keine Beschreibung verfügbar.");
            Loc.Register(" (running)", " (läuft)");
            Loc.Register("Up and Down cycle ventures (list active by default), Space selects the focused venture, D for description, L returns to list. Enter starts the venture, Escape closes.",
                "Hoch und Runter wechseln Unternehmungen (Liste standardmäßig aktiv), Leertaste wählt die fokussierte Unternehmung, D liest die Beschreibung, L kehrt zur Liste zurück. Eingabe startet die Unternehmung, Escape schließt.");

            // ---- FortifyDialog list selection (KeyboardNavigationHandler.ActivateListItem) ----
            // " selected." reused from the Sacrifice block above.
            Loc.Register(" selected", " ausgewählt");
            Loc.Register(". Build is disabled (insufficient goods or invalid choice)",
                ". Bauen ist deaktiviert (zu wenige Güter oder ungültige Wahl)");
            Loc.Register(". Press Enter to build", ". Eingabe zum Bauen");

            // ---- Lore screen summary (ManagementScreenReader.ReadLoreSummary/ReadLoreFull) ----
            // " history entry, ", " history entries, ", " myth. ", " myths. " and
            // "M opens Manual, F5 for status." reused from the LoreScreenNavigator block.
            Loc.Register("History", "Geschichte");
            Loc.Register("Myths", "Mythen");
            Loc.Register("History: ", "Geschichte: ");
            Loc.Register("Myths: ", "Mythen: ");
            Loc.Register(" and ", " und ");
            Loc.Register(" more", " weitere");
            Loc.Register("Tab cycles history and myths zones, Up and Down cycle entries, ",
                "Tab wechselt Geschichts- und Mythen-Bereich, Hoch und Runter wechseln Einträge, ");
            Loc.Register("Enter opens, M opens Manual.", "Eingabe öffnet, M öffnet das Handbuch.");
            Loc.Register("Tab cycles zones, Up and Down cycle entries, Enter opens, ",
                "Tab wechselt Bereiche, Hoch und Runter wechseln Einträge, Eingabe öffnet, ");

            // ---- Lore reader: Myth / Info / Manual dialogs (LoreReader) ----
            Loc.Register("Lore", "Wissen");
            Loc.Register(" paragraph", " Absatz");
            Loc.Register(" paragraphs", " Absätze");
            Loc.Register(" link", " Link");
            Loc.Register(" links", " Links");
            Loc.Register(" link.", " Link.");
            Loc.Register(" links.", " Links.");
            Loc.Register(" paragraphs total", " Absätze insgesamt");
            Loc.Register(" No readable text. Press Escape to close.",
                " Kein lesbarer Text. Escape zum Schließen.");
            Loc.Register(" No readable text.", " Kein lesbarer Text.");
            Loc.Register(" Link focused: ", " Link im Fokus: ");
            Loc.Register(" Detailed view.", " Detaillierte Ansicht.");
            Loc.Register(" Compact view. D toggles detail.",
                " Kompakte Ansicht. D schaltet Details um.");
            Loc.Register("Switching to compact view.", "Wechsle zur kompakten Ansicht.");
            Loc.Register("Switching to detailed view.", "Wechsle zur detaillierten Ansicht.");
            Loc.Register("No detail view for this topic.", "Keine Detailansicht für dieses Thema.");
            Loc.Register("Start of document.", "Anfang des Dokuments.");
            Loc.Register("End of document.", "Ende des Dokuments.");
            Loc.Register("No further heading.", "Keine weitere Überschrift.");
            Loc.Register("No previous heading.", "Keine vorherige Überschrift.");
            Loc.Register("No links on this page.", "Keine Links auf dieser Seite.");
            Loc.Register("Link ", "Link ");
            Loc.Register(". Enter to follow.", ". Eingabe zum Folgen.");
            Loc.Register("Heading: ", "Überschrift: ");
            Loc.Register("Bullet: ", "Aufzählung: ");
            Loc.Register("Heading", "Überschrift");
            Loc.Register("Bullet", "Aufzählung");
            Loc.Register("Paragraph", "Absatz");
            Loc.Register("Up and Down to read paragraphs, Ctrl plus arrows for headings, Tab for links, Enter to follow, M reads everything, L lists links",
                "Hoch und Runter lesen Absätze, Strg plus Pfeile für Überschriften, Tab für Links, Eingabe zum Folgen, M liest alles, L listet Links");
            Loc.Register(", D toggles detail", ", D schaltet Details um");
            Loc.Register(", Escape closes.", ", Escape schließt.");
            // Dialog-level action buttons surfaced as virtual nodes past the last
            // paragraph — Intro exposes Tutorial/Manual/Close, Manual just Close.
            Loc.Register(" action after the text", " Aktion nach dem Text");
            Loc.Register(" actions after the text", " Aktionen nach dem Text");
            Loc.Register("Action: ", "Aktion: ");
            Loc.Register(". Press Enter to activate.", ". Eingabe zum Aktivieren.");
            Loc.Register(" Action focused: ", " Aktion im Fokus: ");
            Loc.Register("Start tutorial", "Tutorial starten");
            Loc.Register("Open manual", "Handbuch öffnen");
            Loc.Register("Close", "Schließen");
            Loc.Register("Intro", "Intro");
            Loc.Register("Manual", "Handbuch");

            // ---- Caravan dialog (CaravanNavigator + DialogContentReader.ReadCaravan) ----
            // Reused from earlier blocks: "on"/"off", " of ", ", trade partner",
            // ", feud", "Choose Leader is not available.",
            // ". Left and Right to adjust, Shift for larger steps.",
            // ". Left and Right to change.".
            // Zone headers.
            Loc.Register("Recipient clan.", "Empfänger-Klan.");
            Loc.Register("Trade mode.", "Handelsmodus.");
            Loc.Register("Goods to trade.", "Handelswaren.");
            Loc.Register("Treasure list.", "Schatzliste.");
            Loc.Register("Warriors and caravan size.", "Krieger und Karawanengröße.");
            // Clan list zone.
            Loc.Register("No clans available.", "Keine Klans verfügbar.");
            Loc.Register(", selected", ", ausgewählt");
            Loc.Register(" selected. ", " ausgewählt. ");
            Loc.Register("Already trading with this clan — Establish Route is locked, goods and treasures still work.",
                "Mit diesem Klan wird bereits Handel getrieben — Handelsroute einrichten ist gesperrt, Güter und Schätze gehen weiterhin.");
            Loc.Register("No existing trade — Establish Route is available.",
                "Kein bestehender Handel — Handelsroute einrichten ist verfügbar.");
            Loc.Register(" This clan has no horses — Buy Horses and Sell Horses are unavailable.",
                " Dieser Klan hat keine Pferde — Pferde kaufen und Pferde verkaufen sind nicht verfügbar.");
            Loc.Register("Unknown clan", "Unbekannter Klan");
            Loc.Register("Clan", "Klan");
            // Mode + goods toggles.
            Loc.Register("(toggle not available)", "(Schalter nicht verfügbar)");
            Loc.Register(", locked", ", gesperrt");
            Loc.Register(", locked. ", ", gesperrt. ");
            Loc.Register(", not available.", ", nicht verfügbar.");
            Loc.Register(", not available", ", nicht verfügbar");
            Loc.Register(", unavailable", ", nicht verfügbar");
            Loc.Register("Buy", "Kaufen");
            Loc.Register("Sell", "Verkaufen");
            Loc.Register("Auto-selected treasure: ", "Automatisch gewählter Schatz: ");
            Loc.Register("This is a treasure deal or trade route — cannot mix with goods. Turn off treasure or trade route first.",
                "Das ist ein Schatz-Geschäft oder eine Handelsroute — kann nicht mit Gütern gemischt werden. Schalte zuerst Schatz oder Handelsroute aus.");
            Loc.Register("We do not need to buy food right now.", "Wir müssen gerade keine Nahrung kaufen.");
            Loc.Register("We cannot sell food right now.", "Wir können gerade keine Nahrung verkaufen.");
            Loc.Register("We have fewer than 20 goods to sell.", "Wir haben weniger als 20 Güter zum Verkaufen.");
            Loc.Register("We have 100 herds or fewer — too few to sell.",
                "Wir haben 100 Herden oder weniger — zu wenige zum Verkaufen.");
            Loc.Register("The recipient clan has no horses.", "Der Empfänger-Klan hat keine Pferde.");
            Loc.Register("Our own horses are scant — cannot sell.", "Unsere eigenen Pferde sind knapp — Verkauf nicht möglich.");
            Loc.Register("We have no treasures to sell.", "Wir haben keine Schätze zum Verkaufen.");
            // Treasure zone.
            Loc.Register("No treasures.", "Keine Schätze.");
            Loc.Register("Unknown", "Unbekannt");
            Loc.Register("Treasure selected.", "Schatz ausgewählt.");
            // Escort zone.
            Loc.Register("(toggle group not available)", "(Schaltergruppe nicht verfügbar)");
            Loc.Register("None selected. Left and Right to change.", "Keine Auswahl. Links und Rechts zum Ändern.");
            // Leader zone.
            Loc.Register("Caravan leader. ", "Karawanen-Anführer. ");
            Loc.Register(". Press Space to change. D for personal info.",
                ". Leertaste zum Wechseln. D für persönliche Informationen.");
            Loc.Register("No leader chosen. Press Space to pick one.",
                "Kein Anführer gewählt. Leertaste zum Auswählen.");
            // Trade routes (R key).
            Loc.Register("No active trade routes.", "Keine aktiven Handelsrouten.");
            Loc.Register(" trade route: ", " Handelsroute: ");
            Loc.Register(" trade routes: ", " Handelsrouten: ");
            Loc.Register("Could not read trade routes.", "Handelsrouten konnten nicht gelesen werden.");
            // Send / close.
            Loc.Register("Send is disabled. Pick a recipient clan first.",
                "Senden ist deaktiviert. Wählt zuerst einen Empfänger-Klan.");
            Loc.Register("Send is disabled. Pick a leader first.",
                "Senden ist deaktiviert. Wählt zuerst einen Anführer.");
            Loc.Register("Send is disabled. Set warrior escort first — at least two warriors needed in total.",
                "Senden ist deaktiviert. Legt zuerst die Krieger-Eskorte fest — mindestens zwei Krieger insgesamt nötig.");
            Loc.Register("Send is disabled.", "Senden ist deaktiviert.");
            Loc.Register("Send is disabled. Pick something to buy from the goods zone, or switch to a treasure deal or trade route.",
                "Senden ist deaktiviert. Wählt im Waren-Bereich etwas zum Kaufen, oder wechselt zu einem Schatz-Geschäft oder einer Handelsroute.");
            Loc.Register("Send is disabled. Pick something to sell from the goods zone, or switch to a treasure deal or trade route.",
                "Senden ist deaktiviert. Wählt im Waren-Bereich etwas zum Verkaufen, oder wechselt zu einem Schatz-Geschäft oder einer Handelsroute.");
            Loc.Register("Send is disabled. Pick a treasure from the treasure list, or switch to a different deal.",
                "Senden ist deaktiviert. Wählt einen Schatz aus der Schatzliste, oder wechselt zu einem anderen Geschäft.");
            Loc.Register("Send is disabled. Pick a deal: a sell and a buy commodity, a treasure trade, or establish a trade route.",
                "Senden ist deaktiviert. Wählt ein Geschäft: eine Ware zum Verkaufen und eine zum Kaufen, einen Schatz-Handel, oder richtet eine Handelsroute ein.");
            Loc.Register("Send is disabled. Pick a deal: a sell and a buy commodity, or a treasure trade.",
                "Senden ist deaktiviert. Wählt ein Geschäft: eine Ware zum Verkaufen und eine zum Kaufen, oder einen Schatz-Handel.");
            Loc.Register("Close button not found.", "Schließen-Schaltfläche nicht gefunden.");
            // F5 full status.
            Loc.Register("Caravan dialog. ", "Karawanen-Dialog. ");
            Loc.Register("Recipient: ", "Empfänger: ");
            Loc.Register("No recipient. ", "Kein Empfänger. ");
            Loc.Register(" on. ", " an. ");
            Loc.Register(" off. ", " aus. ");
            Loc.Register("Leader: ", "Anführer: ");
            Loc.Register("No leader. ", "Kein Anführer. ");
            Loc.Register("no goods chosen. ", "keine Waren gewählt. ");
            Loc.Register("Press Enter to send. Escape to leave.", "Eingabe zum Senden. Escape zum Verlassen.");
            // DialogContentReader.ReadCaravan.
            Loc.Register("Caravan. ", "Karawane. ");
            Loc.Register(" clans", " Klans");
            Loc.Register("Buy: ", "Kaufen: ");
            Loc.Register(". Sell: ", ". Verkaufen: ");
            Loc.Register(" treasures. ", " Schätze. ");
            Loc.Register("Tab cycles zones: clan list, mode, goods, treasures, escort, leader. L returns to the clan list. Up and Down navigate, Left and Right adjust sliders. Space selects or toggles the focused element, D reads details, R reads active trade routes. Enter sends, Escape closes.",
                "Tab wechselt Bereiche: Klanliste, Modus, Waren, Schätze, Eskorte, Anführer. L kehrt zur Klanliste zurück. Hoch und Runter navigieren, Links und Rechts stellen Schieber ein. Leertaste wählt oder schaltet das fokussierte Element um, D liest Details, R liest aktive Handelsrouten. Eingabe sendet, Escape schließt.");

            // ---- Map screen (MapScreenNavigator) ----
            // Reused keys (registered in earlier blocks, not repeated here):
            // "Goal: ", " of ", ". Left and Right to change.", ", trade partner",
            // "Swords", "Bows", "No description available.", "Filter: ",
            // " entries.", "Leader.", "Leader: ", " selected.", "No leader chosen. ".
            // Send / explore / close actions.
            Loc.Register("Send is not available yet. ", "Senden ist noch nicht verfügbar. ");
            Loc.Register("Could not open the explore panel.", "Erkundungs-Panel konnte nicht geöffnet werden.");
            Loc.Register("Could not close the dialog.", "Der Dialog konnte nicht geschlossen werden.");
            // Zone headers.
            Loc.Register("Mission goal.", "Missionsziel.");
            Loc.Register("Escort.", "Eskorte.");
            Loc.Register("Destination list. Filter ", "Zielliste. Filter ");
            Loc.Register(", sort ", ", Sortierung ");
            Loc.Register(". F filters, O orders.", ". F filtert, O sortiert.");
            Loc.Register("Hex cursor. Arrows move one hex, Shift+arrow five hexes. Page Up and Down jump between points of interest, Ctrl with them switches category. Home jumps to your clan, D describes, Space sets destination.",
                "Hex-Cursor. Pfeile bewegen ein Hex, Umschalt plus Pfeil fünf Hex. Bild-auf und Bild-ab springen zwischen wichtigen Orten, Strg dazu wechselt die Kategorie. Pos1 springt zu eurem Klan, D beschreibt, Leertaste setzt das Reiseziel.");
            // Filter names.
            Loc.Register("all", "alle");
            Loc.Register("clans only", "nur Klans");
            Loc.Register("our tribe", "unser Stamm");
            Loc.Register("feuds", "Fehden");
            Loc.Register("landmarks", "Wahrzeichen");
            // Sort modes.
            Loc.Register("default", "Standard");
            Loc.Register("by distance", "nach Entfernung");
            Loc.Register("alphabetical", "alphabetisch");
            // Goals zone.
            Loc.Register("No goals available.", "Keine Ziele verfügbar.");
            Loc.Register(", currently selected", ", aktuell ausgewählt");
            Loc.Register(". Press D for description.", ". Drückt D für die Beschreibung.");
            Loc.Register("No description for this goal.", "Keine Beschreibung für dieses Ziel.");
            // Sliders zone.
            Loc.Register("Slider not available.", "Schieber nicht verfügbar.");
            // Leader zone.
            Loc.Register("Choose Leader is not available right now.",
                "Anführerwahl ist gerade nicht verfügbar.");
            Loc.Register("No leader chosen yet. Press Space to choose, D for details.",
                "Noch kein Anführer gewählt. Leertaste zum Wählen, D für Details.");
            Loc.Register("Person ", "Person ");
            Loc.Register(". Space opens the picker, D reads full details.",
                ". Leertaste öffnet die Auswahl, D liest die vollen Details.");
            Loc.Register("No leader chosen yet.", "Noch kein Anführer gewählt.");
            Loc.Register("Could not read leader details.", "Anführer-Details konnten nicht gelesen werden.");
            // Destination zone.
            Loc.Register("No description available for this map zone.",
                "Keine Beschreibung für diesen Kartenbereich verfügbar.");
            Loc.Register("No description available for ", "Keine Beschreibung verfügbar für ");
            Loc.Register("Could not read clan description.", "Klan-Beschreibung konnte nicht gelesen werden.");
            Loc.Register("Sort: ", "Sortierung: ");
            // Destination entry names + suffixes.
            Loc.Register("Home (", "Heimat (");
            Loc.Register(" (tribe)", " (Stamm)");
            Loc.Register(" (feud)", " (Fehde)");
            Loc.Register(" (trade partner)", " (Handelspartner)");
            Loc.Register("Abandoned land", "Verlassenes Land");
            Loc.Register("Dunes", "Dünen");
            Loc.Register("Gorp territory", "Gorp-Gebiet");
            Loc.Register("Chaos Nest", "Chaos-Nest");
            Loc.Register("No destinations match the current filter. Press F to widen.",
                "Keine Reiseziele entsprechen dem aktuellen Filter. Drückt F zum Erweitern.");
            Loc.Register(", currently targeted", ", aktuell anvisiert");
            Loc.Register(" miles", " Meilen");
            Loc.Register(". Press Space to set destination.", ". Leertaste setzt das Reiseziel.");
            // Destination activation.
            Loc.Register("Could not set destination.", "Reiseziel konnte nicht gesetzt werden.");
            Loc.Register(" not accepted: ", " nicht angenommen: ");
            Loc.Register("Send is now enabled.", "Senden ist jetzt möglich.");
            Loc.Register("Send is still disabled. ", "Senden ist weiterhin deaktiviert. ");
            Loc.Register(" Hex cursor at ", " Hex-Cursor bei ");
            Loc.Register(" Note: ", " Hinweis: ");
            Loc.Register(" — mission will resolve as foray at home.",
                " — die Mission wird als Vorstoß in der Heimat aufgelöst.");
            Loc.Register("Destination set to ", "Reiseziel gesetzt auf ");
            Loc.Register("Destination set to home.", "Reiseziel auf die Heimat gesetzt.");
            Loc.Register(" selected on map.", " auf der Karte gewählt.");
            // Hex cursor zone.
            Loc.Register("Home location unknown.", "Heimatort unbekannt.");
            Loc.Register("Could not jump to home.", "Sprung zur Heimat nicht möglich.");
            Loc.Register("No description available for this hex.",
                "Keine Beschreibung für dieses Hex verfügbar.");
            Loc.Register("Hex cursor not initialised.", "Hex-Cursor nicht initialisiert.");
            // Hex POI filter (PgUp/PgDn within HexCursor zone).
            Loc.Register("Category {0}, {1} entries. ", "Kategorie {0}, {1} Einträge. ");
            Loc.Register("{0} of {1}. ", "{0} von {1}. ");
            Loc.Register("No points of interest found on this map.",
                "Keine wichtigen Orte auf dieser Karte gefunden.");
            // POI category names.
            Loc.Register("all points of interest", "alle wichtigen Orte");
            Loc.Register("known clans", "bekannte Klans");
            Loc.Register("trade partners", "Handelspartner");
            Loc.Register("chaos and gorp", "Chaos und Gorp");
            Loc.Register("wildlands and dunes", "Wildland und Dünen");
            Loc.Register("named zones", "benannte Zonen");
            Loc.Register("active missions", "aktive Missionen");
            Loc.Register("exploration frontier", "Erkundungsgrenze");
            Loc.Register("dead hexes", "tote Hexe");
            Loc.Register("mentioned hexes", "erwähnte Hexe");
            // POI labels (shown after "N of M. ").
            Loc.Register("active raid", "aktiver Überfall");
            Loc.Register("active emissary", "aktiver Gesandter");
            Loc.Register("active trade mission", "aktive Handelsmission");
            Loc.Register("active war party", "aktive Kriegspartei");
            Loc.Register("active exploration", "aktive Erkundung");
            Loc.Register("active cattle raid", "aktiver Viehraub");
            Loc.Register("active honor raid", "aktiver Ehrenraub");
            Loc.Register("active mission", "aktive Mission");
            Loc.Register("frontier", "Grenze");
            Loc.Register("dead hex", "totes Hex");
            Loc.Register("mentioned", "erwähnt");
            Loc.Register("Dead — no longer exists. ", "Tot — existiert nicht mehr. ");
            Loc.Register("Inaccessible. ", "Unzugänglich. ");
            Loc.Register("Unexplored. ", "Unerkundet. ");
            Loc.Register(" miles from home. ", " Meilen von der Heimat entfernt. ");
            Loc.Register("Space to set destination.", "Leertaste setzt das Reiseziel.");
            // Hex contents description.
            Loc.Register("Home, ", "Heimat, ");
            Loc.Register(", in feud", ", in Fehde");
            // Wild-zone description.
            Loc.Register(" river crossing — currently wild and dangerous",
                " Flussübergang — derzeit wild und gefährlich");
            Loc.Register(" river crossing", " Flussübergang");
            Loc.Register("mountainous ", "gebirgige ");
            Loc.Register("hilly ", "hügelige ");
            Loc.Register("wilderness ", "Wildnis ");
            Loc.Register("river", "Fluss");
            Loc.Register("wilderness", "Wildnis");
            Loc.Register(", difficult terrain", ", schwieriges Gelände");
            // Direction hints (ParseDirectionFromCode). River names kept as proper names.
            Loc.Register("north of Black Eel river", "nördlich des Black-Eel-Flusses");
            Loc.Register("south of Black Eel river", "südlich des Black-Eel-Flusses");
            Loc.Register("north of Oslira river", "nördlich des Oslira-Flusses");
            Loc.Register("south of Oslira river", "südlich des Oslira-Flusses");
            Loc.Register("north of Forantin river", "nördlich des Forantin-Flusses");
            Loc.Register("south of Forantin river", "südlich des Forantin-Flusses");
            // Hex activation.
            Loc.Register("Could not set destination at this hex.",
                "Reiseziel konnte an diesem Hex nicht gesetzt werden.");
            Loc.Register("Hex ", "Hex ");
            Loc.Register(" set as destination.", " als Reiseziel gesetzt.");
            Loc.Register(" Found in list: ", " In der Liste gefunden: ");
            // Destination validity reasons (CheckDestinationValidity).
            Loc.Register("this area no longer exists", "dieses Gebiet existiert nicht mehr");
            Loc.Register("exploration must be near known lands",
                "Erkundung muss in der Nähe bekannter Länder liegen");
            Loc.Register("foraging needs a hex inside known lands (already explored)",
                "Nahrungssuche braucht ein Hex innerhalb bekannter Länder (bereits erkundet)");
            Loc.Register("horse expedition must be inside known lands",
                "Pferde-Expedition muss innerhalb bekannter Länder liegen");
            Loc.Register("spirit expedition must be inside known lands",
                "Geister-Expedition muss innerhalb bekannter Länder liegen");
            Loc.Register("treasure search must be near known lands and not at home",
                "Schatzsuche muss in der Nähe bekannter Länder und nicht in der Heimat liegen");
            Loc.Register("destination not allowed for this goal",
                "Reiseziel für dieses Ziel nicht erlaubt");
            Loc.Register("foraging cannot exceed 50 miles, this destination is ",
                "Nahrungssuche darf 50 Meilen nicht überschreiten, dieses Reiseziel ist ");
            Loc.Register(" miles away", " Meilen entfernt");
            // At-home downgrade reasons (GetAtHomeDowngradeReason).
            Loc.Register("cursor has not been moved yet", "der Cursor wurde noch nicht bewegt");
            Loc.Register("our own clan is selected as destination",
                "unser eigener Klan ist als Reiseziel gewählt");
            Loc.Register("cursor sits inside our own clan territory",
                "der Cursor liegt in unserem eigenen Klan-Gebiet");
            // Send-disabled reasons (WhySendDisabled).
            Loc.Register("No leader chosen.", "Kein Anführer gewählt.");
            Loc.Register("Not enough horses. We have ", "Nicht genug Pferde. Wir haben ");
            Loc.Register(" but need ", " brauchen aber ");
            Loc.Register(" (escort plus leader). Lower swords or bows.",
                " (Eskorte plus Anführer). Verringert Schwerter oder Bögen.");
            Loc.Register("Choose at least two warriors.", "Wählt mindestens zwei Krieger.");
            Loc.Register("Destination is invalid: ", "Reiseziel ist ungültig: ");
            Loc.Register("Capture-horses requires the Horsebreaker blessing or full Golden Daughters knowledge.",
                "Pferdefang erfordert den Segen Pferdebändiger oder volles Wissen der Goldenen Töchter.");
            Loc.Register("Choose a destination first.", "Wählt zuerst ein Reiseziel.");
            Loc.Register("Destination not allowed for this goal.", "Reiseziel für dieses Ziel nicht erlaubt.");
            // F5 full status.
            Loc.Register("Map. ", "Karte. ");
            Loc.Register("Swords ", "Schwerter ");
            Loc.Register(", Bows ", ", Bögen ");
            Loc.Register("Horses available: ", "Verfügbare Pferde: ");
            Loc.Register(" (escort plus leader cannot exceed this). ",
                " (Eskorte plus Anführer darf das nicht überschreiten). ");
            Loc.Register("Destination: home. ", "Reiseziel: Heimat. ");
            Loc.Register("Destination: ", "Reiseziel: ");
            Loc.Register("Destination not yet chosen. ", "Reiseziel noch nicht gewählt. ");
            Loc.Register("Distance to cursor: ", "Entfernung zum Cursor: ");
            Loc.Register(" — over the 50 mile foraging limit",
                " — über dem 50-Meilen-Limit der Nahrungssuche");
            Loc.Register("Press Enter to send. X opens explore panel. Escape cancels.",
                "Eingabe zum Senden. X öffnet das Erkundungs-Panel. Escape bricht ab.");
            Loc.Register("Send is disabled. ", "Senden ist deaktiviert. ");
            Loc.Register(" — mission will resolve as foray at home if sent now.",
                " — die Mission wird als Vorstoß in der Heimat aufgelöst, wenn jetzt gesendet wird.");

            // ---- Combat / battle (CombatScreenReader + battle-objective toggle) ----
            // The combat reader assembles status lines fragment-by-fragment, so each
            // fragment is registered on its own. Numeric values are appended by the
            // caller between fragments (e.g. "13" + " elite" -> "13 Elite").
            // Combat state labels (DescribeState). "Disengaged" / "In contact" form
            // a deliberate pair ("Nicht in Kontakt" / "In Kontakt").
            Loc.Register("Disengaged", "Nicht in Kontakt");
            Loc.Register("In contact", "In Kontakt");
            Loc.Register("Melee", "Nahkampf");
            Loc.Register("Battle final", "Schlacht beendet");
            Loc.Register("Parley proposed", "Verhandlung angeboten");
            Loc.Register("Parleying", "Verhandlung läuft");
            // Enemy stereotype (DescribeEnemyKind). Vingkotling / Hyaloring / Alkothi
            // are proper names — left to fall through to the key unchanged.
            Loc.Register("Charioteers", "Wagenfahrer");
            Loc.Register("Elves", "Elfen");
            Loc.Register("Dwarves", "Zwerge");
            Loc.Register("Trolls", "Trolle");
            // Battle context (BuildBattleContext).
            Loc.Register("We are attacking. ", "Wir greifen an. ");
            Loc.Register("We are defending. ", "Wir verteidigen. ");
            Loc.Register("Enemy: ", "Feind: ");
            Loc.Register("Cattle raid: herds at stake. ", "Viehraub: Herden stehen auf dem Spiel. ");
            Loc.Register("Defender is fortified. ", "Der Verteidiger ist befestigt. ");
            Loc.Register("Defender has no fortifications. ", "Der Verteidiger hat keine Befestigungen. ");
            // Compact / full status (BuildCompactStatus, BuildFullStatus).
            Loc.Register("Combat status. ", "Kampf-Status. ");
            Loc.Register("Us: ", "Wir: ");
            Loc.Register("Enemy ", "Feind ");
            Loc.Register(" elite", " Elite");
            Loc.Register(" elite, ", " Elite, ");
            Loc.Register(" regular", " Reguläre");
            Loc.Register(" plus ", " plus ");
            Loc.Register(" helper elite", " Elite-Helfer");
            Loc.Register(" helper regular", " reguläre Helfer");
            Loc.Register(", reserves available", ", Reserven verfügbar");
            Loc.Register(" Victory points: us ", " Siegpunkte: wir ");
            Loc.Register(", enemy ", ", Feind ");
            Loc.Register(", need ", ", braucht ");
            Loc.Register(", needed ", ", gebraucht ");
            // Outcome + degree of success (AppendOutcome, DescribeDegreeOfSuccess).
            Loc.Register(" Outcome: victory.", " Ausgang: Sieg.");
            Loc.Register(" Outcome: defeat.", " Ausgang: Niederlage.");
            Loc.Register(" Outcome: draw.", " Ausgang: Unentschieden.");
            Loc.Register(" Outcome: we paid tribute.", " Ausgang: Wir haben Tribut gezahlt.");
            Loc.Register(" Outcome: enemy paid tribute.", " Ausgang: Der Feind hat Tribut gezahlt.");
            Loc.Register("Degree of success: ", "Erfolgsgrad: ");
            // Combat start (AnnounceCombatStart).
            Loc.Register("Combat begins", "Kampf beginnt");
            Loc.Register(" against ", " gegen ");
            Loc.Register("Heroic combat: send a champion to fight alone. ",
                "Heldenkampf: schickt einen Vorkämpfer, der allein kämpft. ");
            // Battle setup (AnnounceBattleSetup + DescribeObjectives).
            Loc.Register("Battle setup. ", "Schlacht-Vorbereitung. ");
            Loc.Register("Choose magic to spend with the slider. ",
                "Wählt mit dem Schieber, wie viel Magie ihr einsetzt. ");
            Loc.Register(" battle treasure", " Schlacht-Schatz");
            Loc.Register(" battle treasures", " Schlacht-Schätze");
            Loc.Register(" available — press L to enter the list, then arrow keys, Enter to select. ",
                " verfügbar — drückt L für die Liste, dann Pfeiltasten, Eingabe zum Auswählen. ");
            Loc.Register("Then choose the objective: ", "Wählt dann das Ziel: ");
            Loc.Register("Activate Proceed to begin combat.", "Aktiviert Weiter, um den Kampf zu beginnen.");
            Loc.Register("drive them off, kill as many as possible, or survival. ",
                "sie vertreiben, so viele wie möglich töten, oder Überleben. ");
            Loc.Register("honor, or survival. ", "Ehre, oder Überleben. ");
            Loc.Register("plunder, kill as many as possible, or survival. ",
                "plündern, so viele wie möglich töten, oder Überleben. ");
            // Battle objective radio-button labels (BattleController.SetupBattleScreen).
            // These are baked into the Unity toggle as raw strings — they never flow through
            // PluginImport, so the DE translation mod can't reach them. Register here so the
            // KeyboardNav UIToggle path and the "stays on" radio feedback both speak German.
            Loc.Register("Drive them off", "Sie vertreiben");
            Loc.Register("Kill as many as possible", "So viele wie möglich töten");
            Loc.Register("Survival", "Überleben");
            Loc.Register("Honor", "Ehre");
            Loc.Register("Plunder", "Plündern");
            // Combat-option context (AnnounceCombatOptionContext).
            Loc.Register("Heroic combat. Our champion: ", "Heldenkampf. Unser Vorkämpfer: ");
            Loc.Register("Heroic combat. ", "Heldenkampf. ");
            Loc.Register("Risk warning. Caution on: ", "Risiko-Warnung. Vorsicht bei: ");
            Loc.Register("Risk: ", "Risiko: ");
            // F10 during the setup phase (AnnounceFullStatus).
            Loc.Register("Combat not yet engaged. Adjust the Magic slider, choose a battle treasure if any, choose an objective, then activate Proceed.",
                "Kampf noch nicht begonnen. Stellt den Magie-Schieber ein, wählt gegebenenfalls einen Schlacht-Schatz, wählt ein Ziel und aktiviert dann Weiter.");
            // Battle-results casualty table (AnnounceBattleResults).
            Loc.Register("Casualties. ", "Verluste. ");
            Loc.Register(" elite killed, ", " Elite getötet, ");
            Loc.Register(" wounded; ", " verwundet; ");
            Loc.Register(" regular killed, ", " Reguläre getötet, ");
            Loc.Register(" wounded.", " verwundet.");
            Loc.Register(" No casualties on either side.", " Keine Verluste auf beiden Seiten.");
            // Battle-objective toggle, radio no-op feedback (KeyboardNavigationHandler).
            Loc.Register(" stays on. This is a radio option — to switch, press Tab to find a different option in the same group, then Space there.",
                " bleibt an. Das ist eine Radio-Option — zum Wechseln drückt Tab, sucht in derselben Gruppe eine andere Option und drückt dort Leertaste.");

            // ---- DialogContentReader (dialog summaries + shared helpers) ----
            // Reused keys (registered above): "Treasures", "Choose Leader. ",
            // " candidates", " of ", " healthy", "on", "off", " (not available)",
            // " (selected)", " and ", " more", "Amount", "Leader: ".
            Loc.Register("Scene Info. ", "Szenen-Info. ");
            Loc.Register("Horses: ", "Pferde: ");
            Loc.Register("Food: ", "Nahrung: ");
            Loc.Register("Clans", "Klans");
            Loc.Register("No data available.", "Keine Daten verfügbar.");
            Loc.Register("Dashboard concerns are only available on management screens.",
                "Dashboard-Belange sind nur auf Verwaltungs-Bildschirmen verfügbar.");
            Loc.Register("Could not read concerns.", "Belange konnten nicht gelesen werden.");
            Loc.Register("No candidates. ", "Keine Kandidaten. ");
            Loc.Register("View only — the leader is chosen automatically. Use arrow keys to browse, F sorts by skill, Escape to return.",
                "Nur Ansicht — der Anführer wird automatisch gewählt. Mit den Pfeiltasten blättern, F sortiert nach Fähigkeit, Escape kehrt zurück.");
            Loc.Register("Use arrow keys to browse, F sorts by skill, Space selects a candidate, Enter chooses.",
                "Mit den Pfeiltasten blättern, F sortiert nach Fähigkeit, Leertaste wählt einen Kandidaten, Eingabe bestätigt.");
            // Ritual dialog.
            Loc.Register("Ritual. ", "Ritual. ");
            Loc.Register(" rituals", " Rituale");
            Loc.Register("No rituals available. ", "Keine Rituale verfügbar. ");
            Loc.Register("Up and Down navigate the ritual list, Space selects the ritual, L for list. Enter performs the ritual, Escape closes.",
                "Hoch und Runter navigieren die Ritual-Liste, Leertaste wählt das Ritual, L für die Liste. Eingabe führt das Ritual durch, Escape schließt.");
            // Build (temple) dialog.
            Loc.Register("Build Temple. ", "Tempel bauen. ");
            Loc.Register("All tiers: ", "Alle Stufen: ");
            Loc.Register("Tab cycles the buttons, Space activates the focused one. Enter builds the temple, Escape closes.",
                "Tab wechselt durch die Knöpfe, Leertaste aktiviert den fokussierten. Eingabe baut den Tempel, Escape schließt.");
            Loc.Register("Temple is damaged: ", "Tempel ist beschädigt: ");
            Loc.Register(" hit points. ", " Trefferpunkten. ");
            Loc.Register("Build", "Bauen");
            Loc.Register("Reduce", "Verkleinern");
            Loc.Register(" (disabled)", " (deaktiviert)");
            Loc.Register(" to ", " zu ");
            Loc.Register(" to nothing — removes the shrine. ", " zu nichts — entfernt den Schrein. ");
            Loc.Register("cost ", "Kosten ");
            Loc.Register(" herds, ", " Herden, ");
            Loc.Register(" blessings, ", " Segen, ");
            Loc.Register("maintenance ", "Wartung ");
            Loc.Register(" per year. ", " pro Jahr. ");
            Loc.Register("shrine", "Schrein");
            Loc.Register("temple", "Tempel");
            Loc.Register("great temple", "großer Tempel");
            Loc.Register("tier ", "Stufe ");
            // Warriors dialog (DialogContentReader.ReadWarriors).
            Loc.Register("Warriors. ", "Krieger. ");
            Loc.Register("Offer gifts", "Geschenke anbieten");
            Loc.Register("Severance pay", "Abfindung");
            Loc.Register("Establish route", "Handelsroute einrichten");
            Loc.Register("Mode: ", "Modus: ");
            Loc.Register("Left and Right adjust the slider, Tab cycles toggles, Space flips the focused toggle. Enter recruits or dismisses, Escape closes.",
                "Links und Rechts stellen den Schieber ein, Tab wechselt durch die Schalter, Leertaste schaltet den fokussierten Schalter um. Eingabe rekrutiert oder entlässt, Escape schließt.");
            // Fortify dialog.
            Loc.Register("Fortify. ", "Befestigen. ");
            Loc.Register(" buildable", " baubare");
            Loc.Register(" existing", " bestehende");
            Loc.Register("Cost: ", "Kosten: ");
            Loc.Register("Build available. ", "Bauen verfügbar. ");
            Loc.Register("Build disabled. ", "Bauen deaktiviert. ");
            Loc.Register("Up and Down navigate buildable fortifications, Space picks one, L returns to list, Tab switches to existing fortifications. Cost is announced when you pick one. Enter builds, Escape closes.",
                "Hoch und Runter navigieren die baubaren Befestigungen, Leertaste wählt eine, L kehrt zur Liste zurück, Tab wechselt zu den bestehenden Befestigungen. Die Kosten werden angesagt, wenn ihr eine wählt. Eingabe baut, Escape schließt.");
            // FortifyNavigator runtime strings (Buildable / Existing zones, summary).
            Loc.Register("No buildable fortifications.", "Keine baubaren Befestigungen.");
            Loc.Register("No existing fortifications.", "Keine bestehenden Befestigungen.");
            Loc.Register("unknown fortification", "unbekannte Befestigung");
            Loc.Register("no cost shown", "keine Kosten angezeigt");
            Loc.Register(". Build is disabled (insufficient goods or invalid choice).",
                ". Bauen ist deaktiviert (zu wenige Güter oder ungültige Wahl).");
            Loc.Register(". Press Enter to build.", ". Enter zum Bauen.");
            Loc.Register("Existing fortifications are read-only. Press Tab to switch to buildable.",
                "Bestehende Befestigungen sind nur zur Ansicht. Mit Tab zu den baubaren wechseln.");
            Loc.Register("Buildable fortifications, ", "Baubare Befestigungen, ");
            Loc.Register("Existing fortifications, ", "Bestehende Befestigungen, ");
            Loc.Register("Cannot fortify yet. Pick a buildable fortification you can afford.",
                "Befestigen noch nicht möglich. Wählt eine baubare Befestigung, die ihr euch leisten könnt.");
            Loc.Register("a fortification", "eine Befestigung");
            Loc.Register("You build {0}.", "Du baust {0}.");
            Loc.Register("You build {0}. {1}.", "Du baust {0}. {1}.");
            Loc.Register("Fortify status. ", "Befestigen-Status. ");
            Loc.Register(" buildable, ", " baubare, ");
            Loc.Register(" existing. ", " bestehende. ");
            Loc.Register("Up and Down browse, Tab switches zones, L returns to buildable, D re-reads, Enter builds, Escape closes.",
                "Hoch und Runter durchblättern, Tab wechselt Bereiche, L kehrt zu den baubaren zurück, D liest erneut, Eingabe baut, Escape schließt.");
            // Shared helper fragments.
            Loc.Register("Options: ", "Optionen: ");
            Loc.Register("Treasure", "Schatz");

            // ---- ManagementScreenReader (War / Wealth / Magic-full / Saga) ----
            // Reused keys (registered above): " healthy", "sick", "wounded",
            // " treasure, ", " treasures, ", " trade partner. ", " trade partners. ",
            // "Treasures", "Blessings: ", "rewards", "entries",
            // "No rewards earned yet.", "Restores used ", " of ", " and ", " more".
            Loc.Register("Elite warriors: ", "Elite-Krieger: ");
            Loc.Register(". Regular warriors: ", ". Reguläre Krieger: ");
            Loc.Register("Regular warriors: ", "Reguläre Krieger: ");
            Loc.Register(" fortifications", " Befestigungen");
            Loc.Register("absent", "abwesend");
            Loc.Register("Fortifications", "Befestigungen");
            Loc.Register("Raided by", "Überfallen von");
            Loc.Register("We raided", "Wir überfielen");
            // Wealth screen summary + full.
            Loc.Register("Cattle: ", "Vieh: ");
            Loc.Register(", Goats: ", ", Ziegen: ");
            Loc.Register(", Horses: ", ", Pferde: ");
            Loc.Register(", Goods: ", ", Güter: ");
            Loc.Register(", Exotic: ", ", Exotisch: ");
            Loc.Register(", Food: ", ", Nahrung: ");
            Loc.Register(", Market: ", ", Markt: ");
            Loc.Register(". Goats: ", ". Ziegen: ");
            Loc.Register(". Horses: ", ". Pferde: ");
            Loc.Register(". Goods: ", ". Güter: ");
            Loc.Register(". Exotic goods: ", ". Exotische Güter: ");
            Loc.Register(". Food: ", ". Nahrung: ");
            Loc.Register(". Market: ", ". Markt: ");
            Loc.Register("Trade partners", "Handelspartner");
            Loc.Register("Tab cycles treasures and trade partners zones, ",
                "Tab wechselt zwischen Schätze- und Handelspartner-Bereich, ");
            Loc.Register("Up and Down cycle items, Space selects, D for description. ",
                "Hoch und Runter wechseln Einträge, Leertaste wählt, D für Beschreibung. ");
            Loc.Register("Enter or C opens the Caravan dialog.",
                "Eingabe oder C öffnet den Karawanen-Dialog.");
            Loc.Register("Tab cycles zones, Up and Down cycle items, Space selects, D for description, ",
                "Tab wechselt Bereiche, Hoch und Runter wechseln Einträge, Leertaste wählt, D für Beschreibung, ");
            Loc.Register("Enter or C opens the Caravan dialog, F5 for status.",
                "Eingabe oder C öffnet den Karawanen-Dialog, F5 für den Status.");
            // Magic screen full read.
            Loc.Register(" (active)", " (wirkt)");
            Loc.Register(". Press L to navigate list", ". Drückt L, um die Liste zu navigieren");
            // Saga screen summary + full.
            Loc.Register("Up and Down to pick a year. D for full text. ",
                "Hoch und Runter wählen ein Jahr. D für den vollen Text. ");
            Loc.Register("Enter to restore. ", "Eingabe zum Wiederherstellen. ");
            Loc.Register("F5 for status.", "F5 für den Status.");
            Loc.Register("No saga text available.", "Kein Sagentext verfügbar.");

            // ---- WarScreenHandler (war-screen action hotkeys) ----
            // "Opening " reused from the LoreScreenNavigator block.
            Loc.Register("Hotkeys: W warriors, R raid, C cattle raid, O honor raid, F fortify. Tab also cycles these buttons.",
                "Tastenkürzel: W Krieger, R Überfall, C Viehraub, O Ehrenraub, F Befestigen. Tab wechselt ebenfalls durch diese Knöpfe.");
            Loc.Register(" dialog.", "-Dialog.");

            // ---- ConcernReader (F4 dashboard cycle + Ctrl+F4 / auto-announce) ----
            // Reused keys (registered above): the six management-screen names
            // "Clan"/"Magic"/"Map"/"Relations"/"War"/"Wealth" (screen-name block,
            // wrapped here via Loc.Get(GameScreens.NameOf(...))), and
            // "No concerns and no active magic." (registered near the top).
            Loc.Register("Could not cycle dashboard.", "Dashboard konnte nicht durchlaufen werden.");
            Loc.Register("End of dashboard.", "Ende des Dashboards.");
            Loc.Register("Dashboard. ", "Dashboard. ");
            Loc.Register("Dashboard. Nothing to report.", "Dashboard. Nichts zu berichten.");
            Loc.Register(" item. ", " Eintrag. ");
            Loc.Register(" items. ", " Einträge. ");
            Loc.Register(", on ", ", auf ");
            Loc.Register("(unspecified)", "(nicht angegeben)");
            Loc.Register("(unreadable)", "(nicht lesbar)");
            Loc.Register(" Spirit", "-Geist");
            Loc.Register(", deity dead", ", Gottheit tot");
            Loc.Register(", permanent", ", dauerhaft");
            Loc.Register(", transient", ", vorübergehend");
            Loc.Register("Active magic: ", "Aktive Magie: ");
            // Category plural labels (cycle header, e.g. "Warnings, 3 items.").
            Loc.Register("Stresses", "Belastungen");
            Loc.Register("Advantages", "Vorteile");
            Loc.Register("Warnings", "Warnungen");
            Loc.Register("Omens", "Omen");
            Loc.Register("Active magic", "Aktive Magie");
            Loc.Register("Known magic", "Bekannte Magie");
            Loc.Register("Unlearned magic", "Unerlernte Magie");
            Loc.Register("Items", "Einträge");
            // Category singular labels (cycle item, e.g. "Warning 2: ...").
            Loc.Register("Stress", "Belastung");
            Loc.Register("Advantage", "Vorteil");
            Loc.Register("Warning", "Warnung");
            Loc.Register("Omen", "Omen");
            Loc.Register("Active", "Aktiv");
            Loc.Register("Known", "Bekannt");
            Loc.Register("Unlearned", "Unerlernt");
            Loc.Register("Item", "Eintrag");
            // Overview labels (count-inflected, e.g. "Dashboard. 3 Belastungen, 1 Warnung.").
            Loc.Register("stress", "Belastung");
            Loc.Register("stresses", "Belastungen");
            Loc.Register("advantage", "Vorteil");
            Loc.Register("advantages", "Vorteile");
            Loc.Register("warning", "Warnung");
            Loc.Register("warnings", "Warnungen");
            Loc.Register("omen", "Omen");
            Loc.Register("omens", "Omen");
            Loc.Register("active blessing", "aktiver Segen");
            Loc.Register("active blessings", "aktive Segen");
            Loc.Register("known blessing", "bekannter Segen");
            Loc.Register("known blessings", "bekannte Segen");
            Loc.Register("unlearned blessing", "unerlernter Segen");
            Loc.Register("unlearned blessings", "unerlernte Segen");
            Loc.Register("items", "Einträge");

            // ---- Hints (central key-hint source — see Hints.cs) ----
            // Every recurring key-hint phrase, defined once. The Shift+F1 key-hint
            // help composes its per-screen text from these plus a short screen-specific
            // literal, so a binding is only re-worded in one place. Short-sentence
            // style (user choice 2026-05-22). F5/F6/L are deliberately absent.
            Loc.Register("Arrow keys move the focus. ", "Pfeiltasten bewegen den Fokus. ");
            Loc.Register("Left and Right adjust a slider. ",
                "Pfeil links und rechts verstellen einen Schieber. ");
            Loc.Register("Tab cycles the buttons. ", "Tab wechselt durch die Knöpfe. ");
            Loc.Register("Tab cycles the zones. ", "Tab wechselt die Zonen. ");
            Loc.Register("D reads details. ", "D liest Details. ");
            Loc.Register("H repeats the tutorial hint. ", "H wiederholt den Tutorial-Hinweis. ");
            Loc.Register("Shift F1 lists the keys for this screen. ",
                "Umschalt F1 nennt die Tasten für diesen Bildschirm. ");
            Loc.Register("F1 opens the game's own help. ",
                "F1 öffnet die spieleigene Hilfe. ");
            Loc.Register("F2 status, F3 advisor, F4 concerns. ",
                "F2 Status, F3 Berater, F4 Belange. ");
            Loc.Register("F2 status, F3 advisor. ", "F2 Status, F3 Berater. ");
            Loc.Register("F10 reads the combat status. ", "F10 liest den Kampfstatus. ");
            Loc.Register("S advances to the next season. ", "S rückt zur nächsten Saison vor. ");
            Loc.Register("Ctrl 1 to 9 switches the management screen. ",
                "Strg 1 bis 9 wechselt den Verwaltungs-Bildschirm. ");
            Loc.Register("F changes the filter. ", "F ändert den Filter. ");
            Loc.Register("I toggles the scene information. ",
                "I blendet die Szenen-Information ein oder aus. ");
            Loc.Register("P switches between picture and text. ",
                "P wechselt zwischen Bild und Text. ");

            // ---- Shift+F1 key-hint help — per-screen headers (AnnounceScreenShortcuts) ----
            Loc.Register("Keys for the game selection. ", "Tasten für die Spielauswahl. ");
            Loc.Register("Keys for combat. ", "Tasten für den Kampf. ");
            Loc.Register("Keys for the scene. ", "Tasten für die Szene. ");
            Loc.Register("Keys for the tutorial. ", "Tasten für das Tutorial. ");
            Loc.Register("Keys for the game over screen. ", "Tasten für den Spielende-Bildschirm. ");
            Loc.Register("Keys for choosing a leader. ", "Tasten für die Anführer-Wahl. ");
            Loc.Register("Keys for Magic. ", "Tasten für Magie. ");
            Loc.Register("Keys for Relations. ", "Tasten für Beziehungen. ");
            Loc.Register("Keys for War. ", "Tasten für Krieg. ");
            Loc.Register("Keys for Wealth. ", "Tasten für Wohlstand. ");
            Loc.Register("Keys for Lore. ", "Tasten für Lore. ");
            Loc.Register("Keys for the Saga. ", "Tasten für die Saga. ");
            Loc.Register("Keys for Sacred Time. ", "Tasten für die Heilige Zeit. ");
            Loc.Register("Keys for the Map. ", "Tasten für die Karte. ");
            Loc.Register("Keys for reorganizing the clan. ", "Tasten für die Klan-Umordnung. ");
            Loc.Register("Keys for the caravan dialog. ", "Tasten für den Karawanen-Dialog. ");
            Loc.Register("Keys for the emissary dialog. ", "Tasten für den Gesandten-Dialog. ");
            Loc.Register("Keys for the raid dialog. ", "Tasten für den Überfall-Dialog. ");
            Loc.Register("Keys for the warriors dialog. ", "Tasten für den Krieger-Dialog. ");
            Loc.Register("Keys for the sacrifice dialog. ", "Tasten für den Opfer-Dialog. ");
            Loc.Register("Keys for the spirit dialog. ", "Tasten für den Geister-Dialog. ");
            Loc.Register("Keys for this dialog. ", "Tasten für diesen Dialog. ");
            Loc.Register("Keys for the clan screen. ", "Tasten für den Klan-Bildschirm. ");
            Loc.Register("Keys for this screen. ", "Tasten für diesen Bildschirm. ");

            // ---- Shift+F1 key-hint help — per-screen specifics (AnnounceScreenShortcuts) ----
            Loc.Register(
                "Up and Down choose a save, Enter loads it. Delete removes a save; press it twice within three seconds to confirm. Escape goes back. ",
                "Pfeil hoch und runter wählen einen Spielstand, Eingabe lädt ihn. Entf löscht einen Spielstand; zweimal binnen drei Sekunden bestätigt das. Escape geht zurück. ");
            Loc.Register(
                "Arrow keys move between the combat options, Enter activates the focused option. ",
                "Pfeiltasten wechseln zwischen den Kampf-Optionen, Eingabe aktiviert die fokussierte Option. ");
            Loc.Register(
                "Up and Down move through the response options, Enter selects a response. ",
                "Pfeil hoch und runter gehen durch die Antwortmöglichkeiten, Eingabe wählt eine Antwort. ");
            Loc.Register(
                "Up and Down re-read the tutorial paragraphs, Enter continues. ",
                "Pfeil hoch und runter lesen die Tutorial-Absätze erneut, Eingabe fährt fort. ");
            Loc.Register(
                "Arrow keys and Tab move the focus, Enter activates. In the saga overview, Up and Down choose a year and Enter restores it. ",
                "Pfeiltasten und Tab bewegen den Fokus, Eingabe aktiviert. In der Saga-Übersicht wählen Pfeil hoch und runter ein Jahr, Eingabe stellt es wieder her. ");
            Loc.Register(
                "Up and Down move between the candidates. Space selects a person, Enter confirms. D reads the full biography. Escape closes. ",
                "Pfeil hoch und runter wechseln zwischen den Kandidaten. Leertaste wählt eine Person aus, Eingabe bestätigt. D liest die vollständige Biografie. Escape schließt. ");
            Loc.Register("Space toggles the focused blessing. ",
                "Leertaste schaltet den fokussierten Segen. ");
            Loc.Register(
                "Tab reaches the map markers for clans with available missions, Enter opens the emissary dialog. ",
                "Tab erreicht die Karten-Markierungen für Klans mit verfügbaren Missionen, Eingabe öffnet den Gesandten-Dialog. ");
            Loc.Register(
                "W opens Warriors, R starts a raid, C a cattle raid, O an honor raid, F a fortify. Each key says so if the action is unavailable. ",
                "W öffnet Krieger, R startet einen Überfall, C einen Viehraub, O einen Ehrenraub, F eine Befestigung. Jede Taste meldet, wenn die Aktion nicht verfügbar ist. ");
            Loc.Register(
                "Space activates the focused item. Enter or C opens the caravan dialog. ",
                "Leertaste aktiviert das fokussierte Element. Eingabe oder C öffnet den Karawanen-Dialog. ");
            Loc.Register("Enter opens the focused entry, M opens the manual. ",
                "Eingabe öffnet den fokussierten Eintrag, M öffnet das Handbuch. ");
            Loc.Register(
                "Up and Down choose a year, D reads the full text, Enter restores the chosen year. ",
                "Pfeil hoch und runter wählen ein Jahr, D liest den Volltext, Eingabe stellt das gewählte Jahr wieder her. ");
            Loc.Register(
                "Tab cycles between the forecast and the allocation. Left and Right adjust a value, D re-reads the line. G opens the saga chronicle, Enter continues. ",
                "Tab wechselt zwischen Vorhersage und Zuteilung. Pfeil links und rechts passen einen Wert an, D liest die Zeile erneut. G öffnet die Saga-Chronik, Eingabe fährt fort. ");
            Loc.Register(
                "G focuses the target zone, K the target list, X the foray panel. Enter sends the mission, Escape cancels. ",
                "G springt zur Zielzone, K zur Ziel-Liste, X zum Foray-Panel. Eingabe sendet die Mission, Escape bricht ab. ");
            Loc.Register(
                "In the hex cursor zone, Page Up and Down jump between points of interest, Ctrl with them switches category. ",
                "Im Hex-Cursor-Bereich springen Bild-auf und Bild-ab zwischen wichtigen Orten, Strg dazu wechselt die Kategorie. ");
            Loc.Register(
                "In the list, Space toggles ring membership and C makes the person chieftain. F changes the sort order. Enter applies the reorganization, Escape discards it. ",
                "In der Liste schaltet Leertaste die Ring-Mitgliedschaft, C macht die Person zum Häuptling. F ändert die Sortierung. Eingabe wendet die Umordnung an, Escape verwirft sie. ");
            Loc.Register(
                "R lists the active trade routes. Enter sends the caravan, Escape closes. ",
                "R nennt die aktiven Handelsrouten. Eingabe sendet die Karawane, Escape schließt. ");
            Loc.Register("Enter sends the emissary, Escape closes. ",
                "Eingabe sendet den Gesandten, Escape schließt. ");
            Loc.Register(
                "In the helpers list, F changes the filter. Enter starts the raid, Escape closes. ",
                "In der Helfer-Liste ändert F den Filter. Eingabe startet den Überfall, Escape schließt. ");
            Loc.Register("Enter carries out the main action, Escape closes. ",
                "Eingabe führt die Hauptaktion aus, Escape schließt. ");
            Loc.Register("Enter confirms the sacrifice, Escape closes. ",
                "Eingabe bestätigt das Opfer, Escape schließt. ");
            Loc.Register(
                "Enter carries out the main action, Space activates a button, Escape closes. ",
                "Eingabe führt die Hauptaktion aus, Leertaste betätigt einen Knopf, Escape schließt. ");
            Loc.Register("D reads a clan description; press D again to cycle the paragraphs. ",
                "D liest eine Klan-Beschreibung; D erneut geht durch die Absätze. ");
            Loc.Register(
                "Enter activates the focused item, Space toggles a switch, Escape goes back. ",
                "Eingabe aktiviert das fokussierte Element, Leertaste schaltet einen Schalter um, Escape geht zurück. ");

            // ---- Game help overlay (HelpScreenReader / HelpScreenNavigator) ----
            Loc.Register("Help. ", "Hilfe. ");
            Loc.Register("Left:", "Links:");
            Loc.Register("Right:", "Rechts:");
            Loc.Register("Keys:", "Tasten:");
            Loc.Register("Use Up and Down to read it section by section. Escape closes.",
                "Mit Pfeil hoch und runter Abschnitt für Abschnitt lesen. Escape schließt.");
            Loc.Register("No help text.", "Kein Hilfetext.");
            Loc.Register("Beginning of help.", "Anfang der Hilfe.");
            Loc.Register("End of help.", "Ende der Hilfe.");

            // ---- Double-Enter summary templates (commit dialogs) ----
            // Shared fallback names when a slot is empty at commit time.
            Loc.Register("no leader", "keinen Anführer");
            Loc.Register("no clan", "keinen Klan");
            Loc.Register("no target", "kein Ziel");
            Loc.Register("no goal", "kein Ziel");
            Loc.Register("no destination", "kein Reiseziel");
            Loc.Register("no approach", "keinen Ansatz");
            // Shared "X warriors" fragments (singular/plural keep gendered case in DE).
            Loc.Register(", led by {0}", ", angeführt von {0}");
            Loc.Register(", escorted by {0} warrior", ", begleitet von {0} Krieger");
            Loc.Register(", escorted by {0} warriors", ", begleitet von {0} Kriegern");
            // SpiritNavigator.BuildBargainSummary
            Loc.Register("You bargain for {0} via {1}.", "Du verhandelst um {0} mit dem Ansatz {1}.");
            // ChooseLeaderNavigator.BuildChooseSummary
            Loc.Register("You choose {0} as leader.", "Du wählst {0} als Anführer.");
            Loc.Register("You choose no one.", "Du wählst niemanden.");
            // WarriorsNavigator.BuildWarriorsSummary — DE uses identical Singular/Plural
            // forms ("ein Krieger"/"zwei Krieger") so both keys map to the same string.
            Loc.Register("You recruit {0} warrior.", "Du rekrutierst {0} Krieger.");
            Loc.Register("You recruit {0} warriors.", "Du rekrutierst {0} Krieger.");
            Loc.Register("You dismiss {0} warrior.", "Du entlässt {0} Krieger.");
            Loc.Register("You dismiss {0} warriors.", "Du entlässt {0} Krieger.");
            // RaidNavigator.BuildRaidSummary
            Loc.Register("You raid {0}", "Du überfällst {0}");
            Loc.Register(" with {0} warrior", " mit {0} Krieger");
            Loc.Register(" with {0} warriors", " mit {0} Kriegern");
            Loc.Register(", helped by {0}", ", unterstützt von {0}");
            // EmissaryNavigator.BuildEmissarySummary
            Loc.Register("You send an emissary to {0}", "Du sendest einen Gesandten zu {0}");
            Loc.Register(", with gifts of {0}", ", mit Geschenken: {0}");
            Loc.Register("{0} goods", "{0} Güter");
            Loc.Register("{0} herds", "{0} Herden");
            Loc.Register("{0} horses", "{0} Pferde");
            // CaravanNavigator.BuildCaravanSummary
            Loc.Register("You send a caravan to {0} to trade, led by {1}",
                "Du sendest eine Karawane zu {0} zum Handeln, angeführt von {1}");
            Loc.Register("You send a caravan to {0} to establish a trade route, led by {1}",
                "Du sendest eine Karawane zu {0}, um eine Handelsroute zu errichten, angeführt von {1}");
            Loc.Register(", selling {0}", ", verkauft {0}");
            Loc.Register(", buying {0}",  ", kauft {0}");
            // MapScreenNavigator.BuildExpeditionSummary
            Loc.Register("You send an expedition to {0}", "Du sendest eine Expedition nach {0}");
            Loc.Register(", goal {0}", ", Ziel {0}");
            Loc.Register(", with {0} warrior", ", mit {0} Krieger");
            Loc.Register(", with {0} warriors", ", mit {0} Kriegern");
            // SacredTimeNavigator.BuildAllocationSummary
            Loc.Register("You allocate no magic. Reserve {0} unused.",
                "Du verteilst keine Magie. {0} Reserve unangetastet.");
            Loc.Register("You allocate magic: {0}. Reserve {1} of {2} unused.",
                "Du verteilst Magie: {0}. Reserve {1} von {2} ungenutzt.");
        }
    }
}
