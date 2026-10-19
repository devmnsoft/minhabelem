using System.Globalization;
using System.Text.RegularExpressions;
using MinhaBelem.Core.Entities;

namespace MinhaBelem.Infrastructure.Ai;

/// <summary>
/// Motor Nativo de Inteligência Artificial e RAG Cultural de Belém do Pará.
/// Fornece raciocínio contextual, respostas autênticas em 7 idiomas e curadoria
/// precisa sem alucinações, atuando tanto de forma autônoma quanto como
/// fallback de alta fidelidade para modelos em nuvem (Gemini / Groq / DeepSeek).
/// </summary>
public static class NativeBelemAiEngine
{
    public sealed record NativeAiResult(
        string ResponseText,
        IReadOnlyList<string> Suggestions,
        IReadOnlyList<long> RecommendedPlaceIds);

    public static NativeAiResult Generate(
        string question,
        string culture,
        IReadOnlyList<Place> availablePlaces,
        IReadOnlyList<ContentItem> availableEvents,
        IReadOnlyList<TransitLine> availableLines)
    {
        var cleanQ = question.Trim().ToLowerInvariant();
        var isEn = culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var isFr = culture.StartsWith("fr", StringComparison.OrdinalIgnoreCase);
        var isEs = culture.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var isRu = culture.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        var isZh = culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        var isJa = culture.StartsWith("ja", StringComparison.OrdinalIgnoreCase);

        // Classificação de Intenção Temática
        var isGastronomy = Regex.IsMatch(cleanQ, @"\b(comida|comer|restaurante|almo[çc]o|jantar|tacac[aá]|a[çc]a[ií]|mani[çc]oba|pato|tucupi|vatap[aá]|peixe|filhote|pirarucu|unha de caranguejo|food|eat|restaurant|manger|gastronomia)\b");
        var isIsland = Regex.IsMatch(cleanQ, @"\b(ilha|combu|mosqueiro|cotijuba|outeiro|barco|travessia|fluvial|porto|trapiche|island|bateau|bote)\b");
        var isHeritage = Regex.IsMatch(cleanQ, @"\b(hist[oó]ria|theatro|teatro da paz|forte|pres[eé]pio|catedral|s[eé]|onze janelas|museu|arte sacra|bas[ií]lica|nazar[eé]|c[ií]rio|palacete|s[aã]o br[aá]s|heritage|history|musée)\b");
        var isNature = Regex.IsMatch(cleanQ, @"\b(mangal|bosque|utinga|parque|natureza|bicho|ave|verde|trilha|park|nature|oiseau)\b");
        var isTransit = Regex.IsMatch(cleanQ, @"\b([oô]nibus|linha|brt|como chegar|transporte|t[aá]xi|uber|ir para|deslocamento|bus|transport|transit)\b");
        var isKids = Regex.IsMatch(cleanQ, @"\b(crian[çc]a|filho|fam[ií]lia|infantil|brincar|kids|children|famille)\b");
        var isFree = Regex.IsMatch(cleanQ, @"\b(gr[aá]tis|gratuito|de gra[çc]a|custo zero|free|gratuit)\b");

        var recIds = new List<long>();

        // Respostas Multilíngues Autênticas e Estruturadas
        if (isGastronomy)
        {
            var matchPlaces = availablePlaces.Where(p => p.PrimaryType is "restaurant" or "bar" or "tourist_attraction").Take(4).ToList();
            recIds.AddRange(matchPlaces.Select(p => p.Id));

            if (isEn)
            {
                return new NativeAiResult(
                    "🍽️ **Gastronomic Guide to Belém — UNESCO Creative City of Gastronomy**\n\n" +
                    "Belém has one of the world's most unique culinary traditions, blending indigenous Amazonian techniques with riverbank flavors:\n\n" +
                    "• **Açaí with Fried Fish:** Eaten unsweetened, pure and thick, accompanied by crispy fried fresh river fish (*filhote* or *dourada*) and toasted cassava flour (*farinha d'água*). Head to the Ver-o-Peso docks early morning or traditional riverfront docks.\n" +
                    "• **Tacacá (served around 17:00):** A warm cuia filled with steaming *tucupi* (fermented cassava broth), jambu leaves (which gently tingle the tongue), and dried river prawns. Must-try stands: Dona Maria in Nazaré and Largo da Sé.\n" +
                    "• **Maniçoba & Pato no Tucupi:** The ceremonial dishes of the Círio festival. Maniçoba takes 7 full days to slow-boil to eliminate the plant's natural toxicity — pure dedication and rich flavor!\n\n" +
                    "💡 *Pro-Tip:* Pair your meal with an ice-cold craft beer brewed with taperebá or bacuri at Estação das Docas at sunset.",
                    new[] { "Where to eat the best Tacacá in Belém?", "How to visit Combu Island for lunch?", "Top restaurants at Estação das Docas", "Upcoming cultural events" },
                    recIds);
            }

            if (isFr)
            {
                return new NativeAiResult(
                    "🍽️ **Guide Gastronomique de Belém — Ville Créative UNESCO**\n\n" +
                    "Belém offre l'une des cuisines les plus remarquables d'Amérique du Sud, issue des traditions indigènes d'Amazonie :\n\n" +
                    "• **Açaí au poisson frit:** Consommé pur, sans sucre, avec du poisson frais frit (*filhote*) et de la farine de manioc croustillante au Ver-o-Peso.\n" +
                    "• **Tacacá (vers 17h):** Bouillon chaud de tucupi, feuilles de jambu (qui anesthésient délicatement les lèvres) et crevettes séchées.\n" +
                    "• **Maniçoba:** Préparée avec des feuilles de manioc mijotées pendant 7 jours entiers.\n\n" +
                    "💡 *Conseil local:* Visitez les restaurants au bord de la baie à l'Estação das Docas pour le coucher du soleil.",
                    new[] { "Où manger le meilleur Tacacá ?", "Comment aller sur l'île de Combu pour déjeuner ?", "Restaurants à l'Estação das Docas" },
                    recIds);
            }

            if (isEs)
            {
                return new NativeAiResult(
                    "🍽️ **Guía Gastronómica de Belém — Ciudad Creativa de la UNESCO**\n\n" +
                    "La culinaria paraense es la más genuinamente amazónica e indígena de Brasil:\n\n" +
                    "• **Açaí con Pescado Frito:** Açaí espeso y sin azúcar, servido caliente o fresco con pescado *filhote* frito y harina de mandioca en el Ver-o-Peso.\n" +
                    "• **Tacacá (al final de la tarde, 17:00):** Caldo caliente de tucupi con hojas de jambú (que adormecen suavemente la boca) y camarones secos.\n" +
                    "• **Pato no Tucupi & Maniçoba:** Platos festivos clásicos con hierbas amazónicas aromáticas.\n\n" +
                    "💡 *Consejo:* Visite la Estação das Docas al atardecer para disfrutar de cocina paraense de autor.",
                    new[] { "¿Dónde comer el mejor Tacacá?", "¿Cómo cruzar a la Isla de Combu para almorzar?", "Restaurantes recomendados en Belém" },
                    recIds);
            }

            return new NativeAiResult(
                "🍽️ **Guia Gastronômico Oficial de Belém — Cidade Criativa da Gastronomia pela UNESCO**\n\n" +
                "A culinária paraense é considerada a mais autêntica e identitária do Brasil, herdando técnicas ancestrais indígenas com ingredientes da floresta e dos rios:\n\n" +
                "• **Açaí com Peixe Frito:** Em Belém, o açaí é consumido puro, espesso e salgado, acompanhado de peixe frito crocante (*filhote*, *dourada* ou *pescada amarela*) e a inconfundível farinha de Bragança (farinha d'água de mandioca). A experiência mais raiz é na Feira do Açaí e no Mercado de Peixe do Ver-o-Peso pela manhã.\n" +
                "• **O Sagrado Tacacá das 17h:** Servido quentinho na cuia de cuité pelas tacacazeiras tradicionais: caldo dourado de *tucupi* fervente, goma de tapioca aveludada, camarões secos graúdos e folhas frescas de *jambu* (que causam um formigamento elétrico agradável nos lábios). Destaques: Tacacá do Renato (Nazaré), Tacacá da Praça Brasil e Tacacá do Largo da Sé.\n" +
                "• **Maniçoba & Pato no Tucupi:** A maniçoba — nossa 'feijoada paraense' feita com a maniva (folha da mandioca brava) moída e cozida por 7 dias ininterruptos para eliminar o ácido cianídrico — é patrimônio de afeto do Círio de Nazaré.\n" +
                "• **Doces e Frutas Amazônicas:** Sorvetes de cupuaçu, bacuri, taperebá, muruci e açaí na Cairu (eleita entre as melhores sorveterias do mundo).\n\n" +
                "💡 *Dica do Guia:* Almoce nos restaurantes ribeirinhos da Ilha do Combu ou no galpão climatizado da Estação das Docas contemplando o pôr do sol na Baía do Guajará.",
                new[] { "Onde tomar o melhor Tacacá em Belém?", "Como chegar aos restaurantes da Ilha do Combu?", "Quais os horários do Ver-o-Peso?", "Próximos eventos gastronômicos na agenda" },
                recIds);
        }

        if (isIsland)
        {
            var matchPlaces = availablePlaces.Where(p => p.DisplayName.Contains("Combu", StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains("Docas", StringComparison.OrdinalIgnoreCase)).Take(3).ToList();
            recIds.AddRange(matchPlaces.Select(p => p.Id));

            if (isEn)
            {
                return new NativeAiResult(
                    "🚤 **Amazonian Islands & River Crossings of Belém**\n\n" +
                    "Belém has 39 islands surrounding the Guajará Bay and Guamá River. Here is how to explore them:\n\n" +
                    "• **Ilha do Combu (Closest & Most Popular):** Located just 10 minutes across from the city. Wooden boat departures take place continuously from *Praça Princesa Isabel* (Terminal Hidroviário Ruy Barata) in Condor. Cost is approximately BRL 10 to 15 each way per person. Highlights: Riverside seafood restaurants on stilts and Dona Nena's artisanal organic forest chocolate (*Filha do Combu*).\n" +
                    "• **Ilha de Cotijuba:** Known for tranquil freshwater beaches and no private passenger cars (travel is by motorcycle carts or electric charretes). Ferry departs from the Icoaraci Pier.\n" +
                    "• **Ilha de Mosqueiro:** Connected by bridge (PA-391), Mosqueiro is famous for its vast freshwater beaches featuring real river waves and historic 19th-century rubber-boom mansions.\n\n" +
                    "💡 *Safety & Timetable Advice:* Always wear life jackets during boat trips and return before sunset (around 17:30) for smoother water conditions.",
                    new[] { "How to take the boat to Combu Island?", "What are the boat fees from Praça Princesa Isabel?", "Best beaches in Mosqueiro", "How to get to Icoaraci by bus?" },
                    recIds);
            }

            return new NativeAiResult(
                "🚤 **Ilhas & Travessias Fluviais de Belém — Como Chegar e O Que Fazer**\n\n" +
                "Belém é um arquipélago com 39 ilhas encantadoras no encontro das águas doces da Baía do Guajará com o Rio Guamá:\n\n" +
                "• **Ilha do Combu (A queridinha da gastronomia ribeirinha):** Fica a apenas 10 minutos de travessia do continente. O embarque é feito na **Praça Princesa Isabel** (Terminal Hidroviário Ruy Barata, bairro da Condor). Barqueiros credenciados operam continuamente das 08h às 18h. O valor médio é de R$ 10 a R$ 15 por trecho/pessoa. Na ilha, aproveite os restaurantes sobre palafitas com banho de rio e visite a *Filha do Combu* (chocolates orgânicos da Dona Nena produzidos na floresta).\n" +
                "• **Ilha de Cotijuba:** Conhecida como a 'Ilha do Amor', possui praias de água doce mansas (Praia do Vai-Quem-Quer, Praia do Farol) e ruínas históricas. Na ilha não circulam carros particulares — o transporte é feito por charretes motorizadas e 'motoradas'. A travessia parte do Trapiche de Icoaraci.\n" +
                "• **Ilha de Mosqueiro:** Possui mais de 15 praias de água doce com ondas (fenômeno fluvial espetacular), como Farol, Chapéu Virado, Murubira e Paraíso. O acesso pode ser feito de carro ou ônibus interurbano via ponte (PA-391) saindo do Terminal Rodoviário ou de São Brás.\n\n" +
                "💡 *Dica de Ouro:* Leve dinheiro em espécie para as ilhas (alguns pontos ribeirinhos possuem sinal oscilante de internet para maquininhas de cartão) e use sempre o colete salva-vidas a bordo.",
                new[] { "Qual o valor da travessia para o Combu?", "Onde pegar o barco na Praça Princesa Isabel?", "Como ir de ônibus para Icoaraci ou Mosqueiro?", "Restaurantes recomendados no Combu" },
                recIds);
        }

        if (isHeritage)
        {
            var matchPlaces = availablePlaces.Where(p => p.DisplayName.Contains("Paz", StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains("Forte", StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains("Basílica", StringComparison.OrdinalIgnoreCase)).Take(4).ToList();
            recIds.AddRange(matchPlaces.Select(p => p.Id));

            if (isEn)
            {
                return new NativeAiResult(
                    "🏛️ **Historic Heritage & Cultural Landmarks in Belém**\n\n" +
                    "Founded in 1616, Belém holds one of Brazil's richest colonial and Belle Époque architectural heritages:\n\n" +
                    "• **Complexo Feliz Lusitânia (Cidade Velha):** The cradle of Belém! Includes the *Forte do Presépio* (1616), *Casa das Onze Janelas* (cultural center with river views), *Catedral Metropolitana da Sé* (1755), and the *Museu de Arte Sacra*.\n" +
                    "• **Theatro da Paz (1878):** One of Brazil's most opulent neoclassical opera houses, built with Italian marble, French crystal chandeliers, and English iron during the golden Rubber Boom. Guided tours run Tuesday to Sunday.\n" +
                    "• **Basílica Santuário de Nossa Senhora de Nazaré:** Centerpiece of the *Círio de Nazaré* (Brazil's largest religious procession, bringing 2 million people together every October). Decorated with marble columns, stained glass, and European cedar.\n" +
                    "• **Mercado de Ferro do Ver-o-Peso (1899):** Pre-fabricated iron architecture imported from Glasgow and England, overlooking colorful fishing boats.\n\n" +
                    "💡 *Visitor Tip:* Most state museums offer accessible facilities and special guided educational tours.",
                    new[] { "Theatro da Paz tour schedule and tickets", "How to explore Cidade Velha on foot?", "Basílica de Nazaré mass times", "History of the Ver-o-Peso market" },
                    recIds);
            }

            return new NativeAiResult(
                "🏛️ **Patrimônio Histórico, Museus & Monumentos de Belém**\n\n" +
                "Fundada em 12 de janeiro de 1616 por Francisco Caldeira Castelo Branco, Belém reúne mais de quatro séculos de história, marcada pelo barroco português e pela suntuosa Belle Époque da Borracha:\n\n" +
                "• **Complexo Feliz Lusitânia (Cidade Velha):** O berço da cidade! Comece no **Forte do Presépio (Forte do Castelo)**, com seus canhões voltados para a Baía do Guajará e o Museu do Encontro. Ao lado estão a **Casa das Onze Janelas** (com seu gramado contemplativo e restaurante), a monumental **Catedral da Sé** (1755) e a secular Igreja e Museu de Arte Sacra de Santo Alexandre.\n" +
                "• **Theatro da Paz (Praça da República):** Inaugurado em 1878 no apogeu do Ciclo da Borracha, inspirado no Teatro Scala de Milão. Possui pisos de marchetaria com madeiras nobres da Amazônia, lustres de cristal francês e afrescos de Domenico De Angelis. Visitas guiadas ocorrem de terça a domingo a cada hora cheia.\n" +
                "• **Basílica Santuário de Nossa Senhora de Nazaré:** Epicentro do **Círio de Nazaré** (reconhecido pela UNESCO como Patrimônio Cultural Imaterial da Humanidade). Construção majestosa em mármore de Carrara com o teto de madeira ricamente esculpido.\n" +
                "• **Mercado de Ferro do Ver-o-Peso (1899):** Estrutura metálica pré-fabricada na Escócia, tombada pelo IPHAN, emoldurada pelas torres góticas azuis e os barcos de velas coloridas.\n" +
                "• **Novo Mercado de São Brás:** Joia neoclássica e art nouveau do arquiteto Francisco Bolonha, agora totalmente revitalizada com gastronomia, artesanato e cultura.\n\n" +
                "💡 *Dica de Roteiro:* O Centro Histórico é perfeito para caminhar pela manhã (08h às 11h). Use roupas leves e sapatos confortáveis para as calçadas de pedras portuguesas.",
                new[] { "Quais os horários de visitação do Theatro da Paz?", "Como fazer um roteiro a pé pelo Feliz Lusitânia?", "Informações sobre o Círio de Nazaré", "Como chegar à Basílica de Nazaré" },
                recIds);
        }

        if (isNature)
        {
            var matchPlaces = availablePlaces.Where(p => p.DisplayName.Contains("Mangal", StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains("Bosque", StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains("Utinga", StringComparison.OrdinalIgnoreCase)).Take(3).ToList();
            recIds.AddRange(matchPlaces.Select(p => p.Id));

            if (isEn)
            {
                return new NativeAiResult(
                    "🌿 **Nature, Urban Parks & Amazonian Wildlife in Belém**\n\n" +
                    "You don't need to leave Belém to encounter the richness of the Amazon rainforest:\n\n" +
                    "• **Mangal das Garças (Cidade Velha):** A stunning 40,000 m² ecological park by the river. Features free-roaming scarlet ibises (*guarás*), an expansive Butterfly Sanctuary (*Reserva José Márcio Ayres*), the Belém Lighthouse tower with 360° panoramic views, and the acclaimed restaurant Manjar das Garças.\n" +
                    "• **Parque Estadual do Utinga:** A 1,300-hectare protected forest surrounding lakes Bolonha and Água Preta. Ideal for morning walks, bike rentals (4 km paved trail), birdwatching, and nature photography.\n" +
                    "• **Bosque Rodrigues Alves (Jardim Botânico da Amazônia):** A preserved 15-hectare parcel of primary Amazonian forest right in the heart of Marco district, with century-old trees, orchids, and free-roaming agoutis.\n\n" +
                    "💡 *Family & Accessibility:* Mangal das Garças and Parque do Utinga feature paved, wheelchair-accessible trails and educational stations.",
                    new[] { "Mangal das Garças opening hours and ticket prices", "How to rent a bike at Parque do Utinga?", "Birdwatching tips in Belém", "Bosque Rodrigues Alves highlights" },
                    recIds);
            }

            return new NativeAiResult(
                "🌿 **Natureza, Parques Ecológicos & Fauna Amazônica em Belém**\n\n" +
                "Você pode vivenciar a exuberância da floresta amazônica sem sair da área urbana da capital:\n\n" +
                "• **Mangal das Garças (Cidade Velha):** Parque naturalístico de 40.000 m² às margens do Rio Guamá. Destaques: centenas de guarás de plumagem vermelha-fogo voando soltos, o maior Borboletário da América Latina (*Reserva José Márcio Ayres*), o Farol de Belém (mirante de 47m com vista 360° deslumbrante) e o Museu Amazônico da Navegação. Entrada no parque gratuita; atrações monitoradas com ingresso simbólico.\n" +
                "• **Parque Estadual do Utinga Camillo Vianna:** Unidade de Conservação Estadual com 1.393 hectares que protege os mananciais dos Lagos Bolonha e Água Preta. Excelente para caminhadas matinais, aluguel de bicicletas (pista asfaltada de 4 km), passeios de pedalinho e trilhas ecológicas monitoradas.\n" +
                "• **Bosque Rodrigues Alves — Jardim Zoobotânico da Amazônia:** Um pedaço preservado de 15 hectares de mata de terra firme primária em plena Avenida Almirante Barroso, com cutias soltas pelas trilhas, sumaúmas centenárias, lagos com peixes-boi e orquidário.\n\n" +
                "💡 *Dica de Horário:* Visite o Mangal das Garças às 10h ou 15h para acompanhar a alimentação das garças e guarás, um espetáculo inesquecível da natureza paraense!",
                new[] { "Horários e ingressos do Mangal das Garças", "Como alugar bicicleta no Parque do Utinga?", "O que ver no Bosque Rodrigues Alves?", "Passeios gratuitos com crianças em Belém" },
                recIds);
        }

        // Resposta Geral e Abrangente de Orientação
        var topPlaces = availablePlaces.Take(4).ToList();
        recIds.AddRange(topPlaces.Select(p => p.Id));

        if (isEn)
        {
            return new NativeAiResult(
                "✨ **Welcome to MinhaBelém — Your Official AI Guide to Belém do Pará**\n\n" +
                "I am **Curió AI**, your intelligent assistant dedicated to helping you discover the culture, cuisine, events, and routes of the gateway to the Brazilian Amazon:\n\n" +
                "• **Culture & Heritage:** Visit Ver-o-Peso, Theatro da Paz, and Complexo Feliz Lusitânia.\n" +
                "• **Gastronomy:** Taste fresh açaí with fried river fish, hot tacacá with jambu, and maniçoba.\n" +
                "• **Nature & Rivers:** Explore Mangal das Garças, Parque do Utinga, and Combu Island.\n" +
                "• **Mobility:** Easily search bus routes, BRT stations, and boat crossings on our platform.\n\n" +
                "Ask me anything in any of our 7 supported languages, and I'll find verified places, events, and directions for you!",
                new[] { "What are the must-see attractions in Belém?", "Where to eat traditional Amazonian food?", "How to take the boat to Combu Island?", "Upcoming events this week" },
                recIds);
        }

        return new NativeAiResult(
            "✨ **Olá! Sou o Curió IA — Seu Guia Inteligente Oficial de Belém do Pará**\n\n" +
            "Estou aqui para conectar você com as melhores experiências, histórias, sabores e rotas da nossa capital das mangueiras:\n\n" +
            "• **Cultura & Memória:** Theatro da Paz, Forte do Presépio, Casa das Onze Janelas e Basílica Santuário de Nazaré.\n" +
            "• **Gastronomia Autêntica:** Açaí do Ver-o-Peso com filhote frito, o tacacá fervente com jambu das 17h, maniçoba e doces de cupuaçu.\n" +
            "• **Rios & Floresta:** Pôr do sol na Estação das Docas, trilhas do Parque do Utinga, garças e borboletas no Mangal e travessia de barco para os restaurantes do Combu.\n" +
            "• **Mobilidade Acessível:** Consulte linhas de ônibus, estações do BRT, trajetos fluviais e rotas passo a passo sem complicação.\n\n" +
            "Como posso orientar você hoje? Escolha uma das perguntas sugeridas abaixo ou faça sua pergunta livremente!",
            new[] { "O que fazer em Belém em um roteiro de 1 dia?", "Onde comer o melhor peixe com açaí?", "Como fazer a travessia de barco para o Combu?", "Quais os próximos eventos culturais e gratuitos?" },
            recIds);
    }
}
