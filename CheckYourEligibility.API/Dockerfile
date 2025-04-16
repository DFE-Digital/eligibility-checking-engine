# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CheckYourEligibility.API/CheckYourEligibility.API.csproj", "CheckYourEligibility.API/"]
RUN dotnet restore "./CheckYourEligibility.API/CheckYourEligibility.API.csproj"
COPY . .
WORKDIR "/src/CheckYourEligibility.API"
RUN dotnet build "./CheckYourEligibility.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CheckYourEligibility.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final

ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    ca-certificates \
    libicu-dev && \
    update-ca-certificates && \
    rm -rf /var/lib/apt/lists/*
#RUN apk add --no-cache \
#    icu-data-full \
#    icu-libs
USER $APP_UID

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CheckYourEligibility.API.dll"]