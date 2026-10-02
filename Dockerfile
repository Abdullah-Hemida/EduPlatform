# 🟢 CRUCIAL FIX: Ensure it says ://microsoft.com
FROM ://microsoft.com AS build
WORKDIR /src

# Copy the solution file and all .csproj files to restore dependencies
COPY EduPlatform.sln ./
COPY src/Edu.Web/Edu.Web.csproj ./src/Edu.Web/
COPY src/Edu.Application/Edu.Application.csproj ./src/Edu.Application/
COPY src/Edu.Contracts/Edu.Contracts.csproj ./src/Edu.Contracts/
COPY src/Edu.Domain/Edu.Domain.csproj ./src/Edu.Domain/
COPY src/Edu.Infrastructure/Edu.Infrastructure.csproj ./src/Edu.Infrastructure/

RUN dotnet restore EduPlatform.sln
COPY src/ ./src/

WORKDIR /src/src/Edu.Web
RUN dotnet publish Edu.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# 🟢 CRUCIAL FIX: Ensure it says ://microsoft.com
FROM ://microsoft.com AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Edu.Web.dll"]

