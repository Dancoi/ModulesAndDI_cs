using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pr2.ModulesAndDi.Core;
using Pr2.ModulesAndDi.Services;

namespace Pr2.ModulesAndDi.Modules;

/// <summary>
/// Модуль уведомлений.
/// Зависит от Analytics (для использования аналитической информации), 
/// Logging (для логирования уведомлений) и Core (для доступа ко времени).
/// </summary>
public sealed class NotificationModule : IAppModule
{
    public string Name => "Notification";

    public IReadOnlyCollection<string> Requires => new[] { "Analytics", "Logging", "Core" };

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IAppAction, NotificationAction>();
    }

    public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private sealed class NotificationAction : IAppAction
    {
        private readonly IStorage _storage;
        private readonly IClock _clock;
        private readonly ILogger<NotificationAction> _logger;

        public NotificationAction(IStorage storage, IClock clock, ILogger<NotificationAction> logger)
        {
            _storage = storage;
            _clock = clock;
            _logger = logger;
        }

        public string Title => "Отправка уведомлений";

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var items = _storage.GetAll();
            var timestamp = _clock.Now;

            _logger.LogInformation("Отправка уведомлений в {Timestamp}", timestamp);

            if (items.Count == 0)
            {
                _logger.LogWarning("Нет данных для уведомления");
                Console.WriteLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] Уведомление: нет данных");
            }
            else if (items.Count < 3)
            {
                _logger.LogWarning("Внимание: мало данных в системе ({Count})", items.Count);
                Console.WriteLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] Уведомление: низкий уровень данных ({items.Count} шт)");
            }
            else
            {
                _logger.LogInformation("Успешно: данные в системе ({Count})", items.Count);
                Console.WriteLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] Уведомление: система работает нормально ({items.Count} элементов)");
            }

            return Task.CompletedTask;
        }
    }
}
