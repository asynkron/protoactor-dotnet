using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Cluster.Seed;


[PublicAPI]
public interface ISeedNodeDiscovery
{
    Task Register(string memberId, string host, int port);
    Task Remove(string memberId);
    Task<(string memberId, string host, int port)[]> GetAll();
}
