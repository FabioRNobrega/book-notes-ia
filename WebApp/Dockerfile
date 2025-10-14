# --------------------------
# Stage 1 - Build environment
# --------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
# Restore dependencies
RUN dotnet restore

# --------------------------
# Stage 2 - Development runtime
# --------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0
WORKDIR /app
# Copy restored source
COPY --from=build /src .
# ASP.NET URLs
ENV ASPNETCORE_URLS=http://+:8080
# Enable hot reload and watch
CMD ["dotnet", "watch", "run", "--urls=http://0.0.0.0:8080"]
