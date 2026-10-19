namespace MinhaBelem.Web.Resources;

public static class Sprint21Translations
{
    public static void Register(Dictionary<string, Dictionary<string, string>> catalogs)
    {
        var rows = new Dictionary<string, string[]>
        {
            ["Landmarks"] = ["Marcos do guia", "Guide landmarks", "Repères du guide", "Hitos de la guía", "Ориентиры гида", "指南地标", "ガイドの目印"],
            ["LandmarkLead"] = ["Pontos com coordenada do cadastro MinhaBelém. A foto, quando existe, tem licença aberta e crédito. Horário e preço não estão nesta lista.", "Places with a MinhaBelém coordinate. A photo, when present, has an open license and a credit. Hours and prices are not in this list.", "Lieux avec une coordonnée MinhaBelém. La photo, lorsqu'elle existe, a une licence ouverte et un crédit. Horaires et prix ne sont pas dans cette liste.", "Lugares con coordenada de MinhaBelém. La foto, cuando existe, tiene licencia abierta y crédito. Horario y precio no están en esta lista.", "Места с координатой MinhaBelém. Фото, если оно есть, имеет открытую лицензию и указание автора. Часы и цены в этом списке не указаны.", "带有 MinhaBelém 坐标的地点。若有照片，则为开放许可并署名。此列表不含开放时间和价格。", "MinhaBelémの座標を持つ地点です。写真がある場合はオープンライセンスとクレジット付きです。営業時間と料金はこの一覧にありません。"],
            ["LandmarkUnverified"] = ["Horário e preço deste ponto não foram confirmados por uma fonte verificada. A coordenada serve para chegar.", "Hours and price for this place have not been confirmed by a verified source. The coordinate is for getting there.", "L'horaire et le prix de ce lieu n'ont pas été confirmés par une source vérifiée. La coordonnée sert à s'y rendre.", "El horario y el precio de este lugar no fueron confirmados por una fuente verificada. La coordenada sirve para llegar.", "Часы и цена этого места не подтверждены проверенным источником. Координата нужна, чтобы добраться.", "该地点的开放时间和价格尚未经核实来源确认。坐标用于前往。", "この地点の営業時間と料金は確認済みの情報源によるものではありません。座標は到着のためのものです。"],
            ["VisitRequestTitle"] = ["Pedido de visita", "Visit request", "Demande de visite", "Pedido de visita", "Запрос на визит", "到访请求", "訪問の依頼"],
            ["VisitRequestLead"] = ["A equipe recebe o pedido. Isto não reserva ingresso, mesa nem embarcação e não gera cobrança.", "The team receives the request. It does not reserve a ticket, a table or a boat and it creates no charge.", "L'équipe reçoit la demande. Cela ne réserve ni billet, ni table, ni bateau et ne crée aucun paiement.", "El equipo recibe el pedido. Esto no reserva entrada, mesa ni embarcación y no genera cobro.", "Команда получает запрос. Он не бронирует билет, стол или лодку и не создаёт оплату.", "团队会收到此请求。它不预订门票、座位或船只，也不产生收费。", "担当が依頼を受け取ります。切符、席、船は予約されず、料金も発生しません。"],
            ["VisitRequestName"] = ["Nome", "Name", "Nom", "Nombre", "Имя", "姓名", "氏名"],
            ["VisitRequestEmail"] = ["E-mail", "Email", "E-mail", "Correo", "Эл. почта", "电子邮箱", "メール"],
            ["VisitRequestPhone"] = ["Telefone", "Phone", "Téléphone", "Teléfono", "Телефон", "电话", "電話"],
            ["VisitRequestParty"] = ["Pessoas", "People", "Personnes", "Personas", "Человек", "人数", "人数"],
            ["VisitRequestDate"] = ["Data preferida", "Preferred date", "Date souhaitée", "Fecha preferida", "Желаемая дата", "希望日期", "希望日"],
            ["VisitRequestMessage"] = ["Recado", "Note", "Message", "Recado", "Сообщение", "留言", "伝言"],
            ["VisitRequestConsent"] = ["Li a política de privacidade e autorizo o uso destes dados só para responder este pedido.", "I have read the privacy policy and allow these details to be used only to answer this request.", "J'ai lu la politique de confidentialité et j'autorise l'usage de ces données seulement pour répondre à cette demande.", "Leí la política de privacidad y autorizo el uso de estos datos solo para responder este pedido.", "Я прочитал политику конфиденциальности и разрешаю использовать эти данные только для ответа на этот запрос.", "我已阅读隐私政策，并同意这些资料仅用于回复此请求。", "プライバシー方針を読み、この依頼への返信にだけ使うことに同意します。"],
            ["VisitRequestSubmit"] = ["Enviar pedido", "Send request", "Envoyer la demande", "Enviar pedido", "Отправить запрос", "发送请求", "依頼を送る"],
            ["VisitRequestReceived"] = ["Pedido registrado para a equipe. Não há reserva nem cobrança.", "Request recorded for the team. There is no reservation and no charge.", "Demande enregistrée pour l'équipe. Il n'y a ni réservation ni paiement.", "Pedido registrado para el equipo. No hay reserva ni cobro.", "Запрос записан для команды. Брони и оплаты нет.", "请求已记录给团队。没有预订，也没有收费。", "依頼を担当に記録しました。予約も料金もありません。"],
            ["VisitRequestInvalid"] = ["Confira nome, e-mail, número de pessoas, data e o consentimento. A data fica entre hoje e 180 dias.", "Check the name, email, party size, date and consent. The date must be between today and 180 days ahead.", "Vérifiez le nom, l'e-mail, le nombre de personnes, la date et le consentement. La date se situe entre aujourd'hui et 180 jours.", "Revise nombre, correo, número de personas, fecha y consentimiento. La fecha queda entre hoy y 180 días.", "Проверьте имя, почту, число людей, дату и согласие. Дата — от сегодня до 180 дней.", "请检查姓名、邮箱、人数、日期和同意项。日期须在今天起 180 天内。", "氏名、メール、人数、日付、同意を確認してください。日付は本日から180日以内です。"],
            ["AdminVisits"] = ["Pedidos de visita", "Visit requests", "Demandes de visite", "Pedidos de visita", "Запросы на визит", "到访请求", "訪問の依頼"],
            ["VisitRequestHandled"] = ["Pedido marcado como tratado.", "Request marked as handled.", "Demande marquée comme traitée.", "Pedido marcado como atendido.", "Запрос отмечен как обработанный.", "请求已标为已处理。", "依頼を対応済みにしました。"],
            ["VisitRequestEmpty"] = ["Nenhum pedido em aberto.", "No open requests.", "Aucune demande en cours.", "Ningún pedido abierto.", "Нет открытых запросов.", "没有未处理的请求。", "未対応の依頼はありません。"],
            ["VisitRequestMark"] = ["Marcar como tratado", "Mark as handled", "Marquer comme traité", "Marcar como atendido", "Отметить обработанным", "标为已处理", "対応済みにする"]
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
