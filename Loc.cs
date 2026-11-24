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
        ["btn_label"] = ("Düğmeler (birden fazla seçilebilir)", "Buttons (pick one or more)"),
        ["seg_left"] = ("Sol", "Left"),
        ["seg_middle"] = ("Orta", "Middle"),
        ["seg_right"] = ("Sağ", "Right"),
        ["press_label"] = ("Basma süresi", "Press time"),
        ["press_note"] = ("Her tıklamada fare düğmesinin ne kadar süre basılı kalacağı. Çoğu yerde 20 ms yeterlidir, değiştirmeniz gerekmez. Bir oyun ya da uygulama tıklamaları görmüyorsa 30–50 ms deneyin.", "How long the mouse button stays pressed on each click. 20 ms is fine almost everywhere, so you normally don't need to change it. If a game or app ignores the clicks, try 30–50 ms."),
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
                        ["hex_note"] = ("Çarktan seçin ya da #RRGGBB yazın, sonra Uygula'ya basın. Okunabilirlik için renk otomatik ayarlanır.", "Pick from the wheel or type #RRGGBB, then press Apply. The color is adjusted automatically for readability."),
        ["hex_invalid"] = ("Geçersiz renk. #RRGGBB biçiminde yazın (örneğin #2F7DF6).", "Invalid color. Use the #RRGGBB format (for example #2F7DF6)."),
        ["language"] = ("Dil", "Language"),
        ["hotkey_title"] = ("Kısayol ve başlatma", "Hotkey and start"),
        ["hotkey_change"] = ("Değiştir", "Change"),
                ["hotkey_prompt"] = ("Yeni kısayola basın (klavye ya da fare yan düğmesi)… (Esc: vazgeç)", "Press the new hotkey (keyboard or a mouse side button)… (Esc: cancel)"),
                ["hotkey_desc"] = ("Başlatır ve durdurur; başka bir uygulamadayken de çalışır. Klavye tuşu yerine fare yan düğmesini (XButton1 ya da XButton2) de seçebilirsiniz; seçtiğiniz yan düğmenin normal geri/ileri işlevi çalışmaz.", "Starts and stops; works while another app is in front. Instead of a keyboard key you can pick a mouse side button (XButton1 or XButton2); the normal back/forward action of that button is blocked."),
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
        ["notice_nobutton"] = ("En az bir fare düğmesi seçin.", "Pick at least one mouse button."),
        ["sound_type"] = ("Ses türü", "Sound"),
        ["sound_soft"] = ("Yumuşak", "Soft"),
        ["sound_click"] = ("Klik", "Click"),
        ["sound_digital"] = ("Dijital", "Digital"),
        ["sound_crystal"] = ("Kristal", "Crystal"),
        ["sound_pop"] = ("Pop", "Pop"),
        ["sound_preview"] = ("Önizleme", "Preview"),
        ["sound_volume"] = ("Ses düzeyi", "Volume"),
        ["hotkey_first_title"] = ("Başlat/durdur kısayolunuz", "Your start/stop hotkey"),
        ["hotkey_first_text"] = ("FoxClicker'ı başka bir uygulamadayken de başlatıp durduran tuş budur. Varsayılan F6; isterseniz şimdi değiştirin. Sonradan Ayarlar'dan da değiştirebilirsiniz.", "This key starts and stops FoxClicker even while another app is in front. The default is F6; change it now if you like. You can also change it later in Settings."),
        ["hotkey_first_ok"] = ("Tamam", "Done"),
        ["update_open"] = ("Sürüm sayfasını aç", "Open release page"),
        ["update_text_portable"] = ("FoxClicker {0} yayımlandı. Taşınabilir sürümü sürüm sayfasından indirebilirsiniz.", "FoxClicker {0} is out. You can download the portable version from the release page."),
        ["tour_back"] = ("Geri", "Back"),
        ["tour_next"] = ("İleri", "Next"),
        ["tour_done"] = ("Bitir", "Finish"),
        ["tour_skip"] = ("Turu atla", "Skip tour"),
        ["tour_progress"] = ("{0} / {1}", "{0} / {1}"),
        ["tour_start"] = ("Turu başlat", "Start the tour"),
        ["tour_1_title"] = ("FoxClicker'a hoş geldiniz", "Welcome to FoxClicker"),
        ["tour_1_text"] = ("Ana bölümleri kısa bir turla gösterelim. İstediğiniz an Esc ile çıkabilir, Ayarlar'dan turu yeniden başlatabilirsiniz.", "A quick tour of the main parts. Press Esc to leave at any time; you can restart the tour from Settings."),
        ["tour_2_title"] = ("Fare ve klavye ayrı ayrı", "Mouse and keyboard, separately"),
        ["tour_2_text"] = ("Her sekmenin sağ üstündeki anahtarla o tıklayıcıyı açıp kapatırsınız. İkisi de açıksa birlikte çalışır.", "The switch at the top right of each tab turns that clicker on or off. If both are on, they run together."),
        ["tour_3_title"] = ("Hangi düğme, nasıl tıklansın", "Which button, which click"),
        ["tour_3_text"] = ("Sol, orta ve sağ düğmeden istediğiniz kadarını seçin; birlikte basılır. Tek tık, çift tık ya da basılı tutma da burada.", "Pick any of left, middle and right; they are pressed together. Single click, double click and hold are here too."),
        ["tour_4_title"] = ("Ne sıklıkla", "How often"),
        ["tour_4_text"] = ("Hızlı, Normal ya da Yavaş ön ayarını seçin ya da süreyi milisaniyeye kadar kendiniz yazın. Tekrar sayısı sağda.", "Choose the Fast, Normal or Slow preset, or type the time down to the millisecond. The repeat count is on the right."),
        ["tour_5_title"] = ("Nerede tıklansın", "Where to click"),
        ["tour_5_text"] = ("İmlecin olduğu yerde ya da sabit bir noktada. «Konum seç» ekranı karartır; hedefe tıklayarak seçersiniz.", "Where the cursor is, or at a fixed point. “Pick position” dims the screen; click the target to choose it."),
        ["tour_6_title"] = ("Klavye dizisi", "Keyboard sequence"),
        ["tour_6_text"] = ("Tuşları tek tek ekleyin ya da kaydı başlatıp yazın. Sırayı oklarla değiştirebilir, istediğiniz tuşu silebilirsiniz.", "Add keys one by one, or start a recording and type them. Reorder with the arrows, or remove any key."),
        ["tour_7_title"] = ("Profiller", "Profiles"),
        ["tour_7_text"] = ("Fare ve klavye ayarlarını birlikte bir isimle kaydedin; sonra tek tıkla yükleyin, güncelleyin ya da silin.", "Save mouse and keyboard settings together under a name; later load, update or delete them in one click."),
        ["tour_8_title"] = ("Kısayol ve geri sayım", "Hotkey and countdown"),
        ["tour_8_text"] = ("Başlat/durdur kısayolu başka bir uygulamadayken de çalışır; isterseniz basılı tuttukça çalışacak şekilde de ayarlayabilirsiniz. Düğmeyle başlatırken kaç saniye bekleneceğini de buradan seçersiniz.", "The start/stop hotkey works even while another app is in front; you can also make it run only while you hold it. You also choose here how long to wait when starting from the button."),
        ["tour_9_title"] = ("Sesler", "Sounds"),
        ["tour_9_text"] = ("Başlayınca ve durunca kısa bir ses çalar. Beş türden birini seçin, önizleyin, ses düzeyini ayarlayın.", "A short sound plays when it starts and stops. Pick one of five styles, preview it and set the volume."),
        ["tour_10_title"] = ("Başlat ve sayaçlar", "Start and counters"),
        ["tour_10_text"] = ("Buradan ya da kısayolla başlatın. Solda durumu, ortada tıklama, tuş ve süre sayaçlarını görürsünüz.", "Start here or with the hotkey. The state is on the left; clicks, key presses and time are in the middle."),
        ["tour_11_title"] = ("Mini pencere", "Mini window"),
        ["tour_11_text"] = ("Ana pencereyi gizleyip küçük, her zaman üstte duran bir kontrol penceresi açar.", "Hides the main window and opens a small always-on-top control window."),
        ["tour_12_title"] = ("Hepsi bu kadar", "That's it"),
        ["tour_12_text"] = ("Logoya tıklayınca Hakkında açılır. İyi tıklamalar!", "Click the logo to open About. Happy clicking!"),
        ["maint_title"] = ("Uygulama", "App"),
        ["maint_tour"] = ("Tanıtım turu", "Intro tour"),
        ["maint_tour_note"] = ("Ana bölümleri kısa bir turla yeniden gösterir.", "Shows the main parts again in a short tour."),
        ["maint_reset"] = ("Ayarları sıfırla", "Reset settings"),
        ["maint_reset_note"] = ("Tema, kısayol, fare ve klavye ayarları varsayılana döner. Profilleriniz ve dil korunur.", "Theme, hotkey, mouse and keyboard settings go back to the defaults. Your profiles and language are kept."),
        ["maint_reset_confirm"] = ("Emin misiniz? Tekrar tıklayın", "Sure? Click again"),
        ["maint_reset_done"] = ("Ayarlar sıfırlandı.", "Settings were reset."),
        ["maint_uninstall"] = ("Uygulamayı kaldır", "Uninstall FoxClicker"),
        ["maint_uninstall_note"] = ("Kaldırıcıyı açar. Ayar dosyanız silinmez.", "Opens the uninstaller. Your settings file is not deleted."),
        ["maint_uninstall_confirm"] = ("Kaldırmak için tekrar tıklayın", "Click again to uninstall"),
        ["maint_uninstall_portable"] = ("Bu taşınabilir sürüm; kaldırmak için klasörünü silmeniz yeterli.", "This is the portable version; to remove it, just delete its folder."),
        ["tour_skip_short"] = ("Atla", "Skip"),
        ["tour_skipped_toast"] = ("Turu Ayarlar → Uygulama bölümünden istediğiniz zaman yeniden başlatabilirsiniz.", "You can restart the tour any time from Settings → App."),
        ["accent_bright"] = ("Parlaklık", "Brightness"),
        ["hex_label"] = ("Hex kodu", "Hex code"),
        ["accent_lavender"] = ("Pastel mor", "Pastel purple"),
        ["accent_orange"] = ("Turuncu", "Orange"),
        ["accent_custom"] = ("Özel renk", "Custom color"),
        ["accent_cancel"] = ("Vazgeç", "Cancel"),
        ["about_feedback"] = ("Geri bildirim için Discord", "Send feedback on Discord"),
        ["hotkey_mode"] = ("Kısayol davranışı", "Hotkey behavior"),
        ["hotkey_mode_toggle"] = ("Bas, aç/kapat", "Press to toggle"),
        ["hotkey_mode_hold"] = ("Basılı tuttukça çalışsın", "Run while held"),
        ["hotkey_mode_note"] = ("İkinci seçenekte kısayol tuşunu basılı tuttuğunuz sürece çalışır, bıraktığınızda durur.", "With the second option it runs for as long as you hold the hotkey and stops when you release it."),
        ["win_minimize"] = ("Küçült", "Minimize"),
        ["win_maximize"] = ("Büyüt", "Maximize"),
        ["win_restore"] = ("Önceki boyut", "Restore"),
        ["win_close"] = ("Kapat", "Close"),
        ["loading"] = ("Yükleniyor", "Loading"),
        ["updates_title"] = ("Güncellemeler", "Updates"),
        ["update_auto"] = ("Açılışta güncellemeleri otomatik kontrol et", "Check for updates automatically on startup"),
        ["update_auto_note"] = ("Yeni sürüm yayımlanınca uygulama açılışta haber verir.", "The app tells you at startup when a new version is out."),
        ["update_check"] = ("Şimdi kontrol et", "Check now"),
        ["update_checking"] = ("Kontrol ediliyor…", "Checking…"),
        ["update_uptodate"] = ("Uygulama güncel. Sürüm {0}.", "The app is up to date. Version {0}."),
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
