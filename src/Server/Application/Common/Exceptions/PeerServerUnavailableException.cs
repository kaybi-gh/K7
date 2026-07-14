namespace K7.Server.Application.Common.Exceptions;

public class PeerServerUnavailableException : Exception
{
    public PeerServerUnavailableException(string message) : base(message) { }
}
