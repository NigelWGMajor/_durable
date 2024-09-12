// this class contains the domain-specific content that is passed between operations.
// The flow history is attached to the enclosing Product.
// This must be serializable.
public class Payload
{
    public string Name { get; set; } = "";
    // insert whatever your domain needs!
}
