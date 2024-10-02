// this class contains the domain-specific content that is passed between operations.
// The flow history is attached to the enclosing Product.
// This must be serializable.
using Degreed.SafeTest;
using System.Diagnostics;

[DebuggerStepThrough]
public class Payload
{
    public string Name { get; set; } = "";
    public string UniqueKey { get; set; } = "";
    public int Id { get; set; }

    // insert whatever your domain needs!
}
