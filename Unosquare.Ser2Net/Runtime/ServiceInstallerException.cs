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
        ResultCode = DefaultErrorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceInstallerException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    public ServiceInstallerException(int errorCode)
        : base(UnkownErrorMessage)
    {
        ResultCode = errorCode;
    }

    public ServiceInstallerException(int errorCode, Exception innerException)
        : base(UnkownErrorMessage, innerException)
    {
        ResultCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceInstallerException"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception.</param>
    public ServiceInstallerException(Exception innerException)
        : base(UnkownErrorMessage, innerException)
    {
        ResultCode = DefaultErrorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceInstallerException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The message.</param>
    public ServiceInstallerException(int errorCode, string message)
        : base(message)
    {
        ResultCode = errorCode;
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
        ResultCode = errorCode;
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
    public int ResultCode { get; } = DefaultErrorCode;


}
