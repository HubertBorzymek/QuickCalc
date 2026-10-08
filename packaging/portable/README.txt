QUICKCALC PORTABLE — WINDOWS 10/11 x64
======================================

Ten folder zawiera kompletna, samowystarczalna aplikacje.
Nie trzeba instalowac .NET ani Visual Studio.

PIERWSZE URUCHOMIENIE
1. Rozpakuj CALY folder w stale miejsce, np. Dokumenty\QuickCalc.
2. Uruchom Install-QuickCalc-Autostart.cmd (QuickCalc wystartuje przy kazdym logowaniu).
3. Kliknij dwukrotnie Start-QuickCalc.cmd, aby uruchomic go od razu.
4. Ikona QuickCalc pojawi sie w zasobniku systemowym (obok zegara).

AKTUALIZACJA
- Uruchom Update-QuickCalc.cmd. Skrypt pobierze najnowsza wersje z GitHub,
  zamknie QuickCalc, podmieni pliki i uruchomi go ponownie.
- Twoje ustawienia (hotkeys.json) zostaja zachowane.

AUTOSTART
- Install-QuickCalc-Autostart.cmd dodaje skrot do autostartu.
- Remove-QuickCalc-Autostart.cmd usuwa skrot.
- Po przeniesieniu folderu ponownie uruchom instalator autostartu.

SKROTY DOMYSLNE
- F16: tryb kontekstowy; kolejne F16 przelacza Compact/Expanded.
- Ctrl+F16: tryb schowka.
- Enter: zastosuj wynik.
- Shift+Enter: zastosuj wynik z czytelna jednostka SI.
- Escape: anuluj bez zmiany pola ani schowka.

WAZNE
- Nie kopiuj samego QuickCalc.App.exe — potrzebuje pozostalych plikow z folderu.
- Ustawienia skrotow sa zapisywane w hotkeys.json obok aplikacji.
- Aplikacja bez uprawnien administratora nie moze sterowac programem uruchomionym
  jako administrator.
