# Lista kontrolna przed upublicznieniem

**STOP: nie zmieniaj widoczności repozytorium, dopóki wszystkie punkty nie zostaną wykonane.**

- [ ] Usunięto rzeczywiste faktury XML/PDF z bieżących gałęzi oraz wszystkich tagów i gałęzi historycznych.
- [ ] Przepisano historię Git tak, aby rzeczywiste faktury nie były dostępne przez stare commity; wypchnięto oczyszczone referencje.
- [ ] Sprawdzono Pull Requests, Issues, załączniki, Actions i Releases pod kątem tych samych danych.
- [ ] Zweryfikowano, że w kodzie nie ma tokenów, haseł, prywatnych adresów i innych sekretów.
- [ ] Sprawdzono prawa do ikon, grafiki png, schematów XSD i innych zasobów; materiały bez odpowiedniej licencji zastąpiono lub usunięto.
- [ ] Sprawdzono licencje zależności (m.in. QRCoder) i wymagane informacje prawne.
- [ ] Zastąpiono dane testowe danymi syntetycznymi i uruchomiono testy.
- [ ] Uzupełniono pełny, niezmodyfikowany tekst GPLv3 w pliku LICENSE.
- [ ] Zweryfikowano instrukcję instalacji, kompilację i instalator.
- [ ] Dopiero wtedy zmieniono repozytorium na publiczne.

## Oczyszczenie historii

Samo `git rm` nie wystarcza. Do usunięcia plików ze wszystkich commitów można wykorzystać `git filter-repo` na świeżym lokalnym klonie lustrzanym. Przykładowe ścieżki do wykluczenia: `xml/`, `Wydruki/`, `KsefPrinter/Resources/*.xml` (po przeglądzie), inne faktury i pliki PDF. Po przepisaniu historii wymagane jest ostrożne wypchnięcie zmian oraz weryfikacja wszystkich gałęzi i tagów. Nie wykonuj tego na jedynej kopii repozytorium.

**Uwaga:** GitHub może zachowywać niektóre niedostępne już obiekty lub odwołania; w razie ujawnienia danych wrażliwych może być potrzebny kontakt z GitHub Support.
