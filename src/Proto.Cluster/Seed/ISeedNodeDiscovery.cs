using System.Threading.Tasks;

namespace Proto.Cluster.Seed;

public interface ISeedNodeDiscovery
{
    Task Register(string memberId, string host, int port);
    Task Remove(string memberId);
    Task<(string memberId,string host, int port)[]> GetAll();
}