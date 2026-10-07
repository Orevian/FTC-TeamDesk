using FTC.TeamDesk.UI.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FTC.TeamDesk.Infrastructure;

/// <summary>Creates page view models through the container (the UI project itself has no DI dependency).</summary>
public sealed class ViewModelFactory : IViewModelFactory
{
    private readonly IServiceProvider _provider;
    public ViewModelFactory(IServiceProvider provider) { _provider = provider; }
    // ActivatorUtilities builds the view model with its dependencies from the container WITHOUT the container
    // tracking (and therefore keeping alive) every page instance that was ever shown.
    public T Create<T>() where T : class => ActivatorUtilities.CreateInstance<T>(_provider);
}
