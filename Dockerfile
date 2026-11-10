# =========================
# Build stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

ENV NUGET_PACKAGES=/root/.nuget/packages
ENV NUGET_FALLBACK_PACKAGES=""

# Copy only project file first
COPY HospitalManagement.Api.csproj ./

# Restore inside Linux container
RUN dotnet restore HospitalManagement.Api.csproj --force

# Copy source code
COPY . .

# Remove any Windows-generated build artifacts
RUN rm -rf ./bin ./obj

# Restore again after source copy
RUN dotnet restore HospitalManagement.Api.csproj --force

# Publish
RUN dotnet publish HospitalManagement.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# =========================
# Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "HospitalManagement.Api.dll"]
