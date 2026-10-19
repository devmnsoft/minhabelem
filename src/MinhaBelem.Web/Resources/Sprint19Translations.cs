namespace MinhaBelem.Web.Resources;

public static class Sprint19Translations
{
    public static void Register(Dictionary<string, Dictionary<string, string>> catalogs)
    {
        var rows = new Dictionary<string, string[]>
        {
            ["A11y_Panel_Title"] = ["Preferências de acessibilidade", "Accessibility preferences", "Préférences d'accessibilité", "Preferencias de accesibilidad", "Настройки доступности", "无障碍偏好设置", "アクセシビリティ設定"],
            ["A11y_Open_Panel"] = ["Acessibilidade", "Accessibility", "Accessibilité", "Accesibilidad", "Доступность", "无障碍", "アクセシビリティ"],
            ["A11y_Close_Panel"] = ["Fechar painel de acessibilidade", "Close accessibility panel", "Fermer le panneau d'accessibilité", "Cerrar panel de accesibilidad", "Закрыть панель доступности", "关闭无障碍面板", "アクセシビリティパネルを閉じる"],
            ["A11y_Font_Size"] = ["Tamanho da fonte", "Font size", "Taille de police", "Tamaño de fuente", "Размер шрифта", "字号大小", "文字サイズ"],
            ["A11y_Font_Normal"] = ["Padrão (100%)", "Default (100%)", "Par défaut (100%)", "Predeterminado (100%)", "По умолчанию (100%)", "默认 (100%)", "標準 (100%)"],
            ["A11y_Font_Medium"] = ["Média (+25%)", "Medium (+25%)", "Moyenne (+25%)", "Mediana (+25%)", "Средний (+25%)", "中 (+25%)", "中 (+25%)"],
            ["A11y_Font_Large"] = ["Grande (+50%)", "Large (+50%)", "Grande (+50%)", "Grande (+50%)", "Крупный (+50%)", "大 (+50%)", "大 (+50%)"],
            ["A11y_Spacing"] = ["Espaçamento de leitura", "Reading spacing", "Espacement de lecture", "Espaciado de lectura", "Интервал чтения", "阅读间距", "読書行間"],
            ["A11y_Spacing_Normal"] = ["Normal", "Normal", "Normal", "Normal", "Обычный", "正常", "標準"],
            ["A11y_Spacing_Wide"] = ["Ampliado", "Expanded", "Élargi", "Ampliado", "Увеличенный", "较宽", "広め"],
            ["A11y_High_Contrast"] = ["Alto contraste (WCAG AAA)", "High contrast (WCAG AAA)", "Contraste élevé (WCAG AAA)", "Alto contraste (WCAG AAA)", "Высокая контрастность (WCAG AAA)", "高对比度 (WCAG AAA)", "高コントラスト (WCAG AAA)"],
            ["A11y_Reduced_Motion"] = ["Reduzir animações e transições", "Reduce motion and transitions", "Réduire animations et transitions", "Reducir animaciones y transiciones", "Уменьшить анимацию и переходы", "减少动画与过渡", "視覚効果とアニメーションを抑制"],
            ["A11y_Focus_Highlight"] = ["Destaque de foco visual (3px)", "Enhanced focus indicator (3px)", "Indicateur de focus renforcé (3px)", "Indicador de foco resaltado (3px)", "Усиленная подсветка фокуса (3px)", "强化焦点高亮 (3px)", "高視認性フォーカス表示 (3px)"],
            ["A11y_Reset"] = ["Restaurar padrões", "Reset to default", "Rétablir les valeurs par défaut", "Restablecer valores", "Сбросить настройки", "恢复默认", "初期状態に戻す"],
            ["Password_Show"] = ["Mostrar senha", "Show password", "Afficher le mot de passe", "Mostrar contraseña", "Показать пароль", "显示密码", "パスワードを表示"],
            ["Password_Hide"] = ["Ocultar senha", "Hide password", "Masquer le mot de passe", "Ocultar contraseña", "Скрыть пароль", "隐藏密码", "パスワードを非表示"],
            ["CapsLock_Warning"] = ["Atenção: tecla Caps Lock ativada.", "Warning: Caps Lock is on.", "Attention : la touche Verr Maj est activée.", "Atención: Bloq Mayús está activado.", "Внимание: включён Caps Lock.", "注意：大写锁定已开启。", "注意：Caps Lockが有効です。"],
            ["Upcoming_Belém_Events"] = ["Próximos eventos em Belém", "Upcoming events in Belém", "Prochains événements à Belém", "Próximos eventos en Belém", "Ближайшие события в Белене", "贝伦近期活动", "ベレンの今後のイベント"],
            ["Upcoming_Belém_Events_Subtitle"] = ["Programação cultural, festivais, espetáculos e celebrações confirmadas.", "Confirmed cultural events, festivals, shows and celebrations.", "Programme culturel, festivals, spectacles et célébrations confirmés.", "Programación cultural, festivales, espectáculos y celebraciones confirmadas.", "Подтверждённая культурная программа, фестивали и спектакли.", "已确认的文化演出、节庆与盛典活动。", "文化プログラム、音楽祭、舞台公演、祭典情報。"],
            ["Event_Confirmed_Free"] = ["Entrada gratuita confirmada", "Confirmed free admission", "Entrée gratuite confirmée", "Entrada gratuita confirmada", "Подтверждённый бесплатный вход", "已确认免费入场", "入場無料（確認済み）"],
            ["Event_Paid"] = ["Ingresso ou valor informado", "Tickets or admission fee", "Billetterie ou tarif indiqué", "Entrada o tarifa indicada", "Входной билет или тариф", "需购票或已注明费用", "有料・要チケット"],
            ["Event_Check_Price"] = ["Consultar valores na fonte oficial", "Check prices at official source", "Consulter les tarifs à la source", "Consultar tarifas en la fuente oficial", "Уточняйте цены у организатора", "请在官方渠道确认票价", "公式情報で料金を確認"],
            ["Event_Details_Link"] = ["Ver detalhes do evento", "View event details", "Voir les détails de l'événement", "Ver detalles del evento", "Подробнее о событии", "查看活动详情", "イベントの詳細を見る"],
            ["Event_Official_Source"] = ["Fonte oficial do evento", "Official event source", "Source officielle de l'événement", "Fuente oficial del evento", "Официальный источник события", "活动官方发布渠道", "イベントの公式情報元"],
            ["Event_Add_Calendar"] = ["Salvar no meu calendário (.ics)", "Add to my calendar (.ics)", "Ajouter à mon calendrier (.ics)", "Añadir a mi calendario (.ics)", "Добавить в календарь (.ics)", "添加到我的日历 (.ics)", "カレンダーに追加 (.ics)"],
            ["A11y_Map_Alternative"] = ["Alternativa textual ao mapa", "Text alternative for map", "Alternative textuelle au plan", "Alternativa textual al mapa", "Текстовое описание маршрута", "地图的文字替代说明", "地図のテキスト代替情報"],
            ["A11y_Route_Steps"] = ["Etapas do trajeto", "Route steps", "Étapes de l'itinéraire", "Etapas del trayecto", "Этапы маршрута", "行程步骤", "ルートの手順"],
            ["A11y_Boarding_Points"] = ["Pontos de embarque", "Boarding points", "Points d'embarquement", "Puntos de embarque", "Пункты посадки", "乘车乘船点", "乗降場所"],
            ["A11y_Schedule_Tariffs"] = ["Horários e tarifas", "Schedules and fares", "Horaires et tarifs", "Horarios y tarifas", "Расписание и тарифы", "时刻表与票价", "運行時刻と料金"],
            ["Radar_Service_Status"] = ["Situação do serviço do Windows", "Windows service status", "État du service Windows", "Estado del servicio Windows", "Состояние службы Windows", "Windows服务状态", "Windowsサービスの状態"],
            ["Radar_Real_Status_Active"] = ["Executando em tempo real", "Running in real time", "En cours d'exécution en temps réel", "Ejecutándose en tiempo real", "Работает в реальном времени", "实时运行中", "リアルタイム稼働中"],
            ["Radar_Real_Status_Inactive"] = ["Serviço parado ou não verificado", "Service stopped or unverified", "Service arrêté ou non vérifié", "Servicio detenido o no verificado", "Служба остановлена или не подтверждена", "服务已停止或未验证", "サービス停止中または未確認"],
            ["Copy_Success"] = ["Copiado para a área de transferência com sucesso.", "Copied to clipboard successfully.", "Copié dans le presse-papiers avec succès.", "Copiado al portapapeles con éxito.", "Успешно скопировано в буфер обмена.", "已成功复制到剪贴板。", "クリップボードに正常にコピーされました。"],
            ["Copy_Secret"] = ["Copiar chave secreta", "Copy secret key", "Copier la clé secrète", "Copiar clave secreta", "Скопировать секретный ключ", "复制密钥", "シークレットキーをコピー"],
            ["Copy_Recovery_Codes"] = ["Copiar todos os códigos de recuperação", "Copy all recovery codes", "Copier tous les codes de récupération", "Copiar todos los códigos de recuperación", "Скопировать все коды восстановления", "复制所有备用恢复代码", "すべてのリカバリーコードをコピー"]
        };

        var cultures = new[] { "pt-BR", "en", "fr", "es", "ru", "zh-Hans", "ja" };
        foreach (var (key, translations) in rows)
        {
            for (var i = 0; i < cultures.Length; i++)
            {
                var culture = cultures[i];
                if (!catalogs.ContainsKey(culture)) catalogs[culture] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                catalogs[culture][key] = translations[i];
            }
        }
    }
}
