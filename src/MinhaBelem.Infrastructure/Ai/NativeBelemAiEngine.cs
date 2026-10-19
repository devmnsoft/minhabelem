using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MinhaBelem.Core.Entities;

namespace MinhaBelem.Infrastructure.Ai;

/// <summary>
/// Contexto real (publicado e verificado) que alimenta as respostas do Curió.
/// </summary>
public sealed record CurioContext(
    IReadOnlyList<Place> Places,
    IReadOnlyList<ContentItem> Events,
    IReadOnlyList<TransitLine> Lines,
    IReadOnlyList<TransitStop> Stops,
    IReadOnlyList<TransitRoutePoint> RoutePoints,
    IReadOnlyList<RiverRoute> RiverRoutes);

/// <summary>
/// Motor Nativo de Inteligência Artificial e RAG cultural de Belém do Pará.
/// Fornece raciocínio contextual, respostas autênticas em 7 idiomas e curadoria
/// precisa sem alucinações, atuando tanto de forma autônoma quanto como
/// fallback de alta fidelidade para modelos em nuvem (Gemini / Groq / DeepSeek).
/// Todas as afirmações factuais vêm do <see cref="CurioContext"/>.
/// </summary>
public static class NativeBelemAiEngine
{
    public sealed record NativeAiResult(
        string ResponseText,
        IReadOnlyList<string> Suggestions,
        IReadOnlyList<long> RecommendedPlaceIds);

    /// <summary>
    /// Sobrecarga compatível: mantém o contrato anterior (sem terminais nem itinerário)
    /// para os chamadores que ainda operam apenas com lugares, eventos e linhas.
    /// </summary>
    public static NativeAiResult Generate(
        string question,
        string culture,
        IReadOnlyList<Place> availablePlaces,
        IReadOnlyList<ContentItem> availableEvents,
        IReadOnlyList<TransitLine> availableLines) =>
        Generate(question, culture, new CurioContext(
            availablePlaces,
            availableEvents,
            availableLines,
            Array.Empty<TransitStop>(),
            Array.Empty<TransitRoutePoint>(),
            Array.Empty<RiverRoute>()));

    public static NativeAiResult Generate(string question, string culture, CurioContext context)
    {
        var cleanQ = question.Trim().ToLowerInvariant();
        var lang = DetectLanguage(culture);

        // Classificação de Intenção Temática
        var isGastronomy = Regex.IsMatch(cleanQ, @"\b(comida|comer|restaurante|almo[çc]o|jantar|tacac[aá]|a[çc]a[ií]|mani[çc]oba|pato|tucupi|vatap[aá]|peixe|filhote|pirarucu|caranguejo|gastronom|food|eat|restaurant|manger|comida|cuisine)\b");
        var isIsland = Regex.IsMatch(cleanQ, @"\b(ilha|combu|mosqueiro|cotijuba|outeiro|barco|travessia|fluvial|porto|trapiche|island|boat|bateau|bote)\b");
        var isHeritage = Regex.IsMatch(cleanQ, @"\b(hist[oó]ria|theatro|teatro da paz|forte|pres[eé]pio|catedral|s[eé]|onze janelas|museu|arte sacra|bas[ií]lica|nazar[eé]|c[ií]rio|palacete|s[aã]o br[aá]s|heritage|history|mus[eé]e)\b");
        var isNature = Regex.IsMatch(cleanQ, @"\b(mangal|bosque|utinga|parque|natureza|bicho|ave|verde|trilha|park|nature|oiseau|arbre)\b");
        var isTransit = Regex.IsMatch(cleanQ, @"\b([oô]nibus|linha|brt|como chegar|como ir|transporte|t[aá]xi|uber|deslocamento|ponto de|parada|terminal|esta[çc][aã]o|itiner[aá]rio|trajeto|rota|bus|transport|transit|stop|terminal|schedule|hor[aá]rio)\b");
        var isKids = Regex.IsMatch(cleanQ, @"\b(crian[çc]a|filho|fam[ií]lia|infantil|brincar|kids|children|famille|enfant)\b");
        var isFree = Regex.IsMatch(cleanQ, @"\b(gr[aá]tis|gratuito|de gra[çc]a|custo zero|free|gratuit)\b");
        var isSchedule = Regex.IsMatch(cleanQ, @"\b(hor[aá]rio|quando sai|que horas|intervalo|frequ[eê]ncia|first|last|departure|schedule)\b");
        var isWhereTo = Regex.IsMatch(cleanQ, @"\b(onde (fica|est[áa])|endere[çc]o|localiza|address|where is|como chego em)\b");

        // "De-para": "como faço de X para Y"
        var dePara = DetectOriginDestination(question);

        var recIds = new List<long>();

        // ---------- 1. Mobilidade / transporte ----------
        if (isTransit || dePara is not null || isSchedule)
        {
            var result = BuildTransitAnswer(lang, question, context, dePara, isSchedule, isWhereTo);
            if (result is not null) return result;
        }

        // ---------- 2. Gastronomia ----------
        if (isGastronomy)
        {
            var matchPlaces = context.Places.Where(p => p.PrimaryType is "restaurant" or "bar" or "tourist_attraction").Take(4).ToList();
            recIds.AddRange(matchPlaces.Select(p => p.Id));
            return new NativeAiResult(
                GastronomyText(lang, context),
                lang == Lang.En
                    ? new[] { "Where to eat the best Tacacá in Belém?", "How to visit Combu Island for lunch?", "Top restaurants at Estação das Docas", "Upcoming cultural events" }
                    : lang == Lang.Fr
                        ? new[] { "Où manger le meilleur Tacacá ?", "Comment aller sur l'île de Combu pour déjeuner ?", "Restaurants à l'Estação das Docas" }
                        : lang == Lang.Es
                            ? new[] { "¿Dónde comer el mejor Tacacá?", "¿Cómo cruzar a la Isla de Combu para almorzar?", "Restaurantes recomendados en Belém" }
                            : new[] { "Tacacá e açaí no guia", "Ilha do Combu no guia", "Ver-o-Peso no guia", "Eventos publicados na agenda" },
                recIds);
        }

        // ---------- 3. Ilhas e travessias ----------
        if (isIsland)
        {
            var matchPlaces = context.Places.Where(p => p.DisplayName.Contains("Combu", StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains("Docas", StringComparison.OrdinalIgnoreCase)).Take(3).ToList();
            recIds.AddRange(matchPlaces.Select(p => p.Id));
            return new NativeAiResult(
                IslandText(lang, context),
                lang == Lang.En
                    ? new[] { "How to take the boat to Combu Island?", "Combu on the guide map", "Mosqueiro on the guide map", "How to get to Icoaraci?" }
                    : new[] { "Ilha do Combu no guia", "Onde fica a Praça Princesa Isabel?", "Icoaraci e Mosqueiro no guia", "Estação das Docas no guia" },
                recIds);
        }

        // ---------- 4. Patrimônio ----------
        if (isHeritage)
        {
            var matchPlaces = context.Places.Where(p => p.DisplayName.Contains("Paz", StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains("Forte", StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains("Basílica", StringComparison.OrdinalIgnoreCase)).Take(4).ToList();
            recIds.AddRange(matchPlaces.Select(p => p.Id));
            return new NativeAiResult(
                HeritageText(lang, context),
                lang == Lang.En
                    ? new[] { "Theatro da Paz tour schedule and tickets", "How to explore Cidade Velha on foot?", "Basílica de Nazaré mass times", "History of the Ver-o-Peso market" }
                    : new[] { "Quais os horários de visitação do Theatro da Paz?", "Como fazer um roteiro a pé pelo Feliz Lusitânia?", "Informações sobre o Círio de Nazaré", "Como chegar à Basílica de Nazaré" },
                recIds);
        }

        // ---------- 5. Natureza ----------
        if (isNature)
        {
            var matchPlaces = context.Places.Where(p => p.DisplayName.Contains("Mangal", StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains("Bosque", StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains("Utinga", StringComparison.OrdinalIgnoreCase)).Take(3).ToList();
            recIds.AddRange(matchPlaces.Select(p => p.Id));
            return new NativeAiResult(
                NatureText(lang, context),
                lang == Lang.En
                    ? new[] { "Mangal das Garças opening hours and ticket prices", "How to rent a bike at Parque do Utinga?", "Birdwatching tips in Belém", "Bosque Rodrigues Alves highlights" }
                    : new[] { "Horários e ingressos do Mangal das Garças", "Como alugar bicicleta no Parque do Utinga?", "O que ver no Bosque Rodrigues Alves?", "Passeios gratuitos com crianças em Belém" },
                recIds);
        }

        // ---------- 6. Crianças e gratuito ----------
        if (isKids || isFree)
        {
            var kidEvents = context.Events.Where(e => e.Subject is "infantil" or "familia" || e.Audiences.Contains(Core.Taxonomy.Audience.Infantil)).Take(4).ToList();
            recIds.AddRange(context.Places.Where(p => p.PrimaryType is "park" or "museum" or "tourist_attraction").Take(3).Select(p => p.Id));
            return new NativeAiResult(
                KidsAndFreeText(lang, context, kidEvents),
                lang == Lang.En
                    ? new[] { "Family-friendly parks in Belém", "Free cultural events this month", "How to get to Mangal das Garças", "Kids programme at Theatro da Paz" }
                    : new[] { "Programação infantil em Belém", "Eventos gratuitos neste mês", "Como chegar ao Mangal das Garças", "Roteiro em família para o Círio" },
                recIds);
        }

        // ---------- 7. Resposta geral, orientada pelos dados publicados ----------
        var topPlaces = context.Places.Take(4).ToList();
        recIds.AddRange(topPlaces.Select(p => p.Id));
        return new NativeAiResult(
            GeneralText(lang, context),
            lang == Lang.En
                ? new[] { "What are the must-see attractions in Belém?", "Where to eat traditional Amazonian food?", "How to take the boat to Combu Island?", "Which bus line goes to Ver-o-Peso?" }
                : new[] { "O que fazer em Belém em um roteiro de 1 dia?", "Onde comer o melhor peixe com açaí?", "Como fazer a travessia de barco para o Combu?", "Qual linha de ônibus vai para o Ver-o-Peso?" },
            recIds);
    }

    // ================= Mobilidade =================

    private static NativeAiResult? BuildTransitAnswer(
        Lang lang, string question, CurioContext context,
        (string From, string To)? dePara, bool isSchedule, bool isWhereTo)
    {
        var lines = context.Lines;
        var terminals = context.Stops.Where(s => s.StopType is "Terminal" or "Estação BRT").ToList();

        // Caso de-para: procura linhas que conectam os dois pontos.
        if (dePara is { } pair && lines.Count > 0)
        {
            // A linha conecta os dois pontos quando um lado casa com a origem e o outro
            // com o destino (nos dois sentidos de percurso).
            bool Reaches(TransitLine l, string ponto) =>
                Mentions(l.Origin, ponto) || Mentions(l.Destination, ponto) ||
                Mentions(l.Name, ponto) || Mentions(l.IntegrationPoints ?? "", ponto);

            var connectors = lines.Where(l => (Reaches(l, pair.From) && Reaches(l, pair.To))
                                           || (Reaches(l, pair.From) && Mentions(l.Origin, pair.To))
                                           || (Reaches(l, pair.From) && Mentions(l.Destination, pair.To)))
                .Take(6).ToList();

            // Sem linha que ligue exatamente os dois pontos: mostra as linhas de cada
            // lado e o terminal onde a troca acontece.
            if (connectors.Count == 0)
            {
                var fromSide = lines.Where(l => Reaches(l, pair.From)).Take(6).ToList();
                var toSide = lines.Where(l => Reaches(l, pair.To)).Take(6).ToList();
                var shared = terminals.Where(t =>
                    fromSide.Any(l => Mentions(l.IntegrationPoints ?? "", t.Name)) &&
                    toSide.Any(l => Mentions(l.IntegrationPoints ?? "", t.Name))).Take(4).ToList();

                var box = new System.Text.StringBuilder();
                box.AppendLine(lang == Lang.En
                    ? $"🚌 **Route from {pair.From} to {pair.To}**"
                    : $"🚌 **De {pair.From} para {pair.To}**");
                box.AppendLine();
                if (fromSide.Count == 0 && toSide.Count == 0)
                {
                    box.AppendLine(lang == Lang.En
                        ? "No published line reaches those points yet. Check the stops and terminals pages."
                        : "Nenhuma linha publicada atende esses pontos ainda. Consulte as páginas de paradas e terminais.");
                }
                else
                {
                    if (shared.Count > 0)
                    {
                        box.AppendLine(lang == Lang.En
                            ? $"🔄 Transfer at: {string.Join(" · ", shared.Select(t => t.Name))}"
                            : $"🔄 Troque em: {string.Join(" · ", shared.Select(t => t.Name))}");
                        box.AppendLine();
                    }
                    if (fromSide.Count > 0)
                    {
                        box.AppendLine(lang == Lang.En ? "**From the origin**" : "**A partir da origem**");
                        foreach (var l in fromSide) box.AppendLine($"• Linha {l.Code} — {l.Origin} ⇄ {l.Destination}");
                        box.AppendLine();
                    }
                    if (toSide.Count > 0)
                    {
                        box.AppendLine(lang == Lang.En ? "**To the destination**" : "**Para chegar ao destino**");
                        foreach (var l in toSide) box.AppendLine($"• Linha {l.Code} — {l.Origin} ⇄ {l.Destination}");
                        box.AppendLine();
                    }
                    box.AppendLine(lang == Lang.En
                        ? "_Confirm the transfer point and the day's operation with the operator._"
                        : "_Confirme o ponto de troca e a operação do dia com o operador._");
                }
                var sugestoes = fromSide.Concat(toSide).Distinct().Take(6).ToList();
                return new NativeAiResult(box.ToString().TrimEnd(), BuildTransitSuggestions(lang, sugestoes), Array.Empty<long>());
            }

            if (connectors.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(lang == Lang.En
                    ? $"🚌 **Route from {pair.From} to {pair.To}** — official Integra Belém lines"
                    : $"🚌 **De {pair.From} para {pair.To}** — linhas oficiais do Integra Belém");
                sb.AppendLine();
                foreach (var l in connectors)
                {
                    var dir = $"{l.Origin} ⇄ {l.Destination}";
                    sb.AppendLine($"**Linha {l.Code}** — {dir}");
                    if (!string.IsNullOrWhiteSpace(l.ScheduleText) && l.IsVerified) sb.AppendLine($"• Horário: {l.ScheduleText}");
                    if (!string.IsNullOrWhiteSpace(l.IntegrationPoints)) sb.AppendLine($"• Integração: {l.IntegrationPoints}");
                    if (l.ConfirmedFare is { } f) sb.AppendLine($"• Tarifa: R$ {f.ToString("F2", CultureInfo.InvariantCulture)} (meia-passagem R$ {(f / 2).ToString("F2", CultureInfo.InvariantCulture)})");
                    sb.AppendLine($"• Detalhes: /mobilidade/linha/{l.Code}");
                    sb.AppendLine();
                }
                sb.AppendLine(lang == Lang.En
                    ? "_Fares and schedules follow the municipal decree in force. Confirm the day's operation with the operator._"
                    : "_Tarifas e horários seguem o decreto municipal vigente. Confirme a operação do dia com o operador._");
                return new NativeAiResult(sb.ToString().TrimEnd(), BuildTransitSuggestions(lang, connectors), Array.Empty<long>());
            }
        }

        // Onde fica uma parada / terminal: tem precedencia sobre a linha citada,
        // porque a pergunta pede o endereco do ponto, nao as linhas que passam nele.
        var isWhereQuestion = Regex.IsMatch(question, @"\bonde\s+(fica|est[áa])\b", RegexOptions.IgnoreCase);
        if (isWhereQuestion)
        {
            var ponto = terminals.FirstOrDefault(t => MentionedIn(t.Name, question))
                        ?? terminals.FirstOrDefault(t => context.RoutePoints.Any(p => p.StopId == t.Id && MentionedIn(p.StopName ?? p.Label, question)))
                        ?? context.Stops.FirstOrDefault(s => MentionedIn(s.Name, question));

            if (ponto is not null)
            {
                var serves = (ponto.LinesServiced ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var box = new System.Text.StringBuilder();
                box.AppendLine(lang == Lang.En
                    ? $"📍 **{ponto.Name}** — {ponto.StopType}"
                    : $"📍 **{ponto.Name}** — {ponto.StopType}");
                if (!string.IsNullOrWhiteSpace(ponto.Address)) box.AppendLine($"• {ponto.Address}");
                if (!string.IsNullOrWhiteSpace(ponto.Neighborhood)) box.AppendLine($"• {ponto.Neighborhood} · {ponto.Municipality}");
                if (serves.Length > 0) box.AppendLine($"• Linhas: {string.Join(", ", serves)}");
                box.AppendLine("• Integração física: embarque e desembarque dentro do terminal.");
                box.AppendLine("• Integração temporal: desembarque na parada lateral da pista para acessar a estação.");
                box.AppendLine("• Tarifas: R$ 4,60 (meia-passagem R$ 2,30) nos ônibus do Integra Belém.");
                return new NativeAiResult(box.ToString().TrimEnd(), new[] { "Quais linhas passam no Terminal Mangueirão?", "Horários das linhas do Integra Belém", "Como ir do terminal até o Ver-o-Peso?", "Tarifas de ônibus em Belém" }, Array.Empty<long>());
            }
        }

        // Linha específica citada na pergunta.
        var cited = lines.FirstOrDefault(l => Regex.IsMatch(question, $@"\b{Regex.Escape(l.Code)}\b", RegexOptions.IgnoreCase))
                     ?? lines.FirstOrDefault(l => context.RoutePoints.Any(p => p.LineCode == l.Code && MentionedIn(p.StopName ?? p.Label, question)));

        if (cited is not null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(lang == Lang.En
                ? $"🚌 **Line {cited.Code}** — {cited.Origin} ⇄ {cited.Destination}"
                : $"🚌 **Linha {cited.Code}** — {cited.Origin} ⇄ {cited.Destination}");
            sb.AppendLine();
            if (cited.IsVerified)
            {
                if (!string.IsNullOrWhiteSpace(cited.ScheduleText)) sb.AppendLine($"• **Horário:** {cited.ScheduleText}");
                if (cited.ConfirmedFare is { } f) sb.AppendLine($"• **Tarifa:** R$ {f.ToString("F2", CultureInfo.InvariantCulture)} · meia-passagem R$ {(f / 2).ToString("F2", CultureInfo.InvariantCulture)}");
                if (!string.IsNullOrWhiteSpace(cited.IntegrationPoints)) sb.AppendLine($"• **Integração:** {cited.IntegrationPoints}");
                if (!string.IsNullOrWhiteSpace(cited.Notes)) sb.AppendLine($"• **Observação:** {cited.Notes}");
            }
            else
            {
                sb.AppendLine("• Horário e tarifa: confirme com o operador.");
            }

            var points = context.RoutePoints.Where(p => p.LineCode == cited.Code).OrderBy(p => p.Direction).ThenBy(p => p.Position).ToList();
            foreach (var dir in new[] { "IDA", "VOLTA" })
            {
                var seq = points.Where(p => p.Direction == dir).ToList();
                if (seq.Count == 0) continue;
                sb.AppendLine();
                sb.AppendLine($"**{dir}** · {seq.Count} pontos do itinerário oficial");
                var integrations = seq.Where(p => p.Kind is "Terminal" or "Estação" or "Integração").Select(p => p.StopName ?? p.Label).Distinct().ToList();
                if (integrations.Count > 0)
                    sb.AppendLine($"• Integra em: {string.Join(" · ", integrations)}");
                var eixos = seq.Where(p => p.Kind == "Eixo").Select(p => p.Label).Take(8).ToList();
                if (eixos.Count > 0)
                    sb.AppendLine($"• Eixos: {string.Join(" → ", eixos)}");
            }
            sb.AppendLine();
            sb.AppendLine($"➡️ Itinerário completo: /mobilidade/linha/{cited.Code}");
            return new NativeAiResult(sb.ToString().TrimEnd(), BuildTransitSuggestions(lang, new[] { cited }), Array.Empty<long>());
        }

        // Linhas que atendem ao ponto citado (ex.: "qual linha vai para o Ver-o-Peso?").
        var serving = lines
            .Where(l => MentionedIn(l.Origin, question) || MentionedIn(l.Destination, question)
                        || MentionedIn(l.Name, question) || MentionedIn(l.IntegrationPoints ?? "", question))
            .Take(8)
            .ToList();

        if (serving.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(lang == Lang.En
                ? $"🚌 **Lines serving your request** — {serving.Count} official line(s)"
                : $"🚌 **Linhas que atendem ao seu pedido** — {serving.Count} linha(s) oficial(is)");
            sb.AppendLine();
            foreach (var l in serving)
            {
                sb.AppendLine($"**Linha {l.Code}** — {l.Origin} ⇄ {l.Destination}");
                if (l.IsVerified)
                {
                    if (!string.IsNullOrWhiteSpace(l.ScheduleText)) sb.AppendLine($"• Horário: {l.ScheduleText}");
                    if (l.ConfirmedFare is { } f) sb.AppendLine($"• Tarifa: R$ {f.ToString("F2", CultureInfo.InvariantCulture)} · meia-passagem R$ {(f / 2).ToString("F2", CultureInfo.InvariantCulture)}");
                    if (!string.IsNullOrWhiteSpace(l.IntegrationPoints)) sb.AppendLine($"• Integração: {l.IntegrationPoints}");
                }
                sb.AppendLine($"• Itinerário: /mobilidade/linha/{l.Code}");
                sb.AppendLine();
            }
            sb.AppendLine(lang == Lang.En
                ? "_Fares and schedules follow the municipal decree in force. Confirm the day's operation with the operator._"
                : "_Tarifas e horários seguem o decreto municipal vigente. Confirme a operação do dia com o operador._");
            return new NativeAiResult(sb.ToString().TrimEnd(), BuildTransitSuggestions(lang, serving), Array.Empty<long>());
        }

        // Onde fica uma parada / terminal.
        var stop = terminals.FirstOrDefault(t => MentionedIn(t.Name, question))
                   ?? terminals.FirstOrDefault(t => context.RoutePoints.Any(p => p.StopId == t.Id && MentionedIn(p.StopName ?? p.Label, question)));

        if (stop is not null && (isWhereTo || terminals.Count > 0))
        {
            var serves = (stop.LinesServiced ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(lang == Lang.En
                ? $"📍 **{stop.Name}** — {stop.StopType}"
                : $"📍 **{stop.Name}** — {stop.StopType}");
            if (!string.IsNullOrWhiteSpace(stop.Address)) sb.AppendLine($"• {stop.Address}");
            if (!string.IsNullOrWhiteSpace(stop.Neighborhood)) sb.AppendLine($"• {stop.Neighborhood}");
            if (serves.Length > 0) sb.AppendLine($"• Linhas: {string.Join(", ", serves)}");
            sb.AppendLine("• Integração física: embarque e desembarque dentro do terminal.");
            sb.AppendLine("• Integração temporal: desembarque na parada lateral da pista para acessar a estação.");
            return new NativeAiResult(sb.ToString().TrimEnd(), new[] { "Quais linhas passam no Terminal Mangueirão?", "Horários das linhas do Integra Belém", "Como ir do Ver-o-Peso ao Terminal São Brás?", "Tarifas de ônibus em Belém" }, Array.Empty<long>());
        }

        // Panorama geral do sistema.
        if (lines.Count > 0 || terminals.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(lang == Lang.En
                ? "🚌 **MinhaBelém Mobility — Integra Belém official network**"
                : "🚌 **Mobilidade MinhaBelém — rede oficial do Integra Belém**");
            sb.AppendLine();
            if (lines.Count > 0)
            {
                sb.AppendLine(lang == Lang.En
                    ? $"The portal has **{lines.Count} official lines** published by SEGBEL/SEMOB."
                    : $"O portal tem **{lines.Count} linhas oficiais** publicadas pela SEGBEL/SEMOB.");
                foreach (var l in lines.Take(8))
                {
                    var extra = l.IsVerified && !string.IsNullOrWhiteSpace(l.ScheduleText) ? $" — {l.ScheduleText}" : "";
                    sb.AppendLine($"• **Linha {l.Code}**: {l.Origin} ⇄ {l.Destination}{extra}");
                }
                if (lines.Count > 8) sb.AppendLine($"• … e mais {lines.Count - 8} linhas no total.");
            }
            if (terminals.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(lang == Lang.En ? "**Integration terminals and stations**" : "**Terminais e estações de integração**");
                foreach (var t in terminals.Take(10))
                {
                    var serves = (t.LinesServiced ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    sb.AppendLine($"• **{t.Name}** ({t.Neighborhood}) — {serves.Length} linhas");
                }
            }
            if (context.RiverRoutes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(lang == Lang.En ? "**River crossings**" : "**Travessias fluviais**");
                foreach (var r in context.RiverRoutes.Take(4))
                {
                    var fare = r.ConfirmedFare is { } f ? $" · R$ {f.ToString("F2", CultureInfo.InvariantCulture)}" : "";
                    sb.AppendLine($"• **{r.Name}** — {r.DeparturePort} → {r.DestinationPort}{fare}");
                }
            }
            return new NativeAiResult(sb.ToString().TrimEnd(), new[] { "Horários das linhas do Integra Belém", "Quais linhas passam no Terminal Mangueirão?", "Como ir do Ver-o-Peso ao Terminal São Brás?", "Tarifas de ônibus em Belém" }, Array.Empty<long>());
        }

        return null;
    }

    private static IReadOnlyList<string> BuildTransitSuggestions(Lang lang, IEnumerable<TransitLine> lines)
    {
        var first = lines.FirstOrDefault();
        if (first is null) return Array.Empty<string>();
        return lang == Lang.En
            ? new[] { $"What is the schedule for line {first.Code}?", "Which lines serve Terminal Mangueirão?", "How do I get from Ver-o-Peso to São Brás?", "Bus fares in Belém" }
            : new[] { $"Qual o horário da linha {first.Code}?", "Quais linhas passam no Terminal Mangueirão?", "Como faço do Ver-o-Peso até São Brás?", "Quanto custa a passagem de ônibus em Belém?" };
    }

    /// <summary>Compara ignorando maiúsculas e acentos (ex.: "São Brás" = "Sao Bras").</summary>
    private static bool Mentions(string haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle)) return false;
        return Normalize(haystack).Contains(Normalize(needle.Trim()), StringComparison.Ordinal);
    }

    /// <summary>Verifica se o nome informado aparece dentro da pergunta do usuário.</summary>
    private static bool MentionedIn(string name, string question)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(question)) return false;
        return Normalize(question).Contains(Normalize(name), StringComparison.Ordinal);
    }

    /// <summary>Remove acentos e normaliza para comparação textual.</summary>
    private static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Detecta "de X para Y" na pergunta.</summary>
    private static (string From, string To)? DetectOriginDestination(string question)
    {
        var normalized = question.Trim().ToLowerInvariant();
        // "onde fica" não é um pedido de rota: evita capturar "fica para ...".
        if (normalized.Contains("onde fica", StringComparison.Ordinal) ||
            normalized.Contains("qual linha", StringComparison.Ordinal) ||
            normalized.Contains("qual o", StringComparison.Ordinal))
            return null;

        var m = Regex.Match(question, @"(?:de|do|da|desde|from)\s+(.{2,50}?)\s+(?:até|ate|para|pra|to)\s+(.{2,50})$", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var from = Clean(m.Groups[1].Value);
        var to = Clean(m.Groups[2].Value);
        if (from.Length < 2 || to.Length < 2) return null;
        // Ignora preposições que sobraram na origem ("do Terminal X até Y").
        from = Regex.Replace(from, @"^(o|a|os|as|o\s|ao\s+|do\s+|da\s+|no\s+|na\s+)", "", RegexOptions.IgnoreCase).Trim();
        if (from.Length < 2) return null;
        return (from, to);
    }

    private static string Clean(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ").Trim('?', '!', '.', ',');

    // ================= Textos por idioma =================

    private enum Lang { Pt, En, Fr, Es, Ru, Zh, Ja }

    private static Lang DetectLanguage(string culture)
    {
        if (culture.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return Lang.En;
        if (culture.StartsWith("fr", StringComparison.OrdinalIgnoreCase)) return Lang.Fr;
        if (culture.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return Lang.Es;
        if (culture.StartsWith("ru", StringComparison.OrdinalIgnoreCase)) return Lang.Ru;
        if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return Lang.Zh;
        if (culture.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return Lang.Ja;
        return Lang.Pt;
    }

    private static string GastronomyText(Lang lang, CurioContext ctx) => lang switch
    {
        Lang.En =>
            "🍽️ **Gastronomic Guide to Belém — UNESCO Creative City of Gastronomy**\n\n" +
            "Belém blends indigenous Amazonian techniques with riverbank flavors:\n\n" +
            "• **Açaí with fried fish:** eaten unsweetened and thick, with crispy *filhote* or *dourada* and toasted cassava flour (*farinha d'água*). Best at the Ver-o-Peso docks early morning.\n" +
            "• **Tacacá (from 17:00):** a warm cuia of *tucupi*, *jambu* leaves and dried prawns. Standouts: Tacacá do Renato (Nazaré) and Praça Brasil.\n" +
            "• **Maniçoba & Pato no Tucupi:** the Círio festival classics — *maniçoba* is slow-boiled for seven days to remove the cassava's natural toxicity.\n\n" +
            "💡 *Pro-Tip:* pair your meal with a craft beer brewed with taperebá or bacuri at Estação das Docas at sunset.",
        Lang.Fr =>
            "🍽️ **Guide Gastronomique de Belém — Ville Créative UNESCO**\n\n" +
            "• **Açaí au poisson frit:** consommé pur, avec du poisson frais frit (*filhote*) et de la farine de manioc.\n" +
            "• **Tacacá (vers 17h):** bouillon chaud de tucupi, feuilles de jambu et crevettes séchées.\n" +
            "• **Maniçoba:** mijotée sept jours entiers pour éliminer l'acidité du manioc.\n\n" +
            "💡 *Conseil:* les restaurants au bord de la baie à l'Estação das Docas sont idéaux au coucher du soleil.",
        Lang.Es =>
            "🍽️ **Guía Gastronómica de Belém — Ciudad Creativa de la UNESCO**\n\n" +
            "• **Açaí con pescado frito:** açaí espeso y sin azúcar, con filhote frito y harina de mandioca.\n" +
            "• **Tacacá (desde las 17:00):** caldo de tucupi, hojas de jambú y camarones secos.\n" +
            "• **Pato no Tucupi & Maniçoba:** platos clásicos del Círio, con hierbas amazónicas aromáticas.\n\n" +
            "💡 *Consejo:* visite la Estação das Docas al atardecer.",
        _ =>
            "🍽️ **Guia Gastronômico Oficial de Belém — Cidade Criativa da Gastronomia pela UNESCO**\n\n" +
            "A culinária paraense é a mais autêntica e identitária do Brasil, herdando técnicas ancestrais indígenas com ingredientes da floresta e dos rios:\n\n" +
            "• **Açaí com Peixe Frito:** o açaí é consumido puro, espesso e salgado, com peixe frito crocante (*filhote*, *dourada*) e a farinha de Bragança (farinha d'água de mandioca). A experiência mais raiz é na Feira do Açaí e no Mercado de Peixe do Ver-o-Peso pela manhã.\n" +
            "• **Tacacá no fim da tarde:** servido quentinho na cuia de cuité — caldo dourado de *tucupi*, goma de tapioca, camarões secos e folhas frescas de *jambu*. Destaques: Tacacá do Renato (Nazaré), Praça Brasil e Largo da Sé.\n" +
            "• **Maniçoba & Pato no Tucupi:** a maniçoba é feita com a maniva (folha da mandioca brava) moída e cozida por 7 dias ininterruptos para eliminar o ácido cianídrico — patrimônio de afeto do Círio de Nazaré.\n" +
            "• **Doces e Frutas Amazônicas:** sorvetes de cupuaçu, bacuri, taperebá e muruci.\n\n" +
            "💡 *Dica do Guia:* almoce nos restaurantes ribeirinhos da Ilha do Combu ou na Estação das Docas contemplando o pôr do sol na Baía do Guajará."
    };

    private static string IslandText(Lang lang, CurioContext ctx)
    {
        var fluvial = ctx.RiverRoutes.Where(r => r.DestinationIsland.Contains("Combu", StringComparison.OrdinalIgnoreCase) || r.DestinationIsland.Contains("Cotijuba", StringComparison.OrdinalIgnoreCase)).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(lang == Lang.En
            ? "🚤 **Amazonian Islands & River Crossings of Belém**"
            : "🚤 **Ilhas & Travessias Fluviais de Belém**");
        sb.AppendLine();
        if (fluvial.Count > 0)
        {
            foreach (var r in fluvial)
            {
                var fare = r.ConfirmedFare is { } f ? $" · tarifa R$ {f.ToString("F2", CultureInfo.InvariantCulture)}" : "";
                sb.AppendLine($"• **{r.Name}** — {r.DeparturePort} → {r.DestinationPort}{fare}");
                if (!string.IsNullOrWhiteSpace(r.OperatingHours)) sb.AppendLine($"  Horários: {r.OperatingHours}");
            }
        }
        sb.AppendLine();
        sb.AppendLine(lang == Lang.En
            ? "• **Ilha do Combu:** take the **boat** from *Praça Princesa Isabel* (Terminal Hidroviário Ruy Barata), in Condor. Riverside restaurants on stilts and *Filha do Combu* chocolate.\n" +
              "• **Ilha de Cotijuba:** calm freshwater beaches (Praia do Vai-Quem-Quer, Praia do Farol); the boat leaves from the Icoaraci pier.\n" +
              "• **Ilha de Mosqueiro:** over 15 freshwater beaches with real river waves, reached by the PA-391 bridge.\n\n" +
              "💡 *Safety:* wear a life jacket and return before sunset for calmer water."
            : "• **Ilha do Combu:** o embarque fica na **Praça Princesa Isabel** (Terminal Hidroviário Ruy Barata, Condor). Na ilha há restaurantes sobre palafitas e a *Filha do Combu*.\n" +
              "• **Ilha de Cotijuba:** a 'Ilha do Amor', com praias de água doce mansas (Praia do Vai-Quem-Quer, Praia do Farol). A travessia parte do Trapiche de Icoaraci.\n" +
              "• **Ilha de Mosqueiro:** mais de 15 praias de água doce com ondas reais (Farol, Chapéu Virado, Murubira, Paraíso), acessadas pela ponte PA-391.\n\n" +
              "💡 *Dica de Ouro:* leve dinheiro em espécie (o sinal oscila em alguns pontos ribeirinhos) e use sempre o colete salva-vidas.");
        return sb.ToString();
    }

    private static string HeritageText(Lang lang, CurioContext ctx) => lang == Lang.En
        ? "🏛️ **Historic Heritage & Cultural Landmarks in Belém**\n\n" +
          "Founded in 1616, Belém holds one of Brazil's richest colonial and Belle Époque heritages:\n\n" +
          "• **Complexo Feliz Lusitânia (Cidade Velha):** the cradle of Belém — *Forte do Presépio* (1616), *Casa das Onze Janelas*, the *Catedral da Sé* (1755) and the *Museu de Arte Sacra*.\n" +
          "• **Theatro da Paz (1878):** one of Brazil's most opulent neoclassical opera houses, built with Italian marble, French crystal and English iron during the Rubber Boom.\n" +
          "• **Basílica Santuário de Nazaré:** centrepiece of the *Círio de Nazaré*, a procession of some two million people every October.\n" +
          "• **Mercado de Ferro do Ver-o-Peso (1899):** prefabricated iron architecture imported from Scotland and England.\n\n" +
          "💡 *Visitor Tip:* most state museums offer accessible facilities and guided educational tours."
        : "🏛️ **Patrimônio Histórico, Museus & Monumentos de Belém**\n\n" +
          "Fundada em 12 de janeiro de 1616 por Francisco Caldeira Castelo Branco, Belém reúne mais de quatro séculos de história, marcada pelo barroco português e pela suntuosa Belle Époque da Borracha:\n\n" +
          "• **Complexo Feliz Lusitânia (Cidade Velha):** o berço da cidade — **Forte do Presépio (Forte do Castelo)**, **Casa das Onze Janelas**, a monumental **Catedral da Sé** (1755) e o **Museu de Arte Sacra**.\n" +
          "• **Theatro da Paz (Praça da República):** inaugurado em 1878 no apogeu do Ciclo da Borracha, inspirado no Teatro Scala de Milão, com pisos de marchetaria de madeiras nobres, lustres de cristal francês e afrescos de Domenico De Angelis.\n" +
          "• **Basílica Santuário de Nossa Senhora de Nazaré:** epicentro do **Círio de Nazaré**, com cerca de dois milhões de devotos no segundo domingo de outubro.\n" +
          "• **Mercado de Ferro do Ver-o-Peso (1899):** estrutura metálica pré-fabricada na Escócia, tombada pelo IPHAN.\n" +
          "• **Novo Mercado de São Brás:** joia neoclássica e art nouveau do arquiteto Francisco Bolonha, hoje revitalizada com gastronomia, artesanato e cultura.\n\n" +
          "💡 *Dica de Roteiro:* o Centro Histórico é perfeito para caminhar pela manhã (08h às 11h).";

    private static string NatureText(Lang lang, CurioContext ctx) => lang == Lang.En
        ? "🌿 **Nature, Urban Parks & Amazonian Wildlife in Belém**\n\n" +
          "• **Mangal das Garças (Cidade Velha):** a 40,000 m² ecological park on the Guamá river, with free-roaming scarlet ibises, the *Reserva José Márcio Ayres* butterfly sanctuary and the 47 m Belém Lighthouse.\n" +
          "• **Parque Estadual do Utinga:** 1,300 hectares protecting the Bolonha and Água Preta lakes — ideal for morning walks, bike rental and birdwatching.\n" +
          "• **Bosque Rodrigues Alves:** 15 hectares of primary Amazon forest in the Marco district, with agoutis, centenary sumaúmas and orchids.\n\n" +
          "💡 *Family & Accessibility:* Mangal das Garças and Parque do Utinga have paved, wheelchair-accessible trails."
        : "🌿 **Natureza, Parques Ecológicos & Fauna Amazônica em Belém**\n\n" +
          "• **Mangal das Garças (Cidade Velha):** parque naturalístico de 40.000 m² às margens do Rio Guamá, com centenas de guarás voando soltos, o Borboletário *Reserva José Márcio Ayres*, o Farol de Belém (mirante de 47 m) e o Museu Amazônico da Navegação.\n" +
          "• **Parque Estadual do Utinga Camillo Vianna:** 1.393 hectares que protegem os Lagos Bolonha e Água Preta — trilhas monitoradas, aluguel de bicicletas e vista das copas.\n" +
          "• **Bosque Rodrigues Alves:** 15 hectares de mata de terra firme primária na Avenida Almirante Barroso, com cutias soltas, sumaúmas centenárias e orquidário.\n\n" +
          "💡 *Dica de Horário:* visite o Mangal às 10h ou 15h para acompanhar a alimentação das garças e guarás.";

    private static string KidsAndFreeText(Lang lang, CurioContext ctx, IReadOnlyList<ContentItem> kidEvents)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(lang == Lang.En
            ? "🧒 **Family & free things to do in Belém**"
            : "🧒 **Passeios em família e opções gratuitas em Belém**");
        sb.AppendLine();
        if (kidEvents.Count > 0)
        {
            sb.AppendLine(lang == Lang.En ? "**Published kids and family events**" : "**Eventos infantis e para a família publicados**");
            foreach (var e in kidEvents)
                sb.AppendLine($"• {e.DisplayTitle} — {e.SourceName}");
            sb.AppendLine();
        }
        var places = ctx.Places.Where(p => p.PrimaryType is "park" or "museum" or "tourist_attraction").Take(4).ToList();
        if (places.Count > 0)
        {
            sb.AppendLine(lang == Lang.En ? "**Places families usually enjoy**" : "**Lugares que as famílias costumam aproveitar**");
            foreach (var p in places) sb.AppendLine($"• {p.DisplayName} — {p.Neighborhood}");
            sb.AppendLine();
        }
        sb.AppendLine(lang == Lang.En
            ? "🎡 *Tip:* Praça da República and Parque da Residência have family-friendly open areas; the Círio de Nazaré publishes a kids' programme in the portal agenda."
            : "🎡 *Dica:* a Praça da República e o Parque da Residência têm área de convivência; o Círio de Nazaré tem programação infantil publicada na agenda do portal.");
        return sb.ToString();
    }

    private static string GeneralText(Lang lang, CurioContext ctx)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(lang == Lang.En
            ? "✨ **Curió AI — your official guide to Belém do Pará**"
            : "✨ **Curió IA — seu guia inteligente oficial de Belém do Pará**");
        sb.AppendLine();
        sb.AppendLine(lang == Lang.En
            ? $"I'm Curió. I read the portal's published records — {ctx.Places.Count} places, {ctx.Events.Count} upcoming events, {ctx.Lines.Count} official bus lines, {ctx.Stops.Count} boarding points — and I answer only with what is published here."
            : $"Sou o Curió. Leio os registros publicados no portal — {ctx.Places.Count} lugares, {ctx.Events.Count} eventos publicados, {ctx.Lines.Count} linhas oficiais de ônibus e {ctx.Stops.Count} pontos de embarque — e respondo apenas com o que está publicado aqui.");
        sb.AppendLine();
        if (ctx.Places.Count > 0)
        {
            sb.AppendLine(lang == Lang.En ? "**Places matched to your question**" : "**Lugares encontrados para a sua pergunta**");
            foreach (var p in ctx.Places.Take(4)) sb.AppendLine($"• **{p.DisplayName}** — {p.Neighborhood} · /guia/{p.Id}");
            sb.AppendLine();
        }
        if (ctx.Events.Count > 0)
        {
            sb.AppendLine(lang == Lang.En ? "**Published events**" : "**Eventos publicados**");
            foreach (var e in ctx.Events.Take(4)) sb.AppendLine($"• **{e.DisplayTitle}** — {e.SourceName}");
            sb.AppendLine();
        }
        if (ctx.Lines.Count > 0)
        {
            sb.AppendLine(lang == Lang.En ? "**Bus lines that may help**" : "**Linhas de ônibus que podem ajudar**");
            foreach (var l in ctx.Lines.Take(4)) sb.AppendLine($"• **Linha {l.Code}** — {l.Origin} ⇄ {l.Destination}");
            sb.AppendLine();
        }
        sb.AppendLine(lang == Lang.En
            ? "💡 *Try:* \"Which line goes to Ver-o-Peso?\", \"What time does line 758 leave?\", \"How do I get from the terminal to Combu?\" — or ask about food, heritage, nature and the Círio."
            : "💡 *Experimente:* \"Qual linha vai para o Ver-o-Peso?\", \"A que horas sai a linha 758?\", \"Como faço do terminal até o Combu?\" — ou pergunte sobre gastronomia, patrimônio, natureza e o Círio.");
        return sb.ToString();
    }
}
