# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy solution and project files
COPY EduPlatform.sln ./
COPY src/Edu.Web/Edu.Web.csproj ./src/Edu.Web/
COPY src/Edu.Application/Edu.Application.csproj ./src/Edu.Application/
COPY src/Edu.Contracts/Edu.Contracts.csproj ./src/Edu.Contracts/
COPY src/Edu.Domain/Edu.Domain.csproj ./src/Edu.Domain/
COPY src/Edu.Infrastructure/Edu.Infrastructure.csproj ./src/Edu.Infrastructure/

# Restore dependencies
RUN dotnet restore EduPlatform.sln

# Copy source code
COPY src/ ./src/

# Publish Web project
WORKDIR /src/src/Edu.Web
RUN dotnet publish Edu.Web.csproj -c Release -o /app/publish /p:UseAppHost=false


# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Edu.Web.dll"]



