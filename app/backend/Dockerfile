# syntax=docker/dockerfile:1

# Build stage runs on the build host's native platform (fast on Apple Silicon); the output is
# portable IL, so it runs on whatever platform the runtime stage targets (e.g. linux/amd64).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/GymNote.Api/GymNote.Api.csproj src/GymNote.Api/
RUN dotnet restore src/GymNote.Api/GymNote.Api.csproj
COPY src/ src/
RUN dotnet publish src/GymNote.Api/GymNote.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
# Listen address is controlled by ASPNETCORE_URLS; clear the base image's HTTP_PORTS to avoid a conflict warning.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_HTTP_PORTS=
EXPOSE 8080
# Built-in non-root "app" user shipped with the official .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "GymNote.Api.dll"]
