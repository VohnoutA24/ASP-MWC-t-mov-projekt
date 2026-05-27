# Multi-stage build for .NET 10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["sum.csproj", "./"]
RUN dotnet restore "sum.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "sum.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .


ENTRYPOINT ["dotnet", "sum.dll"]
