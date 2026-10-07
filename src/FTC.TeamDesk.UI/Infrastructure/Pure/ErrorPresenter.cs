using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Localization;
using FTC.TeamDesk.UI.Abstractions;

namespace FTC.TeamDesk.UI;

public sealed class ErrorPresenter : IErrorPresenter
{
    private readonly ILocalizationService _loc;
    private readonly IAppLogger _logger;
    private readonly Func<IToastService> _toasts;

    public ErrorPresenter(ILocalizationService loc, IAppLogger logger, Func<IToastService> toasts)
    { _loc = loc; _logger = logger; _toasts = toasts; }

    public string Describe(Exception exception)
    {
        if (exception is ILocalizedError localized)
        {
            var text = _loc.Get(localized.ErrorKey);
            if (text != localized.ErrorKey) return text;
        }
        _logger.Error("Unhandled operation error.", exception);
        return exception switch
        {
            UnauthorizedAccessException => _loc["Error.AccessDenied"],
            IOException => _loc["Error.FileAccess"],
            HttpRequestException => _loc["Error.Network"],
            _ => _loc["Error.Unexpected"]
        };
    }

    public void Show(Exception exception)
    {
        if (exception is OperationCanceledException) return;
        _toasts().Show(Describe(exception), ToastKind.Error);
    }
}
