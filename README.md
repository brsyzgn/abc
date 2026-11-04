# TwinS ORBIT.API

.NET 8 / PostgreSQL tabanlı TwinS sunucu API'si. Bu depo kullanıcı JWT oturumlarını,
akvaryum ve cihaz sahipliğini, Device Identity eşleştirmesini ve GRID cihaz
heartbeat/telemetri altyapısını içerir.

## Güvenlik

- Gerçek `appsettings.json`, `.env`, sertifika, veritabanı yedeği ve anahtar dosyaları Git'e alınmaz.
- Üretimde sırlar yalnız sunucu secret store veya `/etc/orbit-api/orbit-api.env` gibi erişimi kısıtlı bir dosyadan verilir.
- `Jwt__Key` en az 32 karakterlik kriptografik rastgele bir değer olmalıdır.
- Üretimde PostgreSQL bağlantısında TLS kullanılmalıdır (`SSL Mode=Require`).
- API yalnız loopback üzerinde çalıştırılmalı; internet trafiği TLS sonlandıran Nginx üzerinden gelmelidir.
- Cihaz sırları veritabanında yalnız SHA-256 özeti olarak tutulur ve loglanmaz.

## Gereksinimler

- Ubuntu 24.04 LTS veya Windows Server 2022
- 2 vCPU, en az 2 GB RAM (önerilen 4 GB), 20 GB SSD
- .NET 8 ASP.NET Core Runtime; build/migration için .NET 8 SDK
- PostgreSQL 16 (Npgsql / EF Core 8)
- Nginx ve geçerli genel TLS sertifikası
- Kalıcı fotoğraf dizini ve düzenli SQL/dosya yedeği

## Yerel doğrulama ve yayın paketi

```powershell
dotnet restore
dotnet build ORBIT.API.csproj -c Release
dotnet run --project Tests/ORBIT.API.SenderTests.csproj -c Release
./scripts/publish.ps1
```

Çıktı `.artifacts/publish` altındadır ve Git tarafından yok sayılır.
`appsettings.json` bilinçli olarak yayın paketine alınmaz.

## Sunucu yapılandırması

`.env.example` içindeki değişkenleri sunucunun secret store'una taşıyın. En az:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`
- `RESEND_API_KEY`, `RESEND_FROM_ADDRESS`
- `AquariumPhotos__RootPath`

Linux'ta `/etc/orbit-api/orbit-api.env` dosyasını `root:orbit`, `0640` izinleriyle oluşturun.
Uygulama dosyalarını `/opt/orbit-api`, fotoğrafları `/var/lib/orbit-api/aquarium-photos`
altında tutun. Örnek systemd ve Nginx dosyaları `deploy/` dizinindedir.

## Veritabanı kurulumu

1. Boş bir `orbit` veritabanı ve yalnız bu veritabanında yetkili uygulama kullanıcısı oluşturun.
2. Veritabanının/yedeklerin anlık yedeğini alın.
3. Migration listesini inceleyin:
   `dotnet ef migrations list --project ORBIT.API.csproj --startup-project ORBIT.API.csproj`
4. Önce staging veritabanında uygulayın ve smoke test yapın.
5. Üretim bakım penceresinde çalıştırın:
   `./scripts/migrate.ps1 -ConnectionString "..."`

Migration komutu otomatik olarak uygulama başlangıcında çalışmaz; böylece yanlış hedef veritabanı
kazara değiştirilemez. Bu depodaki hazırlık sırasında hiçbir migration canlı veritabanına uygulanmamıştır.

## Yayın sırası

1. DNS ve TLS sertifikasını hazırlayın.
2. SQL ve fotoğraf yedeğini doğrulayın.
3. Migration'ı staging, sonra üretimde kontrollü uygulayın.
4. Publish paketini `/opt/orbit-api` altına kopyalayın.
5. systemd servisini ve Nginx'i etkinleştirin.
6. HTTPS üzerinden oturum, eşleştirme ve GRID heartbeat smoke testlerini çalıştırın.
7. Loglarda parola, JWT, cihaz sırrı veya bağlantı dizesi bulunmadığını doğrulayın.

GitHub'a gönderme ve canlı yayın bu hazırlığın parçası değildir; ayrıca onaylanmalıdır.
