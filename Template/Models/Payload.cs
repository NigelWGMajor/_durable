// this class contains the domain-specific content that is passed between operations.
// The flow history is attached to the enclosing Product.
// This must be serializable.

// The IPayload defines what is explicitly needed by the FlowManager framework.

using Degreed.SafeTest;
using System.Diagnostics;

public interface IPayload
{
    public string Name { get; set; }
    public string UniqueKey { get; set; }
}

[DebuggerStepThrough]
public class Payload : IPayload
{
    public string Name { get; set; } = "";
    public string UniqueKey { get; set; } = "";
}
