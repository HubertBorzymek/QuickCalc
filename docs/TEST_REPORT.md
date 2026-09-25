# Raport testów QuickCalc

Data: 2026-09-25

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

- `QuickCalc.Core.Tests`: 32 zaliczone, 0 niezaliczonych, 0 pominiętych;
- `QuickCalc.Integration.Tests`: 8 zaliczonych, 0 niezaliczonych, 1 pominięty;
- łącznie: 41 przypadków, z czego 40 zaliczonych i 1 pominięty.

Nowe testy integracyjne potwierdzają dostarczenie komunikatu `WM_HOTKEY` do okna komunikatów oraz zachowanie pierwszego skrótu po wymuszonym konflikcie rejestracji drugiego skrótu.

Testy integracyjne potwierdzają także rzeczywiste pokazanie popupu i podglądu `10+5 = 15`, wykrycie dokładnego zaznaczenia standardowej kontrolki oraz pełny przepływ względny: zaznaczone `25mm`, wyrażenie `+5`, wynik i zastąpienie tekstu `30mm`. Test awaryjnego `SendInput` dla kontrolki bez indeksów Win32 został pominięty, ponieważ runner testów nie dostał prawa do okna pierwszoplanowego; sama ścieżka jest wykonywana po skrócie użytkownika i wymaga końcowej kontroli w konkretnej przeglądarce.

Test konfliktów wykonany również bez uruchomionego QuickCalc wykazał błąd Win32 `1409` dla zwykłych klawiszy `F13` i `F15`. Oznacza to, że są wcześniej rejestrowane przez inny proces lub sterownik. Na życzenie użytkownika końcowa konfiguracja domyślna używa `F16` i `Ctrl+F16`. Skróty można zmieniać w czasie działania programu z okna diagnostycznego; nieudana rejestracja przywraca poprzednią konfigurację.

Pominięty test otwiera rzeczywistą kontrolkę WinForms i wymaga przyznania fokusu pierwszoplanowego. Bieżąca sesja testowa nie udostępniła aplikacji natywnych warstwie automatyzacji i Windows nie przyznał fokusu oknu procesu testowego. Nie jest to raportowane jako pozytywna weryfikacja GUI.

Samowystarczalna publikacja x64 została utworzona, a `QuickCalc.App.exe` uruchomił się i pozostawał aktywny podczas testu dymnego.

## Niezweryfikowane automatycznie

- rzeczywisty globalny przepływ klawiatury, fokus, zaznaczenie i wstawienie w interaktywnym pulpicie;
- współpraca z kontrolkami innych frameworków i aplikacjami na innym poziomie integralności;
- Altium Designer 18.0.12 — nie był zainstalowany ani dostępny. Wykryto katalog Altium AD25, ale innej wersji nie użyto jako dowodu zgodności;
- zachowanie przy zmianach topologii monitorów/DPI w trakcie otwarcia okna.

Te punkty wymagają testów ręcznych opisanych w README. Sam fakt kompilacji kodu Windows nie jest traktowany jako potwierdzenie zgodności z konkretną aplikacją.

## Znane ograniczenia

- UI Automation TextPattern pozwala potwierdzić zaznaczenie, ale nie zapewnia uniwersalnej modyfikacji tekstu. Gdy nie można odtworzyć dokładnego zakresu, QuickCalc bezpiecznie odmawia zastąpienia. Wstawianie bez zaznaczenia w kontrolkach niestandardowych zależy od ich obsługi wejścia Unicode.
- Windows UIPI blokuje sterowanie aplikacją uruchomioną z wyższymi uprawnieniami.
- Obsługiwany jest wymiar długości; pola, potęgi, funkcje i złożone jednostki nie należą do prototypu.
- Historia jest przechowywana tylko w pamięci bieżącego procesu.
