# QuickCalc

QuickCalc to lokalny kalkulator kontekstowy dla Windows. Działa w tle, otwiera małe okno nad bieżącą aplikacją i potrafi zastąpić dokładnie zaznaczony fragment albo wstawić wynik w pozycji kursora. Projekt jest przygotowany dla Visual Studio 2026 i .NET 9.

## Dlaczego C#

Wybrano C# z Windows Forms. W porównaniu z natywnym C++ daje prostsze testowanie i utrzymanie, a wymagane operacje Win32, UI Automation oraz globalne skróty są dostępne przez P/Invoke i COM. AutoHotkey nie usuwa trudności związanych z wykrywaniem prawdziwego zaznaczenia, przywracaniem fokusu ani bezpieczeństwem schowka, dlatego nie jest zależnością aplikacji.

## Uruchamianie

Najprościej dwukrotnie kliknąć `Start-QuickCalc.cmd`. Plik uruchamia gotową aplikację z `artifacts\publish`, niezależnie od bieżącego katalogu roboczego. Po starcie ikona QuickCalc jest widoczna w zasobniku systemowym. Deweloperski `Start-QuickCalc.ps1` dodatkowo potrafi uruchomić projekt przez `dotnet run`, gdy publikacja jeszcze nie istnieje.

Aby uruchamiać QuickCalc przy logowaniu do Windows, dwukrotnie kliknij `Install-QuickCalc-Autostart.cmd`. Tworzy on skrót `QuickCalc.lnk` w folderze Autostart bieżącego użytkownika; skrót wskazuje na `Start-QuickCalc.cmd`, więc przenoszenie samego pliku do folderu Autostart nie jest potrzebne.

### Przenoszenie na inny komputer

Uruchom `Create-QuickCalc-Release.ps1`. Skrypt tworzy gotowy folder `artifacts\release\QuickCalc-portable-win-x64` oraz odpowiadający mu plik ZIP. Pakiet jest samowystarczalny dla Windows 10/11 x64 i nie wymaga instalowania .NET ani Visual Studio. Na drugi komputer należy przenieść cały folder lub ZIP, rozpakować go w stałym miejscu i dwukrotnie kliknąć znajdujący się wewnątrz `Start-QuickCalc.cmd`. Do pakietu dołączone są osobne skrypty dodawania i usuwania autostartu.

W Visual Studio 2026:

1. Otwórz `QuickCalc.sln`.
2. Wybierz profil uruchamiania `QuickCalc` lub ustaw `QuickCalc.App` jako projekt startowy.
3. Naciśnij F5.

Domyślne skróty:

- `F16` — kalkulator kontekstowy; wynik zastępuje zaznaczenie lub trafia w pozycję kursora.
- `Ctrl+F16` — kalkulator schowka; wynik zostaje skopiowany, ale nie jest wklejany.
- `Enter` — zatwierdzenie z zachowaniem jednostki wejściowej.
- `Shift+Enter` — zatwierdzenie z konwersją do czytelnej jednostki SI.
- `Escape` — anulowanie bez zmiany tekstu i schowka.

Program kończy się przez polecenie **Zakończ** w menu ikony zasobnika. Nie rejestruje autostartu.

Popup domyślnie otwiera się jako ciemny, półprzezroczysty pasek **Compact** bez systemowego paska tytułu. Pokazuje zaznaczoną wartość, pole wyrażenia i wynik aktualizowany podczas pisania. Ponowne naciśnięcie `F16`, gdy popup jest otwarty, przełącza **Compact ↔ Expanded** bez kasowania wyrażenia ani utraty fokusu. Widok Expanded dodaje tryb pracy, zaznaczenie, rodzaj działania, historię, skróty oraz błąd parsera. Zwykłe błędy są pokazywane wewnątrz popupu, bez MessageBoxów.

W trybie kontekstowym popup jest ustawiany lekko pod aktywnym polem tekstowym (lub nad nim, gdy pod spodem brakuje miejsca). Tryb schowka otwiera się przy kursorze myszy. Położenie i rozmiar są przeliczane dla bieżącego monitora i jego DPI, a popup pozostaje w obszarze roboczym. Tryb schowka nie uruchamia analizy zaznaczenia, a zapytanie UI Automation trybu kontekstowego ma twardy limit czasu, więc wadliwy dostawca edytora nie może blokować pojawienia się okna.

### Diagnostyka skrótów

Kliknij prawym przyciskiem ikonę QuickCalc w zasobniku i wybierz **Diagnostyka…**. Okno pokazuje osobny stan rejestracji obu skrótów, dokładny błąd Win32, numer sesji procesu oraz dziennik komunikatów `WM_HOTKEY`. Pola **Klawisz** i **Modyfikatory** pozwalają zmienić oba skróty bez restartu; przycisk **Zastosuj skróty** zapisuje konfigurację. Modyfikatory wpisuje się jako `None`, `Control`, `Shift`, `Alt`, `Win` lub połączenie, np. `Control+Shift`. Jeśli nowy skrót jest zajęty, poprzednia konfiguracja zostanie przywrócona.

Przycisk **Otwórz test kalkulatora** uruchamia kalkulator schowka bez skrótu. W oknie można również sprawdzić kod klawisza rzeczywiście wysyłany przez programowalną klawiaturę i skopiować raport.

Menu ikony zawiera również osobne polecenia otwierające oba tryby bez użycia skrótu.

## Wyrażenia i jednostki

Obsługiwane są `+`, `-`, `*`, `/`, potęgowanie `p`, nawiasy, znaki jednoargumentowe, kropka i przecinek dziesiętny. Pierwiastek kwadratowy zapisuje się jako `r`, np. `r81`, `rx` albo `r(9+7)`; akceptowany jest również znak `√`. Logarytm naturalny zapisuje się jako `ln`, a dziesiętny jako `log`, np. `lnx`, `ln(e)`, `log1000` albo `log(100)`. Dostępne są stałe `e`, `pi` i `π`.

Litera `x` oznacza zaznaczoną wartość, dlatego działają pełne wyrażenia, np. `-x`, `1/x`, `xp2` i `(x+5)/2`. Zachowane są skróty względne: `+5`, `-5`, `*2`, `/2`, samo `r`, `ln` i `log`. Bez zaznaczenia użycie `x` albo skrótu względnego powoduje czytelny błąd. Potęgowanie zapisuje się przez `p`, np. `2p8 = 256`; znak `^` oznacza teraz XOR.

Dostępne są operatory całkowitoliczbowe `&`, `|`, `^`, `~`, `<<` i `>>`. Nie przyjmują liczb zmiennoprzecinkowych ani wartości z jednostką. Literały dziesiętne, szesnastkowe i binarne można mieszać, np. `10+0x10` i `0b1000+0x10`. Wynik dziedziczy format zaznaczenia, a bez zaznaczenia — pierwszego literału; wynik niecałkowity jest zawsze dziesiętny. Format całego wyniku można wymusić końcowym `h`/`hex`, `b`/`bin` albo `d`/`dec`, ze spacją lub bez. Pełny literał HEX ma pierwszeństwo, więc `0xABCD` pozostaje liczbą, natomiast `0xABCD d` wymusza DEC.

Parser obsługuje jednostki bazowe SI i ich przedrostki (m.in. `m`, `kg`, `s`, `A`, `K`, `mol`, `cd`) oraz jednostki używane w elektronice i technice: `V`, `Ω`/`ohm`, `F`, `H`, `Hz`, `W`, `N`, `Pa`, `J`, `C`, `S`, `Wb`, `T`. Dostępne są również `in`/`inch`, `ft`, `yd`, `mi`, `mil`, `min` i `hr`/`hour`/`hours`. Przedrostek `u` jest przyjmowany jako łatwy do wpisania odpowiednik `µ`. Samo `h` jest zarezerwowane jako suffix wyniku HEX.

Spacja przed jednostką jest zachowywana: `10mm/2` daje `5mm`, natomiast `10 mm/2` daje `5 mm`. `Enter` zachowuje jednostkę wejściową, a `Shift+Enter` dobiera czytelną jednostkę SI. Przy pustym polu `Shift+Enter` konwertuje samo zaznaczenie, np. `0.0000047 F` na `4.7 µF`; bez zaznaczenia niczego nie zmienia. Odwrotność częstotliwości lub czasu zmienia wymiar, dlatego `2.5 kHz`, `1/x` i `Shift+Enter` daje `400 µs`.

Przykłady względne: zaznaczone `25mm` i `+5` daje `30mm`; zaznaczone `81` i `r*2+1` daje `19`; zaznaczone `100` i `log*3` daje `6`. Potęgowanie, pierwiastkowanie i logarytmy wielkości z jednostkami pozostają odrzucane, z wyjątkiem obsługiwanej odwrotności czasu i częstotliwości.

## Zmiana skrótów

Edytuj `hotkeys.json` obok pliku wykonywalnego (w źródłach: `src/QuickCalc.App/hotkeys.json`) i uruchom program ponownie. Nazwy klawiszy odpowiadają `System.Windows.Forms.Keys`, a modyfikatory to `Control`, `Alt`, `Shift`, `Win` albo `None`, łączone znakiem `+`.

## Budowanie, testy i publikacja

```powershell
dotnet build QuickCalc.sln -c Debug
dotnet test QuickCalc.sln -c Release
.\Publish-QuickCalc.ps1
```

Publikacja samowystarczalna dla Windows x64 trafia do `artifacts/publish`. Nie jest pojedynczym plikiem, aby nie komplikować ładowania bibliotek automatyzacji Windows. Do usunięcia programu wystarczy zakończyć go i skasować katalog projektu lub publikacji.

`tests/QuickCalc.TestHost` jest aplikacją z typowymi polami tekstowymi do ręcznych testów zaznaczenia, kursora, tekstu wielowierszowego i jednostek.

## Bezpieczeństwo i ograniczenia

Standardowe kontrolki Edit/RichEdit są obsługiwane bezpośrednio przez `EM_GETSEL`, `EM_SETSEL` i `EM_REPLACESEL`. Dla innych kontrolek aplikacja najpierw korzysta z UI Automation TextPattern i bezpośredniego `WM_COPY`, a ostatecznie z chwilowego klawiaturowego `Ctrl+C`. Klawiaturowy fallback jest pomijany wyłącznie dla procesu Visual Studio (`devenv`) bez potwierdzonego zakresu, ponieważ tam `Ctrl+C` bez zaznaczenia może uruchomić blokujące polecenie edytora. Wynik jest wstawiany jak przez `Ctrl+V`; wcześniejsza zawartość i formaty schowka są odtwarzane po operacji.

W przeglądarkach, VS Code i aplikacjach Electron UI Automation jest tylko pomocą, a nie warunkiem wstawienia. QuickCalc nie próbuje ponownie aktywować zapamiętanego zaznaczonego zakresu UI Automation po zmianie fokusu, ponieważ niektóre przeglądarki unieważniają taki zakres i mogłyby zmienić zawartość pola. Może natomiast przywrócić pusty zakres kursora, aby kontrolka wybierająca całą zawartość po odzyskaniu fokusu nie nadpisała starego tekstu. Następnie wysyłane jest standardowe `Ctrl+V`. Jeśli wcześniej istniało zaznaczenie, cały wklejony wynik zostaje ponownie zaznaczony; bez zaznaczenia kursor pozostaje za wynikiem. Nieobsłużone wyjątki interfejsu są zapisywane w `quickcalc-error.log` obok programu.

Tryb kontekstowy korzysta ze schowka wyłącznie tymczasowo i przywraca jego wcześniejszą zawartość. Tryb schowka celowo zastępuje jego zawartość. Aplikacja uruchomiona bez podniesionych uprawnień nie może niezawodnie sterować oknem uruchomionym jako administrator. Po otwarciu popupu niektóre aplikacje przestają rysować kolor zaznaczenia, ponieważ tracą fokus; nie oznacza to usunięcia tekstu ani logicznego zakresu zaznaczenia.

## Ręczny test Altium Designer 18

1. Pracuj na kopii testowego projektu PCB.
2. Otwórz kolejno pole X, Y, rozmiar pada i szerokość ścieżki.
3. Zaznacz wyłącznie wartość z jednostką, naciśnij F16, wpisz `+5mil`, zatwierdź i sprawdź zmieniony fragment.
4. Powtórz dla `mm`, `mil`, braku zaznaczenia, anulowania Escape i obu poziomów uprawnień.
5. Sprawdź format po zatwierdzeniu pola przez Altium oraz brak zmiany schowka w trybie kontekstowym.

Altium Designer nie jest wymagany do uruchomienia testów automatycznych.
