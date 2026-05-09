FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY eCommerceApi/eCommerceApi.csproj eCommerceApi/
RUN dotnet restore eCommerceApi/eCommerceApi.csproj
COPY eCommerceApi/ eCommerceApi/
RUN dotnet publish eCommerceApi/eCommerceApi.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "eCommerceApi.dll"]
