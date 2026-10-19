FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Rollcall.slnx ./
COPY src/ src/
RUN dotnet publish src/Rollcall.Web/Rollcall.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeysPath=/app/data-protection-keys

EXPOSE 8080

COPY --from=build /app/publish .

RUN mkdir -p /app/data-protection-keys \
    && chown -R "$APP_UID":"$APP_UID" /app/data-protection-keys

USER $APP_UID

ENTRYPOINT ["dotnet", "Rollcall.Web.dll"]
