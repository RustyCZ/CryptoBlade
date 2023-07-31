#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
RUN apt-get update && apt-get install -y curl
WORKDIR /app
EXPOSE 80
HEALTHCHECK CMD curl --fail http://localhost:80/healthz

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["CryptoBlade/CryptoBlade.csproj", "CryptoBlade/"]
RUN dotnet restore "CryptoBlade/CryptoBlade.csproj"
COPY . .
WORKDIR "/src/CryptoBlade"
RUN dotnet build "CryptoBlade.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CryptoBlade.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CryptoBlade.dll"]