# Stage 1 Builder
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Shared.Contracts/ Shared.Contracts/
COPY src/Shared.Security/ Shared.Security/
COPY src/IdentityAPI/KitchenOrchestrator.IdentityAPI.csproj IdentityAPI/
RUN dotnet restore IdentityAPI/KitchenOrchestrator.IdentityAPI.csproj
COPY src/IdentityAPI/ IdentityAPI/
RUN dotnet publish IdentityAPI/KitchenOrchestrator.IdentityAPI.csproj -c Release -o /app/publish

# Stage 2 Runner
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "KitchenOrchestrator.IdentityAPI.dll"]