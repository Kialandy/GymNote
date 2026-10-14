using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace GymNote.Api.Auth;

/// <summary>
/// Keeps ASP.NET Core data protection keys in memory. The app never protects data with them
/// (auth is stateless JWT), so this only prevents the framework from writing key files to the container.
/// </summary>
public sealed class InMemoryXmlRepository : IXmlRepository
{
    private readonly List<XElement> _elements = [];
    private readonly Lock _lock = new();

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_lock)
        {
            return _elements.Select(e => new XElement(e)).ToList();
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        lock (_lock)
        {
            _elements.Add(new XElement(element));
        }
    }
}
