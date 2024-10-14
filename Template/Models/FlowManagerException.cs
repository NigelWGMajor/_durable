using System;
using System.Diagnostics;
[DebuggerStepThrough]
public class FlowManagerRetryableException : Exception
{
    // Default constructor
    public FlowManagerRetryableException() : base() { }

    // Constructor that accepts a custom message
    public FlowManagerRetryableException(string message) : base(message) { }

    // Constructor that accepts a custom message and an inner exception
    public FlowManagerRetryableException(string message, Exception innerException) : base(message, innerException) { }
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

