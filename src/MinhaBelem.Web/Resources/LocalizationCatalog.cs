using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Localization;

namespace MinhaBelem.Web.Resources;

public static class SupportedCultures
{
    public const string Portuguese = "pt-BR";
    public const string English = "en";
    public const string French = "fr";
    public const string Spanish = "es";
    public const string Russian = "ru";
    public const string Chinese = "zh-Hans";
    public const string Japanese = "ja";

    public static readonly string[] All =
    [
        Portuguese,
        English,
        French,
        Spanish,
        Russian,
        Chinese,
        Japanese
    ];

    public static readonly IReadOnlyDictionary<string, (string Name, string NativeName, string Flag)> Meta =
        new Dictionary<string, (string Name, string NativeName, string Flag)>(StringComparer.OrdinalIgnoreCase)
        {
            [Portuguese] = ("Português (Brasil)", "Português", "🇧🇷"),
            [English] = ("Inglês", "English", "🇺🇸"),
            [French] = ("Francês", "Français", "🇫🇷"),
            [Spanish] = ("Espanhol", "Español", "🇪🇸"),
            [Russian] = ("Russo", "Русский", "🇷🇺"),
            [Chinese] = ("Chinês Simplificado", "简体中文", "🇨🇳"),
            [Japanese] = ("Japonês", "日本語", "🇯🇵")
        };

    public static string Normalize(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return Portuguese;
        var trimmed = culture.Trim();
        foreach (var c in All)
        {
            if (string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase)) return c;
            var prefix = c.Split('-')[0];
            if (string.Equals(prefix, trimmed, StringComparison.OrdinalIgnoreCase)) return c;
        }
        return Portuguese;
    }
}

public static class LocalizationCatalog
{
    private static readonly Dictionary<string, Dictionary<string, string>> Translations =
        new(StringComparer.OrdinalIgnoreCase);

    static LocalizationCatalog()
    {
        // 1. Português (Brasil) - Referência Editorial canônica
        Translations[SupportedCultures.Portuguese] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SiteTitle"] = "MinhaBelém — Nossa cidade. Mil histórias.",
            ["BrandTagline"] = "Nossa cidade. Mil histórias. Belém, Pará, Brasil.",
            ["SkipToContent"] = "Pular para o conteúdo",
            ["NavHome"] = "Início",
            ["NavCulture"] = "Cultura",
            ["NavAgenda"] = "Agenda",
            ["NavCitizenship"] = "Cidadania",
            ["NavMobility"] = "Mobilidade",
            ["NavGuide"] = "Guia da Cidade",
            ["NavExplore"] = "Explore Belém",
            ["NavMyAccount"] = "Minha conta",
            ["NavLogin"] = "Entrar",
            ["NavLogout"] = "Sair",
            ["NavAdmin"] = "Administração",
            ["SelectLanguage"] = "Idioma",
            ["CurrentLanguage"] = "Português (Brasil)",
            ["Search"] = "Buscar",
            ["SearchPlaceholder"] = "Buscar eventos, cultura, lugares em Belém...",
            ["SearchButton"] = "Pesquisar",
            ["Filter"] = "Filtrar",
            ["ClearFilters"] = "Limpar filtros",
            ["AllCategories"] = "Todas as categorias",
            ["AllNeighborhoods"] = "Todos os bairros",
            ["AllMunicipalities"] = "Todos os municípios",
            ["FreeOnly"] = "Apenas gratuitos",
            ["ViewMore"] = "Ver mais",
            ["Details"] = "Ver detalhes",
            ["Back"] = "Voltar",
            ["Send"] = "Enviar",
            ["Save"] = "Salvar",
            ["Cancel"] = "Cancelar",
            ["Edit"] = "Editar",
            ["Delete"] = "Excluir",
            ["Approve"] = "Aprovar",
            ["Reject"] = "Rejeitar",
            ["Publish"] = "Publicar",
            ["Review"] = "Revisar",
            ["Close"] = "Fechar",
            ["ExploreTitle"] = "Explore Belém — Guia Turístico Internacional",
            ["ExploreSubtitle"] = "Roteiros, atrações históricas, gastronomia amazônica, ilhas fluviais e cultura viva na capital do Pará.",
            ["ItinerariesTitle"] = "Roteiros Turísticos Curados",
            ["Itinerary1Day"] = "1 Dia: Belém Express",
            ["Itinerary2Days"] = "2 Dias: Cultura & Ilha do Combu",
            ["Itinerary3Days"] = "3 Dias: Natureza, Rios & Tradição",
            ["Itinerary5Days"] = "5 Dias: Imersão Amazônica Completa",
            ["WhatToVisit"] = "O que visitar",
            ["WhereToEat"] = "Onde comer",
            ["WhereToStay"] = "Onde se hospedar",
            ["HistoricTours"] = "Passeios históricos",
            ["RiverTours"] = "Passeios fluviais e ilhas",
            ["MarketsAndFairs"] = "Mercados e feiras",
            ["ParksAndNature"] = "Parques e floresta urbana",
            ["PracticalTips"] = "Recomendações práticas e segurança",
            ["EquatorialWeather"] = "Clima quente e úmido com chuva característica no meio da tarde. Leve água, roupas leves e sombrinha.",
            ["SafetyRecommendation"] = "Em áreas turísticas como Ver-o-Peso e centro antigo, mantenha pertences próximos e prefira táxis credenciados ou aplicativos de transporte.",
            ["EmergencyPhones"] = "Emergências: Polícia Militar 190 | Bombeiros 193 | SAMU 192 | Delegacia do Turista (DPTUR) (91) 3212-3977",
            ["CulturalNotesTitle"] = "Notas culturais e expressões da Amazônia",
            ["CulturalGlossary"] = "Glossário Amazônico",
            ["UpcomingEvents"] = "Próximos eventos",
            ["ThisWeek"] = "Agenda da semana",
            ["ThisWeekend"] = "O que fazer no fim de semana",
            ["FreeEvents"] = "Eventos gratuitos",
            ["ChildrenProgram"] = "Programação infantil",
            ["EventDate"] = "Data e horário",
            ["TicketPrice"] = "Ingresso",
            ["FreeAdmission"] = "Entrada gratuita",
            ["EventCancelled"] = "Cancelamento confirmado pela organização.",
            ["VerifiedSource"] = "Fonte oficial verificada",
            ["CollaborateTitle"] = "Envie uma sugestão de evento ou notícia",
            ["YourName"] = "Seu nome",
            ["YourMessage"] = "Detalhes da programação ou sugestão",
            ["ConsentTerms"] = "Li e concordo com os termos de privacidade para envio desta colaboração.",
            ["SubmissionNotice"] = "Sugestão recebida com sucesso! Ela passará por conferência editorial antes de eventual publicação.",
            ["PrivacyPolicy"] = "Política de Privacidade",
            ["TermsOfUse"] = "Termos de Uso",
            ["Copyright"] = "MinhaBelém — MNSOFT. Todos os direitos reservados.",
            ["AllRightsReserved"] = "Todos os direitos reservados",
            ["AdminTranslations"] = "Central de Internacionalização e Traduções",
            ["ActiveLanguages"] = "Idiomas do Portal",
            ["TranslateWithAi"] = "Solicitar Tradução via IA",
            ["CompareOriginalTranslation"] = "Comparar Original e Tradução",
            ["PendingTranslations"] = "Traduções Pendentes de Revisão",
            ["ApprovedStatus"] = "Aprovada",
            ["PendingStatus"] = "Pendente",
            ["RejectedStatus"] = "Rejeitada",
            ["TokensUsed"] = "Tokens consumidos",
            ["AiCost"] = "Custo estimado (USD)",
            ["NotFoundTitle"] = "Página não encontrada",
            ["NotFoundMessage"] = "O endereço solicitado não foi localizado ou o conteúdo foi arquivado.",
            ["ErrorTitle"] = "Ocorreu um erro inesperado",
            ["ErrorMessage"] = "Nossa equipe técnica foi notificada. Por favor, tente novamente em instantes."
        };

        // 2. Inglês (en)
        Translations[SupportedCultures.English] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SiteTitle"] = "MinhaBelém — Our city. A thousand stories.",
            ["BrandTagline"] = "Our city. A thousand stories. Belém, Pará, Brazil.",
            ["SkipToContent"] = "Skip to content",
            ["NavHome"] = "Home",
            ["NavCulture"] = "Culture",
            ["NavAgenda"] = "Events",
            ["NavCitizenship"] = "Citizenship",
            ["NavMobility"] = "Mobility",
            ["NavGuide"] = "City Guide",
            ["NavExplore"] = "Explore Belém",
            ["NavMyAccount"] = "My Account",
            ["NavLogin"] = "Sign In",
            ["NavLogout"] = "Sign Out",
            ["NavAdmin"] = "Administration",
            ["SelectLanguage"] = "Language",
            ["CurrentLanguage"] = "English",
            ["Search"] = "Search",
            ["SearchPlaceholder"] = "Search events, culture, places in Belém...",
            ["SearchButton"] = "Search",
            ["Filter"] = "Filter",
            ["ClearFilters"] = "Clear filters",
            ["AllCategories"] = "All categories",
            ["AllNeighborhoods"] = "All neighborhoods",
            ["AllMunicipalities"] = "All municipalities",
            ["FreeOnly"] = "Free admission only",
            ["ViewMore"] = "View more",
            ["Details"] = "View details",
            ["Back"] = "Back",
            ["Send"] = "Submit",
            ["Save"] = "Save",
            ["Cancel"] = "Cancel",
            ["Edit"] = "Edit",
            ["Delete"] = "Delete",
            ["Approve"] = "Approve",
            ["Reject"] = "Reject",
            ["Publish"] = "Publish",
            ["Review"] = "Review",
            ["Close"] = "Close",
            ["ExploreTitle"] = "Explore Belém — International Travel Guide",
            ["ExploreSubtitle"] = "Curated itineraries, historic landmarks, Amazonian gastronomy, river islands, and living culture in the capital of the Amazon.",
            ["ItinerariesTitle"] = "Curated Travel Itineraries",
            ["Itinerary1Day"] = "1 Day: Belém Express",
            ["Itinerary2Days"] = "2 Days: Culture & Combu Island",
            ["Itinerary3Days"] = "3 Days: Nature, Rivers & Heritage",
            ["Itinerary5Days"] = "5 Days: Complete Amazonian Immersion",
            ["WhatToVisit"] = "What to visit",
            ["WhereToEat"] = "Where to eat & Gastronomy",
            ["WhereToStay"] = "Where to stay",
            ["HistoricTours"] = "Historic walking tours",
            ["RiverTours"] = "River excursions & islands",
            ["MarketsAndFairs"] = "Markets and open fairs",
            ["ParksAndNature"] = "Parks & urban rainforest",
            ["PracticalTips"] = "Practical recommendations & safety",
            ["EquatorialWeather"] = "Equatorial hot and humid climate with regular afternoon rain showers. Always carry water, light breathable clothing, and an umbrella.",
            ["SafetyRecommendation"] = "In busy tourist hubs such as Ver-o-Peso and the historic district, keep valuables secure and use official rideshare or licensed taxis.",
            ["EmergencyPhones"] = "Emergency numbers: Police 190 | Fire Department 193 | Ambulance 192 | Tourist Police Station (DPTUR) +55 (91) 3212-3977",
            ["CulturalNotesTitle"] = "Amazonian cultural terms and context",
            ["CulturalGlossary"] = "Amazonian Glossary",
            ["UpcomingEvents"] = "Upcoming events",
            ["ThisWeek"] = "Events this week",
            ["ThisWeekend"] = "Things to do this weekend",
            ["FreeEvents"] = "Free events",
            ["ChildrenProgram"] = "Family & kids program",
            ["EventDate"] = "Date and time",
            ["TicketPrice"] = "Admission",
            ["FreeAdmission"] = "Free admission",
            ["EventCancelled"] = "Event cancellation confirmed by organizers.",
            ["VerifiedSource"] = "Official verified source",
            ["CollaborateTitle"] = "Send an event tip or community announcement",
            ["YourName"] = "Your name",
            ["YourMessage"] = "Event details or recommendation",
            ["ConsentTerms"] = "I have read and agree to the privacy policy terms for this submission.",
            ["SubmissionNotice"] = "Thank you! Your recommendation has been submitted for editorial verification.",
            ["PrivacyPolicy"] = "Privacy Policy",
            ["TermsOfUse"] = "Terms of Use",
            ["Copyright"] = "MinhaBelém — MNSOFT. All rights reserved.",
            ["AllRightsReserved"] = "All rights reserved",
            ["AdminTranslations"] = "Internationalization & Translation Center",
            ["ActiveLanguages"] = "Active Languages",
            ["TranslateWithAi"] = "Request AI Translation",
            ["CompareOriginalTranslation"] = "Side-by-Side Comparison",
            ["PendingTranslations"] = "Pending Review Translations",
            ["ApprovedStatus"] = "Approved",
            ["PendingStatus"] = "Pending",
            ["RejectedStatus"] = "Rejected",
            ["TokensUsed"] = "Tokens consumed",
            ["AiCost"] = "Estimated cost (USD)",
            ["NotFoundTitle"] = "Page not found",
            ["NotFoundMessage"] = "The requested page could not be located or has been archived.",
            ["ErrorTitle"] = "An unexpected error occurred",
            ["ErrorMessage"] = "Our technical team has been alerted. Please try again shortly."
        };

        // 3. Francês (fr)
        Translations[SupportedCultures.French] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SiteTitle"] = "MinhaBelém — Notre ville. Mille histoires.",
            ["BrandTagline"] = "Notre ville. Mille histoires. Belém, Pará, Brésil.",
            ["SkipToContent"] = "Passer au contenu",
            ["NavHome"] = "Accueil",
            ["NavCulture"] = "Culture",
            ["NavAgenda"] = "Agenda",
            ["NavCitizenship"] = "Citoyenneté",
            ["NavMobility"] = "Mobilité",
            ["NavGuide"] = "Guide de la ville",
            ["NavExplore"] = "Explorez Belém",
            ["NavMyAccount"] = "Mon compte",
            ["NavLogin"] = "Connexion",
            ["NavLogout"] = "Déconnexion",
            ["NavAdmin"] = "Administration",
            ["SelectLanguage"] = "Langue",
            ["CurrentLanguage"] = "Français",
            ["Search"] = "Rechercher",
            ["SearchPlaceholder"] = "Événements, culture, lieux à Belém...",
            ["SearchButton"] = "Rechercher",
            ["Filter"] = "Filtrer",
            ["ClearFilters"] = "Effacer les filtres",
            ["AllCategories"] = "Toutes les catégories",
            ["AllNeighborhoods"] = "Tous les quartiers",
            ["AllMunicipalities"] = "Toutes les municipalités",
            ["FreeOnly"] = "Gratuit uniquement",
            ["ViewMore"] = "Voir plus",
            ["Details"] = "Voir les détails",
            ["Back"] = "Retour",
            ["Send"] = "Envoyer",
            ["Save"] = "Enregistrer",
            ["Cancel"] = "Annuler",
            ["Edit"] = "Modifier",
            ["Delete"] = "Supprimer",
            ["Approve"] = "Approuver",
            ["Reject"] = "Rejeter",
            ["Publish"] = "Publier",
            ["Review"] = "Réviser",
            ["Close"] = "Fermer",
            ["ExploreTitle"] = "Explorez Belém — Guide Touristique International",
            ["ExploreSubtitle"] = "Circuits guidés, trésors historiques, gastronomie amazonienne, îles fluviales et culture vivante à Belém.",
            ["ItinerariesTitle"] = "Itinéraires Recommandés",
            ["Itinerary1Day"] = "1 Jour : Belém Express",
            ["Itinerary2Days"] = "2 Jours : Culture & Île de Combu",
            ["Itinerary3Days"] = "3 Jours : Nature, Fleuves & Tradition",
            ["Itinerary5Days"] = "5 Jours : Immersion Amazonienne Complète",
            ["WhatToVisit"] = "Que visiter",
            ["WhereToEat"] = "Où manger & Gastronomie",
            ["WhereToStay"] = "Où loger",
            ["HistoricTours"] = "Parcours historiques",
            ["RiverTours"] = "Excursions fluviales et îles",
            ["MarketsAndFairs"] = "Marchés et foires",
            ["ParksAndNature"] = "Parcs et forêt urbaine",
            ["PracticalTips"] = "Conseils pratiques et sécurité",
            ["EquatorialWeather"] = "Climat équatorial chaud et humide avec averses l'après-midi. Prévoyez des vêtements légers et un parapluie.",
            ["SafetyRecommendation"] = "Dans les zones touristiques, veillez sur vos effets personnels et privilégiez les taxis officiels ou applications de transport.",
            ["EmergencyPhones"] = "Numéros d'urgence : Police 190 | Pompiers 193 | SAMU 192 | Police Touristique (DPTUR) +55 (91) 3212-3977",
            ["CulturalNotesTitle"] = "Notes culturelles amazoniennes",
            ["CulturalGlossary"] = "Glossaire Amazonien",
            ["UpcomingEvents"] = "Événements à venir",
            ["ThisWeek"] = "Cette semaine",
            ["ThisWeekend"] = "Que faire ce week-end",
            ["FreeEvents"] = "Événements gratuits",
            ["ChildrenProgram"] = "Programme jeunesse & famille",
            ["EventDate"] = "Date et horaire",
            ["TicketPrice"] = "Entrée",
            ["FreeAdmission"] = "Entrée libre",
            ["EventCancelled"] = "Annulation confirmée par l'organisateur.",
            ["VerifiedSource"] = "Source officielle vérifiée",
            ["CollaborateTitle"] = "Proposer un événement ou une nouvelle",
            ["YourName"] = "Votre nom",
            ["YourMessage"] = "Détails de l'événement ou message",
            ["ConsentTerms"] = "J'ai lu et j'accepte la politique de confidentialité.",
            ["SubmissionNotice"] = "Suggestion envoyée avec succès pour examen éditorial.",
            ["PrivacyPolicy"] = "Politique de Confidentialité",
            ["TermsOfUse"] = "Conditions d'Utilisation",
            ["Copyright"] = "MinhaBelém — MNSOFT. Tous droits réservés.",
            ["AllRightsReserved"] = "Tous droits réservés",
            ["AdminTranslations"] = "Centre d'Internationalisation et Traductions",
            ["ActiveLanguages"] = "Langues Actives",
            ["TranslateWithAi"] = "Traduire par IA",
            ["CompareOriginalTranslation"] = "Comparaison Original / Traduction",
            ["PendingTranslations"] = "Traductions en Attente de Révision",
            ["ApprovedStatus"] = "Approuvée",
            ["PendingStatus"] = "En attente",
            ["RejectedStatus"] = "Rejetée",
            ["TokensUsed"] = "Tokens consommés",
            ["AiCost"] = "Coût estimé (USD)",
            ["NotFoundTitle"] = "Page non trouvée",
            ["NotFoundMessage"] = "La page demandée n'existe pas ou a été archivée.",
            ["ErrorTitle"] = "Une erreur inattendue est survenue",
            ["ErrorMessage"] = "Notre équipe technique a été avertie. Veuillez réessayer dans quelques instants."
        };

        // 4. Espanhol (es)
        Translations[SupportedCultures.Spanish] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SiteTitle"] = "MinhaBelém — Nuestra ciudad. Mil historias.",
            ["BrandTagline"] = "Nuestra ciudad. Mil historias. Belém, Pará, Brasil.",
            ["SkipToContent"] = "Saltar al contenido",
            ["NavHome"] = "Inicio",
            ["NavCulture"] = "Cultura",
            ["NavAgenda"] = "Agenda",
            ["NavCitizenship"] = "Ciudadanía",
            ["NavMobility"] = "Movilidad",
            ["NavGuide"] = "Guía de la ciudad",
            ["NavExplore"] = "Explora Belém",
            ["NavMyAccount"] = "Mi cuenta",
            ["NavLogin"] = "Iniciar sesión",
            ["NavLogout"] = "Cerrar sesión",
            ["NavAdmin"] = "Administración",
            ["SelectLanguage"] = "Idioma",
            ["CurrentLanguage"] = "Español",
            ["Search"] = "Buscar",
            ["SearchPlaceholder"] = "Buscar eventos, cultura, lugares en Belém...",
            ["SearchButton"] = "Buscar",
            ["Filter"] = "Filtrar",
            ["ClearFilters"] = "Limpiar filtros",
            ["AllCategories"] = "Todas las categorías",
            ["AllNeighborhoods"] = "Todos los barrios",
            ["AllMunicipalities"] = "Todos los municipios",
            ["FreeOnly"] = "Solo eventos gratuitos",
            ["ViewMore"] = "Ver más",
            ["Details"] = "Ver detalles",
            ["Back"] = "Volver",
            ["Send"] = "Enviar",
            ["Save"] = "Guardar",
            ["Cancel"] = "Cancelar",
            ["Edit"] = "Editar",
            ["Delete"] = "Eliminar",
            ["Approve"] = "Aprobar",
            ["Reject"] = "Rechazar",
            ["Publish"] = "Publicar",
            ["Review"] = "Revisar",
            ["Close"] = "Cerrar",
            ["ExploreTitle"] = "Explora Belém — Guía Turística Internacional",
            ["ExploreSubtitle"] = "Itinerarios guiados, patrimonio histórico, gastronomía amazónica, islas fluviales y cultura viva.",
            ["ItinerariesTitle"] = "Itinerarios Turísticos Curados",
            ["Itinerary1Day"] = "1 Día: Belém Express",
            ["Itinerary2Days"] = "2 Días: Cultura e Isla de Combu",
            ["Itinerary3Days"] = "3 Días: Naturaleza, Ríos y Tradición",
            ["Itinerary5Days"] = "5 Días: Inmersión Amazónica Completa",
            ["WhatToVisit"] = "Qué visitar",
            ["WhereToEat"] = "Dónde comer y gastronomía",
            ["WhereToStay"] = "Dónde alojarse",
            ["HistoricTours"] = "Paseos históricos",
            ["RiverTours"] = "Paseos fluviales e islas",
            ["MarketsAndFairs"] = "Mercados y ferias",
            ["ParksAndNature"] = "Parques y selva urbana",
            ["PracticalTips"] = "Consejos prácticos y seguridad",
            ["EquatorialWeather"] = "Clima ecuatorial cálido y húmedo con lluvias habituales por la tarde. Lleve ropa ligera y paraguas.",
            ["SafetyRecommendation"] = "En zonas turísticas como Ver-o-Peso y el centro histórico, cuide sus pertenencias y use taxis autorizados o aplicaciones de transporte.",
            ["EmergencyPhones"] = "Teléfonos de emergencia: Policía 190 | Bomberos 193 | Ambulancia 192 | Comisaría del Turista (DPTUR) +55 (91) 3212-3977",
            ["CulturalNotesTitle"] = "Glosario y notas culturales amazónicas",
            ["CulturalGlossary"] = "Glosario Amazónico",
            ["UpcomingEvents"] = "Próximos eventos",
            ["ThisWeek"] = "Agenda de la semana",
            ["ThisWeekend"] = "Qué hacer este fin de semana",
            ["FreeEvents"] = "Eventos gratuitos",
            ["ChildrenProgram"] = "Programación infantil",
            ["EventDate"] = "Fecha y hora",
            ["TicketPrice"] = "Entrada",
            ["FreeAdmission"] = "Entrada libre y gratuita",
            ["EventCancelled"] = "Cancelación confirmada por los organizadores.",
            ["VerifiedSource"] = "Fuente oficial verificada",
            ["CollaborateTitle"] = "Enviar una propuesta o evento",
            ["YourName"] = "Su nombre",
            ["YourMessage"] = "Detalles del evento o mensaje",
            ["ConsentTerms"] = "He leído y acepto los términos de la política de privacidad.",
            ["SubmissionNotice"] = "Sugerencia enviada correctamente para revisión editorial.",
            ["PrivacyPolicy"] = "Política de Privacidad",
            ["TermsOfUse"] = "Términos de Uso",
            ["Copyright"] = "MinhaBelém — MNSOFT. Todos los derechos reservados.",
            ["AllRightsReserved"] = "Todos los derechos reservados",
            ["AdminTranslations"] = "Centro de Internacionalización y Traducciones",
            ["ActiveLanguages"] = "Idiomas Activos",
            ["TranslateWithAi"] = "Solicitar Traducción con IA",
            ["CompareOriginalTranslation"] = "Comparar Original y Traducción",
            ["PendingTranslations"] = "Traducciones Pendientes de Revisión",
            ["ApprovedStatus"] = "Aprobada",
            ["PendingStatus"] = "Pendiente",
            ["RejectedStatus"] = "Rechazada",
            ["TokensUsed"] = "Tokens utilizados",
            ["AiCost"] = "Costo estimado (USD)",
            ["NotFoundTitle"] = "Página no encontrada",
            ["NotFoundMessage"] = "La página solicitada no existe o ha sido archivada.",
            ["ErrorTitle"] = "Ha ocurrido un error inesperado",
            ["ErrorMessage"] = "Nuestro equipo técnico ha sido notificado. Por favor, inténtelo de nuevo en breve."
        };

        // 5. Russo (ru)
        Translations[SupportedCultures.Russian] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SiteTitle"] = "MinhaBelém — Наш город. Тысяча историй.",
            ["BrandTagline"] = "Наш город. Тысяча историй. Белен, Пара, Бразилия.",
            ["SkipToContent"] = "Перейти к основному содержимому",
            ["NavHome"] = "Главная",
            ["NavCulture"] = "Культура",
            ["NavAgenda"] = "Афиша",
            ["NavCitizenship"] = "Горожанам",
            ["NavMobility"] = "Транспорт",
            ["NavGuide"] = "Гид по городу",
            ["NavExplore"] = "Откройте Белен",
            ["NavMyAccount"] = "Личный кабинет",
            ["NavLogin"] = "Войти",
            ["NavLogout"] = "Выйти",
            ["NavAdmin"] = "Управление",
            ["SelectLanguage"] = "Язык",
            ["CurrentLanguage"] = "Русский",
            ["Search"] = "Поиск",
            ["SearchPlaceholder"] = "Поиск событий, культуры, мест в Белене...",
            ["SearchButton"] = "Найти",
            ["Filter"] = "Фильтры",
            ["ClearFilters"] = "Сбросить фильтры",
            ["AllCategories"] = "Все категории",
            ["AllNeighborhoods"] = "Все районы",
            ["AllMunicipalities"] = "Все муниципалитеты",
            ["FreeOnly"] = "Только бесплатные",
            ["ViewMore"] = "Показать больше",
            ["Details"] = "Подробнее",
            ["Back"] = "Назад",
            ["Send"] = "Отправить",
            ["Save"] = "Сохранить",
            ["Cancel"] = "Отмена",
            ["Edit"] = "Редактировать",
            ["Delete"] = "Удалить",
            ["Approve"] = "Одобрить",
            ["Reject"] = "Отклонить",
            ["Publish"] = "Опубликовать",
            ["Review"] = "Проверить",
            ["Close"] = "Закрыть",
            ["ExploreTitle"] = "Откройте Белен — Международный туристический гид",
            ["ExploreSubtitle"] = "Маршруты, исторические памятники, амазонская гастрономия, речные острова и живые традиции столицы штата Пара.",
            ["ItinerariesTitle"] = "Рекомендованные туристические маршруты",
            ["Itinerary1Day"] = "1 день: Белен Экспресс",
            ["Itinerary2Days"] = "2 дня: Культура и остров Комбу",
            ["Itinerary3Days"] = "3 дня: Природа, реки и наследие",
            ["Itinerary5Days"] = "5 дней: Полное погружение в Амазонию",
            ["WhatToVisit"] = "Что посетить",
            ["WhereToEat"] = "Где поесть и кухня",
            ["WhereToStay"] = "Где остановиться",
            ["HistoricTours"] = "Исторические прогулки",
            ["RiverTours"] = "Речные прогулки и острова",
            ["MarketsAndFairs"] = "Рынки и ярмарки",
            ["ParksAndNature"] = "Парки и тропический лес",
            ["PracticalTips"] = "Практические советы и безопасность",
            ["EquatorialWeather"] = "Экваториальный жаркий и влажный климат с регулярными послеполуденными дождями. Носите легкую одежду и зонт.",
            ["SafetyRecommendation"] = "В туристических центрах, таких как Вер-о-Пезо, следите за вещами и пользуйтесь официальными такси или приложениями.",
            ["EmergencyPhones"] = "Экстренные службы: Полиция 190 | Пожарные 193 | Скорая помощь 192 | Туристическая полиция (DPTUR) +55 (91) 3212-3977",
            ["CulturalNotesTitle"] = "Культурный глоссарий Амазонии",
            ["CulturalGlossary"] = "Глоссарий Амазонии",
            ["UpcomingEvents"] = "Ближайшие события",
            ["ThisWeek"] = "События недели",
            ["ThisWeekend"] = "Куда сходить в выходные",
            ["FreeEvents"] = "Бесплатные события",
            ["ChildrenProgram"] = "Детская программа",
            ["EventDate"] = "Дата и время",
            ["TicketPrice"] = "Билеты",
            ["FreeAdmission"] = "Вход свободный",
            ["EventCancelled"] = "Событие отменено организатором.",
            ["VerifiedSource"] = "Проверенный официальный источник",
            ["CollaborateTitle"] = "Предложить событие или новость",
            ["YourName"] = "Ваше имя",
            ["YourMessage"] = "Описание события или текст",
            ["ConsentTerms"] = "Я ознакомлен и согласен с политикой конфиденциальности.",
            ["SubmissionNotice"] = "Спасибо! Ваше предложение отправлено на редакционную проверку.",
            ["PrivacyPolicy"] = "Политика конфиденциальности",
            ["TermsOfUse"] = "Условия использования",
            ["Copyright"] = "MinhaBelém — MNSOFT. Все права защищены.",
            ["AllRightsReserved"] = "Все права защищены",
            ["AdminTranslations"] = "Центр интернационализации и переводов",
            ["ActiveLanguages"] = "Активные языки",
            ["TranslateWithAi"] = "Запросить перевод ИИ",
            ["CompareOriginalTranslation"] = "Сравнить оригинал и перевод",
            ["PendingTranslations"] = "Переводы на модерации",
            ["ApprovedStatus"] = "Одобрено",
            ["PendingStatus"] = "На проверке",
            ["RejectedStatus"] = "Отклонено",
            ["TokensUsed"] = "Использовано токенов",
            ["AiCost"] = "Оценочная стоимость (USD)",
            ["NotFoundTitle"] = "Страница не найдена",
            ["NotFoundMessage"] = "Запрошенная страница не существует или была перемещена в архив.",
            ["ErrorTitle"] = "Произошла непредвиденная ошибка",
            ["ErrorMessage"] = "Наша техническая служба уже оповещена. Пожалуйста, повторите попытку через минуту."
        };

        // 6. Chinês Simplificado (zh-Hans)
        Translations[SupportedCultures.Chinese] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SiteTitle"] = "MinhaBelém — 我们的城市，千般故事。",
            ["BrandTagline"] = "我们的城市，千般故事。巴西帕拉州贝伦。",
            ["SkipToContent"] = "跳至主要内容",
            ["NavHome"] = "首页",
            ["NavCulture"] = "文化",
            ["NavAgenda"] = "活动日历",
            ["NavCitizenship"] = "市民服务",
            ["NavMobility"] = "城市交通",
            ["NavGuide"] = "城市指南",
            ["NavExplore"] = "探索贝伦",
            ["NavMyAccount"] = "个人中心",
            ["NavLogin"] = "登录",
            ["NavLogout"] = "退出",
            ["NavAdmin"] = "后台管理",
            ["SelectLanguage"] = "语言",
            ["CurrentLanguage"] = "简体中文",
            ["Search"] = "搜索",
            ["SearchPlaceholder"] = "搜索贝伦的活动、文化与景点...",
            ["SearchButton"] = "搜索",
            ["Filter"] = "筛选",
            ["ClearFilters"] = "清除筛选",
            ["AllCategories"] = "所有分类",
            ["AllNeighborhoods"] = "所有街区",
            ["AllMunicipalities"] = "所有城市",
            ["FreeOnly"] = "仅限免费活动",
            ["ViewMore"] = "查看更多",
            ["Details"] = "查看详情",
            ["Back"] = "返回",
            ["Send"] = "发送",
            ["Save"] = "保存",
            ["Cancel"] = "取消",
            ["Edit"] = "编辑",
            ["Delete"] = "删除",
            ["Approve"] = "通过",
            ["Reject"] = "拒绝",
            ["Publish"] = "发布",
            ["Review"] = "审核",
            ["Close"] = "关闭",
            ["ExploreTitle"] = "探索贝伦 — 国际旅游权威指南",
            ["ExploreSubtitle"] = "精心规划的旅行路线、历史名胜、亚马逊风味美食、江心岛屿与雨林生机文化。",
            ["ItinerariesTitle"] = "精选特色游览路线",
            ["Itinerary1Day"] = "1日游：贝伦精粹快览",
            ["Itinerary2Days"] = "2日游：历史文化与孔布岛风情",
            ["Itinerary3Days"] = "3日游：生态雨林、江河与非遗传承",
            ["Itinerary5Days"] = "5日游：亚马逊全景深度沉浸之旅",
            ["WhatToVisit"] = "必游景点",
            ["WhereToEat"] = "特色美食与餐厅",
            ["WhereToStay"] = "住宿推荐",
            ["HistoricTours"] = "历史漫步之旅",
            ["RiverTours"] = "游船巡江与岛屿行",
            ["MarketsAndFairs"] = "集市与特色露天市集",
            ["ParksAndNature"] = "城市公园与原始雨林",
            ["PracticalTips"] = "实用旅行建议与安全提示",
            ["EquatorialWeather"] = "赤道热带气候，温热多湿，午后常有对流雷阵雨。建议穿着轻薄透气衣物并随身备伞。",
            ["SafetyRecommendation"] = "在韦尔奥佩索市场及老城景区游览时，请妥善保管贵重物品，出行建议乘坐网约车或正规出租车。",
            ["EmergencyPhones"] = "紧急联络电话：报警 190 | 消防 193 | 急救 192 | 旅游警察局 (DPTUR) +55 (91) 3212-3977",
            ["CulturalNotesTitle"] = "亚马逊特色文化词汇注解",
            ["CulturalGlossary"] = "亚马逊文化词典",
            ["UpcomingEvents"] = "近期活动预告",
            ["ThisWeek"] = "本周精彩日程",
            ["ThisWeekend"] = "周末去哪儿",
            ["FreeEvents"] = "免费精彩活动",
            ["ChildrenProgram"] = "亲子与儿童专区",
            ["EventDate"] = "日期与时间",
            ["TicketPrice"] = "门票价格",
            ["FreeAdmission"] = "免费入场",
            ["EventCancelled"] = "组织方已确认取消此活动。",
            ["VerifiedSource"] = "官方认证真实信源",
            ["CollaborateTitle"] = "提交活动线索或城市建议",
            ["YourName"] = "您的姓名",
            ["YourMessage"] = "活动详情或线索内容",
            ["ConsentTerms"] = "我已阅读并同意隐私政策条款。",
            ["SubmissionNotice"] = "感谢提交！您的内容已送交编辑部进行事实核实。",
            ["PrivacyPolicy"] = "隐私政策",
            ["TermsOfUse"] = "使用条款",
            ["Copyright"] = "MinhaBelém — MNSOFT. 版权所有。",
            ["AllRightsReserved"] = "保留所有权利",
            ["AdminTranslations"] = "多语言国际化管理中心",
            ["ActiveLanguages"] = "当前启用语言",
            ["TranslateWithAi"] = "请求AI智能翻译",
            ["CompareOriginalTranslation"] = "原文与译文对照",
            ["PendingTranslations"] = "待审核译文列表",
            ["ApprovedStatus"] = "已审核发布",
            ["PendingStatus"] = "待人工复核",
            ["RejectedStatus"] = "已驳回",
            ["TokensUsed"] = "消耗 Token 总数",
            ["AiCost"] = "预估费用 (美元)",
            ["NotFoundTitle"] = "页面未找到",
            ["NotFoundMessage"] = "您访问的页面不存在或已被归档处理。",
            ["ErrorTitle"] = "系统发生意外错误",
            ["ErrorMessage"] = "技术团队已收到系统报警，请稍后再试。"
        };

        // 7. Japonês (ja)
        Translations[SupportedCultures.Japanese] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SiteTitle"] = "MinhaBelém — 私たちの街。千の物語。",
            ["BrandTagline"] = "私たちの街。千の物語。ブラジル・パラー州ベレン。",
            ["SkipToContent"] = "本文へスキップ",
            ["NavHome"] = "ホーム",
            ["NavCulture"] = "文化",
            ["NavAgenda"] = "イベント情報",
            ["NavCitizenship"] = "市民情報",
            ["NavMobility"] = "交通案内",
            ["NavGuide"] = "シティガイド",
            ["NavExplore"] = "ベレンを巡る",
            ["NavMyAccount"] = "マイアカウント",
            ["NavLogin"] = "ログイン",
            ["NavLogout"] = "ログアウト",
            ["NavAdmin"] = "管理パネル",
            ["SelectLanguage"] = "言語選択",
            ["CurrentLanguage"] = "日本語",
            ["Search"] = "検索",
            ["SearchPlaceholder"] = "ベレンのイベント・文化・スポットを検索...",
            ["SearchButton"] = "検索する",
            ["Filter"] = "絞り込み",
            ["ClearFilters"] = "条件をクリア",
            ["AllCategories"] = "すべてのカテゴリー",
            ["AllNeighborhoods"] = "すべてのエリア",
            ["AllMunicipalities"] = "すべての自治体",
            ["FreeOnly"] = "無料イベントのみ",
            ["ViewMore"] = "さらに見る",
            ["Details"] = "詳細を見る",
            ["Back"] = "戻る",
            ["Send"] = "送信",
            ["Save"] = "保存",
            ["Cancel"] = "キャンセル",
            ["Edit"] = "編集",
            ["Delete"] = "削除",
            ["Approve"] = "承認",
            ["Reject"] = "却下",
            ["Publish"] = "公開",
            ["Review"] = "確認",
            ["Close"] = "閉じる",
            ["ExploreTitle"] = "ベレンを巡る — 公式国際観光ガイド",
            ["ExploreSubtitle"] = "歴史的建造物、アマゾン伝統料理、川の島々への小旅行、パラー州の豊かな文化を体験するモデルコース。",
            ["ItinerariesTitle"] = "おすすめの観光モデルコース",
            ["Itinerary1Day"] = "1日コース：ベレン・エクスプレス",
            ["Itinerary2Days"] = "2日コース：歴史文化とコンブー島",
            ["Itinerary3Days"] = "3日コース：大自然・河川と伝統工芸",
            ["Itinerary5Days"] = "5日コース：アマゾン完全周遊プラン",
            ["WhatToVisit"] = "観光名所",
            ["WhereToEat"] = "おすすめグルメ・名物料理",
            ["WhereToStay"] = "宿泊施設",
            ["HistoricTours"] = "歴史街歩きツアー",
            ["RiverTours"] = "川のクルーズ・島めぐり",
            ["MarketsAndFairs"] = "市場・オープンマーケット",
            ["ParksAndNature"] = "公園・都市熱帯雨林",
            ["PracticalTips"] = "安全と旅行の実践アドバイス",
            ["EquatorialWeather"] = "熱帯雨林気候で高温多湿、午後はスコール（雨）が降りやすくなります。通気性の良い服装と雨具をご用意ください。",
            ["SafetyRecommendation"] = "ベル・オ・ペーゾ市場や旧市街では貴重品の管理にご注意いただき、移動は配車アプリや認可タクシーのご利用をおすすめします。",
            ["EmergencyPhones"] = "緊急連絡先：警察 190 | 消防 193 | 救急 192 | 観光警察（DPTUR）+55 (91) 3212-3977",
            ["CulturalNotesTitle"] = "アマゾンの伝統文化用語解説",
            ["CulturalGlossary"] = "アマゾン文化用語集",
            ["UpcomingEvents"] = "近日開催のイベント",
            ["ThisWeek"] = "今週のイベント",
            ["ThisWeekend"] = "今週末のおでかけ",
            ["FreeEvents"] = "入場無料イベント",
            ["ChildrenProgram"] = "ファミリー・子ども向け",
            ["EventDate"] = "日時",
            ["TicketPrice"] = "料金",
            ["FreeAdmission"] = "入場無料",
            ["EventCancelled"] = "主催者により中止が確認されました。",
            ["VerifiedSource"] = "確認済み公式情報源",
            ["CollaborateTitle"] = "イベント情報やご意見の提供",
            ["YourName"] = "お名前",
            ["YourMessage"] = "情報詳細またはメッセージ",
            ["ConsentTerms"] = "プライバシーポリシーに同意して送信します。",
            ["SubmissionNotice"] = "情報のご提供ありがとうございます！編集部にて確認のうえ掲載を検討いたします。",
            ["PrivacyPolicy"] = "プライバシーポリシー",
            ["TermsOfUse"] = "利用規約",
            ["Copyright"] = "MinhaBelém — MNSOFT. All rights reserved.",
            ["AllRightsReserved"] = "無断転載を禁じます",
            ["AdminTranslations"] = "多言語・翻訳管理センター",
            ["ActiveLanguages"] = "対応言語一覧",
            ["TranslateWithAi"] = "AI翻訳をリクエスト",
            ["CompareOriginalTranslation"] = "原文と翻訳の比較",
            ["PendingTranslations"] = "承認待ち翻訳一覧",
            ["ApprovedStatus"] = "承認済み",
            ["PendingStatus"] = "承認待ち",
            ["RejectedStatus"] = "却下",
            ["TokensUsed"] = "消費トークン数",
            ["AiCost"] = "推定コスト (USD)",
            ["NotFoundTitle"] = "お探しのページが見つかりません",
            ["NotFoundMessage"] = "指定されたURLは存在しないか、アーカイブに移動されました。",
            ["ErrorTitle"] = "予期しないエラーが発生しました",
            ["ErrorMessage"] = "技術チームに通知されました。しばらく時間をおいてから再度お試しください。"
        };
            Sprint14Translations.Register(Translations);
            Sprint15Translations.Register(Translations);
            Sprint15bTranslations.Register(Translations);
            AgendaTranslations.Register(Translations);
            Sprint17Translations.Register(Translations);
            Sprint18Translations.Register(Translations);
            Sprint19Translations.Register(Translations);
    }

    public static string Get(string key, string? culture = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        var targetCulture = SupportedCultures.Normalize(culture ?? CultureInfo.CurrentUICulture.Name);
        var normalizedKey = key.Replace("_", "");

        if (Translations.TryGetValue(targetCulture, out var dict))
        {
            if (dict.TryGetValue(key, out var val) || dict.TryGetValue(normalizedKey, out val))
                return val;
        }

        // Fallback para pt-BR
        if (Translations.TryGetValue(SupportedCultures.Portuguese, out var pt))
        {
            if (pt.TryGetValue(key, out var fallback) || pt.TryGetValue(normalizedKey, out fallback))
                return fallback;
        }

        return key;
    }

    public static IReadOnlyList<string> GetAllKeys()
    {
        return Translations[SupportedCultures.Portuguese].Keys.OrderBy(k => k).ToList();
    }

    /// <summary>
    /// Verificação automática de chaves ausentes por idioma.
    /// Retorna lista vazia se 100% das chaves estiverem presentes em todos os idiomas.
    /// </summary>
    public static IReadOnlyList<string> VerifyCompleteness()
    {
        var baseKeys = GetAllKeys();
        var missingReport = new List<string>();

        foreach (var culture in SupportedCultures.All)
        {
            if (!Translations.TryGetValue(culture, out var dict))
            {
                missingReport.Add($"Idioma '{culture}' não possui dicionário carregado.");
                continue;
            }

            foreach (var key in baseKeys)
            {
                if (!dict.TryGetValue(key, out var val) || string.IsNullOrWhiteSpace(val))
                {
                    missingReport.Add($"Chave ausente: [{culture}] -> '{key}'");
                }
            }
        }

        return missingReport;
    }
}

/// <summary>
/// Provedor nativo para IStringLocalizer integrado ao LocalizationCatalog.
/// </summary>
public sealed class MinhaBelemStringLocalizer : IStringLocalizer, IStringLocalizer<SharedResource>
{
    public LocalizedString this[string name]
    {
        get
        {
            var value = LocalizationCatalog.Get(name, CultureInfo.CurrentUICulture.Name);
            return new LocalizedString(name, value, resourceNotFound: value == name);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var format = LocalizationCatalog.Get(name, CultureInfo.CurrentUICulture.Name);
            var value = string.Format(CultureInfo.CurrentCulture, format, arguments);
            return new LocalizedString(name, value, resourceNotFound: format == name);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        foreach (var key in LocalizationCatalog.GetAllKeys())
        {
            yield return new LocalizedString(key, LocalizationCatalog.Get(key, currentCulture));
        }
    }
}

public sealed class MinhaBelemStringLocalizerFactory : IStringLocalizerFactory
{
    private static readonly ConcurrentDictionary<string, IStringLocalizer> Localizers = new();

    public IStringLocalizer Create(Type resourceSource)
    {
        return Localizers.GetOrAdd(resourceSource.FullName ?? nameof(SharedResource), _ => new MinhaBelemStringLocalizer());
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        return Localizers.GetOrAdd($"{baseName}_{location}", _ => new MinhaBelemStringLocalizer());
    }
}
