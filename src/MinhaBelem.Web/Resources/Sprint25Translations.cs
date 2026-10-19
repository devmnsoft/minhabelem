namespace MinhaBelem.Web.Resources;

public static class Sprint25Translations
{
    public static void Register(Dictionary<string, Dictionary<string, string>> catalogs)
    {
        var rows = new Dictionary<string, string[]>
        {
            ["EstacaoDocasPhoto"] = ["Estação das Docas, na orla de Belém", "Estação das Docas on Belém’s waterfront", "Estação das Docas sur les quais de Belém", "Estação das Docas en la ribera de Belém", "Эстасан-дас-Докас на набережной Белена", "贝伦河畔的 Estação das Docas", "ベレンの水辺にある Estação das Docas"],
            ["TransitStops"] = ["Paradas e terminais", "Stops and terminals", "Arrêts et terminaux", "Paradas y terminales", "Остановки и терминалы", "站点与终点站", "停留所とターミナル"],
            ["AgendaToday"] = ["O que fazer hoje", "Things to do today", "Que faire aujourd’hui", "Qué hacer hoy", "Чем заняться сегодня", "今日活动", "今日の楽しみ方"],
            ["CityQuestion"] = ["O que você quer fazer em Belém?", "What would you like to do in Belém?", "Que voulez-vous faire à Belém ?", "¿Qué quieres hacer en Belém?", "Чем вы хотите заняться в Белене?", "您想在贝伦做什么？", "ベレンで何をしたいですか？"],
            ["CityWelcome"] = ["Entre mercados, sabores e rios, descubra a cidade no seu ritmo.", "Discover markets, local flavours and rivers at your own pace.", "Découvrez marchés, saveurs et fleuves à votre rythme.", "Descubre mercados, sabores y ríos a tu ritmo.", "Откройте рынки, местную кухню и реки в своём ритме.", "按自己的节奏探索市场、美食与河流。", "市場、郷土料理、川辺を自分のペースで楽しみましょう。"],
            ["CityToday"] = ["Belém hoje", "Belém today", "Belém aujourd’hui", "Belém hoy", "Белен сегодня", "今日贝伦", "今日のベレン"],
            ["CityMore"] = ["Mais", "More", "Plus", "Más", "Ещё", "更多", "その他"],
            ["CityRoutes"] = ["Rotas", "Routes", "Trajets", "Rutas", "Маршруты", "路线", "ルート"],
            ["CitySearch"] = ["O que você procura em Belém?", "What are you looking for in Belém?", "Que cherchez-vous à Belém ?", "¿Qué buscas en Belém?", "Что вы ищете в Белене?", "您在贝伦寻找什么？", "ベレンで何をお探しですか？"],
            ["CityMove"] = ["Como você quer chegar ao seu destino?", "How would you like to get there?", "Comment voulez-vous arriver à destination ?", "¿Cómo quieres llegar a tu destino?", "Как вы хотите добраться?", "您想如何前往目的地？", "目的地までどう行きますか？"],
            ["CityMoveText"] = ["Consulte linhas, paradas e travessias. Confira a fonte e a validade antes de sair.", "Find lines, stops and crossings. Check sources and validity before leaving.", "Consultez lignes, arrêts et traversées. Vérifiez sources et validité avant le départ.", "Consulta líneas, paradas y travesías. Comprueba fuentes y vigencia antes de salir.", "Найдите линии, остановки и переправы. Перед поездкой проверьте источники и сроки.", "查询线路、站点与渡船。出发前请核对来源和有效期。", "路線、停留所、渡船を検索。出発前に情報源と有効期限をご確認ください。"],
            ["CityFluvialText"] = ["Embarques e travessias cadastradas", "Registered departures and crossings", "Embarquements et traversées répertoriés", "Embarques y travesías registradas", "Зарегистрированные причалы и переправы", "已登记的码头与渡运", "登録済みの乗り場と渡船"],
            ["CityCurio"] = ["Planeje com o Curió", "Plan with Curió", "Planifiez avec Curió", "Planea con Curió", "Планируйте с Curió", "与 Curió 一起规划", "Curió と計画する"],
            ["CityNoVideo"] = ["Nenhum vídeo publicado no momento.", "No videos published yet.", "Aucune vidéo publiée pour le moment.", "No hay vídeos publicados por ahora.", "Опубликованных видео пока нет.", "暂无已发布的视频。", "公開済みの動画はまだありません。"],
            ["CityTheme"] = ["Alternar tema", "Toggle theme", "Changer de thème", "Cambiar tema", "Сменить тему", "切换主题", "テーマを切り替え"],
            ["CityGallery"] = ["Belém em imagens", "Belém in pictures", "Belém en images", "Belém en imágenes", "Белен в фотографиях", "贝伦影像", "写真で見るベレン"],
            ["CityNews"] = ["Notícias da cidade", "City news", "Actualités de la ville", "Noticias de la ciudad", "Новости города", "城市新闻", "街のニュース"]
        };
        foreach (var (key, values) in rows)
            for (var i = 0; i < SupportedCultures.All.Length; i++)
                catalogs[SupportedCultures.All[i]][key] = values[i];
    }
}
