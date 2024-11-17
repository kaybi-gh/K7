FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App
# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o /k7


# Build runtime image
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /k7
COPY --from=build-env /k7 .
ENTRYPOINT ["dotnet", "K7.Server.Web.dll", "--init-db"]