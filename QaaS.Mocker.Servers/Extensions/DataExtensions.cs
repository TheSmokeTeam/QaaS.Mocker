using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Mocker.Servers.Extensions;

public static class DataExtensions
{
    public static DetailedData<object> CloneDetailed(this Data<object> data, DateTime? datetime = null)
    {
        return new DetailedData<object>
        {
            Timestamp = datetime ?? DateTime.UtcNow,
            Body = data.Body,
            MetaData = data.MetaData
        };
    }
}