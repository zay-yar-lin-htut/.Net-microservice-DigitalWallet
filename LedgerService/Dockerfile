FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ENV PATH="/root/.dotnet/tools:${PATH}"

RUN dotnet tool install --global dotnet-ef --version 8.0.31

WORKDIR /src

COPY LedgerService/LedgerService.csproj LedgerService/

RUN dotnet restore LedgerService/LedgerService.csproj

COPY LedgerService/ LedgerService/

WORKDIR /src/LedgerService

RUN dotnet publish LedgerService.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8060

EXPOSE 8060

ENTRYPOINT ["dotnet", "LedgerService.dll"]
