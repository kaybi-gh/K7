namespace K7.Server.Web.Endpoints;

public interface IEndpoint
{
    public abstract void Map(IEndpointRouteBuilder app);
}
