# QuickCalc

QuickCalc to lokalny kalkulator kontekstowy dla Windows. Działa w tle, otwiera małe okno nad bieżącą aplikacją i potrafi zastąpić dokładnie zaznaczony fragment albo wstawić wynik w pozycji kursora. Projekt jest przygotowany dla Visual Studio 2026 i .NET 9.

## Dlaczego C#

Wybrano C# z Windows Forms. W porównaniu z natywnym C++ daje prostsze testowanie i utrzymanie, a wymagane operacje Win32, UI Automation oraz globalne skróty są dostępne przez P/Invoke i COM. AutoHotkey nie usuwa trudności związanych z wykrywaniem prawdziwego zaznaczenia, przywracaniem fokusu ani bezpieczeństwem schowka, dlatego nie jest zależnością aplikacji.

## Uruchamianie

Najprościej dwukrotnie kliknąć `Start-QuickCalc.cmd`. Plik uruchamia gotową aplikację z `artifacts\publish`, niezależnie od bieżącego katalogu roboczego. Po starcie ikona QuickCalc jest widoczna w zasobniku systemowym. Deweloperski `Start-QuickCalc.ps1` dodatkowo potrafi uruchomić projekt przez `dotnet run`, gdy publikacja jeszcze nie istnieje.

Aby uruchamiać QuickCalc przy logowaniu do Windows, dwukrotnie kliknij `Install-QuickCalc-Autostart.cmd`. Tworzy on skrót `QuickCalc.lnk` w folderze Autostart bieżącego użytkownika; skrót wskazuje na `Start-QuickCalc.cmd`, więc przenoszenie samego pliku do folderu Autostart nie jest potrzebne.

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

W trybie kontekstowym popup jest ustawiany lekko pod aktywnym polem tekstowym (lub nad nim, gdy pod spodem brakuje miejsca). Tryb schowka otwiera się przy kursorze myszy. Tryb schowka nie uruchamia analizy zaznaczenia, a zapytanie UI Automation trybu kontekstowego ma twardy limit czasu, więc wadliwy dostawca edytora nie może blokować pojawienia się okna.

### Diagnostyka skrótów

Kliknij prawym przyciskiem ikonę QuickCalc w zasobniku i wybierz **Diagnostyka…**. Okno pokazuje osobny stan rejestracji obu skrótów, dokładny błąd Win32, numer sesji procesu oraz dziennik komunikatów `WM_HOTKEY`. Pola **Klawisz** i **Modyfikatory** pozwalają zmienić oba skróty bez restartu; przycisk **Zastosuj skróty** zapisuje konfigurację. Modyfikatory wpisuje się jako `None`, `Control`, `Shift`, `Alt`, `Win` lub połączenie, np. `Control+Shift`. Jeśli nowy skrót jest zajęty, poprzednia konfiguracja zostanie przywrócona.

Przycisk **Otwórz test kalkulatora** uruchamia kalkulator schowka bez skrótu. W oknie można również sprawdzić kod klawisza rzeczywiście wysyłany przez programowalną klawiaturę i skopiować raport.

Menu ikony zawiera również osobne polecenia otwierające oba tryby bez użycia skrótu.

## Wyrażenia i jednostki

Obsługiwane są `+`, `-`, `*`, `/`, potęgowanie `^`, nawiasy, znaki jednoargumentowe, kropka i przecinek dziesiętny. Pierwiastek kwadratowy zapisuje się krótko jako `r`, np. `r81` albo `r(9+7)`; akceptowany jest również znak `√`. Dla zaznaczonej liczby `^2` podnosi ją do kwadratu, samo `r` ją pierwiastkuje, a dalsze działania można dopisać normalnie, np. zaznaczone `81` i `r*2+1` daje `19`.

Parser obsługuje jednostki bazowe SI i ich przedrostki (m.in. `m`, `kg`, `s`, `A`, `K`, `mol`, `cd`) oraz jednostki używane w elektronice i technice: `V`, `Ω`/`ohm`, `F`, `H`, `Hz`, `W`, `N`, `Pa`, `J`, `C`, `S`, `Wb`, `T`. Dostępne są również `in`/`inch`, `ft`, `yd`, `mi`, `mil`, `min` i `h`. Przedrostek `u` jest przyjmowany jako łatwy do wpisania odpowiednik `µ`.

Spacja przed jednostką jest zachowywana: `10mm/2` daje `5mm`, natomiast `10 mm/2` daje `5 mm`. `Enter` zachowuje jednostkę wejściową, a `Shift+Enter` dobiera czytelną jednostkę SI, np. zaznaczone `2cm` i `/500` daje `0.04mm`, zaś `5ft` i `*1` daje `1.524m`.

Wyrażenie zaczynające się od operatora jest względne względem prawidłowo zaznaczonej liczby; dotyczy to również `^`. Samo `r`/`√` albo pierwiastek z następującym działaniem również używa zaznaczenia. Wyjątkiem jest `-5` bez zaznaczenia, które oznacza liczbę ujemną. Przykłady: zaznaczone `25mm` i `+5` daje `30mm`; `1in+5mm` daje wynik w calach. Potęgowanie i pierwiastkowanie wielkości z jednostkami pozostaje odrzucane, ponieważ wynik wymagałby obsługi jednostek złożonych.

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

Standardowe kontrolki Edit/RichEdit są obsługiwane bezpośrednio przez `EM_GETSEL`, `EM_SETSEL` i `EM_REPLACESEL`. Dla innych kontrolek aplikacja najpierw korzysta z UI Automation TextPattern. Chwilowe `Ctrl+C` jest używane tylko wtedy, gdy UI Automation potwierdzi niezerowy zakres, ale nie zwróci jego treści — nigdy w zwykłym trybie bez zaznaczenia. Wynik jest wstawiany jak przez `Ctrl+V`; wcześniejsza zawartość i formaty schowka są odtwarzane po operacji.

W przeglądarkach, VS Code i aplikacjach Electron UI Automation jest tylko pomocą, a nie warunkiem wstawienia. QuickCalc nie próbuje ponownie aktywować zapamiętanego zaznaczonego zakresu UI Automation po zmianie fokusu, ponieważ niektóre przeglądarki unieważniają taki zakres i mogłyby zmienić zawartość pola. Może natomiast przywrócić pusty zakres kursora, aby kontrolka wybierająca całą zawartość po odzyskaniu fokusu nie nadpisała starego tekstu. Następnie wysyłane jest standardowe `Ctrl+V`. Jeśli wcześniej istniało zaznaczenie, cały wklejony wynik zostaje ponownie zaznaczony; bez zaznaczenia kursor pozostaje za wynikiem. Nieobsłużone wyjątki interfejsu są zapisywane w `quickcalc-error.log` obok programu.

Tryb kontekstowy korzysta ze schowka wyłącznie tymczasowo i przywraca jego wcześniejszą zawartość. Tryb schowka celowo zastępuje jego zawartość. Aplikacja uruchomiona bez podniesionych uprawnień nie może niezawodnie sterować oknem uruchomionym jako administrator. Po otwarciu popupu niektóre aplikacje przestają rysować kolor zaznaczenia, ponieważ tracą fokus; nie oznacza to usunięcia tekstu ani logicznego zakresu zaznaczenia.

## Ręczny test Altium Designer 18

1. Pracuj na kopii testowego projektu PCB.
2. Otwórz kolejno pole X, Y, rozmiar pada i szerokość ścieżki.
3. Zaznacz wyłącznie wartość z jednostką, naciśnij F16, wpisz `+5mil`, zatwierdź i sprawdź zmieniony fragment.
4. Powtórz dla `mm`, `mil`, braku zaznaczenia, anulowania Escape i obu poziomów uprawnień.
5. Sprawdź format po zatwierdzeniu pola przez Altium oraz brak zmiany schowka w trybie kontekstowym.

Altium Designer nie jest wymagany do uruchomienia testów automatycznych.
