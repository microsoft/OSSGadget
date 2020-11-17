FROM mcr.microsoft.com/dotnet/sdk:5.0
COPY . /app/
WORKDIR /app/src

RUN dotnet build
