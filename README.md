# Base Modular Monolith - Teknik Dokümantasyon ve Kurulum Kılavuzu

Bu belge, **Base Modular Monolith** projesinin mimari kararlarını, performans optimizasyonlarını, güvenlik standartlarını ve operasyonel süreçlerini detaylandırmaktadır. Proje, saniyede yüksek istek (high throughput) ve düşük gecikme (low latency) hedeflenerek modern .NET 9 standartlarında geliştirilmiştir.

---

## 🏗️ 1. Mimari Yapı (Architecture)

Uygulama, **Modular Monolith** prensiplerini takip ederek dağıtık sistemlerin karmaşıklığına girmeden servis tabanlı bir ayrışma sunar.

### 🧩 Modülerlik
- her modül (örn. `User`) kendi **Domain**, **Application**, **Infrastructure** ve **Api** katmanlarına sahiptir.
- Modüller arası iletişim asenkron (Domain Events) veya BuildingBlocks üzerinden gerçekleştirilir.
- Bağımlılıklar sıkı bir şekilde izole edilmiştir; bir modülün veritabanı şeması diğerinden bağımsızdır.

### 🏹 Tasarım Desenleri
- **CQRS**: Okuma (`UserReadService.cs` - Dapper) ve Yazma (`UserRepository.cs` - EF Core) işlemleri tamamen ayrılmıştır.
- **Domain-Driven Design (DDD)**: İş mantığı anemic modeller yerine zengin Domain modelleri içinde encapsulate edilmiştir.
- **Idempotency**: `IdempotencyBehavior.cs` ile aynı isteğin mükerrer işlenmesi uygulama seviyesinde engellenir.
- **Outbox Pattern**: Veri bütünlüğünü sağlamak için `OutboxMessages` tablosu kullanılır. Domain eventleri, asıl işlemle aynı transaction içinde veritabanına kaydedilir ve `OutboxProcessor` tarafından asenkron olarak işlenir. Bu sayede "Eventual Consistency" (Nihai Tutarlılık) sağlanır.

---

## ⚡ 2. Performans Optimizasyonları (Performance Deep-Dive)

Proje "Performance First" yaklaşımıyla tasarlanmıştır.

### 🛠️ Veri Erişimi (Data Access)
- **Native Dapper Performansı**: Okuma sorguları için Dapper kullanılır. ADO.NET bağlantı havuzu (pooling) doğrudan yönetilir.
- **Keyset Pagination**: Derin sayfalama işlemlerinde `OFFSET/FETCH` yerine `Keyset` (Index-based) yöntemi kullanılarak CPU ve IO yükü minimize edilir.
- **Dirty Reads (`NOLOCK`)**: Okuma işlemlerinde SQL Server seviyesinde kilitlenme (deadlock) riskini azaltmak ve hızı artırmak için `WITH (NOLOCK)` hintleri kullanılır.
- **Delayed Durability**: SQL Server tarafında transaction log yazma maliyetini asenkron hale getirerek yazma performansını %20-40 artırır.

### 🧵 İş Parçacığı ve Runtime (Threading & Runtime)
- **ThreadPool Tuning**: Donanım çekirdek sayısına (Processor Count) göre dinamik ThreadPool yapılandırması yapılır.
- **Zero-Allocation**: Kritik yollarda `ValueTask` ve `AggressiveInlining` kullanımıyla GC (Garbage Collection) yükü azaltılır.
- **Brotli/Gzip Compression**: Yanıtlar en hızlı seviyede sıkıştırılarak ağ trafiği optimize edilir.
- **High-Performance JSON**: `System.Text.Json` source generation (AppJsonContext) ile metadata yükü olmadan ışık hızında serileştirme yapılır.

### ⚙️ Kritik Konfigürasyon Değerleri (Tuning)
Uygulama varsayılan olarak aşağıdaki performans parametreleri ile çalışır:
- **Kestrel**: `MaxConcurrentConnections: 50,000`
- **DbContext Pool**: `Size: 4096` (Yüksek yük altında Context oluşturma maliyetini sıfıra indirir)
- **SQL Connection Pool**: `Read: 200`, `Write: 100` (Max pool limitleri eşzamanlı sorgular için optimize edildi)
- **ThreadPool**: `MinThreads: ProcessorCount * 2` (Giriş yükü dalgalanmalarında thread gecikmesini önler)

---

## 🛡️ 3. Dayanıklılık ve Güvenlik (Resilience & Security)

### ⛓️ Resilience (Polly v8)
`ResiliencePipelines.cs` üzerinden merkezi hata yönetimi yapılır:
- **Retry Strategy**: Geçici ağ/DB hatalarında üstel geri çekilme (exponential backoff) ve jitter ile tekrar deneme.
- **Circuit Breaker**: Hata oranı %50'yi geçtiğinde trafiği keserek sistemin "cascading failure" durumuna düşmesini engeller.
- **Timeout**: Belirlenen süreyi aşan (örn. 5 sn) işlemler otomatik iptal edilir.
- **Reliable Messaging**: Outbox Pattern ve `UPDLOCK, READPAST` (Skip Locked) teknikleri ile mesajların kaybolmadan ve çakışmadan işlenmesi (At Least Once delivery) garanti edilir.

### 🔐 Güvenlik Katmanı
- **RS256 JWT**: Tokenlar simetrik anahtarlar yerine Private/Public key çifti ile üretilir ve doğrulanır.
- **Security Headers**: `SecurityHeadersMiddleware.cs` ile HSTS, CSP (Content Security Policy) ve Frame Options gibi tarayıcı seviyesindeki güvenlik önlemleri aktif edilir.
- **Rate Limiting**: `FixedWindow` ve `SlidingWindow` algoritmaları ile API brute-force ve gereksiz trafikten korunur.
- **Antiforgery**: XSRF/CSRF saldırılarına karşı `X-XSRF-TOKEN` header kontrolü ve secure cookie politikası uygulanır.
- **Audit Logging**: `AuditLoggingBehavior.cs` ile tüm kritik yazma işlemleri kimin tarafından ne zaman yapıldığı bilgisiyle kaydedilir.

---

## 📂 4. Proje Klasör Yapısı

```text
BaseModularMonolith/
├── src/
│   ├── Api/                  # Merkezi API Host, Middlewares, Auth Configuration
│   ├── BuildingBlocks/       # Ortak Pipeline'lar, Resilience, CQRS Base
│   ├── Modules/
│   │   └── User/             # Örnek Kullanıcı Modülü
│   │       ├── Api/          # Modül Endpoint'leri
│   │       ├── Application/  # Commands, Queries, Handlers, Validators
│   │       ├── Domain/       # Entities, Value Objects, Domain Events
│   │       └── Infrastructure/# DbContext, Dapper Services, Repositories
│   └── SharedKernel/         # Paylaşılan DTO'lar, Helpers
├── infra/                    # Altyapı konfigürasyonları (Prometheus, etc.)
├── scripts/                  # k6 Yük ve Stres Testleri
└── docker-compose.yaml       # Multi-container deployment orchestrator
```

---

## 🚀 5. Kurulum ve Çalıştırma (Installation)

### 🐳 Docker ile Hızlı Başlangıç (Önerilen)
Tüm servisleri (API, SQL Server, Prometheus, Grafana) tek komutla başlatabilirsiniz:

```bash
docker-compose up -d --build
```

### 💻 Manuel Geliştirme Ortamı Kurulumu
1. **SQL Server**: Bir MSSQL instance'ı oluşturun ve `appsettings.json` içindeki `DefaultConnection`'ı güncelleyin.
2. **Migration**: Veritabanı şemasını oluşturmak için:
   ```bash
   dotnet ef database update --project src/Modules/User/Infrastructure
   ```
3. **Çalıştır**:
   ```bash
   dotnet run --project src/Api
   ```

---

## 📊 6. İzleme ve Test (Monitoring & Testing)

### 📈 Metrikler ve Dashboards
- **Prometheus**: `http://localhost:9090` - Uygulama metriklerini sorgulayabilirsiniz.
- **Grafana**: `http://localhost:3000` - Dashboard'lar üzerinden performansı görselleştirebilirsiniz.
- **Server-Timing**: API yanıtlarının HTTP header'larında işlemin hangi aşamada ne kadar vakit harcadığını görebilirsiniz.

### 🧪 Yük ve Stres Testleri (k6)
`scripts/` klasöründeki JS dosyaları ile sistemi test edebilirsiniz. (k6 yüklü olmalıdır):

```bash
# Hızlı test
k6 run scripts/quick-test.js

# Stres testi (Sınırları zorlar)
k6 run scripts/stress-test.js
```

---

## 📋 7. MediatR Pipeline Sıralaması
Her istek aşağıdaki sırayla işlenir. Bu sıralama sistemin tutarlılığı için kritiktir:
1. **Logging**: İstek girişi.
2. **Validation**: FluentValidation (Hata varsa handler'a gitmeden döner).
3. **Idempotency**: Tekil anahtar kontrolü.
4. **Transaction**: DB Transaction başlatılır.
5. **Audit**: İşlem logu oluşturulur.
6. **Handler**: İş mantığı çalıştırılır.


