# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY LeadManagementPortal.sln ./
COPY LeadManagementPortal/LeadManagementPortal.csproj LeadManagementPortal/
COPY LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj LeadManagementPortal.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish LeadManagementPortal/LeadManagementPortal.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./
COPY .render/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["/entrypoint.sh"]
