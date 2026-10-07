<!--
  Paste this text into the GitHub "Release" description for the installer.
  Before publishing: replace the checksum placeholder, optionally add the VirusTotal link, and attach FTC-TeamDesk-Setup-1.0.0.exe.
-->

# FTC TeamDesk 1.0.0 — Installer / Kurulum Dosyası

🇬🇧 [English](#-english) · 🇹🇷 [Türkçe](#-türkçe)

---

# 🇬🇧 English

> ⚠️ **Disclaimer:** FTC TeamDesk is an independent, community-made, open-source project. It is **not** affiliated with, endorsed by or sponsored by FIRST® or the FIRST Tech Challenge. It is provided **"as is", without warranty**, under the MIT License; you use it at your own risk and the authors are not liable for data loss or damage. Back up your data. A forgotten Password Vault master password **cannot** be recovered.

> 🛡️ **This is NOT a virus.** FTC TeamDesk contains no malware, spyware, miner, telemetry, ads or tracking. It never sends anything on its own and connects to the internet **only** if you set up Google Sheets sync or an AI provider yourself. Passwords, API keys and tokens are never stored in plain text. The full source code is public in this repository — read it, or build the installer yourself.

## What is in this release

`FTC-TeamDesk-Setup-1.0.0.exe` — the Windows installer. Everything is bundled: **you do not need to install .NET**.

| | |
|---|---|
| Windows | Windows 10 (1809) or Windows 11, 64-bit |
| Language | Installer and app start in your **Windows language** (Turkish or English); you can switch later in *Settings → General* |
| Admin rights | **Not required** (per-user install by default; you may choose "for all users") |
| Install size | ≈ 150–200 MB (self-contained .NET runtime) |

## Install

1. Download **FTC-TeamDesk-Setup-1.0.0.exe** from *Assets* below.
2. (Recommended) Verify the download — the SHA-256 must match:
   ```powershell
   Get-FileHash .\FTC-TeamDesk-Setup-1.0.0.exe -Algorithm SHA256
   ```
   `SHA-256: <paste checksum here>` · [VirusTotal scan](<paste link here>)
3. Run the installer and follow the steps. Optionally tick *Create a desktop shortcut*.
4. Start **FTC TeamDesk** from the Start menu. The Password Vault always starts locked; you create the master password on first use.

### Windows shows "Windows protected your PC"?

That is **Microsoft Defender SmartScreen** reacting to a *new, unsigned* program from a small project — a false positive, not a detection of malware. The project does not have a paid code-signing certificate yet. Click **More info → Run anyway**. If you prefer not to trust a binary at all, build it from source (see the README).

## Your data

Stored in `%LOCALAPPDATA%\FTC TeamDesk\` (database, settings, encrypted secrets, logs). Updating keeps it. When you uninstall, you are asked whether to delete it (default: keep). Use *Settings → Data → Create backup* regularly.

## Update / uninstall

Run a newer installer over the old version — your data and settings stay. To remove: *Settings → Apps → Installed apps → FTC TeamDesk → Uninstall*.

## What's new in 1.0.0

- First public release: Dashboard, Team, Applications (Google Sheets sync), Budget, Analytics, Password Vault, AI Assistant, Activity log, Backup/Restore, Turkish & English UI, light/dark theme.

## Known limitations

- AI answers are not streamed yet.
- Date-picker pop-ups only partly follow the dark theme.

Found a bug? Please open an [issue](../../issues) and attach the newest file from `%LOCALAPPDATA%\FTC TeamDesk\logs` (it never contains passwords or keys).

---

# 🇹🇷 Türkçe

> ⚠️ **Sorumluluk reddi:** FTC TeamDesk bağımsız, topluluk tarafından geliştirilen, açık kaynaklı bir projedir. FIRST® veya FIRST Tech Challenge ile **bağlantısı yoktur**, onlar tarafından onaylanmamış veya desteklenmemiştir. MIT Lisansı kapsamında **"olduğu gibi", garanti verilmeksizin** sunulur; kullanım riski size aittir, geliştiriciler veri kaybı veya zarardan sorumlu değildir. Verilerinizi yedekleyin. Unutulan Şifre Kasası ana şifresi **geri getirilemez**.

> 🛡️ **Bu bir VİRÜS DEĞİLDİR.** FTC TeamDesk; zararlı yazılım, casus yazılım, madenci, telemetri, reklam veya takip içermez. Kendiliğinden hiçbir şey göndermez ve internete **yalnızca** siz Google Sheets eşitlemesini veya bir yapay zekâ sağlayıcısını ayarlarsanız bağlanır. Şifreler, API anahtarları ve jetonlar asla düz metin saklanmaz. Kaynak kodun tamamı bu depoda herkese açıktır — okuyabilir veya kurulum dosyasını kendiniz derleyebilirsiniz.

## Bu sürümde ne var

`FTC-TeamDesk-Setup-1.0.0.exe` — Windows kurulum dosyası. Her şey içindedir: **.NET kurmanız gerekmez**.

| | |
|---|---|
| Windows | Windows 10 (1809) veya Windows 11, 64 bit |
| Dil | Kurulum ve uygulama **Windows dilinizde** (Türkçe veya İngilizce) açılır; sonra *Ayarlar → Genel*'den değiştirebilirsiniz |
| Yönetici hakkı | **Gerekmez** (varsayılan kullanıcıya özel kurulum; "tüm kullanıcılar için" seçilebilir) |
| Kurulum boyutu | ≈ 150–200 MB (uygulamayla birlikte gelen .NET çalışma zamanı) |

## Kurulum

1. Aşağıdaki *Assets* bölümünden **FTC-TeamDesk-Setup-1.0.0.exe** dosyasını indirin.
2. (Önerilir) İndirmeyi doğrulayın — SHA-256 değeri eşleşmelidir:
   ```powershell
   Get-FileHash .\FTC-TeamDesk-Setup-1.0.0.exe -Algorithm SHA256
   ```
   `SHA-256: <sağlama toplamını buraya yapıştırın>` · [VirusTotal taraması](<bağlantıyı buraya yapıştırın>)
3. Kurulum dosyasını çalıştırıp adımları izleyin. İsterseniz *Masaüstü kısayolu oluştur* seçeneğini işaretleyin.
4. **FTC TeamDesk**'i Başlat menüsünden açın. Şifre Kasası her zaman kilitli başlar; ana şifreyi ilk kullanımda belirlersiniz.

### Windows "Bilgisayarınızı korudu" mu diyor?

Bu, küçük bir projenin *yeni ve imzasız* programına tepki veren **Microsoft Defender SmartScreen**'dir — zararlı yazılım tespiti değil, yanlış alarmdır. Projede henüz ücretli bir kod imzalama sertifikası yoktur. **Ek bilgi → Yine de çalıştır** seçin. Hiçbir ikili dosyaya güvenmek istemiyorsanız kaynaktan derleyin (README'ye bakın).

## Verileriniz

`%LOCALAPPDATA%\FTC TeamDesk\` klasöründe saklanır (veritabanı, ayarlar, şifreli gizli bilgiler, günlükler). Güncelleme bunları korur. Kaldırırken silinip silinmeyeceği sorulur (varsayılan: sakla). *Ayarlar → Veri → Yedek oluştur* ile düzenli yedek alın.

## Güncelleme / kaldırma

Yeni kurulum dosyasını eskisinin üzerine çalıştırın — verileriniz ve ayarlarınız korunur. Kaldırmak için: *Ayarlar → Uygulamalar → Yüklü uygulamalar → FTC TeamDesk → Kaldır*.

## 1.0.0'da yenilikler

- İlk herkese açık sürüm: Pano, Takım, Başvurular (Google Sheets eşitleme), Bütçe, Analitik, Şifre Kasası, Yapay Zekâ Asistanı, Etkinlik günlüğü, Yedekleme/Geri yükleme, Türkçe ve İngilizce arayüz, açık/koyu tema.

## Bilinen sınırlamalar

- Yapay zekâ yanıtları henüz akış (streaming) olarak gelmiyor.
- Tarih seçici açılır pencereleri koyu temayı yalnızca kısmen izliyor.

Hata mı buldunuz? Lütfen bir [issue](../../issues) açın ve `%LOCALAPPDATA%\FTC TeamDesk\logs` içindeki en yeni dosyayı ekleyin (içinde şifre veya anahtar bulunmaz).
