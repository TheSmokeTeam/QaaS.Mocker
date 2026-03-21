using QaaS.Framework.SDK.Session.DataObjects;

namespace QaaS.Mocker.Servers.Extensions;

/// <summary>
/// Helpers for converting transport data into cache-friendly shapes.
/// </summary>
public static class DataExtensions
{
    /// <summary>
    /// Clones a payload into a <see cref="DetailedData{T}"/> instance with a capture timestamp.
    /// </summary>
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
