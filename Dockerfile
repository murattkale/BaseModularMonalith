# =============================================================================
# BUILD STAGE
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy csproj files and restore
# Copy csproj files and restore
# Optimized layer caching: Copy all csproj files keeping directory structure
COPY Directory.Build.props ./
COPY src/Api/Api.csproj ./src/Api/
COPY src/BuildingBlocks/BuildingBlocks.csproj ./src/BuildingBlocks/
COPY src/SharedKernel/SharedKernel.csproj ./src/SharedKernel/
COPY src/Modules/User/Domain/User.Domain.csproj ./src/Modules/User/Domain/
COPY src/Modules/User/Application/User.Application.csproj ./src/Modules/User/Application/
COPY src/Modules/User/Infrastructure/User.Infrastructure.csproj ./src/Modules/User/Infrastructure/

RUN dotnet restore src/Api/Api.csproj

# Copy everything else and build
COPY src ./src
RUN dotnet publish src/Api/Api.csproj -c Release -o out /p:UseAppHost=false --no-restore

# =============================================================================
# RUN STAGE
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

# Güvenlik: Non-root user
USER $APP_UID

# Environment variables
# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_EnableDiagnostics=0
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Api.dll"]
