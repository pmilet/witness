FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY src/Witness.Domain/Witness.Domain.csproj src/Witness.Domain/
COPY src/Witness.Application/Witness.Application.csproj src/Witness.Application/
COPY src/Witness.Infrastructure/Witness.Infrastructure.csproj src/Witness.Infrastructure/
COPY src/Witness.AspNetCore/Witness.AspNetCore.csproj src/Witness.AspNetCore/
COPY demo/Witness.DemoApi/Witness.DemoApi.csproj demo/Witness.DemoApi/
RUN dotnet restore demo/Witness.DemoApi/Witness.DemoApi.csproj

# Copy everything and publish
COPY src/ src/
COPY demo/Witness.DemoApi/ demo/Witness.DemoApi/
RUN dotnet publish demo/Witness.DemoApi/Witness.DemoApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Witness.DemoApi.dll"]
