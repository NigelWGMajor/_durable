using System;
using System.Diagnostics;

namespace Models;
/// <summary>
/// A recoverable error, handled by the innermost retry policy.
/// </summary>
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
/// <summary>
/// An unrecoverable error will bubble up through all levels 
/// </summary>
[DebuggerStepThrough]
public class FlowManagerFatalException : Exception
{
    // Default constructor
    public FlowManagerFatalException() : base() { }

    // Constructor that accepts a custom message
    public FlowManagerFatalException(string message) : base(message) { }

    // Constructor that accepts a custom message and an inner exception
    public FlowManagerFatalException(string message, Exception innerException) : base(message, innerException) { }
}
/// <summary>
/// An error at the infrastructure level - handled by the outermost retry policy
/// </summary>
[DebuggerStepThrough]
public class FlowManagerInfraException : Exception
{
    // Default constructor
    public FlowManagerInfraException() : base() { }

    // Constructor that accepts a custom message
    public FlowManagerInfraException(string message) : base(message) { }

    // Constructor that accepts a custom message and an inner exception
    public FlowManagerInfraException(string message, Exception innerException) : base(message, innerException) { }
}