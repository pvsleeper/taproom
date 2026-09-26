FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY backend/Taproom.Api/Taproom.Api.csproj backend/Taproom.Api/
RUN dotnet restore backend/Taproom.Api/Taproom.Api.csproj
COPY backend/Taproom.Api/ backend/Taproom.Api/
COPY --from=frontend-build /src/frontend/dist/ backend/Taproom.Api/wwwroot/
RUN dotnet publish backend/Taproom.Api/Taproom.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=backend-build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Taproom.Api.dll"]
