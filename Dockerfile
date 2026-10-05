# Render no tiene runtime nativo para .NET — este Dockerfile es el único
# artefacto de despliegue del backend (un único servicio, no microservicios).
# Ver docs/DEPLOYMENT.md para el resto del setup (env vars, ambientes).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BoxService_BackEnd.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish BoxService_BackEnd.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# La imagen base viene recortada al mínimo y no trae libgssapi_krb5 — lo
# pide la pila de TLS de Npgsql/Google.Apis.Auth al conectar a Supabase o
# validar tokens de Google, y sin esto el proceso ni arranca
# ("libgssapi_krb5.so.2: cannot open shared object file").
RUN apt-get update \
    && apt-get install -y --no-install-recommends libkrb5-3 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Render setea PORT en runtime; Program.cs lo lee y escucha en 0.0.0.0:$PORT.
# El valor acá es solo el default para correr la imagen fuera de Render.
ENV PORT=10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "BoxService_BackEnd.dll"]
