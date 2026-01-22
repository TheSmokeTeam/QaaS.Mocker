using Autofac;

namespace QaaS.Mocker;

public class Bootstrap
{
    private static ILifetimeScope BuildParentContainer()
    {
        var containerBuilder = new ContainerBuilder();
        return containerBuilder.Build();
    }
}