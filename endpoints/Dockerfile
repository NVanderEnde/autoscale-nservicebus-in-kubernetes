FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-alpha
WORKDIR /src
COPY ["Alpha/Alpha.csproj", "Alpha/"]
RUN dotnet restore "Alpha/Alpha.csproj"
COPY . .
WORKDIR "/src/Alpha"
RUN dotnet build "Alpha.csproj" -c Release -o /app/build
RUN dotnet publish "Alpha.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-beta
WORKDIR /src
COPY ["Beta/Beta.csproj", "Beta/"]
RUN dotnet restore "Beta/Beta.csproj"
COPY . .
WORKDIR "/src/Beta"
RUN dotnet build "Beta.csproj" -c Release -o /app/build
RUN dotnet publish "Beta.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime-alpha
WORKDIR /app
COPY --from=build-alpha /app/publish .
ENTRYPOINT ["dotnet", "Alpha.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime-beta
WORKDIR /app
COPY --from=build-beta /app/publish .
ENTRYPOINT ["dotnet", "Beta.dll"]
