# QaaS.Mocker

The `QaaS.Mocker` is a package available as part of the `QaaS` Framework that is used for running mocker server projects.

> Written In C# 14 & .net 10

## Projects

### User Interfaces

* `QaaS.Mocker` - From here the QaaS mocker server is initialized, has command line arguments for running options.

### Functional Projects

* `QaaS.Mocker.Stubs` - Responsible for building the provided stubs according to given configurations.

* `QaaS.Mocker.Servers` - Responsible for running servers according to given configuration.

* `QaaS.Mocker.Controller` - Responsible for handling the server configuration API which the QaaS Tests can communicate with in runtime.

### Tests

* `QaaS.Mocker.<OtherQaaSRunnerProjectName>.Tests` - NUnit test project for the other project referenced in the name.

### Example 

* `QaaS.Mocker.Example` - Example project of a server that can be ran locally (and publishes example image via CI).
