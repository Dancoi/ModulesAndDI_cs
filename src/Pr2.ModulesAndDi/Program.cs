using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pr2.ModulesAndDi.Core;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var enabled = configuration.GetSection("Modules").Get<string[]>() ?? Array.Empty<string>();

var discovered = ModuleCatalog.DiscoverFromAssembly(Assembly.GetExecutingAssembly());
var ordered = ModuleCatalog.BuildExecutionOrder(discovered, enabled);

var services = new ServiceCollection();

foreach (var module in ordered)
{
    module.RegisterServices(services);
}

var provider = services.BuildServiceProvider();

foreach (var module in ordered)
{
    await module.InitializeAsync(provider, CancellationToken.None);
}

var actions = provider.GetServices<IAppAction>().ToArray();

Console.WriteLine("Запуск действий модулей");
foreach (var action in actions)
{
    Console.WriteLine($"Действие {action.Title}");
    await action.ExecuteAsync(CancellationToken.None);
}
