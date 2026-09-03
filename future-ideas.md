# Orbit - Gelecek Fikirleri

Bu doküman `new-features.md`'deki önceki öneri listesinden **sonrası** için hazırlandı. Şu an itibarıyla
v0.2.0 sürümüyle birlikte threshold ve kota sıfırlanma (reset) bildirimleri + Windows sistem sesi efekti,
Top-Center yatay notch / dynamic island modu, multi-monitor desteği, Local REST API, CLI ve Inno Setup kurulumu
tamamlanmış durumdadır. Aşağıdaki fikirler sonraki sürümler için yol haritasını kapsamaktadır.
Yine Impact / Effort / Priority (Impact - Effort + 3) formatında puanlanmıştır.

| # | Fikir | Impact | Effort | Priority | Kategori |
|---|-------|:---:|:---:|:---:|---|
| 1 | Kullanım geçmişi + sparkline grafik | 4 | 3 | **4** | Fonksiyonel |
| 2 | Otomatik güncelleme (GitHub Releases) | 4 | 4 | **3** | Altyapı |
| 3 | ChatGPT & Gemini scraper'larını tamamlama/doğrulama | 5 | 3 | **5** | Fonksiyonel |
| 4 | Snooze / Pause tepsi aksiyonları | 3 | 2 | **4** | UX |
| 5 | Açık tema (Light theme) desteği | 3 | 3 | **3** | Görsel |
| 6 | Selector config'i UI'dan yönetme (self-healing scraper) | 4 | 4 | **3** | Altyapı |
| 7 | Ayarları dışa/içe aktarma (backup) | 2 | 1 | **4** | Altyapı |
| 8 | Stream Deck / Rainmeter için hazır şablon paketleri | 3 | 2 | **4** | Ekosistem |
| 9 | Haftalık/aylık kullanım özeti bildirimi | 3 | 2 | **4** | Fonksiyonel |
| 10 | Takım/çoklu hesap desteği (aynı serviste birden fazla profil) | 3 | 4 | **2** | Fonksiyonel |
| 11 | Menü çubuğu / sistem tepsisi ikon renk-kod göstergesi | 4 | 1 | **6** ⭐ | UX |
| 12 | "Şimdi ne kullanmalıyım" öneri motoru | 3 | 3 | **3** | Akıllı özellik |
| 13 | Widget/Overlay modu (oyun içi / her zaman üstte mini bar) | 3 | 3 | **3** | UX |
| 14 | Telemetri olmadan crash/log raporlama paneli | 2 | 2 | **3** | Altyapı |
| 15 | Açık kaynak katkı: plugin/provider SDK'sı | 3 | 4 | **2** | Ekosistem |

---

## 11. Tepsi ikonunda renk kodlu durum göstergesi - Priority 6 ⭐

**Ne:** Sistem tepsisindeki statik Orbit ikonu, en kritik servisin doldurulma yüzdesine göre renk
değiştirsin (yeşil → sarı → kırmızı), notch'u açmadan tek bakışta genel durum görülsün.

**Nasıl:** `TrayIconManager` içinde her `UsageScraperService` güncellemesinde en yüksek yüzdeye sahip
servisin rengine göre küçük bir overlay/badge çizip `NotifyIcon.Icon`'u güncelle (basit `Bitmap` + `Graphics`
ile üretilebilir, ek asset gerekmez). Çok düşük efor, yüksek görünürlük kazancı.

## 3. ChatGPT & Gemini scraper'larını olgunlaştırma - Priority 5

**Ne:** `ChatGptUsageProvider` zaten var ama README hâlâ "Claude, web portalları" diyor; Gemini için
provider yok. Selector kırılmalarına karşı dayanıklılık (fallback selector listesi, otomatik yeniden
deneme, kullanıcıya "selector bozuldu" uyarısı) eklenmeli.

**Nasıl:** `selectors.json`'a Gemini girişleri eklenip `IUsageProvider` implementasyonu yazılır;
`SelectorUsageScraper`'a selector başına birden fazla fallback CSS/XPath denenmesi ve başarısızlıkta
`NotificationService` üzerinden kullanıcıya "Orbit X servisini okuyamıyor, giriş gerekebilir" uyarısı
eklenir.

## 1. Kullanım geçmişi + sparkline - Priority 4

`new-features.md`'deki #2 ile aynı fikir, hâlâ yapılmamış. `%LOCALAPPDATA%\Orbit\history.json` ring-buffer
+ `RadialGauge` altına küçük `Polyline` sparkline. Genişletilmiş panelde son 24 saat / 7 gün trendi.

## 4. Snooze / Pause tepsi aksiyonları - Priority 4

Tepsi sağ tık menüsüne "1 saat ertele" ve "Otomatik yenilemeyi duraklat" eklenir; `UsageScraperService`
timer state'i kontrol edilir. Toplantı/sunum sırasında bildirim gürültüsünü keser.

## 9. Haftalık/aylık kullanım özeti bildirimi - Priority 4

Pazartesi sabahı ya da ay başında "Geçen hafta Claude'u %X, ChatGPT'yi %Y kullandın" şeklinde tek seferlik
özet toast'ı. `history.json` verisi zaten #1 için toplanacaksa neredeyse maliyetsiz bir ek özellik.

## 7. Ayarları dışa/içe aktarma - Priority 4

`settings.json` + `selectors.json`'ı tek bir `.orbitbackup.json` dosyasına paketleme/geri yükleme. Yeni PC
kurulumunda veya birden fazla makinede senkron kullanım için düşük efor, yüksek pratiklik.

## 8. Stream Deck / Rainmeter için hazır şablon paketleri - Priority 4

README zaten manuel kurulum adımlarını anlatıyor; bunun yerine `.streamDeckPlugin` ve hazır `.rmskin`
Rainmeter cilt dosyası paylaşmak, entegrasyonu "kopyala-yapıştır"tan "indir-çalıştır"a indirger. Ekosistem
büyümesi için düşük efor.

## 2. Otomatik güncelleme mekanizması - Priority 3

Uygulama açılışında GitHub Releases API'sini kontrol edip yeni sürüm varsa tepsi bildirimi + "İndir ve
Kur" akışı. Squirrel.Windows veya basit "indir + Inno Setup sessiz kurulumu tetikle" yaklaşımı yeterli.

## 5. Açık tema desteği - Priority 3

`AppSettings`'e `NotchTheme { System, Dark, Light }` eklenip renkler `DynamicResource`'a taşınır
(Dark.xaml / Light.xaml). Şu an tamamen koyu tema sabit.

## 6. Selector config'i UI'dan yönetme - Priority 3

Scraper'lar CSS selector'lara bağımlı ve siteler UI değiştirdikçe kırılabiliyor. Settings penceresine
"Gelişmiş > Selector Düzenleyici" sekmesi eklenip kullanıcı (ya da destek ekibi) uygulamayı yeniden derlemeden
`selectors.json`'ı canlı düzenleyip test edebilsin ("Test Et" butonuyla anlık scrape deneyip sonucu gösterir).

## 12. "Şimdi ne kullanmalıyım" öneri motoru - Priority 3

Birden fazla servis (Claude, ChatGPT, Antigravity/Gemini) aboneliği olan kullanıcılar için: en boş
kotaya sahip servisi öner ("Claude %85 dolu, ChatGPT %20 dolu — şu an ChatGPT kullan"). Notch'ta küçük bir
rozet olarak gösterilebilir.

## 13. Widget/Overlay modu - Priority 3

Notch dışında, her zaman üstte kalan ufak yarı saydam bir çubuk/overlay modu (oyun oynarken veya tam ekran
uygulamalarda da görünür kalması için `WS_EX_LAYERED` + `SetWindowDisplayAffinity` benzeri yaklaşımlar).

## 14. Crash/log raporlama paneli - Priority 3

Tray menüsüne "Tanılama Bilgilerini Aç" — son loglar ve son scrape hatalarını gösteren basit bir pencere;
kullanıcı destek isterken kopyala-yapıştır yapabilsin. Telemetri göndermez, tamamen yerel.

## 10. Çoklu hesap / profil desteği - Priority 2

Aynı serviste (ör. iki farklı Claude hesabı) birden fazla profili aynı anda izleme. WebView2 profil
klasörlerini servis+hesap bazlı ayırmayı gerektirir, orta-yüksek efor.

## 15. Plugin/Provider SDK'sı - Priority 2

`IUsageProvider` arayüzünü dışa açıp topluluk üyelerinin kendi servis provider'larını (ör. Perplexity,
Cursor, Midjourney) DLL plugin olarak ekleyebilmesini sağlamak. Uzun vadeli, açık kaynak büyümesi için.

---

## Hızlı kazanımlar (bu hafta yapılabilir)
- **#11** Tepsi ikonu renk göstergesi — birkaç saatlik iş, en yüksek görünürlük/efor oranı.
- **#7** Ayar yedekleme — mevcut `SettingsService`'e ince bir export/import katmanı eklemek yeterli.
- **#4** Snooze/Pause — `TrayIconManager` ve `UsageScraperService` zaten timer'ı yönetiyor, sadece UI + state.
