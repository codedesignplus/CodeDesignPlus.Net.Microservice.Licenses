FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/domain/", "domain/"]
COPY ["src/entrypoints/CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker/", "entrypoints/CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker/"]
RUN dotnet restore "./entrypoints/CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker/CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.csproj"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN dotnet publish "./entrypoints/CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker/CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.csproj" \
    -c "$BUILD_CONFIGURATION" \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app
EXPOSE 5002
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_GCDynamicAdaptationMode=1 \
    DOTNET_GCConserveMemory=5 \
    DOTNET_GCHeapHardLimitPercent=70
USER app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.dll"]
