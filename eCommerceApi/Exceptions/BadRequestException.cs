namespace eCommerceApi.Exceptions;

public class BadRequestException : AppException
{
    public BadRequestException(string message) : base(400, "BAD_REQUEST", message) { }
}
