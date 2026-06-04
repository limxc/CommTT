using CommTT.Domain.Interfaces;

namespace CommTT.Domain.Models;

public record SerialConfig(
    string PortName,
    int BaudRate,
    int DataBits = 8,
    System.IO.Ports.Parity Parity = System.IO.Ports.Parity.None,
    System.IO.Ports.StopBits StopBits = System.IO.Ports.StopBits.One
) : ICommConfig;
