# Base Modular Monolith

Bu proje, yüksek performanslı, ölçeklenebilir ve maintainable (sürdürülebilir) bir .NET 9 uygulaması geliştirmek için tasarlanmış modern bir **Modüler Monolit** mimari şablonudur. Domain-Driven Design (DDD), Clean Architecture ve CQRS prensiplerini temel alır.

## 🏗 Mimari ve Tasarım

Proje, işlevselliği belirli iş alanlarına (domain) göre ayıran **Modüler Monolit** mimarisi üzerine inşa edilmiştir. Bu yaklaşım, monolitik uygulamanın dağıtım kolaylığını korurken, mikroservis mimarisinin sunduğu sınırların netliği (separation of concerns) avantajını sunar.

### Temel Prensipler
*   **Modular Monolith:** Uygulama, birbirinden bağımsız çalışabilen modüllere (örn: User Module) ayrılmıştır. Her modül kendi dikey katmanlarına (Domain, Application, Infrastructure, API) sahiptir.
*   **DDD (Domain-Driven Design):** İş kuralları ve domain mantığı projenin merkezindedir.
*   **CQRS (Command Query Responsibility Segregation):** Okuma ve yazma işlemleri birbirinden ayrılmıştır. Yazma işlemleri Domain Entity'leri üzerinden, okuma işlemleri ise performans için optimize edilmiş sorgularla yapılır.
*   **Clean Architecture:** Dış katmanlar iç katmanlara bağımlıdır, ancak iç katmanlar dışarıdan habersizdir.

## 🚀 Kullanılan Teknolojiler

Bu projede kullanılan temel teknolojiler ve kullanım amaçları şunlardır:

### Core & Framework
*   **[.NET 9](https://dotnet.microsoft.com/):** En güncel ve yüksek performanslı runtime.
*   **[ASP.NET Core Web API](https://asp.net/):** RESTful servisleri sunmak için kullanılan ana çatısı.
*   **[Docker & Docker Compose](https://www.docker.com/):** Uygulamanın ve bağımlılıklarının (MSSQL, Grafana, vb.) konteynerize edilmesi ve kolayca ayağa kaldırılması için.

### Veri Erişimi (Data Access)
*   **[Entity Framework Core 9](https://docs.microsoft.com/ef/core/):** Yazma işlemleri (Commands) için ORM aracı. Domain entity'lerinin yönetimi ve veri tutarlılığı için kullanılır.
*   **[Microsoft.Data.SqlClient (Dapper stili)]:** `Sql_db` ile doğrudan iletişim. Performansın kritik olduğu okuma (Query) işlemlerinde veya raw SQL gerektiren durumlarda kullanılır.
*   **[MSSQL Server 2022](https://www.microsoft.com/sql-server):** İlişkisel veritabanı yönetim sistemi.

### Mimari Bileşenler & Kütüphaneler
*   **[MediatR](https://github.com/jbogard/MediatR):** CQRS ve Mediator pattern uygulaması için. Modüller arası ve modül içi (API -> Application) gevşek bağımlılık (loose coupling) sağlar.
*   **[FluentValidation](https://fluentvalidation.net/):** Gelen isteklerin (Command/Query) doğrulanması için kullanılır.
*   **Pipelines (Behaviors):** MediatR pipeline'ı üzerinde merkezi Cross-Cutting Concerns yönetimi:
    *   `LoggingBehavior`: İstek/Cevap loglama.
    *   `ValidationBehavior`: Otomatik doğrulama kontrolleri.
    *   `TransactionBehavior`: Veritabanı transaction yönetimi.
    *   `IdempotencyBehavior`: Tekrarlayan isteklerin güvenli yönetimi.
    *   `AuditLoggingBehavior`: İşlem iz kayıtları.

### Güvenlik (Security)
*   **JWT (JSON Web Token):** Kimlik doğrulama (Authentication) için RS256 algoritması (Public/Private Key) kullanan güvenli token yapısı.
*   **Serilog:** Yapılandırılmış (Structured) loglama için.

### Gözlemlenebilirlik (Observability)
*   **[OpenTelemetry](https://opentelemetry.io/):** Trace ve metrik toplama standardı.
*   **[Prometheus](https://prometheus.io/):** Metriklerin saklanması ve sorgulanması.
*   **[Grafana](https://grafana.com/):** Sistem metriklerinin görselleştirilmesi (Dashboard).
*   **Health Checks:** Uygulamanın ve bağımlılıklarının (DB vb.) sağlık durumunun takibi.

---

## 📂 Proje Yapısı

```
├── src
│   ├── Api                 # Ana giriş noktası (Host)
│   ├── BuildingBlocks      # Paylaşılan çekirdek kodlar (Shared Kernel/Seedwork)
│   ├── Modules             # İş modülleri
│   │   └── User            # Örnek Modül: Kullanıcı yönetimi
│   │       ├── Api         # Modülün API uç noktaları (Controllers/Endpoints)
│   │       ├── Application # Use Case'ler (Commands/Queries)
│   │       ├── Domain      # Entity'ler, Value Object'ler
│   │       └── Infrastructure # DB Context, Repositories
│   └── SharedKernel        # Ortak arayüzler ve modeller
├── infra                   # Altyapı konfigürasyonları (Prometheus vb.)
└── docker-compose.yaml     # Konteyner orkestrasyon dosyası
```

---

## 🛠 Kurulum ve Çalıştırma

Projenin çalıştırılması için iki ana yöntem vardır. En kolayı Docker Compose kullanmaktır.

### Gereksinimler
*   Docker Desktop (veya Docker Engine + Compose)
*   .NET 9 SDK (Lokal geliştirme için)
*   IDE (Visual Studio, Rider veya VS Code)

### Yöntem 1: Docker Compose ile Hızlı Başlangıç (Önerilen)

Tüm sistemi (API, SQL Server, Prometheus, Grafana) tek komutla ayağa kaldırabilirsiniz.

1.  Terminali proje kök dizininde açın.
2.  Aşağıdaki komutu çalıştırın:
    ```bash
    docker-compose up -d --build
    ```
3.  Servislerin ayağa kalkmasını bekleyin.
    *   **API:** `http://localhost:5000` (veya `http://localhost:5000/swagger`)
    *   **Grafana:** `http://localhost:3000`
    *   **Prometheus:** `http://localhost:9090`
    *   **SQL Server:** `localhost,1433` (Kullanıcı: `sa`, Şifre: `YourStrongPassword123!`)

### Yöntem 2: Lokal Geliştirme (Local Development)

Eğer API'yi IDE üzerinden çalıştırmak isterseniz:

1.  **Veritabanını Başlatın:** Sadece SQL Server'ı Docker ile ayağa kaldırın veya yerel bir instance kullanın.
    ```bash
    docker-compose up -d sql_db
    ```
2.  **Connection String:** `src/Api/appsettings.Development.json` dosyasındaki SQL bağlantı cümlesinin doğru olduğundan emin olun.
3.  **Migration Uygulama:** Uygulama ilk açılışta veritabanını oluşturmaya çalışacaktır (bkz. `Program.cs` migrate adımı). Manuel uygulamak isterseniz `src/Api` dizininde:
    ```bash
    dotnet ef database update --project ../Modules/User/Infrastructure --startup-project .
    ```
4.  **Projeyi Çalıştırın:**
    ```bash
    dotnet run --project src/Api/Api.csproj
    ```

## 📝 Notlar
*   **Performans:** Uygulama `Development` modunda dahi SQL Server için "Delayed Durability" gibi performans ayarlarını otomatik yapacak şekilde yapılandırılmıştır.
*   **Loglar:** Loglar konsola ve `logs/` klasörüne JSON formatında yazılır.
