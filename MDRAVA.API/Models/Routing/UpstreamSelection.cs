using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Models.Routing;

public sealed record UpstreamSelection(RuntimeRoute Route, RuntimeUpstream Upstream);
