# QaaS.Mocker.Example

`mocker.qaas.yaml` is the release-validation example for Mocker.

The file intentionally combines:

- all three supported server types in the example project: `Http`, `Grpc`, and `Socket`
- every processor shipped in `QaaS.Common.Processors`
- one support data source so processor configurations that depend on data source lookup can template successfully

Validate the example with:

```powershell
dotnet run --project D:\QaaS\QaaS.Mocker\QaaS.Mocker.Example\QaaS.Mocker.Example.csproj -c Release -- template mocker.qaas.yaml
```

The command exits `0` when the full example configuration is valid.
