FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY service-control-metric-exporter.csproj .
RUN dotnet restore service-control-metric-exporter.csproj
COPY . .
RUN dotnet build service-control-metric-exporter.csproj -c Release -o /app/build
RUN dotnet publish service-control-metric-exporter.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "service-control-metric-exporter.dll"]