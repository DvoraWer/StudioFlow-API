# StudioFlow API — production image (e.g. Render, Docker runtime).
# Secrets are NOT baked in: supply ConnectionStrings__DefaultConnection, Jwt__Key
# and (optionally) Cors__AllowedOrigins as environment variables at run time.

# ---- build ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (only the project files) so this layer is cached between builds.
COPY StudioFlow.Core/StudioFlow.Core.csproj StudioFlow.Core/
COPY StudioFlow.Data/StudioFlow.Data.csproj StudioFlow.Data/
COPY StudioFlow.Service/StudioFlow.Service.csproj StudioFlow.Service/
COPY StudioFlow.API/StudioFlow.API.csproj StudioFlow.API/
RUN dotnet restore StudioFlow.API/StudioFlow.API.csproj

COPY StudioFlow.Core/ StudioFlow.Core/
COPY StudioFlow.Data/ StudioFlow.Data/
COPY StudioFlow.Service/ StudioFlow.Service/
COPY StudioFlow.API/ StudioFlow.API/
RUN dotnet publish StudioFlow.API/StudioFlow.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Kestrel listens on plain HTTP 8080; the platform terminates TLS in front of it.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# NLog writes to ${basedir}/logs; let the non-root runtime user create files there.
RUN mkdir -p /app/logs && chown $APP_UID /app/logs
USER $APP_UID

ENTRYPOINT ["dotnet", "StudioFlow.API.dll"]
