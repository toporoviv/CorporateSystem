using System.Net;

namespace CorporateSystem.Auth.Domain.Exceptions;

public class ExceptionWithStatusCode : Exception
{
    public HttpStatusCode StatusCode { get; init; }

    public ExceptionWithStatusCode(string message, HttpStatusCode statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public ExceptionWithStatusCode(string message, HttpStatusCode statusCode, Exception? innerException) 
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}