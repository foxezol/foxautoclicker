using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;

namespace FoxAutoClicker;

/// <summary>Arayüz metinleri (Türkçe / İngilizce). Dil değişince bağlı tüm metinler yenilenir.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Lang { get; private set; } = "tr";

    public static void Set(string lang)
    {
        Instance.Lang = lang == "en" ? "en" : "tr";
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item[]"));
    }

    public string this[string key] => Get(key);

    public static string T(string key) => Instance.Get(key);
    public static string F(string key, params object[] args) => string.Format(Instance.Get(key), args);

    string Get(string key)
    {
        if (!Strings.TryGetValue(key, out var v)) return key;
        return Lang == "en" ? v.En : v.Tr;
    }

    static readonly Dictionary<string, (string Tr, string En)> Strings = new()
    {
        // sekmeler ve üst çubuk
        ["tab_mouse"] = ("Fare", "Mouse"),
        ["tab_keyboard"] = ("Klavye", "Keyboard"),
        ["tab_profiles"] = ("Profiller", "Profiles"),
        ["tab_settings"] = ("Ayarlar", "Settings"),
        ["mini_tip"] = ("Mini pencere", "Mini window"),
        ["tagline"] = ("Fare ve klavye otomasyonu", "Mouse and keyboard automation"),

        // etkinleştirme
        ["switch_on"] = ("Açık", "On"),
        ["switch_off"] = ("Kapalı", "Off"),
        ["mouse_title"] = ("Fare", "Mouse"),
        ["key_title"] = ("Klavye", "Keyboard"),
        ["mouse_off_note"] = ("Fare kapalı. Ayarları düzenlemek için sağ üstteki anahtarı açın.", "Mouse is off. Turn on the switch at the top right to edit its settings."),
        ["key_off_note"] = ("Klavye kapalı. Ayarları düzenlemek için sağ üstteki anahtarı açın.", "Keyboard is off. Turn on the switch at the top right to edit its settings."),

        // fare
        ["btn_label"] = ("Düğme", "Button"),
        ["seg_left"] = ("Sol", "Left"),
        ["seg_middle"] = ("Orta", "Middle"),
        ["seg_right"] = ("Sağ", "Right"),
        ["type_label"] = ("Tür", "Type"),
        ["seg_single"] = ("Tek tık", "Single click"),
        ["seg_double"] = ("Çift tık", "Double click"),
        ["seg_hold"] = ("Basılı tut", "Hold"),
        ["mouse_hold_note"] = ("Başlatınca düğme basılı tutulur, durdurunca bırakılır. Aralık ve tekrar kullanılmaz.", "The button is held down when you start and released when you stop. Interval and repeat are not used."),

        // aralık
        ["interval"] = ("Aralık", "Interval"),
        ["preset_fast"] = ("Hızlı", "Fast"),
        ["preset_normal"] = ("Normal", "Normal"),
        ["preset_slow"] = ("Yavaş", "Slow"),
        ["preset_custom"] = ("Özel", "Custom"),
        ["u_hours"] = ("saat", "hours"),
        ["u_minutes"] = ("dakika", "minutes"),
        ["u_seconds"] = ("saniye", "seconds"),
        ["u_ms"] = ("milisaniye", "milliseconds"),
        ["s_h"] = ("sa", "h"),
        ["s_m"] = ("dk", "min"),
        ["s_s"] = ("sn", "s"),
        ["s_ms"] = ("ms", "ms"),
        ["interval_total"] = ("Toplam aralık: {0}", "Total interval: {0}"),
        ["interval_rate"] = (" · yaklaşık {0} eylem/sn", " · about {0} actions/s"),
        ["interval_short"] = (" · çok kısa aralıklar bazı uygulamaları yavaşlatabilir", " · very short intervals can slow some apps down"),
        ["interval_zero"] = ("Aralık 0 olamaz; en az 1 ms kullanılır.", "Interval can't be 0; at least 1 ms is used."),
        ["jitter"] = ("Rastgele sapma", "Random jitter"),
        ["jitter_note"] = ("± ms ekler; 0 kapalı demektir.", "Adds ± ms; 0 means off."),
        ["repeat"] = ("Tekrar", "Repeat"),
        ["repeat_inf"] = ("Durdurulana kadar", "Until stopped"),
        ["repeat_count"] = ("Şu kadar", "Exactly"),
        ["unit_click"] = ("tıklama", "clicks"),
        ["unit_round"] = ("tur", "rounds"),

        // konum
        ["position"] = ("Konum", "Position"),
        ["pos_cursor"] = ("İmlecin olduğu yer", "Where the cursor is"),
        ["pos_fixed"] = ("Sabit konum", "Fixed position"),
        ["pick"] = ("Konum seç", "Pick position"),
        ["pick_note"] = ("«Konum seç»e basın; ekranda kılavuz çizgileri açılır. Fareyi hedefe götürüp tıklayın, konum kaydedilir. Esc iptal eder.", "Press “Pick position”; guide lines appear on screen. Move the mouse to the target and click to save the position. Esc cancels."),
        ["pick_banner"] = ("Hedefe götürüp tıklayın · Esc iptal", "Move to the target and click · Esc cancels"),

        // klavye
        ["keys"] = ("Tuşlar", "Keys"),
        ["keys_hint"] = ("Tuşları tek tek ekleyin ya da kayıt başlatıp sırayla basın.", "Add keys one by one, or start a recording and press them in order."),
        ["keys_empty"] = ("Henüz tuş yok. «Tuş ekle»ye basıp bir tuşa basın ya da «Tuş kaydını başlat»ı kullanın.", "No keys yet. Press “Add key” and then a key, or use “Start key recording”."),
        ["add_key"] = ("Tuş ekle", "Add key"),
        ["add_key_prompt"] = ("Bir tuşa basın… (Esc: vazgeç)", "Press a key… (Esc: cancel)"),
        ["undo_key"] = ("Son tuşu geri al", "Undo last key"),
        ["clear"] = ("Temizle", "Clear"),
        ["record_start"] = ("Tuş kaydını başlat", "Start key recording"),
        ["record_stop"] = ("Kaydı bitir", "Stop recording"),
        ["recording"] = ("Kayıt yapılıyor", "Recording"),
        ["recorded_n"] = ("{0} tuş kaydedildi", "{0} keys recorded"),
        ["record_hint"] = ("Tuşlara sırayla basın. Esc ya da «Kaydı bitir» kaydı bitirir. Kısayol tuşu ({0}) kaydedilmez.", "Press the keys in order. Esc or “Stop recording” ends the recording. The hotkey ({0}) is not recorded."),
        ["key_mode"] = ("Biçim", "Mode"),
        ["km_press"] = ("Bas ve bırak", "Press and release"),
        ["km_hold"] = ("Basılı tut", "Hold"),
        ["flow_press"] = ("Her turda sırayla: {0}. Tur bitince {1} bekler ve baştan başlar.", "Each round, in order: {0}. Then it waits {1} and starts over."),
        ["flow_hold"] = ("Şu tuşlar birlikte basılı tutulur: {0}. Durdurunca bırakılır.", "These keys are held down together: {0}. They are released when you stop."),
        ["flow_empty"] = ("Dizi boş.", "The sequence is empty."),
        ["key_left"] = ("Sola taşı", "Move left"),
        ["key_right"] = ("Sağa taşı", "Move right"),
        ["key_remove"] = ("Sil", "Remove"),

        // profiller
        ["profiles_title"] = ("Profiller", "Profiles"),
        ["profiles_desc"] = ("Fare ve klavye ayarlarını birlikte kaydedin, tek tıkla yükleyin.", "Save mouse and keyboard settings together and load them in one click."),
        ["profile_confirm"] = ("Silinsin mi?", "Delete?"),
        ["profile_ok"] = ("Kaydet", "Save"),
        ["profile_cancel"] = ("Vazgeç", "Cancel"),
        ["mini_minimize"] = ("Alta al", "Minimize"),
        ["profile_name"] = ("Yeni profil adı (en çok 16 karakter)", "New profile name (up to 16 characters)"),
        ["profile_save_new"] = ("Yeni olarak kaydet", "Save as new"),
        ["profile_update"] = ("Seçileni güncelle", "Update selected"),
        ["profile_load"] = ("Yükle", "Load"),
        ["profile_rename"] = ("Ad değiştir", "Rename"),
        ["profile_delete"] = ("Sil", "Delete"),
        ["profile_delete_confirm"] = ("Silmek için tekrar tıklayın", "Click again to delete"),
        ["profiles_empty"] = ("Henüz profil yok. Ayarları yapın, bir ad yazın ve «Yeni olarak kaydet»e basın.", "No profiles yet. Set things up, type a name and press “Save as new”."),
        ["profile_loaded"] = ("«{0}» yüklendi.", "“{0}” loaded."),
        ["profile_saved"] = ("«{0}» kaydedildi.", "“{0}” saved."),
        ["profile_renamed"] = ("Ad «{0}» olarak değişti.", "Renamed to “{0}”."),
        ["profile_deleted"] = ("«{0}» silindi.", "“{0}” deleted."),
        ["profile_need_name"] = ("Önce bir profil adı yazın.", "Type a profile name first."),
        ["profile_exists"] = ("Bu adda bir profil zaten var.", "A profile with this name already exists."),
        ["profile_select"] = ("Önce listeden bir profil seçin.", "Select a profile from the list first."),

        // ayarlar
        ["appearance"] = ("Görünüm", "Appearance"),
        ["theme_dark"] = ("Koyu", "Dark"),
        ["theme_light"] = ("Açık", "Light"),
        ["accent"] = ("Vurgu rengi", "Accent color"),
        ["apply"] = ("Uygula", "Apply"),
        ["hex_note"] = ("Kendi rengini #RRGGBB olarak yazabilirsiniz. Okunabilirlik için renk otomatik ayarlanır.", "You can type your own color as #RRGGBB. It is adjusted automatically for readability."),
        ["hex_invalid"] = ("Geçersiz renk. #RRGGBB biçiminde yazın (örneğin #2F7DF6).", "Invalid color. Use the #RRGGBB format (for example #2F7DF6)."),
        ["language"] = ("Dil", "Language"),
        ["hotkey_title"] = ("Kısayol ve başlatma", "Hotkey and start"),
        ["hotkey_change"] = ("Değiştir", "Change"),
        ["hotkey_prompt"] = ("Yeni kısayola basın… (Esc: vazgeç)", "Press the new hotkey… (Esc: cancel)"),
        ["hotkey_desc"] = ("Başlatır ve durdurur; başka bir uygulamadayken de çalışır.", "Starts and stops; works while another app is in front."),
        ["delay_label"] = ("Düğmeyle başlatınca bekle", "Wait when started with the button"),
        ["delay_note"] = ("İmleci hedefe götürmeniz için geri sayım yapılır. Kısayolla başlatınca beklenmez.", "A countdown gives you time to move the cursor. Starting with the hotkey doesn't wait."),
        ["sec"] = ("sn", "s"),
        ["games_title"] = ("Oyunlarda kullanım", "Use in games"),
        ["games_note"] = ("Anti-cheat yazılımları otomatik girdiyi algılayıp hesabı yasaklayabilir. FoxClicker bunu gizlemez; yalnızca izin verilen yerlerde kullanın.", "Anti-cheat software can detect automated input and ban accounts. FoxClicker doesn't hide it; use it only where it's allowed."),
        ["about"] = ("Hakkında", "About"),
        ["about_desc"] = ("Fare ve klavye için sade bir otomatik tıklayıcı.", "A simple auto clicker for mouse and keyboard."),
        ["about_maker"] = ("Yapımcı: foxezol", "Made by foxezol"),
        ["about_claude"] = ("Claude Code ile geliştirildi", "Built with Claude Code"),
        ["about_github"] = ("GitHub", "GitHub"),
        ["about_share"] = ("Paylaş", "Share"),
        ["about_copied"] = ("Bağlantı panoya kopyalandı.", "Link copied to clipboard."),
        ["about_close"] = ("Kapat", "Close"),
        ["share_text"] = ("FoxClicker: fare ve klavye için sade, ücretsiz otomatik tıklayıcı", "FoxClicker: a simple, free auto clicker for mouse and keyboard"),
        ["sounds_title"] = ("Sesler", "Sounds"),
        ["sound_label"] = ("Başlat ve durdur sesi", "Start and stop sound"),
        ["sound_note"] = ("Tıklayıcı başlayınca yükselen, durunca alçalan kısa bir ses çalar; çalıştığını kulaktan anlarsınız.", "A short rising tone plays when the clicker starts and a falling one when it stops, so you can hear it work."),
        ["loading"] = ("Yükleniyor", "Loading"),
        ["updates_title"] = ("Güncellemeler", "Updates"),
        ["update_auto"] = ("Açılışta güncellemeleri otomatik kontrol et", "Check for updates automatically on startup"),
        ["update_auto_note"] = ("Yeni sürüm yayımlanınca uygulama açılışta haber verir.", "The app tells you at startup when a new version is out."),
        ["update_check"] = ("Şimdi kontrol et", "Check now"),
        ["update_checking"] = ("Kontrol ediliyor…", "Checking…"),
        ["update_uptodate"] = ("Güncelsiniz. Sürüm {0}.", "You're up to date. Version {0}."),
        ["update_available"] = ("Yeni sürüm var: {0}.", "A new version is available: {0}."),
        ["update_error"] = ("Kontrol edilemedi. İnternet bağlantınızı kontrol edin.", "Couldn't check. Check your internet connection."),
        ["update_dev"] = ("Güncelleme, Setup ya da taşınabilir sürümle çalışır; bu çalıştırma bunlardan biri değil.", "Updates work with the Setup or the portable version; this run is neither."),
        ["update_title"] = ("Yeni sürüm hazır", "A new version is ready"),
        ["update_text"] = ("FoxClicker {0} yayımlandı. Şimdi güncelleyebilir ya da daha sonraya bırakabilirsiniz.", "FoxClicker {0} is out. You can update now or leave it for later."),
        ["update_now"] = ("Şimdi güncelle", "Update now"),
        ["update_later"] = ("Daha sonra", "Later"),
        ["update_downloading"] = ("İndiriliyor… %{0}", "Downloading… {0}%"),
        ["update_restarting"] = ("Yeniden başlatılıyor…", "Restarting…"),
        ["update_failed"] = ("Güncelleme indirilemedi. Daha sonra yeniden deneyin.", "The update couldn't be downloaded. Try again later."),
        ["version"] = ("Sürüm {0}", "Version {0}"),

        // durumlar
        ["st_ready"] = ("Hazır", "Ready"),
        ["st_countdown"] = ("Geri sayım", "Countdown"),
        ["st_running"] = ("Çalışıyor", "Running"),
        ["st_stopped"] = ("Durduruldu", "Stopped"),
        ["hotkey_line"] = ("Başlat/durdur: {0}", "Start/stop: {0}"),
        ["btn_start"] = ("Başlat", "Start"),
        ["btn_stop"] = ("Durdur", "Stop"),
        ["btn_cancel"] = ("İptal et", "Cancel"),
        ["m_clicks"] = ("Toplam tıklama", "Total clicks"),
        ["m_keys"] = ("Klavye basışı", "Key presses"),
        ["m_time"] = ("Geçen süre", "Elapsed time"),
        ["countdown_hint"] = ("Fareyi hedefe götürün. Başlamak üzere.", "Move the mouse to the target. Starting soon."),
        ["notice_none"] = ("Fare ya da klavye tıklayıcıyı açın.", "Turn on the mouse or keyboard clicker."),
        ["notice_nokeys"] = ("Klavye için en az bir tuş ekleyin.", "Add at least one key for the keyboard clicker."),
        ["notice_recording"] = ("Önce tuş kaydını bitirin.", "Finish the key recording first."),
        ["hotkey_conflict"] = ("{0} başka bir uygulamada kullanılıyor. Ayarlar'dan başka bir kısayol seçin.", "{0} is used by another app. Pick a different hotkey in Settings."),

        // mini pencere
        ["mini_back"] = ("Ana pencere", "Main window"),
        ["mini_mouse"] = ("Fare", "Mouse"),
        ["mini_key"] = ("Klavye", "Keyboard"),
    };
}

/// <summary>XAML'de {local:L anahtar} biçiminde kullanılır.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    public LExtension() { }
    public LExtension(string key) { Key = key; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
