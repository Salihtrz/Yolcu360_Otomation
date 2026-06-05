namespace Yolcu360.BusinessLayer.Concrete
{

    public class AuthenticationException : Exception
    {
        public AuthenticationException(string message) : base(message) { }
    }
}
