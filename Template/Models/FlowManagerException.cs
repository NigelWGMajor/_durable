using System;

public class FlowManagerException : Exception
{
    // Default constructor
    public FlowManagerException() : base() { }

    // Constructor that accepts a custom message
    public FlowManagerException(string message) : base(message) { }

    // Constructor that accepts a custom message and an inner exception
    public FlowManagerException(string message, Exception innerException) : base(message, innerException) { }
}