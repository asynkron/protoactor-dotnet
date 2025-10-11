using System;

namespace Proto.Cluster;

#pragma warning disable RCS1194
/// <summary>
/// Throwing a <see cref="GrainException"/> from inside a generated grain implementation
/// will rethrow this same exception on the client, with the same message and code.
/// The code property can then be used to detect what kind of error occurred to handle it accordingly.
/// </summary>
/// <remarks>
/// Currently, any other exception thrown from a generated grain will result in a generic Exception
/// being thrown with the entire exception object serialized into the message property.
/// </remarks>
public class GrainException : Exception
{
	public GrainException(string message, string? code = null) : base(message)
	{
		Code = code;
	}

	public string? Code { get; }
}