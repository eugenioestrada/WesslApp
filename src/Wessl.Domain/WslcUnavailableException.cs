namespace Wessl.Domain;

public sealed class WslcUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
