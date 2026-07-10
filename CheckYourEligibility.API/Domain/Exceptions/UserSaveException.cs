public class UserSaveException : Exception
{
    public UserSaveException(string message) : base(message) { }

    public UserSaveException(string message, Exception innerException) : base(message, innerException) { }
}