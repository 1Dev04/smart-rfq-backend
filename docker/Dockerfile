FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 10000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SmartRFQ.API/SmartRFQ.API.csproj", "SmartRFQ.API/"]
RUN dotnet restore "SmartRFQ.API/SmartRFQ.API.csproj"
COPY SmartRFQ.API/ SmartRFQ.API/
WORKDIR "/src/SmartRFQ.API"
RUN dotnet build "SmartRFQ.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SmartRFQ.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENTRYPOINT ["dotnet", "SmartRFQ.API.dll"]