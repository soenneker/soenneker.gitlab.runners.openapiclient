using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.GitLab.Runners.OpenApiClient.Tests;

[Collection("Collection")]
public sealed class GitLabOpenApiClientRunnerTests : FixturedUnitTest
{
    public GitLabOpenApiClientRunnerTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public void Default()
    {

    }
}
