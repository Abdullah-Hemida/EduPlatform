# Use the official Microsoft .NET SDK image to build the project
FROM ://microsoft.com AS build
WORKDIR /src

# Copy everything and restore dependencies
COPY . .
RUN dotnet restore

# Build and publish a release optimization runner
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Use the lighter ASP.NET runtime image for production execution
FROM ://microsoft.com AS final
WORKDIR /app
COPY --from=build /app/publish .

# Define the execution trigger (Ensure this matches your actual Web assembly name)
ENTRYPOINT ["dotnet", "Edu.Web.dll"]
