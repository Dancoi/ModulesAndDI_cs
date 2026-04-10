using Microsoft.Extensions.DependencyInjection;
using Pr2.ModulesAndDi.Core;
using Xunit;

namespace Pr2.ModulesAndDi.Tests;

public sealed class ModuleCatalogTests
{
    [Fact]
    public void Порядок_запуска_учитывает_зависимости()
    {
        var a = new FakeModule("A", Array.Empty<string>());
        var b = new FakeModule("B", new[] { "A" });
        var c = new FakeModule("C", new[] { "B" });

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase)
        {
            [a.Name] = a,
            [b.Name] = b,
            [c.Name] = c
        };

        var order = ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B", "C" });

        Assert.Equal(new[] { "A", "B", "C" }, order.Select(m => m.Name).ToArray());
    }

    [Fact]
    public void Отсутствующий_модуль_даёт_понятную_ошибку()
    {
        var a = new FakeModule("A", Array.Empty<string>());

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase)
        {
            [a.Name] = a
        };

        var ex = Assert.Throws<ModuleLoadException>(() => ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B" }));
        Assert.Contains("Модуль не найден", ex.Message);
    }

    [Fact]
    public void Цикл_зависимостей_обнаруживается()
    {
        var a = new FakeModule("A", new[] { "B" });
        var b = new FakeModule("B", new[] { "A" });

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase)
        {
            [a.Name] = a,
            [b.Name] = b
        };

        var ex = Assert.Throws<ModuleLoadException>(() => ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B" }));
        Assert.Contains("циклическая", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ромбовидная_зависимость_разрешается_правильно()
    {
        // Граф:     A
        //          / \
        //         B   C
        //          \ /
        //           D
        var a = new FakeModule("A", Array.Empty<string>());
        var b = new FakeModule("B", new[] { "A" });
        var c = new FakeModule("C", new[] { "A" });
        var d = new FakeModule("D", new[] { "B", "C" });

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase)
        {
            [a.Name] = a, [b.Name] = b, [c.Name] = c, [d.Name] = d
        };

        var order = ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B", "C", "D" });
        var names = order.Select(m => m.Name).ToArray();

        // A должен быть первым, D последним, B и C между ними
        Assert.Equal("A", names[0]);
        Assert.Equal("D", names[3]);
        Assert.Contains("B", names[1..3]);
        Assert.Contains("C", names[1..3]);
    }

    [Fact]
    public void Несколько_независимых_модулей_загружаются()
    {
        var a = new FakeModule("A", Array.Empty<string>());
        var b = new FakeModule("B", Array.Empty<string>());
        var c = new FakeModule("C", Array.Empty<string>());

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase)
        {
            [a.Name] = a, [b.Name] = b, [c.Name] = c
        };

        var order = ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B", "C" });

        Assert.Equal(3, order.Count);
        Assert.All(order, m => Assert.NotNull(m));
    }

    [Fact]
    public void Отсутствующая_зависимость_даёт_ошибку()
    {
        var a = new FakeModule("A", Array.Empty<string>());
        var b = new FakeModule("B", new[] { "A" });
        var c = new FakeModule("C", new[] { "B", "Missing" });

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase)
        {
            [a.Name] = a, [b.Name] = b, [c.Name] = c
        };

        var ex = Assert.Throws<ModuleLoadException>(() => 
            ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B", "C" }));
        
        Assert.Contains("Missing", ex.Message);
        Assert.Contains("требует", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Цикл_из_трёх_модулей_обнаруживается()
    {
        var a = new FakeModule("A", new[] { "B" });
        var b = new FakeModule("B", new[] { "C" });
        var c = new FakeModule("C", new[] { "A" });

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase)
        {
            [a.Name] = a, [b.Name] = b, [c.Name] = c
        };

        var ex = Assert.Throws<ModuleLoadException>(() => 
            ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B", "C" }));
        
        Assert.Contains("циклическая", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Порядок_входного_массива_не_влияет_на_результат()
    {
        var modules = new[]
        {
            new FakeModule("A", Array.Empty<string>()),
            new FakeModule("B", new[] { "A" }),
            new FakeModule("C", new[] { "B" })
        };

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in modules)
            all[m.Name] = m;

        // Первый порядок: A, B, C
        var order1 = ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B", "C" });
        
        // Второй порядок: C, B, A (инвертированный)
        var order2 = ModuleCatalog.BuildExecutionOrder(all, new[] { "C", "B", "A" });

        Assert.Equal(
            order1.Select(m => m.Name),
            order2.Select(m => m.Name)
        );
    }

    [Fact]
    public async Task Внедрение_зависимостей_работает()
    {
        var services = new ServiceCollection();
        services.AddSingleton<MarkerService>();

        var provider = services.BuildServiceProvider();

        var module = new FakeModule("A", Array.Empty<string>())
        {
            OnInit = sp =>
            {
                var s = sp.GetService<MarkerService>();
                Assert.NotNull(s);
            }
        };

        await module.InitializeAsync(provider, CancellationToken.None);
    }

    [Fact]
    public async Task DI_контейнер_реально_внедряет_зависимости()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ServiceA>();
        services.AddSingleton<ServiceB>();

        var provider = services.BuildServiceProvider();

        ServiceA? capturedA = null;
        ServiceB? capturedB = null;

        var module = new FakeModule("A", Array.Empty<string>())
        {
            OnInit = sp =>
            {
                capturedA = sp.GetService<ServiceA>();
                capturedB = sp.GetService<ServiceB>();
            }
        };

        await module.InitializeAsync(provider, CancellationToken.None);

        // Проверяем что получены реальные экземпляры из контейнера (не null)
        Assert.NotNull(capturedA);
        Assert.NotNull(capturedB);
        
        // Проверяем что это одни и те же экземпляры (singleton)
        var a2 = provider.GetRequiredService<ServiceA>();
        var b2 = provider.GetRequiredService<ServiceB>();
        Assert.Same(capturedA, a2);
        Assert.Same(capturedB, b2);
    }

    private sealed class MarkerService { }
    private sealed class ServiceA { }
    private sealed class ServiceB { }

    private sealed class FakeModule : IAppModule
    {
        public FakeModule(string name, IReadOnlyCollection<string> requires)
        {
            Name = name;
            Requires = requires;
        }

        public string Name { get; }

        public IReadOnlyCollection<string> Requires { get; }

        public Action<IServiceProvider>? OnInit { get; init; }

        public void RegisterServices(IServiceCollection services) { }

        public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            OnInit?.Invoke(serviceProvider);
            return Task.CompletedTask;
        }
    }
}
