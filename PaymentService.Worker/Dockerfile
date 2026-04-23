# BUild stage

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj first for better caching
COPY PaymentService.Worker/PaymentService.Worker.csproj ./PaymentService.Worker/

RUN dotnet restore ./PaymentService.Worker/PaymentService.Worker.csproj

COPY . .

WORKDIR /src/PaymentService.Worker

RUN dotnet publish -c Release -o /app/out

# RUNTIME STAGE

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

# Copy only published output
COPY --from=build /app/out .

# Run the worker
ENTRYPOINT ["dotnet", "PaymentService.Worker.dll"]