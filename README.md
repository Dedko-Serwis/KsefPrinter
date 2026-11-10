# KsefPrinter

KsefPrinter to aplikacja Windows do wczytywania faktur KSeF w formacie XML FA(3), przygotowania podglądu oraz drukowania.

## Darmowy również do użytku komercyjnego

**KsefPrinter jest darmowy — także dla firm i do zastosowań komercyjnych.** Możesz bez opłat używać programu do obsługi własnych faktur, również w działalności gospodarczej. Nie jest wymagany zakup licencji, abonament ani zgoda autora na samo użytkowanie.

Program jest udostępniany na licencji **GNU GPL v3.0**. Możesz również analizować, modyfikować i rozpowszechniać jego kod zgodnie z warunkami GPLv3. Przy rozpowszechnianiu programu lub wersji zmodyfikowanej obowiązują wymagania licencji, w tym zachowanie informacji o autorze i udostępnienie odpowiedniego kodu źródłowego odbiorcom. Szczegóły: [LICENSE](LICENSE) i [NOTICE.md](NOTICE.md).

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

## Schematy KSeF i zależności

Do odczytu dokumentów FA(3) projekt zawiera schematy XSD pochodzące z oficjalnych materiałów Ministerstwa Finansów, opublikowanych w [Centralnym Repozytorium Wzorów Dokumentów Elektronicznych](https://crd.gov.pl/wzor/2025/06/25/13775/). Schematy są materiałami zewnętrznymi; licencja GPLv3 dotycząca kodu KsefPrinter nie jest deklaracją praw autorskich do tych schematów.

Aplikacja wykorzystuje bibliotekę NuGet **QRCoder** do obsługi kodów QR. Projekty testowe wykorzystują **xUnit**, **Microsoft.NET.Test.Sdk** i **xunit.runner.visualstudio**. Każda z tych zależności podlega własnym warunkom licencyjnym; przed dystrybucją pakietów binarnych należy sprawdzić obowiązujące licencje i wymagane informacje o prawach autorskich.

## Prywatność danych

Faktury mogą zawierać dane osobowe i handlowe. **Nie umieszczaj rzeczywistych faktur w zgłoszeniach GitHub, commitach ani pull requestach.** Do testów i zgłoszeń stosuj dane syntetyczne lub skutecznie zanonimizowane.

## Licencja

Program jest dostępny bezpłatnie, również do użytku komercyjnego, na warunkach GNU GPL v3.0. Dodatkowy warunek dotyczący zachowania informacji o autorze i odnośnika do projektu opisano w [NOTICE.md](NOTICE.md) (sekcja 7(b) GPLv3). Pełny tekst licencji: [LICENSE](LICENSE).

Biblioteki i materiały osób trzecich mogą podlegać odrębnym licencjom.
