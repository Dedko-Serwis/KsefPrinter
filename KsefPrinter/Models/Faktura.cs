using System.Globalization;

namespace KsefPrinter.Models;

public class Faktura
{
    public string NumerKSeF { get; set; } = "brak danych";
    public string? KodFormularza { get; set; }
    public string? WariantFormularza { get; set; }
    public DateTime DataWytworzenia { get; set; }
    public string? SystemInfo { get; set; }

    public string? KodWaluty { get; set; }
    public DateTime DataWystawienia { get; set; }
    public string? MiejsceWystawienia { get; set; }
    public string? NumerFaktury { get; set; }
    public DateTime? DataDostawy { get; set; }
    public DateTime? DataDostawyOd { get; set; }
    public DateTime? DataDostawyDo { get; set; }
    public string? RodzajFaktury { get; set; }
    public string? PrzyczynaKorekty { get; set; }
    public string? TypKorekty { get; set; }
    public decimal KwotaNaleznosci { get; set; }

    public Sprzedawca? Sprzedawca { get; set; }
    public Nabywca? Nabywca { get; set; }
    public List<PodmiotTrzeci> PodmiotyTrzecie { get; set; } = [];
    public List<PozycjaFaktury> Pozycje { get; set; } = [];
    public decimal? WartoscZamowienia { get; set; }
    public List<PozycjaZamowienia> PozycjeZamowienia { get; set; } = [];
    public List<PodsumowanieVat> PodsumowanieVat { get; set; } = [];
    public WarunkiPlatnosci? Platnosc { get; set; }
    public List<Platnosc> Platnosci { get; set; } = []; // zgodność z obecnym builderem
    public RozliczenieFaktury? Rozliczenie { get; set; }
    public List<DodatkowyOpis> DodatkoweOpisy { get; set; } = [];
    public List<string> NumeryWZ { get; set; } = [];
    public List<DokumentPowiazany> Umowy { get; set; } = [];
    public List<DokumentPowiazany> Zamowienia { get; set; } = [];
    public List<FakturaPowiazana> FakturyKorygowane { get; set; } = [];
    public List<FakturaPowiazana> FakturyZaliczkowe { get; set; } = [];
    public string? WarunkiDostawy { get; set; }
    public List<string> StopkaFaktury { get; set; } = [];
    public List<DaneRejestrowe> Rejestry { get; set; } = [];
}

public class Sprzedawca
{
    public string? NIP { get; set; }
    public string? Nazwa { get; set; }
    public string? KodKraju { get; set; }
    public string? AdresLinia1 { get; set; }
    public string? AdresLinia2 { get; set; }
    public string? GLN { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string PełnyAdres => $"{AdresLinia1} {AdresLinia2}".Trim();
}

public class Nabywca
{
    public string? NIP { get; set; }
    public string? KodUE { get; set; }
    public string? NrVatUE { get; set; }
    public string? Nazwa { get; set; }
    public string? BrakID { get; set; }
    public string? KodKraju { get; set; }
    public string? AdresLinia1 { get; set; }
    public string? AdresLinia2 { get; set; }
    public string? GLN { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string? JST { get; set; }
    public string? GV { get; set; }
    public string PełnyAdres => $"{AdresLinia1} {AdresLinia2}".Trim();
    public string PelnyNIP => !string.IsNullOrEmpty(KodUE) ? $"{KodUE}{NrVatUE}" : (NIP ?? string.Empty);
}

public class PodmiotTrzeci
{
    public string? NIP { get; set; }
    public string? Nazwa { get; set; }
    public string? BrakID { get; set; }
    public string? Rola { get; set; }
    public string? OpisRoli { get; set; }
    public string? Adres { get; set; }
}

public class PozycjaFaktury
{
    public string? NrWiersza { get; set; }
    public string? UU_ID { get; set; }
    public bool StanPrzed { get; set; }
    public DateTime? DataDostawy { get; set; }
    public string? NazwaTowaru { get; set; }
    public string? Miara { get; set; }
    public string? Ilosc { get; set; }
    public string? CenaNetto { get; set; }
    public string? CenaBrutto { get; set; }
    public string? Rabat { get; set; }
    public string? WartoscNetto { get; set; }
    public string? WartoscBrutto { get; set; }
    public string? KwotaVAT { get; set; }
    public string? StawkaVAT { get; set; }
    public string? Indeks { get; set; }
    public string? GTIN { get; set; }
    public string? GTU { get; set; }
    public string? Procedura { get; set; }
    public string? PKWiU { get; set; }
    public string? CN { get; set; }
    public string? PKOB { get; set; }
    public string? KwotaAkcyzy { get; set; }

    public decimal IloscDecimal => ParseDecimal(Ilosc);
    public decimal CenaNettoDecimal => ParseDecimal(CenaNetto);
    public decimal WartoscNettoDecimal => ParseDecimal(WartoscNetto);
    public decimal KwotaVATDecimal => ParseDecimal(KwotaVAT);
    private static decimal ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
}

public class PodsumowanieVat
{
    public string Kod { get; set; } = string.Empty;
    public decimal? PodstawaOpodatkowania { get; set; }
    public decimal? KwotaVat { get; set; }
    public decimal? KwotaVatPln { get; set; }
}

public class WarunkiPlatnosci
{
    public bool Zaplacono { get; set; }
    public DateTime? DataZaplaty { get; set; }
    public string? ZnacznikZaplatyCzesciowej { get; set; }
    public string? FormaPlatnosci { get; set; }
    public string? OpisPlatnosci { get; set; }
    public string? LinkDoPlatnosci { get; set; }
    public string? IPKSeF { get; set; }
    public List<TerminPlatnosci> Terminy { get; set; } = [];
    public List<RachunekBankowy> Rachunki { get; set; } = [];
    public List<RachunekBankowy> RachunkiFaktora { get; set; } = [];
    public List<Platnosc> PlatnosciCzesciowe { get; set; } = [];
    public List<Skonto> Skonta { get; set; } = [];
}

public class Platnosc
{
    public string? Kwota { get; set; }
    public string? Data { get; set; }
    public string? Forma { get; set; }
    public string? OpisFormy { get; set; }
    public DateTime? DataDateTime => DateTime.TryParse(Data, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;
}

public class TerminPlatnosci { public DateTime? Termin { get; set; } public string? Ilosc { get; set; } public string? Jednostka { get; set; } public string? ZdarzeniePoczatkowe { get; set; } }
public class RachunekBankowy { public string? NrRB { get; set; } public string? SWIFT { get; set; } public string? RachunekWlasnyBanku { get; set; } public string? NazwaBanku { get; set; } public string? OpisRachunku { get; set; } }
public class Skonto { public string? Warunki { get; set; } public decimal? Wysokosc { get; set; } }
public class RozliczenieFaktury { public List<KorektaRozliczenia> Obciazenia { get; set; } = []; public decimal? SumaObciazen { get; set; } public List<KorektaRozliczenia> Odliczenia { get; set; } = []; public decimal? SumaOdliczen { get; set; } public decimal? DoZaplaty { get; set; } public decimal? DoRozliczenia { get; set; } }
public class KorektaRozliczenia { public decimal? Kwota { get; set; } public string? Powod { get; set; } }
public class DodatkowyOpis { public string? Klucz { get; set; } public string? Wartosc { get; set; } }
public class DokumentPowiazany { public DateTime? Data { get; set; } public string? Numer { get; set; } }
public class FakturaPowiazana { public string? NumerKSeF { get; set; } public string? NumerFaktury { get; set; } public DateTime? DataWystawienia { get; set; } }

public class DaneRejestrowe { public string? PelnaNazwa { get; set; } public string? KRS { get; set; } public string? REGON { get; set; } public string? BDO { get; set; } }

// Dane pozycji zamówienia w fakturze zaliczkowej FA(3).
public class PozycjaZamowienia
{
    public string? Numer { get; set; }
    public string? Nazwa { get; set; }
    public string? Indeks { get; set; }
    public string? OpisJednostki { get; set; } // P_8AZ, np. "10 SZT"
    public string? Ilosc { get; set; }         // P_8BZ
    public string? CenaNetto { get; set; }     // P_9AZ
    public string? CenaBrutto { get; set; }    // P_9BZ
    public string? WartoscNetto { get; set; }  // P_11NettoZ
    public string? WartoscVat { get; set; }    // P_11VatZ
    public string? StawkaVat { get; set; }     // P_12Z
}
