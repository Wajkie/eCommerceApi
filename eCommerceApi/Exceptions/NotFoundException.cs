namespace eCommerceApi.Exceptions;

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(404, "NOT_FOUND", message) { }
}
