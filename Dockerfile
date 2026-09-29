# Use the official Microsoft .NET SDK image to build the project
FROM ://microsoft.com AS build
WORKDIR /src

# Copy the solution file and all .csproj files to restore dependencies first (avoids rebuilding layers unnecessarily)
COPY EduPlatform.sln ./
COPY src/Edu.Web/Edu.Web.csproj ./src/Edu.Web/
COPY src/Edu.Application/Edu.Application.csproj ./src/Edu.Application/
COPY src/Edu.Contracts/Edu.Contracts.csproj ./src/Edu.Contracts/
COPY src/Edu.Domain/Edu.Domain.csproj ./src/Edu.Domain/
COPY src/Edu.Infrastructure/Edu.Infrastructure.csproj ./src/Edu.Infrastructure/

# Restore all projects in the solution
RUN dotnet restore EduPlatform.sln

# Copy the rest of the source code code into the compiler container
COPY src/ ./src/

# Compile and publish optimized production artifacts
WORKDIR /src/src/Edu.Web
RUN dotnet publish Edu.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# Use the lighter ASP.NET runtime image for production execution
FROM ://microsoft.com AS final
WORKDIR /app
COPY --from=build /app/publish .

# Define the execution trigger pointing directly to your web entry point assembly
ENTRYPOINT ["dotnet", "Edu.Web.dll"]

