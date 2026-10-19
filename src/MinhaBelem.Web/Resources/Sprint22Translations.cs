namespace MinhaBelem.Web.Resources;

public static class Sprint22Translations
{
    public static void Register(Dictionary<string, Dictionary<string, string>> catalogs)
    {
        var rows = new Dictionary<string, string[]>
        {
            ["AiLead"] = [
                "O Curió organiza o que já está publicado: lugares com coordenada, agenda verificada e linhas conferidas. Ele não confirma horário, tarifa nem ingresso.",
                "Curió arranges what is already published: places with a coordinate, the verified agenda and checked lines. It does not confirm hours, fares or tickets.",
                "Curió range ce qui est déjà publié : lieux avec coordonnée, agenda vérifié et lignes contrôlées. Il ne confirme ni horaire, ni tarif, ni billet.",
                "Curió ordena lo ya publicado: lugares con coordenada, agenda verificada y líneas revisadas. No confirma horario, tarifa ni entrada.",
                "Курио собирает уже опубликованное: места с координатой, проверенную афишу и линии. Он не подтверждает часы, тариф или билет.",
                "Curió 只整理已经发布的内容：带坐标的地点、已核实的日程和已核对的线路。它不确认开放时间、票价或门票。",
                "Curióは公開済みの場所、確認済みの日程、照合済みの路線をまとめます。営業時間、料金、切符は確認しません。"
            ],
            ["AiAsk"] = ["Pergunta sobre Belém do Pará", "Question about Belém do Pará", "Question sur Belém do Pará", "Pregunta sobre Belém do Pará", "Вопрос о Белене", "关于贝伦的问题", "ベレンについての質問"],
            ["AiPlaceholder"] = [
                "Ex.: Ver-o-Peso, barco para o Combu, eventos desta agenda",
                "E.g. Ver-o-Peso, boat to Combu, events on this agenda",
                "Ex. : Ver-o-Peso, bateau pour Combu, événements de l'agenda",
                "Ej.: Ver-o-Peso, barco al Combu, eventos de esta agenda",
                "Напр.: Вер-у-Песу, лодка на Комбу, события афиши",
                "例如：Ver-o-Peso、前往 Combu 的船、本日程中的活动",
                "例：Ver-o-Peso、Combu への船、この日程のイベント"
            ],
            ["AiUseContext"] = [
                "Ordenar a agenda publicada quando houver mais de um item",
                "Rank the published agenda when more than one item matches",
                "Classer l'agenda publié lorsqu'il y a plus d'un item",
                "Ordenar la agenda publicada cuando haya más de un ítem",
                "Упорядочить афишу, если найдено больше одного материала",
                "当有多条已发布日程时按其相关度排序",
                "公開済みの日程が複数あるときは関連順に並べる"
            ],
            ["AiSubmit"] = ["Perguntar", "Ask", "Demander", "Preguntar", "Спросить", "提问", "質問する"],
            ["AiSuggestions"] = ["Perguntas que o guia consegue responder", "Questions the guide can answer", "Questions auxquelles le guide répond", "Preguntas que la guía puede responder", "Вопросы, на которые гид отвечает", "指南能够回答的问题", "ガイドが答えられる質問"],
            ["AiAnswerNote"] = [
                "O texto abaixo é orientação cultural. Os cartões são os registros publicados. Horário, preço e lotação só aparecem na ficha quando uma fonte verificada os confirma.",
                "The text below is cultural orientation. The cards are the published records. Hours, price and crowding appear on the card only when a verified source confirms them.",
                "Le texte ci-dessous est une orientation culturelle. Les cartes sont les fiches publiées. Horaire, prix et affluence n'apparaissent que si une source vérifiée les confirme.",
                "El texto de abajo es orientación cultural. Las fichas son los registros publicados. Horario, precio y aforo solo aparecen si una fuente verificada los confirma.",
                "Текст ниже — культурный ориентир. Карточки — опубликованные записи. Часы, цена и заполненность появляются только после проверки источника.",
                "下面的文字是文化导览。卡片才是已发布的记录。开放时间、价格和拥挤程度只有在核实来源确认后才会出现在卡片上。",
                "下の文章は文化的な案内です。カードが公開済みの記録です。営業時間、料金、混雑は確認済みの情報源があるときだけカードに出ます。"
            ],
            ["AiPlaces"] = ["Lugares publicados", "Published places", "Lieux publiés", "Lugares publicados", "Опубликованные места", "已发布地点", "公開済みの場所"],
            ["AiEvents"] = ["Agenda e matérias", "Agenda and stories", "Agenda et articles", "Agenda y notas", "Афиша и материалы", "日程和报道", "日程と記事"],
            ["AiLines"] = ["Linhas conferidas", "Checked lines", "Lignes vérifiées", "Líneas revisadas", "Проверенные линии", "已核对线路", "照合済みの路線"],
            ["AiMedia"] = ["Fotos e vídeos com licença", "Licensed photos and videos", "Photos et vidéos sous licence", "Fotos y vídeos con licencia", "Фото и видео с лицензией", "有许可的照片和视频", "ライセンス付きの写真と動画"],
            ["AiEmptyLists"] = [
                "Nenhum lugar, evento ou linha publicada combina com esta pergunta.",
                "No published place, event or line matches this question.",
                "Aucun lieu, événement ou ligne publié ne correspond à cette question.",
                "Ningún lugar, evento o línea publicada coincide con esta pregunta.",
                "Нет опубликованного места, события или линии по этому вопросу.",
                "没有已发布的地点、活动或线路符合这个问题。",
                "この質問に合う公開済みの場所、イベント、路線はありません。"
            ],
            ["AiPage"] = ["Páginas do resultado", "Result pages", "Pages du résultat", "Páginas del resultado", "Страницы результата", "结果页", "結果のページ"],
            ["AiPrev"] = ["Anterior", "Previous", "Précédente", "Anterior", "Назад", "上一页", "前へ"],
            ["AiNext"] = ["Seguinte", "Next", "Suivante", "Siguiente", "Дальше", "下一页", "次へ"],
            ["AiListen"] = ["Ouvir", "Listen", "Écouter", "Escuchar", "Слушать", "朗读", "読み上げ"],
            ["AiStop"] = ["Parar", "Stop", "Arrêter", "Detener", "Стоп", "停止", "停止"],
            ["AiCopy"] = ["Copiar", "Copy", "Copier", "Copiar", "Копировать", "复制", "コピー"],
            ["AiCopied"] = ["Copiado", "Copied", "Copié", "Copiado", "Скопировано", "已复制", "コピーしました"],
            ["AiDetails"] = ["Ficha", "Card", "Fiche", "Ficha", "Карточка", "详情", "詳細"],
            ["AiRoute"] = ["Como chegar", "How to get there", "Comment y aller", "Cómo llegar", "Как добраться", "如何到达", "行き方"],
            ["AiRead"] = ["Ler", "Read", "Lire", "Leer", "Читать", "阅读", "読む"]
        };
        var cultures = new[] { "pt-BR", "en", "fr", "es", "ru", "zh-Hans", "ja" };
        foreach (var (key, translations) in rows)
        {
            for (var i = 0; i < cultures.Length; i++)
            {
                if (!catalogs.ContainsKey(cultures[i])) catalogs[cultures[i]] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                catalogs[cultures[i]][key] = translations[i];
            }
        }
    }
}
