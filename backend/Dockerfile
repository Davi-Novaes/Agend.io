# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props Agendio.slnx migrate.sh ./
COPY src/ src/

RUN dotnet restore src/host/Agendio.Api/Agendio.Api.csproj
RUN dotnet publish src/host/Agendio.Api/Agendio.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN useradd --uid 10001 --user-group --create-home --shell /usr/sbin/nologin agendio \
    && mkdir -p /app/uploads \
    && chown -R agendio:agendio /app

COPY --from=build --chown=agendio:agendio /app/publish .

USER agendio
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Agendio.Api.dll"]
