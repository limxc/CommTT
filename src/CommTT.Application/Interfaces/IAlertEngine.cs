using CommTT.Domain.Models;

namespace CommTT.Application.Interfaces;

public interface IAlertEngine
{
    event EventHandler<string> AlertTriggered;
    void Check(CommDataFrame frame);
}
