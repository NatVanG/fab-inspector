# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["FabInspector.CLI/FabInspector.CLI.csproj", "FabInspector.CLI/"]
COPY ["PBIRInspectorImageLibrary/PBIRInspectorImageLibrary.csproj", "PBIRInspectorImageLibrary/"]
COPY ["PBIRInspectorLibrary/PBIRInspectorLibrary.csproj", "PBIRInspectorLibrary/"]
COPY ["PBIRInspectorClientLibrary/PBIRInspectorClientLibrary.csproj", "PBIRInspectorClientLibrary/"]
COPY ["FabInspector.Operators/FabInspector.Operators.csproj", "FabInspector.Operators/"]
COPY ["Ric.Operators/Ric.Operators.csproj", "Ric.Operators/"]

RUN dotnet restore "./FabInspector.CLI/FabInspector.CLI.csproj"
COPY . .
WORKDIR "/src/FabInspector.CLI"
RUN dotnet build "./FabInspector.CLI.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FabInspector.CLI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app

# Install Liberation Sans font
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends fonts-liberation && \
    rm -rf /var/lib/apt/lists/*
USER $APP_UID

COPY --from=publish /app/publish .
COPY PBIRInspectorClientLibrary/Files ./Files
ENTRYPOINT ["dotnet", "fab-inspector.dll"]