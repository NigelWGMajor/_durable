using System;
using System.Diagnostics;
[DebuggerStepThrough]
public class FlowManagerRecoverableException : Exception
{
    // Default constructor
    public FlowManagerRecoverableException() : base() { }

    // Constructor that accepts a custom message
    public FlowManagerRecoverableException(string message) : base(message) { }

    // Constructor that accepts a custom message and an inner exception
    public FlowManagerRecoverableException(string message, Exception innerException) : base(message, innerException) { }
}

public class FlowManagerFatalException : Exception
{
    // Default constructor
    public FlowManagerFatalException() : base() { }

    // Constructor that accepts a custom message
    public FlowManagerFatalException(string message) : base(message) { }

    // Constructor that accepts a custom message and an inner exception
    public FlowManagerFatalException(string message, Exception innerException) : base(message, innerException) { }
}

