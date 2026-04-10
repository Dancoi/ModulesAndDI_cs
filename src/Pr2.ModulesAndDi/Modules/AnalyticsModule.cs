using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pr2.ModulesAndDi.Core;
using Pr2.ModulesAndDi.Services;

namespace Pr2.ModulesAndDi.Modules;

/// <summary>
/// Модуль аналитики данных.
/// Зависит от Core (для доступа к хранилищу) и Logging (для логирования).
/// </summary>
public sealed class AnalyticsModule : IAppModule
{
    public string Name => "Analytics";

    public IReadOnlyCollection<string> Requires => new[] { "Core", "Logging" };

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IAppAction, AnalyticsAction>();
    }

    public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private sealed class AnalyticsAction : IAppAction
    {
        private readonly IStorage _storage;
        private readonly ILogger<AnalyticsAction> _logger;

        public AnalyticsAction(IStorage storage, ILogger<AnalyticsAction> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        public string Title => "Анализ данных";

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var items = _storage.GetAll();
            
            _logger.LogInformation("Начало анализа данных");
            _logger.LogInformation("Всего элементов в хранилище: {Count}", items.Count);

            if (items.Count > 0)
            {
                var shortestItem = items.OrderBy(x => x.Length).First();
                var longestItem = items.OrderByDescending(x => x.Length).First();
                var averageLength = items.Average(x => x.Length);

                _logger.LogInformation("Самый короткий элемент: {Item} (длина: {Length})", shortestItem, shortestItem.Length);
                _logger.LogInformation("Самый длинный элемент: {Item} (длина: {Length})", longestItem, longestItem.Length);
                _logger.LogInformation("Средняя длина элемента: {AvgLength:F2}", averageLength);

                Console.WriteLine($"Анализ завершён: {items.Count} элементов, средняя длина {averageLength:F2}");
            }
            else
            {
                _logger.LogWarning("Хранилище пусто, анализ не проводился");
                Console.WriteLine("Анализ: хранилище пусто");
            }

            return Task.CompletedTask;
        }
    }
}
