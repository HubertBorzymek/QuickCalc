# Raport testów QuickCalc

Data: 2026-09-26

## Zaimplementowane

- osobne biblioteki `QuickCalc.Core` i `QuickCalc.Windows`, aplikacja WinForms oraz host testowy;
- parser bez dynamicznego wykonywania kodu, priorytety, nawiasy, liczby ujemne, jednostki i działania względne;
- globalne skróty z blokadą autorepeat, pojedyncze okno kalkulatora i pojedyncza instancja procesu;
- podgląd wyniku, błędy w oknie, Escape, historia i obsługa wielu monitorów;
- wykrywanie zaznaczenia przez Win32 Edit/RichEdit i UI Automation TextPattern;
- bezpośrednia zamiana w standardowej kontrolce oraz awaryjne wejście Unicode bez naruszania schowka;
- tryb schowka, ikona zasobnika, konfigurowalne skróty i publikacja samowystarczalna.

## Automatycznie przetestowane

Testy obejmują priorytety, nawiasy, obie notacje dziesiętne, wartości ujemne, działania względne i absolutne, konwersje mm/mil/in, zachowanie odstępu jednostki, historię, błędną składnię, brak zaznaczenia, dzielenie przez zero, niedozwolone wymiary oraz bezpieczne odrzucenie nieistniejącego celu.

Ostatnie wykonanie `dotnet test QuickCalc.sln -c Release --no-restore`:

- `QuickCalc.Core.Tests`: 53 zaliczone, 0 niezaliczonych, 0 pominiętych;
- `QuickCalc.Integration.Tests`: 16 zaliczonych, 0 niezaliczonych, 1 pominięty;
- łącznie: 70 przypadków, z czego 69 zaliczonych i 1 pominięty.

Pominięty w pełnym przebiegu test wymaga wyłącznego prawa do okna pierwszoplanowego. Uruchomiony osobno (`VerifiedSelectionWithoutWin32IndexesUsesClipboardPasteFallback`) zakończył się powodzeniem: 1 zaliczony, 0 pominiętych. Potwierdza wklejenie przez `Ctrl+V` i odtworzenie schowka w kontrolce bez zapisanych indeksów Win32.

Nowe testy integracyjne potwierdzają dostarczenie komunikatu `WM_HOTKEY` do okna komunikatów oraz zachowanie pierwszego skrótu po wymuszonym konflikcie rejestracji drugiego skrótu.

Testy integracyjne potwierdzają także rzeczywiste pokazanie popupu i podglądu `10+5 = 15`, wykrycie dokładnego zaznaczenia standardowej kontrolki oraz pełny przepływ względny: zaznaczone `25mm`, wyrażenie `+5`, wynik i zastąpienie tekstu `30mm`. Test awaryjnego `SendInput` dla kontrolki bez indeksów Win32 został pominięty przez runner, ale ta ścieżka została dodatkowo sprawdzona na rzeczywistym polu tekstowym przeglądarki Chromium.

Końcowy test przeglądarkowy wykrył właściwą przyczynę wcześniejszych odmów: zarządzana definicja unii Win32 `INPUT` była za mała w procesie x64, więc `SendInput` zwracał błąd 87. Po dodaniu największego wariantu unii rozmiar jest sprawdzany regresyjnie (`40` bajtów x64, `28` bajtów x86). Na rzeczywistym polu wyszukiwania Chromium potwierdzono dwa przepływy: zaznaczone `42345` i `+3` zostało zastąpione przez `42348`, a wyrażenie bezwzględne `5+3` w pustym polu z kursorem wstawiło `8`.

Po kolejnym raporcie usunięto błędny warunek wymagający, aby dostawca UI Automation po utracie i odzyskaniu fokusu zwrócił identyczny tekst zaznaczenia. Zaznaczenie niestandardowych edytorów jest teraz dodatkowo wykrywane przez tymczasowe `Ctrl+C`, a wynik trafia do nich przez standardowe `Ctrl+V` z odtworzeniem wcześniejszego schowka. Test integracyjny wykrył i naprawił wyścig, w którym stary schowek mógł zostać przywrócony przed obsłużeniem `Ctrl+V`.

Dodane regresje sprawdzają pełne zaznaczenie dłuższego wyniku, pozycję kursora po wstawieniu bez zaznaczenia, zachowanie odstępu przed jednostką, przedrostki SI i jednostki elektroniczne, potęgowanie, pierwiastek `r` oraz zatwierdzenie z konwersją SI przez `Shift+Enter` (`2cm / 500 → 0.04mm`, `5ft → 1.524m`).

Po raporcie o opóźnieniach Visual Studio tryb schowka został całkowicie oddzielony od analizy zaznaczenia. Zapytanie UI Automation ma limit 45 ms i nie może blokować kolejnych wywołań, a awaryjna próba `Ctrl+C` czeka najwyżej około 20 ms. Dziennik zapisuje osobno czas przechwycenia i całkowity czas pokazania popupu. Testy potwierdzają ograniczoną latencję, natychmiastowe przechwycenie podstawowe, zamknięcie popupu po sukcesie oraz ustawienie okna pod prostokątem pola tekstowego.

Po regresjach w VS Code i pasku adresu Opery usunięto ponowne używanie zapamiętanego zakresu UI Automation po zmianie fokusu. Taki zakres COM bywa unieważniany przez przeglądarkę i mógł zmienić albo wyczyścić pole przy anulowaniu. Pusta treść niezerowego zakresu UI Automation jest uzupełniana krótką próbą `Ctrl+C`; wynik jest uznawany za zaznaczenie tylko wtedy, gdy parser rozpoznaje liczbę z opcjonalną jednostką, dzięki czemu funkcja VS Code „kopiuj cały wiersz bez zaznaczenia” nie daje fałszywego wyniku. Ponowne zaznaczenie wklejonego rezultatu korzysta z sekwencji klawiaturowej, która utrzymuje Shift logicznie przypisany do strzałek aż do ich obsłużenia przez kolejkę docelowej aplikacji.

Po wykryciu blokującego polecenia kopiowania w Visual Studio tryb bez zaznaczenia nie uruchamia już awaryjnego `Ctrl+C`. Próba schowka jest dopuszczona wyłącznie wtedy, gdy UI Automation jednoznacznie zgłosi niezerową długość zakresu, lecz nie zwróci jego treści. Regresja tej decyzji jest objęta osobnym testem.

Po raporcie użytkownika naprawiono brak bibliotek UI Automation w publikacji, obsługę zaznaczonej liczby bez jednostki (`23` i `+3` daje `26`), interpretację wyniku Enter, przywracanie pierwszego planu oraz dźwięk systemowy Enter/Escape. Skrypt publikujący kopiuje wymagane biblioteki i uruchamia gotowy EXE z `--self-test`; publikacja jest odrzucana, jeżeli UI Automation lub względny parser nie załadują się z katalogu dystrybucyjnego.

Po wykryciu `TypeLoadException` usunięto niezgodne biblioteki GAC .NET Framework 4.0. Projekt korzysta teraz z oficjalnego `Microsoft.WindowsDesktop.App.WPF` dla .NET 9. Publikacja zawiera `UIAutomationClient` i `UIAutomationTypes` w wersji 9.0.0.0; suma biblioteki klienckiej została porównana z pakietem runtime .NET 9 i była identyczna.

Test konfliktów wykonany również bez uruchomionego QuickCalc wykazał błąd Win32 `1409` dla zwykłych klawiszy `F13` i `F15`. Oznacza to, że są wcześniej rejestrowane przez inny proces lub sterownik. Na życzenie użytkownika końcowa konfiguracja domyślna używa `F16` i `Ctrl+F16`. Skróty można zmieniać w czasie działania programu z okna diagnostycznego; nieudana rejestracja przywraca poprzednią konfigurację.

Pominięty test otwiera rzeczywistą kontrolkę WinForms i wymaga przyznania fokusu pierwszoplanowego. Bieżąca sesja testowa nie udostępniła aplikacji natywnych warstwie automatyzacji i Windows nie przyznał fokusu oknu procesu testowego. Nie jest to raportowane jako pozytywna weryfikacja GUI.

Samowystarczalna publikacja x64 została utworzona, a `QuickCalc.App.exe` uruchomił się i pozostawał aktywny podczas testu dymnego.

## Niezweryfikowane automatycznie

- zachowanie w każdej konkretnej wersji Opery GX i aplikacji ChatGPT; mechanizm UI Automation oraz wstawienie Unicode zweryfikowano w Chromium;
- współpraca z kontrolkami innych frameworków i aplikacjami na innym poziomie integralności;
- Altium Designer 18.0.12 — nie był zainstalowany ani dostępny. Wykryto katalog Altium AD25, ale innej wersji nie użyto jako dowodu zgodności;
- zachowanie przy zmianach topologii monitorów/DPI w trakcie otwarcia okna.

Te punkty wymagają testów ręcznych opisanych w README. Sam fakt kompilacji kodu Windows nie jest traktowany jako potwierdzenie zgodności z konkretną aplikacją.

## Znane ograniczenia

- UI Automation TextPattern pozwala potwierdzić zaznaczenie, ale nie zapewnia uniwersalnej modyfikacji tekstu. W kontrolkach niestandardowych QuickCalc korzysta ze standardowego zachowania zaznaczenia podczas `Ctrl+V`; aplikacje, które celowo kasują zaznaczenie przy utracie fokusu, mogą nie obsługiwać zastąpienia.
- Windows UIPI blokuje sterowanie aplikacją uruchomioną z wyższymi uprawnieniami.
- Obsługiwany jest wymiar długości; pola, potęgi, funkcje i złożone jednostki nie należą do prototypu.
- Historia jest przechowywana tylko w pamięci bieżącego procesu.
