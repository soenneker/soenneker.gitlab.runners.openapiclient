using Soenneker.Tests.HostedUnit;

namespace Soenneker.GitLab.Runners.OpenApiClient.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class GitLabOpenApiClientRunnerTests : HostedUnitTest
{
    public GitLabOpenApiClientRunnerTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {

    }
}
