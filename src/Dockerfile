# Use official .NET SDK for build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /usr/src/app/

# Copy everything into the container
COPY . .

# Ensure Protobuf files are copied to the correct location
COPY ./ /usr/src/app/  

# Restore dependencies
ARG TARGETARCH
RUN dotnet restore ./eShop.AppHost/eShop.AppHost.csproj -r linux-musl-$TARGETARCH

# Publish the application
RUN dotnet publish ./eShop.AppHost/eShop.AppHost.csproj -r linux-musl-$TARGETARCH --no-restore -o /publish

# -----------------------------------------------------------------------------

# Use a lightweight .NET runtime image
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine3.20

WORKDIR /usr/src/app/

# Copy published app from the build stage (correct name!)
COPY --from=build /publish/ .

# Expose necessary ports
EXPOSE 5000 5001

# Start the application
CMD ["dotnet", "eShop.AppHost.dll"]

