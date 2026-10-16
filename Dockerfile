FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/MinhaBelem.Web/MinhaBelem.Web.csproj src/MinhaBelem.Web/
RUN dotnet restore src/MinhaBelem.Web/MinhaBelem.Web.csproj
COPY src/MinhaBelem.Web/ src/MinhaBelem.Web/
RUN dotnet publish src/MinhaBelem.Web/MinhaBelem.Web.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","MinhaBelem.Web.dll"]