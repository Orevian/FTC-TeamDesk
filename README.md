<div align="center">

# FTC TeamDesk

**Team management for FIRST Tech Challenge teams — members, applications, budget, an encrypted password vault and an optional AI assistant.**

🇬🇧 [English](#-english) · 🇹🇷 [Türkçe](#-türkçe)

</div>

---

# 🇬🇧 English

## ⚠️ Disclaimer

- **FTC TeamDesk is an independent, community-made, open-source project. It is NOT affiliated with, endorsed by, or sponsored by FIRST® or the FIRST Tech Challenge.** "FIRST" and "FIRST Tech Challenge" are trademarks of their respective owners.
- The software is provided **"AS IS", without warranty of any kind**, under the [MIT License](LICENSE). You use it **at your own risk**. The authors and contributors are not liable for any data loss, damage or other consequences of using it.
- **Back up your data** regularly (*Settings → Data → Create backup*).
- **If you forget the Password Vault master password, your vault cannot be recovered.** This is by design — there is no back door.
- The AI assistant and the application analysis are **decision support only**. AI answers can be wrong; people make the decisions, and the assistant can never change anything without your explicit confirmation.
- If you use the Google Sheets sync or an AI provider, data you choose to send is handled under **that provider's** terms and privacy policy.

## 🛡️ This is NOT a virus

**FTC TeamDesk is not malware, spyware or a miner.** To be explicit:

- **No telemetry, no analytics, no ads, no tracking.** It does not collect anything about you and does not "phone home".
- It connects to the internet **only if you set up** Google Sheets sync or an AI provider yourself, and only to those services.
- Passwords, API keys and tokens are **never** stored in plain text — not in the database, config or logs. They are protected with Windows DPAPI (keys/tokens) or AES-GCM with your master password (vault).
- It never runs hidden processes, never installs services or drivers and never modifies other programs.
- The **entire source code is public** in this repository. You can read it, or build the program yourself with the steps below instead of trusting a download.

**Why might Windows warn me?** Windows SmartScreen and some antivirus programs warn about *new, unsigned* programs from small projects — a "false positive" that disappears as the file becomes more widely used. A code-signing certificate costs money and this project does not have one yet. If you see the warning, choose **More info → Run anyway**. Every release lists a **SHA-256 checksum** so you can verify your download:

```powershell
Get-FileHash .\FTC-TeamDesk-Setup-1.0.0.exe -Algorithm SHA256
```

You can also upload the file to [VirusTotal](https://www.virustotal.com) yourself. If you ever receive a copy of this program from somewhere other than this repository's *Releases* page, do not trust it.

## ✨ Features

- **Dashboard** – 8 KPIs, upcoming tasks, recent activity.
- **Team** – searchable member table, custom statuses, member detail with a per-area development chart (no single "score").
- **Applications** – import from Google Forms via Google Sheets (read-only OAuth), status workflow, AI-assisted analysis.
- **Budget** – categories, purchases, deterministic calculations (the AI never does the math).
- **Analytics** – 8 interactive charts with tooltips.
- **Password Vault** – master password, AES-GCM, auto-lock, generator, masked passwords, clipboard auto-clear, back-off on failed attempts; always starts locked.
- **AI Assistant** – OpenAI, Anthropic, Google Gemini, OpenRouter, Ollama or any OpenAI-compatible server; per-permission toggles; every change needs your confirmation; **vault access is OFF by default**.
- **Activity log**, backup / restore, export / import, light & dark theme, keyboard shortcuts (`Ctrl+1…8`, `Ctrl+N`, `Ctrl+L`, `F5`).
- **Turkish and English** UI, switchable instantly; the Windows language is detected on first run.

## 📦 Installation

**Easiest:** download `FTC-TeamDesk-Setup-x.y.z.exe` from the [**Releases**](../../releases) page and run it. The installer opens in your Windows language (Turkish or English), needs **no administrator rights** by default and bundles everything — you do **not** need to install .NET.

Requirements: Windows 10 (1809) or Windows 11, 64-bit.

Your data lives in `%LOCALAPPDATA%\FTC TeamDesk\` and is kept when you update or uninstall (the uninstaller asks before deleting it).

## 🛠️ Build from source

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```powershell
git clone https://github.com/Orevian/FTC-TeamDesk.git
cd FTC-TeamDesk
dotnet restore
dotnet build -c Release
dotnet test
dotnet run --project src/FTC.TeamDesk -c Release
```

Create the portable folder (like PyInstaller's *onedir*) and the installer:

```powershell
dotnet publish src/FTC.TeamDesk -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=none -o publish
# then compile setup.iss with Inno Setup 6.3+  →  installer\output\FTC-TeamDesk-Setup-1.0.0.exe
```

**App icon:** put your icon at `src/FTC.TeamDesk/Assets/icon.ico` before building; it is used for the exe, window and installer.

## ⚙️ Configuration

### Google Sheets (your own OAuth client)

1. In Google Cloud Console create a project and enable the **Google Sheets API** and **Google Drive API**.
2. Configure the OAuth consent screen (add yourself as a test user).
3. Create an OAuth client of type **Desktop app**.
4. In *Settings → Google Sheets* paste the client ID (and secret, if shown), click **Connect with Google** and pick the spreadsheet linked to your Google Form.

Only read-only scopes are requested.

### AI providers

*Settings → AI*: choose a provider, paste the API key (stored encrypted), load the model list, pick a model. For Ollama use the server address (default `http://localhost:11434`). Then enable only the permissions you want the assistant to have.

## 🏗️ Architecture

Clean Architecture with a small custom MVVM, dependency injection and async/await throughout.

| Project | Responsibility |
|---|---|
| `FTC.TeamDesk.Core` | Entities, enums, interfaces, budget calculations |
| `FTC.TeamDesk.Data` | EF Core + SQLite, migrations, repositories |
| `FTC.TeamDesk.Services` | Application services, Google Sheets sync, backup |
| `FTC.TeamDesk.AI` | Provider clients, tool layer, permissions & confirmations |
| `FTC.TeamDesk.Security` | DPAPI secret store, vault cryptography |
| `FTC.TeamDesk.Localization` | `en.json` / `tr.json`, localization service |
| `FTC.TeamDesk.UI` | WPF views, view models, themes, charts |
| `FTC.TeamDesk` | Executable & composition root |
| `tests/FTC.TeamDesk.Tests` | xUnit tests |

The AI has **no direct database access** — only a fixed set of controlled tools.

## 🗺️ TODO

- [ ] AI answers are not streamed yet.
- [ ] `DatePicker` / `Calendar` pop-ups follow the dark theme only partly (system control limits).
- [ ] Screenshots.
- [ ] Code signing (removes the SmartScreen warning).

## 🤝 Contributing & license

Issues and pull requests are welcome. Released under the [MIT License](LICENSE).

---

# 🇹🇷 Türkçe

## ⚠️ Sorumluluk reddi

- **FTC TeamDesk bağımsız, topluluk tarafından geliştirilen, açık kaynaklı bir projedir. FIRST® veya FIRST Tech Challenge ile BAĞLANTISI YOKTUR; onlar tarafından onaylanmamış veya desteklenmemiştir.** "FIRST" ve "FIRST Tech Challenge" ilgili sahiplerinin ticari markalarıdır.
- Yazılım, [MIT Lisansı](LICENSE) kapsamında **"OLDUĞU GİBİ", hiçbir garanti verilmeksizin** sunulur. Kullanım **riski size aittir**. Geliştiriciler ve katkıda bulunanlar; veri kaybı, zarar veya kullanımdan doğan diğer sonuçlardan sorumlu tutulamaz.
- Verilerinizi düzenli **yedekleyin** (*Ayarlar → Veri → Yedek oluştur*).
- **Şifre Kasası ana şifresini unutursanız kasanız geri getirilemez.** Bu bilinçli bir tasarımdır; arka kapı yoktur.
- Yapay zekâ asistanı ve başvuru analizi yalnızca **karar desteğidir**. Yapay zekâ yanılabilir; kararı insanlar verir ve asistan açık onayınız olmadan hiçbir şeyi değiştiremez.
- Google Sheets eşitlemesini veya bir yapay zekâ sağlayıcısını kullanırsanız, gönderdiğiniz veriler **o sağlayıcının** şartlarına ve gizlilik politikasına tabidir.

## 🛡️ Bu bir VİRÜS DEĞİLDİR

**FTC TeamDesk zararlı yazılım, casus yazılım veya madenci değildir.** Açıkça belirtmek gerekirse:

- **Telemetri, analitik, reklam veya takip YOKTUR.** Sizinle ilgili hiçbir şey toplamaz ve kendiliğinden hiçbir yere bağlanmaz.
- İnternete **yalnızca siz** Google Sheets eşitlemesini veya bir yapay zekâ sağlayıcısını ayarlarsanız ve yalnızca o servislere bağlanır.
- Şifreler, API anahtarları ve jetonlar **asla** düz metin olarak saklanmaz — ne veritabanında, ne ayar dosyasında, ne de günlüklerde. Windows DPAPI (anahtarlar/jetonlar) veya ana şifrenizle AES-GCM (kasa) ile korunur.
- Gizli işlem çalıştırmaz, hizmet veya sürücü kurmaz, başka programları değiştirmez.
- **Kaynak kodun tamamı bu depoda herkese açıktır.** Bir indirmeye güvenmek yerine, aşağıdaki adımlarla programı kendiniz derleyebilirsiniz.

**Windows neden uyarı verebilir?** Windows SmartScreen ve bazı antivirüsler, küçük projelerin *yeni ve imzasız* programları için uyarı verir — dosya yaygınlaştıkça kaybolan bir "yanlış alarm"dır. Kod imzalama sertifikası ücretlidir ve bu projede henüz yoktur. Uyarıyı görürseniz **Ek bilgi → Yine de çalıştır** seçin. Her sürümde indirmenizi doğrulayabilmeniz için bir **SHA-256 sağlama toplamı** yer alır:

```powershell
Get-FileHash .\FTC-TeamDesk-Setup-1.0.0.exe -Algorithm SHA256
```

Dosyayı kendiniz [VirusTotal](https://www.virustotal.com)'a da yükleyebilirsiniz. Bu programın bir kopyasını bu deponun *Releases* sayfası dışında bir yerden alırsanız ona güvenmeyin.

## ✨ Özellikler

- **Pano** – 8 KPI, yaklaşan görevler, son etkinlikler.
- **Takım** – aranabilir üye tablosu, özel durumlar, alan bazlı gelişim grafiğiyle üye detayı (tek bir "puan" yok).
- **Başvurular** – Google Form → Google Sheets içe aktarma (salt okunur OAuth), durum akışı, yapay zekâ destekli analiz.
- **Bütçe** – kategoriler, satın almalar, deterministik hesaplamalar (matematiği yapay zekâ yapmaz).
- **Analitik** – ipucu balonlu 8 etkileşimli grafik.
- **Şifre Kasası** – ana şifre, AES-GCM, otomatik kilit, şifre üretici, maskeli şifreler, panonun otomatik temizlenmesi, hatalı denemelerde bekleme; her zaman kilitli başlar.
- **Yapay Zekâ Asistanı** – OpenAI, Anthropic, Google Gemini, OpenRouter, Ollama veya OpenAI uyumlu herhangi bir sunucu; izin bazlı anahtarlar; her değişiklik onayınızı ister; **kasa erişimi varsayılan olarak KAPALI**.
- **Etkinlik günlüğü**, yedekleme / geri yükleme, dışa / içe aktarma, açık ve koyu tema, klavye kısayolları (`Ctrl+1…8`, `Ctrl+N`, `Ctrl+L`, `F5`).
- **Türkçe ve İngilizce** arayüz, anında değiştirilebilir; ilk açılışta Windows dili algılanır.

## 📦 Kurulum

**En kolayı:** [**Releases**](../../releases) sayfasından `FTC-TeamDesk-Setup-x.y.z.exe` dosyasını indirip çalıştırın. Kurulum sihirbazı Windows dilinizde (Türkçe veya İngilizce) açılır, varsayılan olarak **yönetici hakkı gerektirmez** ve her şeyi içerir — **.NET kurmanız gerekmez**.

Gereksinimler: Windows 10 (1809) veya Windows 11, 64 bit.

Verileriniz `%LOCALAPPDATA%\FTC TeamDesk\` klasöründedir; güncelleme veya kaldırma sırasında korunur (kaldırıcı silmeden önce sorar).

## 🛠️ Kaynaktan derleme

Gereksinim: Windows üzerinde [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/Orevian/FTC-TeamDesk.git
cd FTC-TeamDesk
dotnet restore
dotnet build -c Release
dotnet test
dotnet run --project src/FTC.TeamDesk -c Release
```

Taşınabilir klasörü (PyInstaller'ın *onedir*'i gibi) ve kurulum dosyasını oluşturun:

```powershell
dotnet publish src/FTC.TeamDesk -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=none -o publish
# sonra setup.iss dosyasını Inno Setup 6.3+ ile derleyin  →  installer\output\FTC-TeamDesk-Setup-1.0.0.exe
```

**Uygulama simgesi:** derlemeden önce simgenizi `src/FTC.TeamDesk/Assets/icon.ico` yoluna koyun; exe, pencere ve kurulum dosyası için kullanılır.

## ⚙️ Yapılandırma

### Google Sheets (kendi OAuth istemciniz)

1. Google Cloud Console'da bir proje oluşturup **Google Sheets API** ve **Google Drive API**'yi etkinleştirin.
2. OAuth onay ekranını yapılandırın (kendinizi test kullanıcısı olarak ekleyin).
3. **Masaüstü uygulaması** türünde bir OAuth istemcisi oluşturun.
4. *Ayarlar → Google Sheets* bölümüne istemci kimliğini (gösteriliyorsa gizli anahtarı da) yapıştırın, **Google ile bağlan**'a tıklayın ve Google Form'unuza bağlı elektronik tabloyu seçin.

Yalnızca salt okunur izinler istenir.

### Yapay zekâ sağlayıcıları

*Ayarlar → Yapay Zekâ*: sağlayıcıyı seçin, API anahtarını yapıştırın (şifreli saklanır), model listesini yükleyin ve bir model seçin. Ollama için sunucu adresini kullanın (varsayılan `http://localhost:11434`). Ardından asistanın sahip olmasını istediğiniz izinleri tek tek açın.

## 🏗️ Mimari

Küçük bir özel MVVM, bağımlılık enjeksiyonu ve baştan sona async/await ile Temiz Mimari (Clean Architecture). Proje tablosu için yukarıdaki İngilizce bölüme bakın.

Yapay zekânın **veritabanına doğrudan erişimi yoktur** — yalnızca sabit bir kontrollü araç kümesini kullanır.

## 🗺️ Yapılacaklar

- [ ] Yapay zekâ yanıtları henüz akış (streaming) olarak gelmiyor.
- [ ] `DatePicker` / `Calendar` açılır pencereleri koyu temayı yalnızca kısmen izliyor (sistem denetimi sınırı).
- [ ] Ekran görüntüleri.
- [ ] Kod imzalama (SmartScreen uyarısını kaldırır).

## 🤝 Katkı ve lisans

Hata bildirimleri ve pull request'ler memnuniyetle karşılanır. [MIT Lisansı](LICENSE) ile yayımlanmıştır.
