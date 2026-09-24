# syntax=docker/dockerfile:1

# One image = API + web. The React build is copied into the API's wwwroot and served by ASP.NET Core,
# so the browser talks to a single origin (no CORS, auth cookie works as first-party).

ARG DOTNET_VERSION=10.0
ARG NODE_VERSION=24

# ---------- web: build the React/Vite frontend ----------
FROM node:${NODE_VERSION}-alpine AS web
WORKDIR /src/web
COPY src/FinPlanner.Web/package.json src/FinPlanner.Web/package-lock.json ./
RUN npm ci
COPY src/FinPlanner.Web/ ./
# Same-origin API: the SPA is served by the API itself.
ARG VITE_API_URL=/api
ENV VITE_API_URL=$VITE_API_URL
RUN npm run lint && npm run build

# ---------- build: restore + compile the .NET solution ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY FinPlanner.slnx ./
COPY src/FinPlanner.Api/FinPlanner.Api.csproj src/FinPlanner.Api/
COPY src/FinPlanner.Api.Tests/FinPlanner.Api.Tests.csproj src/FinPlanner.Api.Tests/
RUN dotnet restore FinPlanner.slnx
COPY src/FinPlanner.Api/ src/FinPlanner.Api/
COPY src/FinPlanner.Api.Tests/ src/FinPlanner.Api.Tests/
RUN dotnet build FinPlanner.slnx -c Release --no-restore

# ---------- test: run with `docker build --target test` ----------
FROM build AS test
RUN dotnet test FinPlanner.slnx -c Release --no-build --logger "trx;LogFileName=test-results.trx" --results-directory /testresults

# ---------- publish: API binaries + EF Core migrations bundle ----------
FROM build AS publish
ARG EF_TOOL_VERSION=10.0.11
RUN dotnet tool install --global dotnet-ef --version ${EF_TOOL_VERSION}
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet publish src/FinPlanner.Api/FinPlanner.Api.csproj -c Release --no-build -o /app/publish \
    && dotnet ef migrations bundle \
        --project src/FinPlanner.Api/FinPlanner.Api.csproj \
        --configuration Release \
        --output /app/publish/efbundle \
        --force
COPY --from=web /src/web/dist /app/publish/wwwroot

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

ARG APP_VERSION=dev
ARG BUILD_NUMBER=0
ARG VCS_REF=unknown
ARG BUILD_DATE=unknown

LABEL org.opencontainers.image.title="finplanner" \
      org.opencontainers.image.description="Family financial planning (API + web)" \
      org.opencontainers.image.version="$APP_VERSION" \
      org.opencontainers.image.revision="$VCS_REF" \
      org.opencontainers.image.created="$BUILD_DATE" \
      org.opencontainers.image.source="https://github.com/dtalajka/finplanner3" \
      com.finplanner.build.number="$BUILD_NUMBER"

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    APP_VERSION="$APP_VERSION" \
    APP_BUILD_NUMBER="$BUILD_NUMBER" \
    APP_VCS_REF="$VCS_REF" \
    APP_BUILD_DATE="$BUILD_DATE"

COPY --from=publish /app/publish ./

# Non-root user provided by the official .NET images (UID 1654).
USER $APP_UID
EXPOSE 8080

# Connection string comes from env: ConnectionStrings__DefaultConnection
# Migrations (e.g. k8s initContainer / Job): ["/app/efbundle", "--connection", "$(ConnectionStrings__DefaultConnection)"]
ENTRYPOINT ["dotnet", "FinPlanner.Api.dll"]
