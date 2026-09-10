FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SalesSaaS.csproj", "./"]
RUN dotnet restore "SalesSaaS.csproj"
COPY . .
RUN dotnet publish "SalesSaaS.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "SalesSaaS.dll"]
