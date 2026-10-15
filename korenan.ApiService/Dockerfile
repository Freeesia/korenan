FROM node:24-slim AS client
WORKDIR /src/korenan.react.client
COPY korenan.react.client/package.json korenan.react.client/package-lock.json ./
RUN npm ci
COPY korenan.react.client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY korenan.ServiceDefaults/korenan.ServiceDefaults.csproj korenan.ServiceDefaults/
COPY korenan.ApiService/korenan.ApiService.csproj korenan.ApiService/
RUN dotnet restore korenan.ApiService/korenan.ApiService.csproj
COPY korenan.ServiceDefaults/ korenan.ServiceDefaults/
COPY korenan.ApiService/ korenan.ApiService/
COPY --from=client /src/korenan.react.client/dist korenan.ApiService/wwwroot/
RUN dotnet publish korenan.ApiService/korenan.ApiService.csproj -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER $APP_UID
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "korenan.ApiService.dll"]
