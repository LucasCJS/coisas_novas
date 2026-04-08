# Dockerfile

# ---- STAGE 1: Build Frontend (React/Vite) ----
FROM node:18 AS build-node
WORKDIR /app/frontend
COPY monitorlistas.client/package*.json ./
RUN npm install
COPY monitorlistas.client/ ./
RUN npm run build

# ---- STAGE 2: Build Backend (.NET API) ----
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-dotnet
WORKDIR /app/backend
COPY MonitorListas.Server/*.csproj ./MonitorListas.Server/
RUN dotnet restore MonitorListas.Server/MonitorListas.Server.csproj
COPY MonitorListas.Server/ ./MonitorListas.Server/
RUN dotnet publish MonitorListas.Server/MonitorListas.Server.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- STAGE 3: Runtime (Debian-based) ----
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app

# ---- OPENSSL LEGACY CONFIGURATION ----
# Habilita TLSv1.0 e reduz SECLEVEL para permitir conexão com instâncias 
# legadas do SQL Server (bypass idêntico ao utilizado na imagem laudio/pyodbc).
RUN echo "openssl_conf = openssl_init" > /etc/ssl/openssl_legacy.cnf \
    && echo "[openssl_init]" >> /etc/ssl/openssl_legacy.cnf \
    && echo "providers = provider_sect" >> /etc/ssl/openssl_legacy.cnf \
    && echo "ssl_conf = ssl_sect" >> /etc/ssl/openssl_legacy.cnf \
    && echo "[provider_sect]" >> /etc/ssl/openssl_legacy.cnf \
    && echo "default = default_sect" >> /etc/ssl/openssl_legacy.cnf \
    && echo "legacy = legacy_sect" >> /etc/ssl/openssl_legacy.cnf \
    && echo "[default_sect]" >> /etc/ssl/openssl_legacy.cnf \
    && echo "activate = 1" >> /etc/ssl/openssl_legacy.cnf \
    && echo "[legacy_sect]" >> /etc/ssl/openssl_legacy.cnf \
    && echo "activate = 0" >> /etc/ssl/openssl_legacy.cnf \
    && echo "[ssl_sect]" >> /etc/ssl/openssl_legacy.cnf \
    && echo "system_default = system_default_sect" >> /etc/ssl/openssl_legacy.cnf \
    && echo "[system_default_sect]" >> /etc/ssl/openssl_legacy.cnf \
    && echo "MinProtocol = TLSv1.0" >> /etc/ssl/openssl_legacy.cnf \
    && echo "CipherString = DEFAULT@SECLEVEL=0" >> /etc/ssl/openssl_legacy.cnf

ENV OPENSSL_CONF=/etc/ssl/openssl_legacy.cnf
# --------------------------------------

# Cópia dos artefatos compilados das etapas anteriores
COPY --from=build-dotnet /app/publish .
COPY --from=build-node /app/frontend/dist ./wwwroot

# Criação dos diretórios estruturais para injeção de volumes via Docker Compose
RUN mkdir -p /app/xml_origem \
    && mkdir -p /app/Quarentena_XML \
    && mkdir -p /app/dados \
    && mkdir -p /app/config_advice

EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "MonitorListas.Server.dll"]