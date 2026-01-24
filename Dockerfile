FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files to leverage layer caching
COPY ["src/Product.Api/Product.Api.csproj", "Product.Api/"]
COPY ["src/Product.Business/Product.Business.csproj", "Product.Business/"]
COPY ["src/Product.Common/Product.Common.csproj", "Product.Common/"]

RUN dotnet restore "Product.Api/Product.Api.csproj"

# Copy source files from src folder
COPY src/. .
WORKDIR /src/Product.Api
RUN dotnet publish "Product.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "Product.Api.dll"]
