FROM node:24-alpine AS frontend
WORKDIR /src/SmartDocumentProcessingSystemFrontend
COPY SmartDocumentProcessingSystemFrontend/package*.json ./
RUN npm ci
COPY SmartDocumentProcessingSystemFrontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend
WORKDIR /src
COPY SmartDocumentProcessingSystemBackend/SmartDocumentProcessingSystem.csproj SmartDocumentProcessingSystemBackend/
RUN dotnet restore SmartDocumentProcessingSystemBackend/SmartDocumentProcessingSystem.csproj
COPY SmartDocumentProcessingSystemBackend/ SmartDocumentProcessingSystemBackend/
RUN dotnet publish SmartDocumentProcessingSystemBackend/SmartDocumentProcessingSystem.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=backend /app/publish .
COPY --from=frontend /src/SmartDocumentProcessingSystemFrontend/dist/SmartDocumentProcessingSystemFrontend/browser ./wwwroot
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet SmartDocumentProcessingSystem.dll"]
