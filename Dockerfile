FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY NuGet.Config Directory.Build.props global.json ./
COPY Vairas.McpServer.Business/ Vairas.McpServer.Business/
COPY Vairas.McpServer/ Vairas.McpServer/
RUN dotnet restore Vairas.McpServer/Vairas.McpServer.csproj
RUN dotnet publish Vairas.McpServer/Vairas.McpServer.csproj -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Vairas.McpServer.dll"]
