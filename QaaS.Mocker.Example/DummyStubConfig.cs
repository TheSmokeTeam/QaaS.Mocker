using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Mocker.Example;

public record DummyStubConfig
{
    [Required, Description("Dummy Json Body Key")]
    public string DummyKey { get; set; }
    
    [Required, Description("Dummy Json Body Value")]
    public string DummyValue { get; set; }
    
}