FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["GLMS.Core/GLMS.Core.csproj", "GLMS.Core/"]
COPY ["GLMS.Api/GLMS.Api.csproj", "GLMS.Api/"]
RUN dotnet restore "GLMS.Api/GLMS.Api.csproj"
COPY . .
WORKDIR /src/GLMS.Api
RUN dotnet publish "GLMS.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GLMS.Api.dll"]
