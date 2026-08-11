FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY EbookParseService.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5082
EXPOSE 5082
ENTRYPOINT ["dotnet", "EbookParseService.Api.dll"]
