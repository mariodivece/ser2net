using Microsoft.Extensions.Primitives;
using System.Text;

namespace Unosquare.Ser2Net.Runtime;

/// <summary>
/// Exception for signalling service installer errors.
/// </summary>
public class ServiceInstallerException : Exception
{
    private const string UnkownErrorMessage = "An unkown error has occurred.";

    /// <summary>
    /// (Immutable) the default error code.
    /// </summary>
    public const int DefaultErrorCode = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceInstallerException"/> class.
    /// </summary>
    public ServiceInstallerException()
        : base(UnkownErrorMessage)
    {
        ErrorCode = DefaultErrorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceInstallerException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    public ServiceInstallerException(int errorCode)
        : base(UnkownErrorMessage)
    {
        ErrorCode = errorCode;
    }

    public ServiceInstallerException(int errorCode, Exception innerException)
        : base(UnkownErrorMessage, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceInstallerException"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception.</param>
    public ServiceInstallerException(Exception innerException)
        : base(UnkownErrorMessage, innerException)
    {
        ErrorCode = DefaultErrorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceInstallerException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The message.</param>
    public ServiceInstallerException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceInstallerException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public ServiceInstallerException(string message)
        : base(message)
    {
        // placeholder
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceInstallerException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ServiceInstallerException(int errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceInstallerException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ServiceInstallerException(string message, Exception innerException)
        : base(message, innerException)
    {
        // placeholder
    }

    /// <summary>
    /// Gets the result code.
    /// </summary>
    public int ErrorCode { get; } = DefaultErrorCode;


    public static ServiceInstallerException InvalidArgument() => new(22, "Invalid argument.");

    public static ServiceInstallerException InvalidRuntimeMode() => new(44, "Argument is only valid when issued from the console.");

    public static ServiceInstallerException GeneralFailure(Exception innerException) => new(64, "Could not perform requested action.", innerException);

    public string ToString(bool addDetails)
    {
        var builder = new StringBuilder($"Error Code {ErrorCode}: {Message}");
        if (!addDetails)
            return builder.ToString();
            

        if (InnerException is not null)
            builder.Append(CultureInfo.InvariantCulture, $"\r\n    ({InnerException.GetType().Name}): {InnerException.Message}");

        if (!string.IsNullOrWhiteSpace(StackTrace))
            builder.Append(CultureInfo.InvariantCulture, $"\r\n Stack Trace follows:\r\n{StackTrace}");

        return builder.ToString();
    }
}
