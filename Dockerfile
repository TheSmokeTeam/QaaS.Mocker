FROM REDA/dotnet/runtime:8.0-alpine
WORKDIR /app                           
COPY ./DotnetBuildOutput .
ENTRYPOINT ["dotnet", "QaaS.Mocker.Example.dll", "run", "-l" ,"information"]
