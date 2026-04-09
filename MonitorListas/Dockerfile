# ---- STAGE 1: Build Frontend (React/Vite) ----
FROM node:lts AS build-node
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

# ---- STAGE 3: Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:6.0-bullseye-slim
WORKDIR /app

# Instala ferramentas essenciais e o cliente sqlite3 para debugar o banco
RUN apt-get update \
    && apt-get install -y openssl ca-certificates sqlite3 \
    && rm -rf /var/lib/apt/lists/*

# Ajuste de TLS para SQL Server legado (SECLEVEL 0)
RUN sed -i 's/@SECLEVEL=2/@SECLEVEL=0/g' /etc/ssl/openssl.cnf \
    && sed -i 's/MinProtocol = TLSv1.2/MinProtocol = TLSv1/g' /etc/ssl/openssl.cnf || true

# Copia os artefatos compilados
COPY --from=build-dotnet /app/publish .
COPY --from=build-node /app/frontend/dist ./wwwroot

# Criação de pastas e permissão de escrita para volumes e SQLite
RUN mkdir -p /app/xml_origem /app/Quarentena_XML /app/dados /app/config_advice \
    && chmod -R 777 /app/Quarentena_XML /app/dados /app/xml_origem

EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "MonitorListas.Server.dll"]