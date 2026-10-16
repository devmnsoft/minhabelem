FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props MINHABELEM.SQL ./
COPY src/MinhaBelem.Core/MinhaBelem.Core.csproj src/MinhaBelem.Core/
COPY src/MinhaBelem.Infrastructure/MinhaBelem.Infrastructure.csproj src/MinhaBelem.Infrastructure/
COPY src/MinhaBelem.Web/MinhaBelem.Web.csproj src/MinhaBelem.Web/
RUN dotnet restore src/MinhaBelem.Web/MinhaBelem.Web.csproj
COPY src/ src/
RUN dotnet publish src/MinhaBelem.Web/MinhaBelem.Web.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","MinhaBelem.Web.dll"]
