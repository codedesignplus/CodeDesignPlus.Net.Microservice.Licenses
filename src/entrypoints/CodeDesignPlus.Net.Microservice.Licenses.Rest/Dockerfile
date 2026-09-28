FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["nuget.config", "nuget.config"]
COPY ["src/domain/CodeDesignPlus.Net.Microservice.Licenses.Application/CodeDesignPlus.Net.Microservice.Licenses.Application.csproj", "domain/CodeDesignPlus.Net.Microservice.Licenses.Application/"]
COPY ["src/domain/CodeDesignPlus.Net.Microservice.Licenses.Domain/CodeDesignPlus.Net.Microservice.Licenses.Domain.csproj", "domain/CodeDesignPlus.Net.Microservice.Licenses.Domain/"]
COPY ["src/domain/CodeDesignPlus.Net.Microservice.Licenses.Infrastructure/CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.csproj", "domain/CodeDesignPlus.Net.Microservice.Licenses.Infrastructure/"]
COPY ["src/entrypoints/CodeDesignPlus.Net.Microservice.Licenses.Rest/CodeDesignPlus.Net.Microservice.Licenses.Rest.csproj", "entrypoints/CodeDesignPlus.Net.Microservice.Licenses.Rest/"]
RUN dotnet restore "./entrypoints/CodeDesignPlus.Net.Microservice.Licenses.Rest/CodeDesignPlus.Net.Microservice.Licenses.Rest.csproj" /p:PublishReadyToRun=true
COPY ["src/domain/", "domain/"]
COPY ["src/entrypoints/CodeDesignPlus.Net.Microservice.Licenses.Rest/", "entrypoints/CodeDesignPlus.Net.Microservice.Licenses.Rest/"]

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN dotnet publish "./entrypoints/CodeDesignPlus.Net.Microservice.Licenses.Rest/CodeDesignPlus.Net.Microservice.Licenses.Rest.csproj" \
    -c "$BUILD_CONFIGURATION" \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app
EXPOSE 5000
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_GCDynamicAdaptationMode=1 \
    DOTNET_GCConserveMemory=5 \
    DOTNET_GCHeapHardLimitPercent=70
USER app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CodeDesignPlus.Net.Microservice.Licenses.Rest.dll"]
