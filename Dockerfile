FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore QaaS.Mocker.sln --configfile NuGet.config
RUN dotnet publish QaaS.Mocker.Example/QaaS.Mocker.Example.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "QaaS.Mocker.Example.dll", "mocker.qaas.yaml"]
