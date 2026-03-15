using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;

namespace QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public record SocketEndpointConfig
{
    [Required, Range(0, 65535), Description("Port that socket listenes and binds connection to")]
    public int? Port { get; set; }

    [Required,
     Description(
         "Specifies the protocol to use in the socket")] // TODO - add RangeIfAnyAttribute to validate right values for Tcp and Udp implementations
    public ProtocolType? ProtocolType { get; set; }

    [Description("Specifies the type of socket"), DefaultValue(SocketType.Stream)]
    public SocketType SocketType { get; set; } = SocketType.Stream;

    [Description("Specifies the addresses masks to approve connections to"),
     DefaultValue(AddressFamily.InterNetwork)]
    public AddressFamily AddressFamily { get; set; } = AddressFamily.InterNetwork;

    [Range(0, int.MaxValue), DefaultValue(65536), Description("The size of communication messages buffer, in bytes")]
    public int BufferSizeBytes { get; set; } = 65536;

    [Description(
         "Whether to use the Nagle Algorithm or not - to reduce small packets and communicate more efficiently over Tcp/Ip connection"),
     DefaultValue(false)] // TODO - add SetToIfAny attribute to validate if true only if Tcp configured
    public bool NagleAlgorithm { get; set; } = false;

    [Description(
         "The number of seconds to retain connection after communication. `null` means it won't remain connected"),
     DefaultValue(null)]
    public int? LingerTimeSeconds { get; set; } = null;

    [Required, Description("Timeout in milliseconds for socket to perform method before terminating connection")]
    public int? TimeoutMs { get; set; }

    [Required, Description("The socket action to perform on the client connection endpoint")]
    public SocketActionConfig? Action { get; set; }
  };
