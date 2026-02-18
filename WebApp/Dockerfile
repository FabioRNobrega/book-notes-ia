# --------------------------
# Stage 1 - Build environment
# --------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore

# --------------------------
# Stage 2 - Development runtime
# --------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0
WORKDIR /app
COPY --from=build /src .

# Install EntityFramework tool in the *runtime* image
RUN dotnet tool install --global dotnet-ef --version 9.*
# Install Code Generator tool in the *runtime* image
RUN dotnet tool install --global dotnet-aspnet-codegenerator --version 9.*
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool restore

ENV ASPNETCORE_URLS=http://+:8080
CMD ["dotnet", "watch", "run", "--urls=http://0.0.0.0:8080"]
