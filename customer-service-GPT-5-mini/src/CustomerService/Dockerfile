FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet restore "CustomerService.csproj"
RUN dotnet publish "CustomerService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./
# Use non-root user for security
RUN useradd -m appuser && chown -R appuser /app
USER appuser
ENTRYPOINT ["dotnet", "CustomerService.dll"]
