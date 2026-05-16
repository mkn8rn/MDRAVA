using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Proxy.Routing;

public sealed record UpstreamSelection(RuntimeRoute Route, RuntimeUpstream Upstream);
