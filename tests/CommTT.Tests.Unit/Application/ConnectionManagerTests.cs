using CommTT.Application.Interfaces;
using CommTT.Application.Logging;
using CommTT.Application.Services;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace CommTT.Tests.Unit.Application;

public class ConnectionManagerTests
{
    private static ConnectionManager CreateManager()
    {
        var logger = new Mock<ILogger<ConnectionManager>>();
        var stateLogger = new ProtocolStateLogger(Path.GetTempPath());
        return new ConnectionManager(logger.Object, stateLogger);
    }

    [Fact]
    public async Task ConnectAsync_Should_Call_Provider_Connect()
    {
        var mockProvider = new Mock<ICommProvider>();
        var manager = CreateManager();
        manager.RegisterProvider(mockProvider.Object);
        await manager.ConnectAsync(new SerialConfig("COM1", 9600));
        mockProvider.Verify(p => p.ConnectAsync(It.IsAny<ICommConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_Without_Provider_Should_Not_Throw()
    {
        var manager = CreateManager();
        await manager.Invoking(m => m.ConnectAsync(new SerialConfig("COM1", 9600)))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_Should_Call_Provider_Send()
    {
        var mockProvider = new Mock<ICommProvider>();
        var manager = CreateManager();
        manager.RegisterProvider(mockProvider.Object);
        var data = new ReadOnlyMemory<byte>(new byte[] { 0x01, 0x02 });
        await manager.SendAsync(data);
        mockProvider.Verify(p => p.SendAsync(data, It.IsAny<CancellationToken>()), Times.Once);
    }
}
