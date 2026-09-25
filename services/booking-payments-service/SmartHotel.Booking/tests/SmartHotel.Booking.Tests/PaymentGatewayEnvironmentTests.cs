using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SmartHotel.Booking.Application.Interfaces;
using SmartHotel.Booking.Infrastructure;
using SmartHotel.Booking.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Booking.Tests;

public class PaymentGatewayEnvironmentTests
{
    private class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "SmartHotel.Booking";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void AddInfrastructure_WhenProductionAndStubGatewayRequestedViaConfig_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var env = new FakeHostEnvironment { EnvironmentName = "Production" };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:UseStubGateway"] = "true"
            })
            .Build();

        var act = () => services.AddInfrastructure(config, env);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*allowed only in Development, Testing, or Test*");
    }

    [Fact]
    public void AddInfrastructure_WhenProductionAndStubGatewayRequestedViaEnvVar_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var env = new FakeHostEnvironment { EnvironmentName = "Production" };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PAYMENT_USE_STUB_GATEWAY"] = "true"
            })
            .Build();

        var act = () => services.AddInfrastructure(config, env);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*allowed only in Development, Testing, or Test*");
    }

    [Fact]
    public void AddInfrastructure_WhenProductionAndStubGatewayNotRequested_RegistersProductionPaymentGateway()
    {
        var services = new ServiceCollection();
        var env = new FakeHostEnvironment { EnvironmentName = "Production" };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:UseStubGateway"] = "false"
            })
            .Build();

        services.AddInfrastructure(config, env);
        var provider = services.BuildServiceProvider();

        var gateway = provider.GetRequiredService<IPaymentGateway>();
        gateway.Should().BeOfType<ProductionPaymentGateway>();
    }

    [Fact]
    public void AddInfrastructure_WhenDevelopmentAndStubGatewayRequested_RegistersStubPaymentGateway()
    {
        var services = new ServiceCollection();
        var env = new FakeHostEnvironment { EnvironmentName = "Development" };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:UseStubGateway"] = "true"
            })
            .Build();

        services.AddInfrastructure(config, env);
        var provider = services.BuildServiceProvider();

        var gateway = provider.GetRequiredService<IPaymentGateway>();
        gateway.Should().BeOfType<StubPaymentGateway>();
    }

    [Fact]
    public void AddInfrastructure_WhenEnvironmentUnspecifiedAndStubRequested_DefaultsToProductionAndThrows()
    {
        var services = new ServiceCollection();
        // Null environment, ASPNETCORE_ENVIRONMENT not set -> defaults to Production
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:UseStubGateway"] = "true"
            })
            .Build();

        var act = () => services.AddInfrastructure(config, environment: null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*allowed only in Development, Testing, or Test*");
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Preview")]
    public void AddInfrastructure_WhenForbiddenEnvironmentRequestsStub_Throws(string environmentName)
    {
        var services = new ServiceCollection();
        var env = new FakeHostEnvironment { EnvironmentName = environmentName };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PAYMENT_USE_STUB_GATEWAY"] = "true"
            })
            .Build();

        var act = () => services.AddInfrastructure(config, env);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*allowed only in Development, Testing, or Test*");
    }
}
