using System.Globalization;
using System.Text;

namespace MinhaBelem.Core.Text;

public sealed record Municipality(string Name, bool IsBelem, string Slug);

/// <summary>
/// Base geográfica de referência: municípios do Pará e bairros de Belém.
/// Serve para validar e normalizar a localidade devolvida pela IA e para popular os filtros do guia.
/// A tabela <c>mb_localities</c> é semeada a partir daqui e pode ser ampliada pela administração
/// sem novo deploy.
/// </summary>
public static class BelemGeography
{
    public const string State = "PA";
    public const string StateName = "Pará";

    public static readonly string[] BelemNeighborhoods = NormalizeMany(
        new[]
        {
        "Açaí", "Acari", "Água Branca", "Amapá", "Andaraí", "Anchieta", "Angeli", "Araruama",
        "Arsenal", "Atalaia", "Aurora", "Bação", "Baenão", "Bengui", "Bico do Papagaio",
        "Bonfim", "Brasil Novo", "Campina", "Canudos", "Curió", "Dom Pedro II", "Jurema",
        "Limoeiro", "Magnólia", "Manguinhos", "Nazaré", "Nossa Senhora das Graças",
        "Oitizeiro", "Reduto", "Sena Madureira", "Tapanu", "Utinga"
        });

    private static readonly string[] ParaMunicipalities =
    [
        "Abaetetuba", "Abel Figueiredo", "Acará", "Afuá", "Água Azul do Norte", "Alenquer", "Almeirim",
        "Altamira", "Anajás", "Ananindeua", "Anapu", "Augusto Corrêa", "Aurora do Pará", "Aveiro",
        "Bagre", "Baião", "Bannach", "Barcarena", "Belém", "Belterra", "Benevides",
        "Bom Jesus do Tocantins", "Bonito", "Bragança", "Brasil Novo", "Brejo Grande do Araguaia",
        "Breu Branco", "Breves", "Bujaru", "Cachoeira do Arari", "Cachoeira do Piriá", "Cametá",
        "Canaã dos Carajás", "Capanema", "Castanhal", "Chaves", "Colares", "Conceição do Araguaia",
        "Concórdia do Pará", "Cumaru do Norte", "Curionópolis", "Curralinho", "Curuá", "Curuçá",
        "Dom Eliseu", "Eldorado do Carajás", "Faro", "Floresta do Araguaia", "Garrafão do Norte",
        "Goianésia do Pará", "Gurupá", "Igarapé-Açu", "Igarapé-Miri", "Inhangapi", "Ipixuna do Pará",
        "Irituia", "Itaituba", "Itupiranga", "Jacareacanga", "Jacundá", "Juruti", "Limoeiro do Ajuru",
        "Mãe do Rio", "Magalhães Barata", "Marabá", "Maracanã", "Marapanim", "Marituba",
        "Medicilândia", "Melgaço", "Mocajuba", "Moju", "Mojuí dos Campos", "Monte Alegre", "Muaná",
        "Nova Esperança do Piriá", "Nova Ipixuna", "Novo Progresso", "Novo Repartimento", "Óbidos",
        "Oeiras do Pará", "Oriximiná", "Ourém", "Ourilândia do Norte", "Pacajá", "Palestina do Pará",
        "Paragominas", "Parauapebas", "Pau d'Arco", "Peixe-Boi", "Piçarra", "Placas",
        "Ponta de Pedras", "Portel", "Porto de Moz", "Prainha", "Pracuúba", "Quatipuru",
        "Redenção", "Riachão", "Rio Maria", "Rondon do Pará", "Rurópolis", "Salinópolis",
        "Salvaterra", "Santa Bárbara do Pará", "Santa Cruz do Arari", "Santa Isabel do Pará",
        "Santa Luzia do Pará", "Santa Maria das Barreiras", "Santa Maria do Pará", "Santana do Araguaia",
        "Santarém", "Santarém Novo", "Santo Antônio do Tauá", "São Caetano de Odivelas",
        "São Domingos do Araguaia", "São Domingos do Capim", "São Félix do Xingu",
        "São Francisco do Pará", "São Geraldo do Araguaia", "São João da Ponta",
        "São João de Pirabas", "São João do Araguaia", "São Miguel do Guamá",
        "São Sebastião da Boa Vista", "Sapucaia", "Senador José Porfírio", "Soure", "Tailândia",
        "Terra Alta", "Terra Santa", "Tomé-Açu", "Tracuateua", "Trairão", "Tucumã", "Tucuruí",
        "Ulianópolis", "Uruará", "Vigia", "Viseu", "Vitória do Xingu", "Xinguara"
    ];

    public static readonly Municipality[] Municipalities = ParaMunicipalities
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        .Select(n => new Municipality(n, string.Equals(n, "Belém", StringComparison.OrdinalIgnoreCase), Slug(n)))
        .ToArray();

    /// <summary>Palavras ignoradas na comparação de similaridade entre títulos.</summary>
    public static readonly HashSet<string> LocalityTokens = new(StringComparer.Ordinal)
    {
        "belem", "para", "brasil", "brazil", "amazonia", "amazon", "amapa", "regional",
        "municipio", "capital", "cidade", "estado"
    };

    private static readonly Dictionary<string, string> NeighborhoodLookup =
        BelemNeighborhoods.ToDictionary(Key, n => n, StringComparer.Ordinal);

    private static readonly Dictionary<string, Municipality> MunicipalityLookup =
        Municipalities.ToDictionary(m => Key(m.Name), m => m, StringComparer.Ordinal);

    private static string Key(string value) => TextNormalizer.Normalize(value).Replace(' ', '-');

    private static string Slug(string name)
    {
        var normalized = TextNormalizer.Normalize(name).Replace(' ', '-');
        return normalized.Length == 0 ? name.ToLowerInvariant() : normalized;
    }

    private static string[] NormalizeMany(IEnumerable<string> values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v))
              .Select(v => v.Trim())
              .Distinct(StringComparer.OrdinalIgnoreCase)
              .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
              .ToArray();

    /// <summary>Normaliza texto vindo da IA para uma chave de busca tolerante a acentos e ruído.</summary>
    public static string SearchKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(char.IsLetter(ch) ? char.ToLowerInvariant(ch) : ' ');
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Resolve um bairro de Belém escrito livremente ("bairro Nazaré", "Nazaré, Belém").</summary>
    public static string? ResolveNeighborhood(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = SearchKey(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Length < 3) continue;
            if (NeighborhoodLookup.TryGetValue(part, out var found)) return found;
        }
        return null;
    }

    /// <summary>Resolve um município do Pará escrito livremente.</summary>
    public static Municipality? ResolveMunicipality(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = SearchKey(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Length < 4) continue;
            if (MunicipalityLookup.TryGetValue(part, out var found)) return found;
        }
        return null;
    }
}