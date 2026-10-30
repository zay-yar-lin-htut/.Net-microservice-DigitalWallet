FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY GateWayAPI/GateWayAPI.csproj GateWayAPI/

RUN dotnet restore GateWayAPI/GateWayAPI.csproj

COPY GateWayAPI/ GateWayAPI/

WORKDIR /src/GateWayAPI

RUN dotnet publish GateWayAPI.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "GateWayAPI.dll"]
