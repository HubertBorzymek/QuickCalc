# QuickCalc

QuickCalc to lokalny kalkulator kontekstowy dla Windows. Działa w tle, otwiera małe okno nad bieżącą aplikacją i potrafi zastąpić dokładnie zaznaczony fragment albo wstawić wynik w pozycji kursora. Projekt jest przygotowany dla Visual Studio 2026 i .NET 9.

## Dlaczego C#

Wybrano C# z Windows Forms. W porównaniu z natywnym C++ daje prostsze testowanie i utrzymanie, a wymagane operacje Win32, UI Automation oraz globalne skróty są dostępne przez P/Invoke i COM. AutoHotkey nie usuwa trudności związanych z wykrywaniem prawdziwego zaznaczenia, przywracaniem fokusu ani bezpieczeństwem schowka, dlatego nie jest zależnością aplikacji.

## Uruchamianie

Najprościej uruchomić `Start-QuickCalc.ps1`. Skrypt użyje opublikowanej aplikacji, jeśli istnieje, albo uruchomi projekt poleceniem `dotnet run`. Po starcie ikona QuickCalc jest widoczna w zasobniku systemowym.

W Visual Studio 2026:

1. Otwórz `QuickCalc.sln`.
2. Wybierz profil uruchamiania `QuickCalc` lub ustaw `QuickCalc.App` jako projekt startowy.
3. Naciśnij F5.

Domyślne skróty:

- `F16` — kalkulator kontekstowy; wynik zastępuje zaznaczenie lub trafia w pozycję kursora.
- `Ctrl+F16` — kalkulator schowka; wynik zostaje skopiowany, ale nie jest wklejany.
- `Escape` — anulowanie bez zmiany tekstu i schowka.

Program kończy się przez polecenie **Zakończ** w menu ikony zasobnika. Nie rejestruje autostartu.

### Diagnostyka skrótów

Kliknij prawym przyciskiem ikonę QuickCalc w zasobniku i wybierz **Diagnostyka…**. Okno pokazuje osobny stan rejestracji obu skrótów, dokładny błąd Win32, numer sesji procesu oraz dziennik komunikatów `WM_HOTKEY`. Pola **Klawisz** i **Modyfikatory** pozwalają zmienić oba skróty bez restartu; przycisk **Zastosuj skróty** zapisuje konfigurację. Modyfikatory wpisuje się jako `None`, `Control`, `Shift`, `Alt`, `Win` lub połączenie, np. `Control+Shift`. Jeśli nowy skrót jest zajęty, poprzednia konfiguracja zostanie przywrócona.

Przycisk **Otwórz test kalkulatora** uruchamia kalkulator schowka bez skrótu. W oknie można również sprawdzić kod klawisza rzeczywiście wysyłany przez programowalną klawiaturę i skopiować raport.

Menu ikony zawiera również osobne polecenia otwierające oba tryby bez użycia skrótu.

## Wyrażenia i jednostki

Obsługiwane są `+`, `-`, `*`, `/`, nawiasy, znaki jednoargumentowe, kropka i przecinek dziesiętny oraz jednostki `mm`, `mil`, `in`/`inch`. Parser nie wykonuje kodu użytkownika. W działaniach bez zaznaczenia wyniki długości zachowują pierwszą jawną jednostkę. W trybie kontekstowym wynik zachowuje jednostkę oraz odstęp zaznaczonego tekstu.

Wyrażenie zaczynające się od operatora jest względne względem prawidłowo zaznaczonej liczby. Wyjątkiem jest `-5` bez zaznaczenia, które oznacza liczbę ujemną. Przykłady: zaznaczone `25mm` i `+5` daje `30mm`; `1in+5mm` daje wynik w calach. Mnożenie dwóch długości i inne nieobsługiwane wymiary są odrzucane.

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

Standardowe kontrolki Edit/RichEdit są obsługiwane bezpośrednio przez `EM_GETSEL`, `EM_SETSEL` i `EM_REPLACESEL`. Dla innych kontrolek UI Automation TextPattern służy do potwierdzenia faktycznego zaznaczenia. Jeśli zaznaczenia nie da się wiarygodnie odczytać, działania względne są wyłączone; absolutne wstawienie nadal może użyć wejścia Unicode po sprawdzeniu istnienia pierwotnego okna.

W przeglądarkach i aplikacjach Electron zaznaczenie jest pobierane z aktualnie aktywnego elementu UI Automation, a wynik zastępuje zachowane zaznaczenie przez wejście Unicode po przywróceniu poprzedniego okna. Błąd dostawcy UI Automation nie blokuje już otwarcia kalkulatora — popup przechodzi wtedy do trybu bezpiecznego bez zaznaczenia. Nieobsłużone wyjątki interfejsu są zapisywane w `quickcalc-error.log` obok programu.

Tryb kontekstowy nie korzysta ze schowka, więc nie musi zapisywać ani odtwarzać cudzych formatów. Tryb schowka celowo zastępuje jego zawartość. Aplikacja uruchomiona bez podniesionych uprawnień nie może niezawodnie sterować oknem uruchomionym jako administrator. Kontrolki niestandardowe mogą nie zachować zaznaczenia po utracie fokusu; program odmawia działania względnego, jeżeli zaznaczenie nie zostało potwierdzone, ale zgodność wstawiania wymaga testu z konkretną aplikacją.

## Ręczny test Altium Designer 18

1. Pracuj na kopii testowego projektu PCB.
2. Otwórz kolejno pole X, Y, rozmiar pada i szerokość ścieżki.
3. Zaznacz wyłącznie wartość z jednostką, naciśnij F16, wpisz `+5mil`, zatwierdź i sprawdź zmieniony fragment.
4. Powtórz dla `mm`, `mil`, braku zaznaczenia, anulowania Escape i obu poziomów uprawnień.
5. Sprawdź format po zatwierdzeniu pola przez Altium oraz brak zmiany schowka w trybie kontekstowym.

Altium Designer nie jest wymagany do uruchomienia testów automatycznych.
