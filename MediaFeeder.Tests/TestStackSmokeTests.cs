using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;
using Shouldly;

namespace MediaFeeder.Tests;

public sealed class TestStackSmokeTests
{
    private static IFixture CreateFixture() => new Fixture().Customize(new AutoNSubstituteCustomization());

    [Fact]
    public void Shouldly_ProvidesFluentAssertions()
    {
        "MediaFeeder".Contains("Feeder", StringComparison.Ordinal).ShouldBeTrue();
        42.ShouldBe(42);
    }

    [Fact]
    public void AutoFixture_GeneratesValues()
    {
        var fixture = CreateFixture();

        var value = fixture.Create<string>();

        value.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void AutoNsSubstitute_InjectsSubstitutedDependencies()
    {
        var fixture = CreateFixture();

        var service = fixture.Create<SampleService>();

        service.Dependency.ShouldNotBeNull();
        service.Dependency.Ping().ShouldBe(0);
    }

    [Fact]
    public void NSubstitute_SupportsSetupAndVerification()
    {
        var dependency = Substitute.For<IDependency>();
        dependency.Ping().Returns(42);

        dependency.Ping().ShouldBe(42);

        dependency.Received(1).Ping();
    }
}
