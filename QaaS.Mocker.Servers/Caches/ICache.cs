
using QaaS.Framework.SDK.Session;

namespace QaaS.Mocker.Servers.Caches;

public interface ICache 
{
    public bool EnableStorage { get; set; }
    public string? CachedAction { get; set; }
    public DataFilter InputDataFilter { get; set; }
    public DataFilter OutputDataFilter { get; set; }

    public string? RetrieveFirstOrDefaultStringInput();
    public string? RetrieveFirstOrDefaultStringOutput();
}