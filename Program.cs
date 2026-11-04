using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.HttpOverrides;
using ORBIT.API.Data;
using ORBIT.API.Models;
using ORBIT.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway (and most PaaS hosts) inject the listening port through PORT. An explicit
// ASPNETCORE_URLS / --urls value (VM + reverse proxy deployments) always takes precedence.
if (string.IsNullOrWhiteSpace(builder.Configuration["URLS"]) &&
    int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var hostPort) &&
    hostPort is > 0 and <= 65535)
{
    // [::] is dual-stack (IPv4 + IPv6), which also keeps Railway private networking reachable.
    builder.WebHost.UseUrls($"http://[::]:{hostPort}");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
}
var configuredJwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(configuredJwtKey) || configuredJwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be configured with at least 32 characters.");
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The service is reached only through a trusted reverse proxy (Nginx on a VM, or the Railway edge proxy).
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Optional extra browser origins (Cors__AllowedOrigins__0=https://app.example.com). Exact origins only;
// credentials are not enabled, so no wildcard or credentialed cross-origin access is introduced.
var configuredCorsOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
builder.Services.AddCors(options =>
{
    options.AddPolicy("OrbitClient", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                return uri.Host is "localhost" or "127.0.0.1" || configuredCorsOrigins.Contains(origin);
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddSingleton<PasswordResetSecurity>();
builder.Services.AddHttpClient<ResendPasswordResetCodeSender>(client =>
    client.BaseAddress = new Uri("https://api.resend.com/"));
builder.Services.AddScoped<IPasswordResetCodeSender>(services =>
    services.GetRequiredService<ResendPasswordResetCodeSender>());

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = configuredJwtKey;
        var issuer = builder.Configuration["Jwt:Issuer"] ?? "ORBIT.API";
        var audience = builder.Configuration["Jwt:Audience"] ?? "ORBIT.Client";

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sessionIdValue = context.Principal?.FindFirstValue("sessionId");
                if (!Guid.TryParse(sessionIdValue, out var sessionId))
                {
                    context.Fail("Session claim is missing.");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var sessionIsActive = await dbContext.UserSessions.AnyAsync(session =>
                    session.Id == sessionId &&
                    session.RevokedAt == null &&
                    session.ExpiresAt > DateTime.UtcNow);
                if (!sessionIsActive)
                {
                    context.Fail("Session is no longer active.");
                }
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MultipartBodyLengthLimit = AquariumPhotoProcessor.MaxUploadBytes + 1024 * 1024);
builder.Services.AddSingleton<IAquariumPhotoStorage, LocalAquariumPhotoStorage>();
builder.Services.AddSingleton<IAquariumPhotoProcessor, AquariumPhotoProcessor>();
builder.Services.AddSingleton<DeviceApiSecurity>();
builder.Services.AddHostedService<DevicePresenceMonitor>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("OrbitClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapPairingTransactions();
app.MapGridDeviceApi();

// Liveness probe for Railway: confirms the process is serving HTTP. It deliberately does not
// touch the database, so a temporary database outage cannot trigger restart loops.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous()
    .WithName("GetHealth")
    .ExcludeFromDescription();

app.MapGet("/api/test", () =>
{
    return Results.Ok(new { message = "Merhaba ORBIT!" });
})
.WithName("GetTest")
.WithOpenApi();

app.MapGet("/api/database-test", async (AppDbContext dbContext) =>
{
    try
    {
        var connected = await dbContext.Database.CanConnectAsync();

        if (connected)
        {
            return Results.Ok(new
            {
                connected = true,
                message = "ORBIT veritabanına bağlantı başarılı."
            });
        }

        return Results.Json(new
        {
            connected = false,
            message = "ORBIT veritabanına bağlanılamadı."
        }, statusCode: StatusCodes.Status500InternalServerError);
    }
    catch
    {
        return Results.Json(new
        {
            connected = false,
            message = "ORBIT veritabanına bağlanılamadı."
        }, statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("GetDatabaseTest")
.WithOpenApi();

app.MapPost("/api/users", async (CreateUserRequest request, AppDbContext dbContext, IPasswordResetCodeSender sender, PasswordResetSecurity security) =>
{
    if (!sender.IsAvailable)
    {
        return Results.Json(new { message = "E-posta gönderim hizmeti henüz yapılandırılmadı." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { message = "Ad soyad zorunludur." });
    }

    if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
    {
        return Results.BadRequest(new { message = "Geçerli bir e-posta adresi girin." });
    }

    if (request.Password.Length < 8)
    {
        return Results.BadRequest(new { message = "Parola en az 8 karakter olmalıdır." });
    }

    var normalizedEmail = request.Email.Trim().ToLowerInvariant();

    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
    if (user?.IsActive == true)
    {
        return Results.Conflict(new { message = "Bu e-posta adresi zaten kayıtlı." });
    }
    if (user is not null)
    {
        var latest = await dbContext.RegistrationVerificationChallenges.Where(x => x.UserId == user.Id).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (latest is not null && latest.CreatedAt > DateTime.UtcNow - VerificationCodePolicy.ResendCooldown)
            return Results.Json(new { message = "Yeni kod istemeden önce bekleyin.", retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((latest.CreatedAt.AddMinutes(1) - DateTime.UtcNow).TotalSeconds)) }, statusCode: StatusCodes.Status429TooManyRequests);
        user.Name = request.Name.Trim();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
    }
    else
    {
        user = new User { Name = request.Name.Trim(), Email = normalizedEmail, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), CreatedAt = DateTime.UtcNow, IsActive = false };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }
    var code = security.GenerateCode();
    dbContext.RegistrationVerificationChallenges.Add(new RegistrationVerificationChallenge { UserId = user.Id, CodeHash = security.Hash(code), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow + VerificationCodePolicy.Lifetime });
    await dbContext.SaveChangesAsync();
    await sender.SendAsync(user.Email, code, EmailCodePurpose.RegistrationVerification);

    return Results.Created($"/api/users/{user.Id}", new
    {
        id = user.Id,
        name = user.Name,
        email = user.Email,
        createdAt = user.CreatedAt,
        lastLogin = user.LastLogin,
        isActive = user.IsActive,
        retryAfterSeconds = 60,
        expiresInSeconds = 600
    });
})
.WithName("CreateUser")
.WithOpenApi();

app.MapPost("/api/aquariums", async (CreateAquariumRequest request, ClaimsPrincipal principal, AppDbContext dbContext) =>
{
    var name = request.Name.Trim();
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
    {
        return Results.BadRequest(new { message = "Akvaryum adı 1-100 karakter olmalıdır." });
    }
    if (request.PhotoUrl?.Trim().Length > 500)
    {
        return Results.BadRequest(new { message = "Fotoğraf adresi en fazla 500 karakter olabilir." });
    }

    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var aquarium = new Aquarium
    {
        UserId = userId,
        Name = name,
        PhotoUrl = request.PhotoUrl,
        CreatedAt = DateTime.UtcNow
    };

    dbContext.Aquariums.Add(aquarium);
    ActivityRecorder.Add(dbContext, userId, "aquarium.created", "Akvaryum oluşturuldu", $"{name} oluşturuldu.");
    await dbContext.SaveChangesAsync();

    return Results.Created($"/api/aquariums/{aquarium.Id}", new
    {
        id = aquarium.Id,
        userId = aquarium.UserId,
        name = aquarium.Name,
        photoUrl = aquarium.PhotoUrl,
        hasPhoto = aquarium.PhotoObjectKey != null,
        createdAt = aquarium.CreatedAt
    });
})
.WithName("CreateAquarium")
.WithOpenApi()
.RequireAuthorization();

app.MapPut("/api/aquariums/{aquariumId:int}", async (
    int aquariumId,
    UpdateAquariumRequest request,
    ClaimsPrincipal principal,
    AppDbContext dbContext) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var aquarium = await dbContext.Aquariums
        .FirstOrDefaultAsync(item => item.Id == aquariumId && item.UserId == userId);
    if (aquarium is null)
    {
        return Results.NotFound(new { message = "Akvaryum bulunamadı." });
    }

    var name = request.Name.Trim();
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
    {
        return Results.BadRequest(new { message = "Akvaryum adı 1-100 karakter olmalıdır." });
    }
    if (request.PhotoUrl?.Trim().Length > 500)
    {
        return Results.BadRequest(new { message = "Fotoğraf adresi en fazla 500 karakter olabilir." });
    }

    aquarium.Name = name;
    aquarium.PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl)
        ? null
        : request.PhotoUrl.Trim();
    ActivityRecorder.Add(dbContext, userId, "aquarium.updated", "Akvaryum güncellendi", $"{name} güncellendi.");
    await dbContext.SaveChangesAsync();

    return Results.Ok(new
    {
        id = aquarium.Id,
        name = aquarium.Name,
        photoUrl = aquarium.PhotoUrl,
        hasPhoto = aquarium.PhotoObjectKey != null,
        createdAt = aquarium.CreatedAt
    });
})
.WithName("UpdateAquarium")
.WithOpenApi()
.RequireAuthorization();

app.MapDelete("/api/aquariums/{aquariumId:int}", async (
    int aquariumId,
    ClaimsPrincipal principal,
    AppDbContext dbContext,
    IAquariumPhotoStorage photoStorage,
    CancellationToken cancellationToken) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var aquarium = await dbContext.Aquariums
        .Include(item => item.Devices)
        .FirstOrDefaultAsync(item => item.Id == aquariumId && item.UserId == userId);
    if (aquarium is null)
    {
        return Results.NotFound(new { message = "Akvaryum bulunamadı." });
    }

    foreach (var device in aquarium.Devices ?? [])
    {
        var usedRecord = await dbContext.UsedDeviceRecords
            .FirstOrDefaultAsync(item => device.ManufacturerId != null
                ? item.ManufacturerId == device.ManufacturerId && item.ImmutableDeviceId == device.ImmutableDeviceId
                : item.ManufacturerId == null && item.SerialNumber == device.SerialNumber);
        if (usedRecord is null)
        {
            usedRecord = new UsedDeviceRecord { SerialNumber = device.SerialNumber };
            dbContext.UsedDeviceRecords.Add(usedRecord);
        }
        DeviceIdentity.From(device)?.ApplyTo(usedRecord);
    usedRecord.DeviceType = device.DeviceType;
        usedRecord.LastName = device.Name;
        usedRecord.LastUserId = userId;
        usedRecord.LastUsedAt = DateTime.UtcNow;
    }
    ActivityRecorder.Add(dbContext, userId, "aquarium.deleted", "Akvaryum silindi", $"{aquarium.Name} silindi.");
    var photoObjectKey = aquarium.PhotoObjectKey;
    dbContext.Aquariums.Remove(aquarium);
    await dbContext.SaveChangesAsync(cancellationToken);
    if (photoObjectKey is not null)
        await photoStorage.DeleteAsync(photoObjectKey, cancellationToken);
    return Results.NoContent();
})
.WithName("DeleteAquarium")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/api/aquariums/{aquariumId:int}/photo", async (
    int aquariumId,
    HttpRequest request,
    ClaimsPrincipal principal,
    AppDbContext dbContext,
    IAquariumPhotoProcessor photoProcessor,
    IAquariumPhotoStorage photoStorage,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var aquarium = await dbContext.Aquariums
        .FirstOrDefaultAsync(item => item.Id == aquariumId && item.UserId == userId, cancellationToken);
    if (aquarium is null)
        return Results.NotFound(new { message = "Akvaryum bulunamadı." });
    if (!request.HasFormContentType)
        return Results.BadRequest(new { message = "Fotoğraf multipart/form-data olarak gönderilmelidir." });

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("photo");
    if (file is null)
        return Results.BadRequest(new { message = "photo alanında fotoğraf dosyası gönderin." });

    ProcessedAquariumPhoto processed;
    try { processed = await photoProcessor.ProcessAsync(file, cancellationToken); }
    catch (AquariumPhotoException error) { return Results.BadRequest(new { message = error.Message }); }

    var newObjectKey = await photoStorage.SaveAsync(processed, cancellationToken);
    var oldObjectKey = aquarium.PhotoObjectKey;
    aquarium.PhotoObjectKey = newObjectKey;
    aquarium.PhotoUrl = null;
    try { await dbContext.SaveChangesAsync(cancellationToken); }
    catch
    {
        await photoStorage.DeleteAsync(newObjectKey, cancellationToken);
        throw;
    }

    if (oldObjectKey is not null)
    {
        try { await photoStorage.DeleteAsync(oldObjectKey, cancellationToken); }
        catch (Exception error) { logger.LogWarning(error, "Old aquarium photo {ObjectKey} could not be deleted.", oldObjectKey); }
    }
    return Results.Ok(new { hasPhoto = true });
})
.WithName("UploadAquariumPhoto")
.DisableAntiforgery()
.RequireAuthorization();

app.MapGet("/api/aquariums/{aquariumId:int}/photo", async (
    int aquariumId,
    ClaimsPrincipal principal,
    AppDbContext dbContext,
    IAquariumPhotoStorage photoStorage,
    CancellationToken cancellationToken) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var objectKey = await dbContext.Aquariums
        .Where(item => item.Id == aquariumId && item.UserId == userId)
        .Select(item => item.PhotoObjectKey)
        .FirstOrDefaultAsync(cancellationToken);
    if (objectKey is null)
        return Results.NotFound(new { message = "Akvaryum fotoğrafı bulunamadı." });
    var stream = await photoStorage.OpenReadAsync(objectKey, cancellationToken);
    return stream is null
        ? Results.NotFound(new { message = "Akvaryum fotoğrafı bulunamadı." })
        : Results.Stream(stream, "image/webp", enableRangeProcessing: true);
})
.WithName("GetAquariumPhoto")
.RequireAuthorization();

app.MapDelete("/api/aquariums/{aquariumId:int}/photo", async (
    int aquariumId,
    ClaimsPrincipal principal,
    AppDbContext dbContext,
    IAquariumPhotoStorage photoStorage,
    CancellationToken cancellationToken) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var aquarium = await dbContext.Aquariums
        .FirstOrDefaultAsync(item => item.Id == aquariumId && item.UserId == userId, cancellationToken);
    if (aquarium is null)
        return Results.NotFound(new { message = "Akvaryum bulunamadı." });
    var objectKey = aquarium.PhotoObjectKey;
    aquarium.PhotoObjectKey = null;
    await dbContext.SaveChangesAsync(cancellationToken);
    if (objectKey is not null)
        await photoStorage.DeleteAsync(objectKey, cancellationToken);
    return Results.NoContent();
})
.WithName("DeleteAquariumPhoto")
.RequireAuthorization();

app.MapGet("/api/aquariums", async (ClaimsPrincipal principal, AppDbContext dbContext) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var aquariums = await dbContext.Aquariums
        .Where(a => a.UserId == userId)
        .OrderBy(a => a.Id)
        .Select(a => new
        {
            id = a.Id,
            name = a.Name,
            photoUrl = a.PhotoUrl,
            hasPhoto = a.PhotoObjectKey != null,
            createdAt = a.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(aquariums);
})
.WithName("GetAquariums")
.WithOpenApi()
.RequireAuthorization();

app.MapPut("/api/devices/{deviceId:int}", async (
    int deviceId,
    UpdateDeviceRequest request,
    ClaimsPrincipal principal,
    AppDbContext dbContext) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var device = await dbContext.Devices
        .Include(item => item.Aquarium)
        .FirstOrDefaultAsync(item =>
            item.Id == deviceId && item.Aquarium != null && item.Aquarium.UserId == userId);
    if (device is null)
    {
        return Results.NotFound(new { message = "Cihaz bulunamadı." });
    }

    var name = request.Name.Trim();
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
    {
        return Results.BadRequest(new { message = "Cihaz adı 1-100 karakter olmalıdır." });
    }

    device.Name = name;
    ActivityRecorder.Add(dbContext, userId, "device.updated", "Cihaz güncellendi", $"{name} güncellendi.");
    await dbContext.SaveChangesAsync();
    return Results.Ok(new
    {
        id = device.Id,
        aquariumId = device.AquariumId,
        deviceType = device.DeviceType,
        name = device.Name,
        serialNumber = device.SerialNumber,
        connectionDescriptor = device.ManufacturerId == null || device.ConnectionSsid == null || device.LocalApiBaseUrl == null
            ? null : new DeviceConnectionDescriptor(1, device.ManufacturerId.Value, device.ImmutableDeviceId!.Value,
                "ap", device.ConnectionSsid, device.LocalApiBaseUrl),
            identityStatus = device.ManufacturerId == null ? "legacy" : "v1",
            identity = device.ManufacturerId == null ? null : new DeviceIdentity(
                device.ManufacturerId.Value, device.ImmutableDeviceId!.Value,
                device.IdentitySchemaVersion!.Value, device.ProductTypeCode!.Value,
                device.HardwareRevision!.Value, new FirmwareVersion(
                    device.FirmwareMajor!.Value, device.FirmwareMinor!.Value, device.FirmwarePatch!.Value)),
        isOnline = device.IsOnline,
        sortOrder = device.SortOrder,
        createdAt = device.CreatedAt
    });
})
.WithName("UpdateDevice")
.WithOpenApi()
.RequireAuthorization();

app.MapDelete("/api/devices/{deviceId:int}", async (
    int deviceId,
    ClaimsPrincipal principal,
    AppDbContext dbContext) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var device = await dbContext.Devices
        .FirstOrDefaultAsync(item =>
            item.Id == deviceId && item.Aquarium != null && item.Aquarium.UserId == userId);
    if (device is null)
    {
        return Results.NotFound(new { message = "Cihaz bulunamadı." });
    }

    var usedRecord = await dbContext.UsedDeviceRecords
        .FirstOrDefaultAsync(item => device.ManufacturerId != null
                ? item.ManufacturerId == device.ManufacturerId && item.ImmutableDeviceId == device.ImmutableDeviceId
                : item.ManufacturerId == null && item.SerialNumber == device.SerialNumber);
    if (usedRecord is null)
    {
        usedRecord = new UsedDeviceRecord { SerialNumber = device.SerialNumber };
        dbContext.UsedDeviceRecords.Add(usedRecord);
    }
    DeviceIdentity.From(device)?.ApplyTo(usedRecord);
    usedRecord.DeviceType = device.DeviceType;
    usedRecord.LastName = device.Name;
    usedRecord.LastUserId = userId;
    usedRecord.LastUsedAt = DateTime.UtcNow;
    dbContext.Devices.Remove(device);
    ActivityRecorder.Add(dbContext, userId, "device.removed", "Cihaz kaldırıldı", $"{device.DeviceType} cihazı akvaryumdan kaldırıldı.");
    await dbContext.SaveChangesAsync();
    return Results.NoContent();
})
.WithName("DeleteDevice")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/api/aquariums/{aquariumId:int}/devices", async (
    int aquariumId,
    CreateDeviceRequest request,
    ClaimsPrincipal principal,
    AppDbContext dbContext) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var ownsAquarium = await dbContext.Aquariums
        .AnyAsync(aquarium => aquarium.Id == aquariumId && aquarium.UserId == userId);
    if (!ownsAquarium)
    {
        return Results.NotFound(new { message = "Akvaryum bulunamadı." });
    }

    var identity = request.Identity;
    if (identity is not null && !identity.IsValid)
        return Results.BadRequest(new { code = "invalid_identity", message = "Geçersiz Device Identity v1." });
    var validationError = request.GetValidationError(out var deviceType, out var name, out var serialNumber);
    if (validationError is not null)
        return Results.BadRequest(new { message = validationError });

    var existing = await dbContext.Devices.FirstOrDefaultAsync(item => identity != null
        ? item.ManufacturerId == identity.ManufacturerId && item.ImmutableDeviceId == identity.DeviceId
        : item.ManufacturerId == null && item.SerialNumber == serialNumber);
    if (existing is not null)
    {
        if (existing.AquariumId != aquariumId || identity is null)
            return Results.Conflict(new { code = "device_already_paired", message = "Cihaz zaten eşleştirilmiş." });
        if (!identity.CompatibleHardware(existing))
            return Results.Conflict(new { code = "identity_metadata_conflict", message = "Aynı kimliğin ürün/donanım bilgisi uyuşmuyor." });
        identity.ApplyTo(existing);
        request.ConnectionDescriptor?.ApplyTo(existing);
        await dbContext.SaveChangesAsync();
        return Results.Ok(new {
            id = existing.Id, aquariumId = existing.AquariumId,
            deviceType = existing.DeviceType, name = existing.Name,
            serialNumber = existing.SerialNumber, isOnline = existing.IsOnline,
            sortOrder = existing.SortOrder, identity = DeviceIdentity.From(existing),
            connectionDescriptor = DeviceConnectionDescriptor.From(existing),
            identityStatus = "v1"
        });
    }

    var usedRecord = await dbContext.UsedDeviceRecords
        .FirstOrDefaultAsync(item => identity != null
            ? item.ManufacturerId == identity.ManufacturerId && item.ImmutableDeviceId == identity.DeviceId
            : item.ManufacturerId == null && item.SerialNumber == serialNumber);
    if (identity is not null && usedRecord is not null && !identity.CompatibleHardware(usedRecord))
        return Results.Conflict(new { code = "identity_metadata_conflict", message = "Kimlik donanım geçmişiyle uyuşmuyor." });
    var usedAction = request.UsedDeviceAction?.Trim().ToLowerInvariant();
    if (usedRecord is not null && usedAction is not ("reset" or "keep"))
    {
        return Results.Conflict(new
        {
            code = "used_device_choice_required",
            message = "Bu cihaz daha önce kullanılmış. ORBIT ayarlarını sıfırlamayı veya korumayı seçin."
        });
    }

    if (usedRecord is not null && usedRecord.DeviceType != deviceType)
    {
        return Results.BadRequest(new { message = "Cihaz tipi seri numarası geçmişiyle uyuşmuyor." });
    }

    var nextSortOrder = (await dbContext.Devices
        .Where(item => item.AquariumId == aquariumId)
        .MaxAsync(item => (int?)item.SortOrder) ?? -1) + 1;

    var device = new Device
    {
        AquariumId = aquariumId,
        DeviceType = deviceType,
        Name = usedRecord is not null && usedAction == "keep" && usedRecord.LastUserId == userId
            ? usedRecord.LastName
            : name,
        SerialNumber = serialNumber,
        IsOnline = true,
        CreatedAt = DateTime.UtcNow,
        SortOrder = nextSortOrder
    };
    identity?.ApplyTo(device);
    request.ConnectionDescriptor?.ApplyTo(device);
    dbContext.Devices.Add(device);
    ActivityRecorder.Add(dbContext, userId, usedRecord is null ? "device.paired" : "device.reassigned", usedRecord is null ? "Cihaz eşleştirildi" : "Kullanılmış cihaz eşleştirildi", $"{device.DeviceType} cihazı akvaryuma eklendi.");
    try { await dbContext.SaveChangesAsync(); }
    catch (DbUpdateException error) when (error.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
        sql.Number is 2601 or 2627)
    {
        return Results.Conflict(new { code = "device_already_paired", message = "Cihaz başka bir istekle eşleştirildi; listeyi yenileyin." });
    }

    return Results.Created($"/api/aquariums/{aquariumId}/devices/{device.Id}", new
    {
        id = device.Id,
        aquariumId = device.AquariumId,
        deviceType = device.DeviceType,
        name = device.Name,
        serialNumber = device.SerialNumber,
        connectionDescriptor = device.ManufacturerId == null || device.ConnectionSsid == null || device.LocalApiBaseUrl == null
            ? null : new DeviceConnectionDescriptor(1, device.ManufacturerId.Value, device.ImmutableDeviceId!.Value,
                "ap", device.ConnectionSsid, device.LocalApiBaseUrl),
            identityStatus = device.ManufacturerId == null ? "legacy" : "v1",
            identity = device.ManufacturerId == null ? null : new DeviceIdentity(
                device.ManufacturerId.Value, device.ImmutableDeviceId!.Value,
                device.IdentitySchemaVersion!.Value, device.ProductTypeCode!.Value,
                device.HardwareRevision!.Value, new FirmwareVersion(
                    device.FirmwareMajor!.Value, device.FirmwareMinor!.Value, device.FirmwarePatch!.Value)),
        isOnline = device.IsOnline,
        sortOrder = device.SortOrder,
        usedDeviceAction = usedRecord is null ? null : usedAction,
        hardwareResetDeferred = usedRecord is not null && usedAction == "reset",
        createdAt = device.CreatedAt
    });
})
.WithName("CreateDevice")
.WithOpenApi()
.RequireAuthorization();

app.MapPut("/api/aquariums/{aquariumId:int}/devices/order", async (
    int aquariumId,
    ReorderDevicesRequest request,
    ClaimsPrincipal principal,
    AppDbContext dbContext) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var devices = await dbContext.Devices
        .Where(item => item.AquariumId == aquariumId && item.Aquarium != null && item.Aquarium.UserId == userId)
        .ToListAsync();
    if (devices.Count == 0 && !await dbContext.Aquariums.AnyAsync(item => item.Id == aquariumId && item.UserId == userId))
    {
        return Results.NotFound(new { message = "Akvaryum bulunamadı." });
    }
    if (request.DeviceIds.Count != devices.Count ||
        request.DeviceIds.Distinct().Count() != devices.Count ||
        request.DeviceIds.Except(devices.Select(item => item.Id)).Any())
    {
        return Results.BadRequest(new { message = "Sıralama akvaryumdaki tüm cihazları tam olarak bir kez içermelidir." });
    }
    var order = request.DeviceIds.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
    foreach (var device in devices) device.SortOrder = order[device.Id];
    ActivityRecorder.Add(dbContext, userId, "device.reordered", "Cihaz sırası değiştirildi", "Akvaryumdaki cihaz kartlarının sırası güncellendi.");
    await dbContext.SaveChangesAsync();
    return Results.NoContent();
})
.WithName("ReorderDevices")
.WithOpenApi()
.RequireAuthorization();

app.MapGet("/api/aquariums/{aquariumId:int}/devices", async (
    int aquariumId,
    ClaimsPrincipal principal,
    AppDbContext dbContext) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var ownsAquarium = await dbContext.Aquariums
        .AnyAsync(aquarium => aquarium.Id == aquariumId && aquarium.UserId == userId);
    if (!ownsAquarium)
    {
        return Results.NotFound(new { message = "Akvaryum bulunamadı." });
    }

    var devices = await dbContext.Devices
        .Where(device => device.AquariumId == aquariumId)
        .OrderBy(device => device.SortOrder)
        .ThenBy(device => device.Id)
        .Select(device => new
        {
            id = device.Id,
            aquariumId = device.AquariumId,
            deviceType = device.DeviceType,
            name = device.Name,
            serialNumber = device.SerialNumber,
            connectionDescriptor = device.ManufacturerId == null || device.ConnectionSsid == null || device.LocalApiBaseUrl == null
                ? null : new DeviceConnectionDescriptor(1, device.ManufacturerId.Value, device.ImmutableDeviceId!.Value,
                    "ap", device.ConnectionSsid, device.LocalApiBaseUrl),
            identityStatus = device.ManufacturerId == null ? "legacy" : "v1",
            identity = device.ManufacturerId == null ? null : new DeviceIdentity(
                device.ManufacturerId.Value, device.ImmutableDeviceId!.Value,
                device.IdentitySchemaVersion!.Value, device.ProductTypeCode!.Value,
                device.HardwareRevision!.Value, new FirmwareVersion(
                    device.FirmwareMajor!.Value, device.FirmwareMinor!.Value, device.FirmwarePatch!.Value)),
            isOnline = device.IsOnline,
            sortOrder = device.SortOrder,
            createdAt = device.CreatedAt
        })
        .ToListAsync();
    return Results.Ok(devices);
})
.WithName("GetAquariumDevices")
.WithOpenApi()
.RequireAuthorization();

app.MapGet("/api/devices", async (ClaimsPrincipal principal, AppDbContext dbContext) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var devices = await dbContext.Devices
        .Where(device => device.Aquarium != null && device.Aquarium.UserId == userId)
        .OrderBy(device => device.AquariumId)
        .ThenBy(device => device.SortOrder)
        .ThenBy(device => device.Id)
        .Select(device => new
        {
            id = device.Id,
            aquariumId = device.AquariumId,
            aquariumName = device.Aquarium!.Name,
            deviceType = device.DeviceType,
            name = device.Name,
            serialNumber = device.SerialNumber,
            connectionDescriptor = device.ManufacturerId == null || device.ConnectionSsid == null || device.LocalApiBaseUrl == null
                ? null : new DeviceConnectionDescriptor(1, device.ManufacturerId.Value, device.ImmutableDeviceId!.Value,
                    "ap", device.ConnectionSsid, device.LocalApiBaseUrl),
            identityStatus = device.ManufacturerId == null ? "legacy" : "v1",
            identity = device.ManufacturerId == null ? null : new DeviceIdentity(
                device.ManufacturerId.Value, device.ImmutableDeviceId!.Value,
                device.IdentitySchemaVersion!.Value, device.ProductTypeCode!.Value,
                device.HardwareRevision!.Value, new FirmwareVersion(
                    device.FirmwareMajor!.Value, device.FirmwareMinor!.Value, device.FirmwarePatch!.Value)),
            isOnline = device.IsOnline,
            sortOrder = device.SortOrder,
            createdAt = device.CreatedAt
        })
        .ToListAsync();
    return Results.Ok(devices);
})
.WithName("GetDevices")
.WithOpenApi()
.RequireAuthorization();

app.MapGet("/api/activity", async (ClaimsPrincipal principal, AppDbContext dbContext) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var cutoff = DateTime.UtcNow.Subtract(ActivityRecorder.Retention);
    await dbContext.ActivityEvents.Where(item => item.CreatedAt < cutoff && !item.EventType.StartsWith("pairing.")).ExecuteDeleteAsync();
    var events = await dbContext.ActivityEvents
        .Where(item => item.UserId == userId && item.CreatedAt >= cutoff && !item.EventType.StartsWith("pairing."))
        .OrderByDescending(item => item.CreatedAt)
        .Select(item => new
        {
            id = item.Id,
            eventType = item.EventType,
            title = item.Title,
            message = item.Message,
            createdAt = item.CreatedAt
        })
        .ToListAsync();
    return Results.Ok(events);
})
.WithName("GetActivity")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/api/auth/login", async (LoginRequest request, AppDbContext dbContext) =>
{
    if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out _) ||
        string.IsNullOrEmpty(request.Password) ||
        string.IsNullOrWhiteSpace(request.DeviceId))
    {
        return Results.BadRequest(new { message = "E-posta, parola ve cihaz kimliği zorunludur." });
    }

    var normalizedEmail = request.Email.Trim().ToLowerInvariant();

    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
    if (user is null)
    {
        return Results.Json(new { message = "E-posta veya parola hatalı." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!user.IsActive)
    {
        return Results.Json(new { code = "email_not_verified", message = "Giriş yapmadan önce e-posta adresinizi doğrulayın." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
    if (!passwordValid)
    {
        return Results.Json(new { message = "E-posta veya parola hatalı." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    await using var loginTransaction = await dbContext.Database.BeginTransactionAsync(
        System.Data.IsolationLevel.Serializable);
    var now = DateTime.UtcNow;
    var activeSessions = await dbContext.UserSessions
        .Where(session =>
            session.UserId == user.Id &&
            session.RevokedAt == null &&
            session.ExpiresAt > now)
        .ToListAsync();
    var deviceId = request.DeviceId.Trim();
    var hasOtherDeviceSession = activeSessions.Any(session => session.DeviceId != deviceId);
    if (hasOtherDeviceSession && !request.Takeover)
    {
        await loginTransaction.RollbackAsync();
        return Results.Conflict(new
        {
            code = "takeover_required",
            message = "Hesabınız başka bir cihazda açık. Bu cihazda devam etmek için diğer oturumu kapatmayı onaylayın."
        });
    }

    foreach (var activeSession in activeSessions)
    {
        activeSession.RevokedAt = now;
    }

    var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured.");
    var issuer = builder.Configuration["Jwt:Issuer"] ?? "ORBIT.API";
    var audience = builder.Configuration["Jwt:Audience"] ?? "ORBIT.Client";
    // appsettings.json is not part of the published package, so a missing/invalid value must not
    // produce tokens that expire immediately (ClockSkew is zero).
    var configuredExpireMinutes = builder.Configuration.GetValue<int?>("Jwt:ExpireMinutes");
    var expireMinutes = configuredExpireMinutes is > 0 ? configuredExpireMinutes.Value : 60;
    var rememberExpireDays = builder.Configuration.GetValue<int?>("Jwt:RememberExpireDays") ?? 30;
    var expiresAt = request.RememberMe
        ? DateTime.UtcNow.AddDays(rememberExpireDays)
        : DateTime.UtcNow.AddMinutes(expireMinutes);
    var session = new UserSession
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        DeviceId = deviceId,
        RememberMe = request.RememberMe,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = expiresAt
    };
    dbContext.UserSessions.Add(session);
    user.LastLogin = DateTime.UtcNow;
    ActivityRecorder.Add(dbContext, user.Id, hasOtherDeviceSession ? "session.takeover" : "session.login", hasOtherDeviceSession ? "Oturum devralındı" : "Giriş yapıldı", hasOtherDeviceSession ? "Hesap oturumu bu uygulama kurulumuna taşındı." : "Hesabınıza giriş yapıldı.");
    await dbContext.SaveChangesAsync();
    await loginTransaction.CommitAsync();

    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(jwtKey);
    var claims = new[]
    {
        new Claim("userId", user.Id.ToString()),
        new Claim("sessionId", session.Id.ToString()),
        new Claim("deviceId", session.DeviceId),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Name, user.Name)
    };

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        Expires = expiresAt,
        Issuer = issuer,
        Audience = audience,
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    var jwt = tokenHandler.WriteToken(token);

    return Results.Ok(new
    {
        id = user.Id,
        name = user.Name,
        email = user.Email,
        token = jwt,
        sessionId = session.Id,
        deviceId = session.DeviceId,
        rememberMe = session.RememberMe,
        expiresAt = expiresAt,
        message = "Giriş başarılı."
    });
})
.WithName("LoginUser")
.WithOpenApi();

app.MapPost("/api/auth/registration/verify", async (VerifyRegistrationRequest request, AppDbContext dbContext, PasswordResetSecurity security) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email);
    if (user is null || user.IsActive) return Results.BadRequest(new { message = "Kod geçersiz veya daha önce kullanılmış." });
    var challenge = await dbContext.RegistrationVerificationChallenges.Where(x => x.UserId == user.Id && x.UsedAt == null).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
    if (challenge is null || challenge.FailedAttempts >= VerificationCodePolicy.MaximumFailedAttempts) return Results.BadRequest(new { message = "Kod geçersiz veya deneme sınırı aşıldı." });
    if (VerificationCodePolicy.IsExpired(challenge.ExpiresAt, DateTime.UtcNow)) return Results.Json(new { message = "Kodun kullanım süresi dolmuş. Yeni kod isteyin." }, statusCode: StatusCodes.Status410Gone);
    if (!security.Verify(request.Code, challenge.CodeHash))
    {
        challenge.FailedAttempts++;
        await dbContext.SaveChangesAsync();
        return Results.BadRequest(new { message = "Kod geçersiz.", remainingAttempts = Math.Max(0, VerificationCodePolicy.MaximumFailedAttempts - challenge.FailedAttempts) });
    }
    challenge.UsedAt = DateTime.UtcNow;
    user.IsActive = true;
    ActivityRecorder.Add(dbContext, user.Id, "account.verified", "E-posta doğrulandı", "ORBIT hesabınız etkinleştirildi.");
    await dbContext.SaveChangesAsync();
    return Results.Ok(new { message = "E-posta doğrulandı. Giriş yapabilirsiniz." });
}).WithName("VerifyRegistration").WithOpenApi();

app.MapPost("/api/auth/registration/resend", async (ResendRegistrationCodeRequest request, AppDbContext dbContext, IPasswordResetCodeSender sender, PasswordResetSecurity security) =>
{
    if (!sender.IsAvailable) return Results.Json(new { message = "E-posta gönderim hizmeti henüz yapılandırılmadı." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email && !x.IsActive);
    if (user is not null)
    {
        var latest = await dbContext.RegistrationVerificationChallenges.Where(x => x.UserId == user.Id).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (latest is not null && latest.CreatedAt > DateTime.UtcNow - VerificationCodePolicy.ResendCooldown)
            return Results.Json(new { message = "Yeni kod istemeden önce bekleyin.", retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((latest.CreatedAt.AddMinutes(1) - DateTime.UtcNow).TotalSeconds)) }, statusCode: StatusCodes.Status429TooManyRequests);
        var code = security.GenerateCode();
        dbContext.RegistrationVerificationChallenges.Add(new RegistrationVerificationChallenge { UserId = user.Id, CodeHash = security.Hash(code), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow + VerificationCodePolicy.Lifetime });
        await dbContext.SaveChangesAsync();
        await sender.SendAsync(user.Email, code, EmailCodePurpose.RegistrationVerification);
    }
    return Results.Accepted(value: new { message = "Hesap doğrulanmayı bekliyorsa yeni kod gönderildi.", retryAfterSeconds = 60, expiresInSeconds = 600 });
}).WithName("ResendRegistrationCode").WithOpenApi();

app.MapGet("/api/auth/session", async (ClaimsPrincipal principal, AppDbContext dbContext) =>
{
    var userId = int.Parse(principal.FindFirstValue("userId")!);
    var sessionId = Guid.Parse(principal.FindFirstValue("sessionId")!);
    var result = await dbContext.UserSessions
        .Where(session => session.Id == sessionId && session.UserId == userId)
        .Select(session => new
        {
            id = session.User!.Id,
            name = session.User.Name,
            email = session.User.Email,
            sessionId = session.Id,
            deviceId = session.DeviceId,
            rememberMe = session.RememberMe,
            expiresAt = session.ExpiresAt
        })
        .SingleAsync();
    return Results.Ok(result);
})
.WithName("GetSession")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/api/auth/password-reset/request", async (
    RequestPasswordResetRequest request,
    AppDbContext dbContext,
    IPasswordResetCodeSender sender,
    PasswordResetSecurity security) =>
{
    if (!sender.IsAvailable)
    {
        return Results.Json(
            new { message = "E-posta gönderim hizmeti henüz yapılandırılmadı." },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    var user = await dbContext.Users.FirstOrDefaultAsync(candidate =>
        candidate.Email.ToLower() == normalizedEmail && candidate.IsActive);
    if (user is not null)
    {
        var latest = await dbContext.PasswordResetChallenges
            .Where(challenge => challenge.UserId == user.Id)
            .OrderByDescending(challenge => challenge.CreatedAt)
            .FirstOrDefaultAsync();
        if (latest is null || latest.CreatedAt <= DateTime.UtcNow.AddMinutes(-1))
        {
            var code = security.GenerateCode();
            dbContext.PasswordResetChallenges.Add(new PasswordResetChallenge
            {
                UserId = user.Id,
                CodeHash = security.Hash(code),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            });
            await dbContext.SaveChangesAsync();
            await sender.SendAsync(user.Email, code);
        }
    }

    return Results.Accepted(value: new
    {
        message = "Hesap varsa doğrulama kodu e-posta adresine gönderildi.",
        retryAfterSeconds = 60,
        expiresInSeconds = 600
    });
})
.WithName("RequestPasswordReset")
.WithOpenApi();

app.MapPost("/api/auth/password-reset/verify", async (
    VerifyPasswordResetCodeRequest request,
    AppDbContext dbContext,
    PasswordResetSecurity security) =>
{
    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    var challenge = await dbContext.PasswordResetChallenges
        .Where(item =>
            item.User != null &&
            item.User.Email.ToLower() == normalizedEmail &&
            item.UsedAt == null &&
            item.CodeVerifiedAt == null)
        .OrderByDescending(item => item.CreatedAt)
        .FirstOrDefaultAsync();

    if (challenge is null || challenge.FailedAttempts >= 5)
    {
        return Results.BadRequest(new { message = "Kod geçersiz veya kullanım süresi dolmuş." });
    }
    if (challenge.ExpiresAt <= DateTime.UtcNow)
    {
        return Results.Json(
            new { message = "Kodun kullanım süresi dolmuş. Yeni kod isteyin." },
            statusCode: StatusCodes.Status410Gone);
    }

    challenge.FailedAttempts++;
    if (!security.Verify(request.Code, challenge.CodeHash))
    {
        await dbContext.SaveChangesAsync();
        return Results.BadRequest(new
        {
            message = "Kod geçersiz.",
            remainingAttempts = Math.Max(0, 5 - challenge.FailedAttempts)
        });
    }

    var resetToken = security.GenerateToken();
    challenge.CodeVerifiedAt = DateTime.UtcNow;
    challenge.ResetTokenHash = security.Hash(resetToken);
    challenge.ResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(10);
    await dbContext.SaveChangesAsync();
    return Results.Ok(new { resetToken, expiresInSeconds = 600 });
})
.WithName("VerifyPasswordResetCode")
.WithOpenApi();

app.MapPost("/api/auth/password-reset/complete", async (
    CompletePasswordResetRequest request,
    AppDbContext dbContext,
    PasswordResetSecurity security) =>
{
    if (request.NewPassword.Length < 8)
    {
        return Results.BadRequest(new { message = "Parola en az 8 karakter olmalıdır." });
    }

    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    var challenge = await dbContext.PasswordResetChallenges
        .Include(item => item.User)
        .Where(item =>
            item.User != null &&
            item.User.Email.ToLower() == normalizedEmail &&
            item.UsedAt == null &&
            item.CodeVerifiedAt != null &&
            item.ResetTokenHash != null)
        .OrderByDescending(item => item.CreatedAt)
        .FirstOrDefaultAsync();
    if (challenge is null ||
        challenge.ResetTokenExpiresAt <= DateTime.UtcNow ||
        !security.Verify(request.ResetToken, challenge.ResetTokenHash!))
    {
        return Results.BadRequest(new { message = "Sıfırlama oturumu geçersiz veya süresi dolmuş." });
    }

    challenge.User!.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
    challenge.UsedAt = DateTime.UtcNow;
    ActivityRecorder.Add(dbContext, challenge.User.Id, "security.password_changed", "Parola değiştirildi", "Hesap parolanız sıfırlama akışıyla değiştirildi.");
    await dbContext.UserSessions
        .Where(session => session.UserId == challenge.UserId && session.RevokedAt == null)
        .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, DateTime.UtcNow));
    await dbContext.SaveChangesAsync();
    return Results.Ok(new { message = "Parola başarıyla güncellendi. Yeniden giriş yapabilirsiniz." });
})
.WithName("CompletePasswordReset")
.WithOpenApi();

app.Run();
