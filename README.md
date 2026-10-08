# QuickCalc

Kalkulator działający w tle na Windows 10/11. Naciskasz skrót, wpisujesz działanie, a wynik trafia prosto do pola, w którym pracujesz (albo do schowka).

## Instalacja na nowym komputerze

Nie potrzebujesz Visual Studio, .NET ani klonowania repozytorium.

1. Pobierz **[QuickCalc-portable-win-x64.zip](https://github.com/HubertBorzymek/QuickCalc/releases/latest/download/QuickCalc-portable-win-x64.zip)** (zawsze najnowsza wersja; lista wszystkich wersji jest w zakładce [Releases](https://github.com/HubertBorzymek/QuickCalc/releases)).
2. Rozpakuj ZIP w stałe miejsce, np. `Dokumenty\QuickCalc`. Nie uruchamiaj programu z wnętrza ZIP-a ani z folderu Pobrane, który potem wyczyścisz.
3. W rozpakowanym folderze kliknij dwukrotnie **`Install-QuickCalc-Autostart.cmd`** — QuickCalc będzie startował przy każdym logowaniu.
4. Kliknij dwukrotnie **`Start-QuickCalc.cmd`**, żeby uruchomić go od razu. Ikona pojawi się w zasobniku obok zegara.

Jeśli Windows SmartScreen pokaże ostrzeżenie „Nieznany wydawca”, wybierz **Więcej informacji → Uruchom mimo to** (aplikacja nie jest podpisana certyfikatem).

## Aktualizacja

W folderze QuickCalc kliknij dwukrotnie **`Update-QuickCalc.cmd`**. Skrypt sprawdzi najnowszą wersję na GitHubie, zamknie QuickCalc, podmieni pliki i uruchomi program ponownie. Twoje skróty i ustawienia (`hotkeys.json`) zostają zachowane, autostartu nie trzeba instalować ponownie.

Wersje starsze niż `v1.1.0` nie mają jeszcze tego skryptu. Żeby je zaktualizować pierwszy raz: zakończ QuickCalc (prawy klik na ikonie → **Zakończ**), pobierz ZIP z linku powyżej i rozpakuj go do tego samego folderu, zastępując pliki. Od tej pory wystarczy `Update-QuickCalc.cmd`.

### Autostart po aktualizacji

W autostarcie jest zawsze jeden skrót `QuickCalc.lnk`, który wskazuje na konkretny folder.

- **Nowa wersja w tym samym folderze** (`Update-QuickCalc.cmd` albo rozpakowanie na stare pliki) — nic nie trzeba robić, skrót dalej działa.
- **Nowa wersja w innym folderze** — uruchom nową kopię, kliknij prawym przyciskiem ikonę w zasobniku i zaznacz **Uruchamiaj przy starcie Windows**. Skrót zostanie przepięty na nową kopię (jeśli wskazywał na starą, opcja ma dopisek „teraz: inna kopia”). Potem stary folder możesz usunąć. To samo robi ponowne uruchomienie `Install-QuickCalc-Autostart.cmd` z nowego folderu.

Żeby pokazać ikonę na stałe obok zegara zamiast w ukrytych ikonach, przeciągnij ją z rozwijanej listy na pasek zadań (albo: Ustawienia → Personalizacja → Pasek zadań → Inne ikony zasobnika → QuickCalc).

## Używanie

| Klawisz | Działanie |
|---|---|
| `F16` | Kalkulator kontekstowy — wynik zastępuje zaznaczenie albo trafia w miejsce kursora. Ponowne `F16` przełącza widok Compact ↔ Expanded. |
| `Ctrl+F16` | Kalkulator schowka — wynik jest kopiowany, nic nie jest wklejane. |
| `Enter` | Zastosuj wynik, zachowując jednostkę. |
| `Shift+Enter` | Zastosuj wynik w czytelnej jednostce SI (np. `0.0000047 F` → `4.7 µF`). |
| `Esc` | Anuluj bez zmiany pola i schowka. |
| `↑` / `↓` | Historia wyrażeń. |

Menu ikony w zasobniku (prawy klik):

- **Diagnostyka…** — zmiana skrótów (kliknij pole i naciśnij nową kombinację), stan rejestracji skrótów, dziennik.
- **Zamykaj popup po utracie fokusu** — kliknięcie innego okna anuluje kalkulator. Zalecane.
- **Uruchamiaj przy starcie Windows** — dodaje/usuwa tę kopię QuickCalc z autostartu.
- **Resetuj popupy** — awaryjnie zamyka wszystkie okna kalkulatora.
- **Zakończ** — wyłącza QuickCalc.

Ustawienia są zapisywane w `hotkeys.json` obok programu.

## Wyrażenia

| Zapis | Znaczenie | Przykład |
|---|---|---|
| `+ - * /`, nawiasy | podstawowe działania | `(2+3)*4` |
| `p` | potęga | `2p8` = `256` |
| `r`, `√` | pierwiastek kwadratowy | `r81` = `9` |
| `ln`, `log` | logarytm naturalny / dziesiętny | `log1000` = `3` |
| `abs` | wartość bezwzględna | `abs(-5)` |
| `e`, `pi`, `π` | stałe | `2*pi` |
| `x` | zaznaczona wartość | `(x+5)/2`, `1/x` |
| `+5`, `*2`, `r`, `log` … | działanie względne na zaznaczeniu | zaznaczone `25mm`, wpisz `+5` → `30mm` |
| `\|\|` | połączenie równoległe `R1*R2/(R1+R2)` | zaznaczone `100Ω`, wpisz `\|\|100` → `50Ω` |
| `& \| ^ ! << >>` | AND, OR, XOR, NOT, przesunięcia (tylko liczby całkowite) | `!0xF0` = `0x0F` |
| `0x…`, `0b…` | literały HEX / BIN, można mieszać z DEC | `10+0x10` |
| `hex`, `bin`, `dec` na końcu | wymuszenie formatu wyniku; samo słowo konwertuje zaznaczenie | `255 hex` = `0xFF` |
| `u2` | wzorzec BIN/HEX w kodzie U2 → DEC | `u2(0b1000)` = `-8` |

Uwaga: `^` to XOR, a nie potęga — potęga to `p`.

Jednostki: przedrostki SI (`u` = `µ`) oraz `m`, `kg`, `s`, `A`, `K`, `mol`, `cd`, `V`, `Ω`/`ohm`, `F`, `H`, `Hz`, `W`, `N`, `Pa`, `J`, `C`, `S`, `Wb`, `T`, `in`/`inch`, `ft`, `yd`, `mi`, `mil`, `min`, `hr`. Spacja przed jednostką jest zachowywana (`10 mm/2` → `5 mm`). Odwrotność czasu i częstotliwości zmienia wymiar (`1/x` dla `2.5 kHz` → `400 µs`).

## Ograniczenia

- QuickCalc uruchomiony bez uprawnień administratora nie może wpisywać do programów uruchomionych jako administrator.
- W nietypowych edytorach (przeglądarki, VS Code, Altium) wynik jest wstawiany przez chwilowe użycie schowka; poprzednia zawartość schowka jest przywracana.
- Niektóre aplikacje przestają podświetlać zaznaczenie, gdy popup przejmie fokus — zaznaczenie nadal istnieje.
- Błędy interfejsu są zapisywane w `quickcalc-error.log` obok programu.

## Dla dewelopera

Wymagany .NET 9 SDK (Visual Studio 2026: otwórz `QuickCalc.sln`, projekt startowy `QuickCalc.App`, F5).

```powershell
dotnet build QuickCalc.sln -c Debug
dotnet test QuickCalc.sln -c Release
.\Create-QuickCalc-Release.ps1      # paczka portable w artifacts\release
```

Ikonę (`src\QuickCalc.App\QuickCalc.ico`) generuje `tools\New-QuickCalcIcon.ps1`.

`Start-QuickCalc.cmd` i `Install-QuickCalc-Autostart.cmd` w katalogu głównym repozytorium uruchamiają lokalną publikację z `artifacts\publish` (tworzy ją `Publish-QuickCalc.ps1`). Pliki dla użytkownika końcowego leżą w `packaging\portable`.

### Wydawanie nowej wersji

Wypchnięcie taga `v*` uruchamia GitHub Actions ([`release.yml`](.github/workflows/release.yml)): testy, budowa paczki portable i publikacja w Releases. Od tej chwili `Update-QuickCalc.cmd` na innych komputerach pobierze nową wersję.

```powershell
git tag v1.1.0
git push origin v1.1.0
```

Struktura: `src/QuickCalc.Core` — parser i jednostki, `src/QuickCalc.Windows` — skróty globalne, zaznaczenie i wstawianie (Win32, UI Automation), `src/QuickCalc.App` — tray i popup, `tests/` — testy jednostkowe, integracyjne i `QuickCalc.TestHost` do ręcznych testów. Szczegóły testów: [docs/TEST_REPORT.md](docs/TEST_REPORT.md).
