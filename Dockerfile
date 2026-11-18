FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/oss-gadget-cli/oss-gadget-cli.csproj \
    -c Release -f net8.0 --self-contained false \
    -p:UseAppHost=false -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
COPY --from=build /app/publish /app
WORKDIR /root
ENTRYPOINT ["dotnet", "/app/ossgadget.dll"]
CMD ["--help"]
