# Use the .NET 9.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Install SQLite
RUN apt-get update && apt-get install -y sqlite3 && rm -rf /var/lib/apt/lists/*

# Copy project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet build -c Release -o /app/build

# Final stage for running tests
FROM build AS final
WORKDIR /src
ENTRYPOINT ["dotnet", "test", "--logger", "console;verbosity=detailed"]