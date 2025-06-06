# Use the .NET 9.0 SDK for building and testing
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet build -c Release -o /app/build

# Final stage for running tests
FROM build AS final
WORKDIR /src

# Set environment variables for database connections
ENV POSTGRES_CONNECTION_STRING=""
ENV SQLSERVER_CONNECTION_STRING=""

ENTRYPOINT ["dotnet", "test", "--logger", "console;verbosity=detailed"]