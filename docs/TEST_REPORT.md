# Raport testów QuickCalc

Data: 2026-09-30

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

- `QuickCalc.Core.Tests`: 173 zaliczone, 0 niezaliczonych, 0 pominiętych;
- `QuickCalc.Integration.Tests`: 30 zaliczonych, 0 niezaliczonych, 1 pominięty;
- łącznie: 204 przypadki, z czego 203 zaliczone i 1 pominięty.

Testy klawiaturowego `Ctrl+C` i awaryjnego `Ctrl+V` wymagają wyłącznego prawa do okna pierwszoplanowego. W ostatnim pełnym przebiegu runner nie przyznał go testowi kopiowania, dlatego został oznaczony jako pominięty zamiast fałszywie pozytywnego. Test wklejenia i ponownego zaznaczenia przeszedł; oba przepływy mają także niezależne testy polityki i schowka.

Nowe testy integracyjne potwierdzają dostarczenie komunikatu `WM_HOTKEY` do okna komunikatów oraz zachowanie pierwszego skrótu po wymuszonym konflikcie rejestracji drugiego skrótu.

Testy integracyjne potwierdzają także rzeczywiste pokazanie popupu i podglądu `10+5 = 15`, wykrycie dokładnego zaznaczenia standardowej kontrolki oraz pełny przepływ względny: zaznaczone `25mm`, wyrażenie `+5`, wynik i zastąpienie tekstu `30mm`. Test awaryjnego `SendInput` dla kontrolki bez indeksów Win32 został pominięty przez runner, ale ta ścieżka została dodatkowo sprawdzona na rzeczywistym polu tekstowym przeglądarki Chromium.

Końcowy test przeglądarkowy wykrył właściwą przyczynę wcześniejszych odmów: zarządzana definicja unii Win32 `INPUT` była za mała w procesie x64, więc `SendInput` zwracał błąd 87. Po dodaniu największego wariantu unii rozmiar jest sprawdzany regresyjnie (`40` bajtów x64, `28` bajtów x86). Na rzeczywistym polu wyszukiwania Chromium potwierdzono dwa przepływy: zaznaczone `42345` i `+3` zostało zastąpione przez `42348`, a wyrażenie bezwzględne `5+3` w pustym polu z kursorem wstawiło `8`.

Po kolejnym raporcie usunięto błędny warunek wymagający, aby dostawca UI Automation po utracie i odzyskaniu fokusu zwrócił identyczny tekst zaznaczenia. Zaznaczenie niestandardowych edytorów jest teraz dodatkowo wykrywane przez tymczasowe `Ctrl+C`, a wynik trafia do nich przez standardowe `Ctrl+V` z odtworzeniem wcześniejszego schowka. Test integracyjny wykrył i naprawił wyścig, w którym stary schowek mógł zostać przywrócony przed obsłużeniem `Ctrl+V`.

Dodane regresje sprawdzają pełne zaznaczenie dłuższego wyniku, pozycję kursora po wstawieniu bez zaznaczenia, zachowanie odstępu przed jednostką, przedrostki SI i jednostki elektroniczne, potęgowanie `p`, pierwiastek `r` oraz zatwierdzenie z konwersją SI przez `Shift+Enter`. Parser sprawdza też zmienną zaznaczenia `x`, skróty względne, XOR `^`, wszystkie operatory bitowe, mieszanie DEC/HEX/BIN, dziedziczenie i wymuszanie formatu wyniku oraz niejednoznaczne zakończenia literałów HEX. Osobne testy obejmują pusty `Shift+Enter` z jednostką i odwrotność częstotliwości `2.5 kHz → 400 µs`.

Kolejne regresje obejmują skrótowe operatory bitowe i `p` względem zaznaczenia, konwersję zaznaczenia przez samo `hex`/`bin`/`dec`, bitowy NOT `!`, odrzucenie `~`, `abs`, połączenie równoległe `||` dla skalarów i rezystancji oraz anulowanie popupu po utracie fokusu bez przywracania poprzedniego okna na pierwszy plan.

Przebudowany popup jest objęty testami rzeczywistego formularza WinForms: borderless Compact, live preview, inline error ze zmianą wysokości, przełączanie Compact/Expanded z zachowaniem tekstu i fokusu, Enter, Shift+Enter dla jednostki oraz skalara, Escape, pozycja pod polem kontekstowym i pozycja przy kursorze w granicach monitora. Oba warianty zostały dodatkowo wyrenderowane i sprawdzone wizualnie. Aplikacja używa trybu DPI `PerMonitorV2`, a zmianę DPI obsługuje ponownym przeliczeniem układu i położenia.

Po raporcie o opóźnieniach Visual Studio tryb schowka został całkowicie oddzielony od analizy zaznaczenia. Zapytanie UI Automation ma limit 45 ms i nie może blokować kolejnych wywołań, a awaryjna próba `Ctrl+C` czeka najwyżej około 20 ms. Dziennik zapisuje osobno czas przechwycenia i całkowity czas pokazania popupu. Testy potwierdzają ograniczoną latencję, natychmiastowe przechwycenie podstawowe, zamknięcie popupu po sukcesie oraz ustawienie okna pod prostokątem pola tekstowego.

Po regresjach w VS Code i pasku adresu Opery usunięto ponowne używanie zapamiętanego zakresu UI Automation po zmianie fokusu. Taki zakres COM bywa unieważniany przez przeglądarkę i mógł zmienić albo wyczyścić pole przy anulowaniu. Pusta treść niezerowego zakresu UI Automation jest uzupełniana krótką próbą `Ctrl+C`; wynik jest uznawany za zaznaczenie tylko wtedy, gdy parser rozpoznaje liczbę z opcjonalną jednostką, dzięki czemu funkcja VS Code „kopiuj cały wiersz bez zaznaczenia” nie daje fałszywego wyniku. Ponowne zaznaczenie wklejonego rezultatu korzysta z sekwencji klawiaturowej, która utrzymuje Shift logicznie przypisany do strzałek aż do ich obsłużenia przez kolejkę docelowej aplikacji.

Po wykryciu blokującego polecenia kopiowania w Visual Studio tryb bez zaznaczenia nie uruchamia już awaryjnego `Ctrl+C`. Próba schowka jest dopuszczona wyłącznie wtedy, gdy UI Automation jednoznacznie zgłosi niezerową długość zakresu, lecz nie zwróci jego treści. Regresja tej decyzji jest objęta osobnym testem.

Po testach aplikacji GPT i pól Altium wyszukiwanie wzorca tekstowego objęło także nadrzędne kontenery aktywnego elementu. Kontrolki realizujące protokół komunikatów Win32 Edit są wykrywane również przy niestandardowej nazwie klasy, z limitami czasu chroniącymi przed zawieszonym oknem. Zaznaczone `-18.3mm` i wynik bez jednostki zachowuje `mm`. Dla braku zaznaczenia zapamiętany pusty zakres UI Automation odtwarza pozycję kursora po powrocie fokusu, ale zakres zawierający zaznaczenie nadal nie jest ponownie aktywowany ze względu na błąd Opery. Parser względny obsługuje zmienną `x`, samo `r`/`√` oraz dalsze działania, np. `81` z `r*2+1` daje `19`.

Ponieważ część pól Altium nie udostępnia zakresu ani przez `EM_GETSEL`, ani UI Automation, dodano bezpośredni `WM_COPY` do aktywnej kontrolki. W przeciwieństwie do klawiaturowego `Ctrl+C` nie uruchamia on polecenia edytora Visual Studio. Schowek jest oznaczany wartością kontrolną i odtwarzany; wynik jest przyjmowany tylko wtedy, gdy kontrolka faktycznie skopiuje poprawną liczbę z opcjonalną jednostką. Test integracyjny potwierdza odczyt zaznaczonego `25mm` oraz brak fałszywego wyniku przy samym kursorze.

Po potwierdzeniu, że pole Altium nie obsługuje również `WM_COPY`, klawiaturowe `Ctrl+C` stało się końcowym fallbackiem dla nieprzezroczystych kontrolek. Jest wykonywane przed pokazaniem popupu, a schowek zostaje odtworzony. Wyjątkiem jest niepotwierdzone zaznaczenie w procesie `devenv`: Visual Studio mapuje `Ctrl+C` bez zaznaczenia na asynchroniczne polecenie edytora, którego nie można anulować po wysłaniu i które wcześniej powodowało blokujący dialog. Test polityki potwierdza włączenie fallbacku dla Altium/X2 i wyłączenie wyłącznie dla `devenv`.

Raport z Altium 18.0.12 potwierdził poprawne wykrycie zaznaczenia przez klawiaturowe `Ctrl+C`, ale ujawnił brak weryfikacji fokusu przed wstawieniem. `Ctrl+V` korzysta teraz z oczekującego `SendKeys.SendWait`, tak samo jak działające kopiowanie. Fokus jest sprawdzany na rzeczywistym wątku kontrolki przed i po wklejeniu. Jeżeli pole go nie odzyska lub utraci, QuickCalc nie wysyła `Shift+Left`, dzięki czemu strzałki nie trafiają do interfejsu aplikacji. Diagnostyka zapisuje uchwyt oczekiwany i rzeczywisty oraz zastosowaną metodę wklejenia. Osobna regresja potwierdza odmowę wstawienia do kontrolki, która nie może odzyskać fokusu.

Po raporcie użytkownika naprawiono brak bibliotek UI Automation w publikacji, obsługę zaznaczonej liczby bez jednostki (`23` i `+3` daje `26`), interpretację wyniku Enter, przywracanie pierwszego planu oraz dźwięk systemowy Enter/Escape. Skrypt publikujący kopiuje wymagane biblioteki i uruchamia gotowy EXE z `--self-test`; publikacja jest odrzucana, jeżeli UI Automation lub względny parser nie załadują się z katalogu dystrybucyjnego.

Po wykryciu `TypeLoadException` usunięto niezgodne biblioteki GAC .NET Framework 4.0. Projekt korzysta teraz z oficjalnego `Microsoft.WindowsDesktop.App.WPF` dla .NET 9. Publikacja zawiera `UIAutomationClient` i `UIAutomationTypes` w wersji 9.0.0.0; suma biblioteki klienckiej została porównana z pakietem runtime .NET 9 i była identyczna.

Test konfliktów wykonany również bez uruchomionego QuickCalc wykazał błąd Win32 `1409` dla zwykłych klawiszy `F13` i `F15`. Oznacza to, że są wcześniej rejestrowane przez inny proces lub sterownik. Na życzenie użytkownika końcowa konfiguracja domyślna używa `F16` i `Ctrl+F16`. Skróty można zmieniać w czasie działania programu z okna diagnostycznego; nieudana rejestracja przywraca poprzednią konfigurację.

Pominięty test otwiera rzeczywistą kontrolkę WinForms i wymaga przyznania fokusu pierwszoplanowego. Bieżąca sesja testowa nie udostępniła aplikacji natywnych warstwie automatyzacji i Windows nie przyznał fokusu oknu procesu testowego. Nie jest to raportowane jako pozytywna weryfikacja GUI.

Samowystarczalna publikacja x64 została utworzona, a `QuickCalc.App.exe` uruchomił się i pozostawał aktywny podczas testu dymnego.

Plik `Start-QuickCalc.cmd` został sprawdzony jako niezależny launcher gotowej publikacji: uruchomił właściwy EXE z poprawnym katalogiem roboczym. Ponowne uruchomienie zakończyło proces pomocniczy i pozostawiło jedną instancję. `Install-QuickCalc-Autostart.cmd` tworzy skrót użytkownika wskazujący na ten sam launcher.

## Niezweryfikowane automatycznie

- zachowanie w każdej konkretnej wersji Opery GX i aplikacji ChatGPT; mechanizm UI Automation oraz wstawienie Unicode zweryfikowano w Chromium;
- współpraca z kontrolkami innych frameworków i aplikacjami na innym poziomie integralności;
- Altium Designer 18.0.12 — nie był zainstalowany ani dostępny. Wykryto katalog Altium AD25, ale innej wersji nie użyto jako dowodu zgodności;
- zachowanie przy zmianach topologii monitorów/DPI w trakcie otwarcia okna.

Te punkty wymagają testów ręcznych opisanych w README. Sam fakt kompilacji kodu Windows nie jest traktowany jako potwierdzenie zgodności z konkretną aplikacją.

## Znane ograniczenia

- UI Automation TextPattern pozwala potwierdzić zaznaczenie, ale nie zapewnia uniwersalnej modyfikacji tekstu. W kontrolkach niestandardowych QuickCalc korzysta ze standardowego zachowania zaznaczenia podczas `Ctrl+V`; aplikacje, które celowo kasują zaznaczenie przy utracie fokusu, mogą nie obsługiwać zastąpienia.
- Windows UIPI blokuje sterowanie aplikacją uruchomioną z wyższymi uprawnieniami.
- Potęgowanie, pierwiastki i logarytmy działają wyłącznie na skalarach; potęgi jednostek i jednostki złożone nie należą do prototypu. Dostępny jest pierwiastek kwadratowy oraz logarytmy o podstawie `e` i 10, bez osobnej składni dla innych stopni i podstaw.
- Historia jest przechowywana tylko w pamięci bieżącego procesu.
