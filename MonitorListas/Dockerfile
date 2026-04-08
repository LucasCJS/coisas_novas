# Dockerfile

# ---- STAGE 1 Build Frontend (ReactVite) ----
FROM node18 AS build-node
WORKDIR appfrontend
COPY monitorlistas.clientpackage.json .
RUN npm install
COPY monitorlistas.client .
RUN npm run build

# ---- STAGE 2 Build Backend (.NET API) ----
FROM mcr.microsoft.comdotnetsdk6.0 AS build-dotnet
WORKDIR appbackend
COPY MonitorListas.Server.csproj .MonitorListas.Server
RUN dotnet restore MonitorListas.ServerMonitorListas.Server.csproj
COPY MonitorListas.Server .MonitorListas.Server
RUN dotnet publish MonitorListas.ServerMonitorListas.Server.csproj -c Release -o apppublish pUseAppHost=false

# ---- STAGE 3 Runtime (Debian-based) ----
FROM mcr.microsoft.comdotnetaspnet6.0
WORKDIR app

# ---- OPENSSL LEGACY CONFIGURATION ----
# Habilita TLSv1.0 e reduz SECLEVEL para permitir conexão com instâncias 
# legadas do SQL Server (bypass idêntico ao utilizado na imagem laudiopyodbc).
RUN cat  etcsslopenssl_legacy.cnf 'EOF'
openssl_conf = openssl_init

[openssl_init]
providers = provider_sect
ssl_conf = ssl_sect

[provider_sect]
default = default_sect
legacy  = legacy_sect

[default_sect]
activate = 1

[legacy_sect]
activate = 0

[ssl_sect]
system_default = system_default_sect

[system_default_sect]
MinProtocol = TLSv1.0
CipherString = DEFAULT@SECLEVEL=0
EOF

ENV OPENSSL_CONF=etcsslopenssl_legacy.cnf
# --------------------------------------

# Cópia dos artefatos compilados das etapas anteriores
COPY --from=build-dotnet apppublish .
COPY --from=build-node appfrontenddist .wwwroot

# Criação dos diretórios estruturais para injeção de volumes via Docker Compose
RUN mkdir -p appxml_origem 
    && mkdir -p appQuarentena_XML 
    && mkdir -p appdados 
    && mkdir -p appconfig_advice

EXPOSE 80
ENV ASPNETCORE_URLS=http+80

ENTRYPOINT [dotnet, MonitorListas.Server.dll]