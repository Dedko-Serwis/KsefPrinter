# KsefPrinter

KsefPrinter to aplikacja Windows do wczytywania faktur KSeF w formacie XML FA(3), przygotowania podglądu oraz drukowania.

**Autor:** DEDKO-Serwis  
**Projekt:** https://github.com/Dedko-Serwis/KsefPrinter

## Funkcje

- Wczytywanie faktur XML metodą przeciągnij i upuść
- Obsługa faktur standardowych, korygujących, zaliczkowych i rozliczeniowych
- Podgląd wydruku i wybór drukarki
- Obsługa kodu QR do weryfikacji faktury KSeF
- Konfiguracja sposobu prezentacji pozycji i cen
- Zapamiętywanie rozmiaru, położenia i powiększenia okna podglądu
- Sprawdzanie dostępności aktualizacji w GitHub Releases; instalacja wyłącznie po decyzji użytkownika (funkcja rozwijana)

## Wymagania

Windows 10/11, platforma .NET 10 (Windows Desktop Runtime, jeżeli publikacja nie zawiera środowiska uruchomieniowego).

## Kompilacja

1. Zainstaluj .NET 10 SDK i Visual Studio z obsługą aplikacji WPF.
2. Otwórz `KsefPrinter.sln`.
3. Zbuduj projekt `KsefPrinter` w konfiguracji Release.

Z wiersza poleceń w Windows:

```powershell
dotnet restore KsefPrinter.sln
dotnet build KsefPrinter.sln -c Release
dotnet test KsefPrinter.Tests/KsefPrinter.Tests.csproj -c Release
```

## Użytkowanie

Przeciągnij plik faktury XML na pole w zakładce **Drukowanie**. W zakładce **Ustawienia** wybierz drukarkę i parametry wydruku. Przed drukiem sprawdź poprawność dokumentu i danych.

## Aktualizacje

Planowane wydania są publikowane w sekcji [Releases](https://github.com/Dedko-Serwis/KsefPrinter/releases). Aplikacja może okresowo sprawdzać ich dostępność. Pobranie i uruchomienie instalatora wymaga potwierdzenia użytkownika. Do czasu opublikowania odpowiedniego instalatora mechanizm aktualizacji nie będzie kompletny.

## Prywatność danych

Faktury mogą zawierać dane osobowe i handlowe. **Nie umieszczaj rzeczywistych faktur w zgłoszeniach GitHub, commitach ani pull requestach.** Do testów i zgłoszeń stosuj dane syntetyczne lub skutecznie zanonimizowane.

## Licencja

Kod źródłowy: GNU GPL v3.0 z dodatkowym warunkiem zachowania informacji o autorze i odnośnika do projektu (sekcja 7(b) GPLv3). Zobacz [LICENSE](LICENSE) i [NOTICE.md](NOTICE.md).

Biblioteki i materiały osób trzecich mogą podlegać odrębnym licencjom.
