FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ENV PATH="/root/.dotnet/tools:${PATH}"

RUN dotnet tool install --global dotnet-ef --version 8.0.31

WORKDIR /src

COPY IdentityService/IdentityService.csproj IdentityService/

RUN dotnet restore IdentityService/IdentityService.csproj

COPY IdentityService/ IdentityService/

WORKDIR /src/IdentityService

RUN dotnet publish IdentityService.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8070

EXPOSE 8070

ENTRYPOINT ["dotnet", "IdentityService.dll"]
