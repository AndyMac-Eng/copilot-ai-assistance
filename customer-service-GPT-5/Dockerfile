# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ./src/CustomerService/CustomerService.csproj ./src/CustomerService/
RUN dotnet restore ./src/CustomerService/CustomerService.csproj
COPY . .
RUN dotnet publish ./src/CustomerService/CustomerService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CustomerService.dll"]
