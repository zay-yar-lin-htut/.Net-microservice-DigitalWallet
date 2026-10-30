FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ENV PATH="/root/.dotnet/tools:${PATH}"

RUN dotnet tool install --global dotnet-ef --version 8.0.31

WORKDIR /src

COPY TransferService/TransferService.csproj TransferService/

RUN dotnet restore TransferService/TransferService.csproj

COPY TransferService/ TransferService/

WORKDIR /src/TransferService

RUN dotnet publish TransferService.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8050

EXPOSE 8050

ENTRYPOINT ["dotnet", "TransferService.dll"]
