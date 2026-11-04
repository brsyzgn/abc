FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ORBIT.API.csproj packages.lock.json ./
RUN dotnet restore ORBIT.API.csproj --locked-mode
COPY . ./
RUN dotnet publish ORBIT.API.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN addgroup --system orbit && adduser --system --ingroup orbit orbit \
    && mkdir -p /var/lib/orbit-api/aquarium-photos \
    && chown -R orbit:orbit /app /var/lib/orbit-api
COPY --from=build --chown=orbit:orbit /app/publish ./
USER orbit
# The listening port is NOT fixed here: Program.cs binds to Railway's PORT variable when present
# (or to ASPNETCORE_URLS if set explicitly); otherwise the .NET 8 image default (8080) applies.
ENV ASPNETCORE_ENVIRONMENT=Production \
    AquariumPhotos__RootPath=/var/lib/orbit-api/aquarium-photos
EXPOSE 8080
ENTRYPOINT ["dotnet", "ORBIT.API.dll"]
