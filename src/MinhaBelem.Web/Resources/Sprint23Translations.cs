namespace MinhaBelem.Web.Resources;

/// <summary>
/// Sprint 23 — Evolução: vídeos, galeria de fotos, tema escuro e IA flutuante.
/// </summary>
public static class Sprint23Translations
{
    public static void Register(Dictionary<string, Dictionary<string, string>> catalogs)
    {
        var rows = new Dictionary<string, string[]>
        {
            ["VideoGalleryTitle"] = [
                "Belém em vídeo",
                "Belém on video",
                "Belém en vidéo",
                "Belém en vídeo",
                "Белен в видео",
                "贝伦视频",
                "ベレンの動画"
            ],
            ["VideoGalleryIntro"] = [
                "Registros em vídeo de eventos, rotas e Boatens. Todos os vídeos exibem a fonte original e o crédito de quem publicou.",
                "Video records of events, routes and Boatens. Every video shows the original source and the publisher credit.",
                "Archives vidéo des événements, itinéraires et Boatens. Chaque vidéo affiche la source d'origine et le crédit de publication.",
                "Archivo en vídeo de eventos, rutas y Boatens. Cada vídeo muestra la fuente original y el crédito de publicación.",
                "Видеоархив событий, маршрутов и боатов. У каждого видео указаны первоисточник и автор публикации.",
                "活动、路线与 Boatens 的视频记录。每个视频均标注原始来源和发布者署名。",
                "イベント・ルート・Boatens の動画アーカイブ。各動画には元情報と公開クレジットが記載されています。"
            ],
            ["ViewAllVideos"] = [
                "Ver todos os vídeos",
                "See all videos",
                "Voir toutes les vidéos",
                "Ver todos los vídeos",
                "Смотреть все видео",
                "查看全部视频",
                "すべての動画を見る"
            ],
            ["PhotoGalleryTitle"] = [
                "Acervo fotográfico",
                "Photo archive",
                "Archives photo",
                "Archivo fotográfico",
                "Фотоархив",
                "照片档案",
                "写真アーカイブ"
            ],
            ["PhotoGalleryIntro"] = [
                "Imagens de Belém com crédito e licença verificados. Clique para ampliar.",
                "Images of Belém with verified credit and licence. Click to enlarge.",
                "Images de Belém avec crédit et licence vérifiés. Cliquez pour agrandir.",
                "Imágenes de Belém con crédito y licencia verificados. Haz clic para ampliar.",
                "Фотографии Белена с проверенным авторством и лицензией. Нажмите, чтобы увеличить.",
                "经过核实的授权与署名图片。点击可放大。",
                "Lizenzとクレジットを確認済みのベレンの写真。クリックで拡大できます。"
            ],
            ["ViewPhotoArchive"] = [
                "Ver acervo completo",
                "See the full archive",
                "Voir l'archive complète",
                "Ver archivo completo",
                "Смотреть весь архив",
                "查看完整档案",
                "全アーカイブを見る"
            ],
            ["UploadPhotoTitle"] = [
                "Envie sua foto de Belém",
                "Send your photo of Belém",
                "Envoyez votre photo de Belém",
                "Envía tu foto de Belém",
                "Пришлите своё фото Белена",
                "上传你的贝伦照片",
                "ベレンの写真を投稿する"
            ],
            ["UploadPhotoDesc"] = [
                "Arraste ou clique para escolher imagens. Toda foto passa por revisão editorial e recebe crédito e licença antes de ser publicada.",
                "Drag or click to choose images. Every photo goes through editorial review and receives credit and licence before publication.",
                "Glissez ou cliquez pour choisir des images. Chaque photo passe par une relecture éditoriale et reçoit crédit et licence avant publication.",
                "Arrastra o haz clic para elegir imágenes. Cada foto pasa por revisión editorial y recibe crédito y licencia antes de publicarse.",
                "Перетащите или нажмите, чтобы выбрать изображения. Каждая фотография проходит редакционную проверку и получает авторство и лицензию перед публикацией.",
                "拖拽或点击选择图片。每张照片都会经过编辑审核，并在发布前标注署名与授权。",
                "ドラッグまたはクリックして画像を選択してください。すべての写真は編集レビューを経て、公開前にクレジットとライセンスが明記されます。"
            ],
            ["AIAssistantTitle"] = [
                "Curió IA",
                "Curió AI",
                "Curió IA",
                "Curió IA",
                "Curió ИИ",
                "Curió 人工智能",
                "Curió AI"
            ],
            ["AIAssistantHint"] = [
                "Pergunte sobre lugares, agenda, linhas de ônibus e cultura de Belém.",
                "Ask about places, agenda, bus lines and culture in Belém.",
                "Posez vos questions sur les lieux, l'agenda, les lignes de bus et la culture de Belém.",
                "Pregunta sobre lugares, agenda, líneas de autobús y cultura de Belém.",
                "Спросите о местах, афише, автобусных линиях и культуре Белена.",
                "询问贝伦的地点、日程、公交线路与文化。",
                "ベレンの場所・日程・バス路線・文化について質問できます。"
            ],
            ["ThemeToggle"] = [
                "Alternar tema claro e escuro",
                "Toggle light and dark theme",
                "Basculer le thème clair et sombre",
                "Alternar tema claro y oscuro",
                "Переключить светлую и тёмную тему",
                "切换明暗主题",
                "ライト／ダークテーマ切り替え"
            ],
            ["VideoPlay"] = [
                "Reproduzir vídeo",
                "Play video",
                "Lire la vidéo",
                "Reproducir vídeo",
                "Воспроизвести видео",
                "播放视频",
                "動画を再生"
            ],
            ["MediaGalleryTitle"] = [
                "Fotos e vídeos",
                "Photos and videos",
                "Photos et vidéos",
                "Fotos y vídeos",
                "Фото и видео",
                "照片与视频",
                "写真と動画"
            ]
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
