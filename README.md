# Modules and Dependency Injection (DI)

## 📋 Описание проекта

Проект демонстрирует **паттерн модульной архитектуры** с **внедрением зависимостей** (Dependency Injection). Приложение:
- 🔍 Автоматически обнаруживает модули в сборке через рефлексию
- 📊 Разрешает зависимости между модулями (граф зависимостей)
- 🔄 Строит правильный порядок загрузки модулей (топологическая сортировка)
- 💉 Регистрирует сервисы в DI контейнер
- ⚡ Инициализирует модули и выполняет их действия

---

## 🎯 Основные компоненты

### Core (Ядро)
| Файл | Назначение |
|------|-----------|
| `Core/IAppModule.cs` | Контракт модуля (Name, Requires, RegisterServices, InitializeAsync) |
| `Core/IAppAction.cs` | Контракт действия (Title, ExecuteAsync) |
| `Core/ModuleCatalog.cs` | Обнаружение модулей и построение порядка (алгоритм Кана) |
| `Core/ModuleLoadException.cs` | Исключение для ошибок загрузки |

### Встроенные модули
| Модуль | Зависимости | Функция |
|--------|-----------|---------|
| **CoreModule** | ❌ нет | Базовая инфраструктура (Clock, Storage) |
| **LoggingModule** | Core | Логирование через консоль |
| **ValidationModule** | Core | Валидация и сохранение данных |
| **ExportModule** | Core, Validation | Экспорт данных в файл |
| **AnalyticsModule** | Core, Logging | **Анализ данных** (новый) |
| **NotificationModule** | Analytics, Logging, Core | **Уведомления** (новый) |
| **ReportModule** | Core, Export | Формирование отчётов |

### Сервисы
| Сервис | Реализация | Где регистрируется |
|--------|-----------|------------------|
| `IClock` | `SystemClock` | CoreModule |
| `IStorage` | `InMemoryStorage` | CoreModule |

---

## 🚀 Как это работает

### Фаза 1: Чтение конфигурации
```csharp
var enabled = configuration.GetSection("Modules").Get<string[]>();
// Читает из appsettings.json: ["Core", "Logging", "Validation", ...]
```

### Фаза 2: Обнаружение модулей
```csharp
var discovered = ModuleCatalog.DiscoverFromAssembly(Assembly.GetExecutingAssembly());
// Через рефлексию находит все классы, реализующие IAppModule
// Результат: {CoreModule, LoggingModule, ValidationModule, ...}
```

### Фаза 3: Построение порядка
```csharp
var ordered = ModuleCatalog.BuildExecutionOrder(discovered, enabled);
// Алгоритм Кана: {Core} → {Logging, Validation} → {Export} → {Analytics} → {...}
```

**Граф зависимостей:**
```
                  Core
                  / | \
                 /  |  \
            Logging |  Validation
              |     |    /
              |    / \  /
              |   /   Export
              |  /      |
              | /    Analytics
              |/        |
              |    Notification
              |
              Report
```

### Фаза 4: Регистрация сервисов
```csharp
var services = new ServiceCollection();
foreach (var module in ordered) {
    module.RegisterServices(services);
}
// CoreModule добавляет: IClock, IStorage
// LoggingModule добавляет: логирование
// AnalyticsModule добавляет: AnalyticsAction
// и т.д.
```

### Фаза 5: Строим провайдер
```csharp
var provider = services.BuildServiceProvider();
// Готовый DI контейнер со всеми сервисами
```

### Фаза 6: Инициализируем модули
```csharp
foreach (var module in ordered) {
    await module.InitializeAsync(provider, CancellationToken.None);
}
// Модули получают в конструкторе нужные им сервисы из провайдера
```

### Фаза 7: Выполняем действия
```csharp
var actions = provider.GetServices<IAppAction>().ToArray();
foreach (var action in actions) {
    Console.WriteLine($"Действие {action.Title}");
    await action.ExecuteAsync(CancellationToken.None);
}
// Выполняются все действия всех модулей в правильном порядке
```

---

## ✨ Почему добавление нового модуля не требует изменений в Program.cs

### Решение основано на трёх принципах:

#### 1️⃣ Рефлексия (Reflection)
```csharp
assembly.GetTypes()
    .Where(t => typeof(IAppModule).IsAssignableFrom(t))
    // Автоматически найдёт любой новый класс, реализующий IAppModule
```

#### 2️⃣ Конфигурация (JSON)
```json
{
  "Modules": ["Core", "NewModule"]
}
```
Просто добавляем имя в список — никаких изменений в коде!

#### 3️⃣ Декларативные зависимости
```csharp
public IReadOnlyCollection<string> Requires => new[] { "Core", "Export" };
// Алгоритм Кана автоматически вычислит правильный порядок
```

### Пример: добавление нового модуля

```csharp
// Файл: Modules/CacheModule.cs
public sealed class CacheModule : IAppModule
{
    public string Name => "Cache";
    public IReadOnlyCollection<string> Requires => new[] { "Core" };

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IAppAction, CacheAction>();
    }

    public Task InitializeAsync(IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;

    private class CacheAction : IAppAction
    {
        public string Title => "Инициализация кэша";
        public Task ExecuteAsync(CancellationToken ct)
        {
            Console.WriteLine("✅ Кэш инициализирован");
            return Task.CompletedTask;
        }
    }
}
```

**В appsettings.json:**
```json
{
  "Modules": ["Core", "Cache", "Logging"]
}
```

**Готово!** Модуль автоматически:
- ✅ Обнаружен
- ✅ Загружен в правильном порядке
- ✅ Его действие выполнено

**Никаких изменений в Program.cs!**

---

## 🧪 Тестирование

### Запуск всех тестов
```bash
dotnet test
```

### Покрытие тестами (10 тестов)

| Тест | История |
|------|---------|
| `Порядок_запуска_учитывает_зависимости` | Линейная цепь: A→B→C |
| `Ромбовидная_зависимость_разрешается_правильно` | Diamond: A→B,C; B,C→D |
| `Несколько_независимых_модулей_загружаются` | Параллельные модули без зависимостей |
| `Отсутствующий_модуль_даёт_понятную_ошибку` | Обработка: модуль не найден |
| `Отсутствующая_зависимость_даёт_ошибку` | Обработка: нет промежуточного модуля |
| `Цикл_зависимостей_обнаруживается` | Цикл A↔B |
| `Цикл_из_трёх_модулей_обнаруживается` | Цикл A→B→C→A |
| `Порядок_входного_массива_не_влияет_на_результат` | Детерминизм алгоритма Кана |
| `Внедрение_зависимостей_работает` | DI контейнер инжектит зависимости |
| `DI_контейнер_реально_внедряет_зависимости` | Singleton проверка и кэширование |

---

## 🏃 Быстрый старт

### Запуск приложения
```bash
cd src/Pr2.ModulesAndDi
dotnet run
```

### Изменение конфигурации
Отредактируйте `src/Pr2.ModulesAndDi/appsettings.json` и запустите снова.

### Запуск тестов
```bash
dotnet test
```

---

## 📊 Архитектурные паттерны

1. **Module Pattern** — каждый модуль независим
2. **Dependency Injection** — зависимости через Constructor Injection
3. **Service Locator** — услуги через IServiceProvider
4. **Factory Pattern** — ModuleCatalog обнаруживает и создаёт модули
5. **Strategy Pattern** — каждый модуль определяет свою стратегию
6. **Observable Pattern** — действия выполняются последовательно

---

## 📂 Структура проекта

```
src/Pr2.ModulesAndDi/
├── Program.cs
├── appsettings.json
├── Core/
│   ├── IAppModule.cs
│   ├── IAppAction.cs
│   ├── ModuleCatalog.cs
│   └── ModuleLoadException.cs
├── Modules/
│   ├── CoreModule.cs
│   ├── LoggingModule.cs
│   ├── ValidationModule.cs
│   ├── ExportModule.cs
│   ├── AnalyticsModule.cs      ← новый
│   ├── NotificationModule.cs   ← новый
│   └── ReportModule.cs
└── Services/
    ├── IClock.cs
    ├── IStorage.cs
    ├── SystemClock.cs
    └── InMemoryStorage.cs

tests/Pr2.ModulesAndDi.Tests/
└── ModuleCatalogTests.cs (10 тестов)

README.md          ← ВЫ ЗДЕСЬ
SCENARIOS.md       ← Демонстрационные сценарии
```

---

## ❓ Часто задаваемые вопросы

**Q: Почему используется алгоритм Кана?**
A: Это стандартный алгоритм для топологической сортировки. Гарантирует правильный порядок при сложных зависимостях и обнаруживает циклы.

**Q: Возможны ли циклические зависимости?**
A: Да, но алгоритм их обнаружит и выбросит `ModuleLoadException` с понятным сообщением.

**Q: Где создаются экземпляры модулей?**
A: В `ModuleCatalog.DiscoverFromAssembly()` через `Activator.CreateInstance()`.

**Q: Как модуль получает зависимости?**
A: Через Constructor Injection. Если класс нужно зависимость, она заявляется в конструкторе, и фреймворк её инжектит.

**Q: Что если модуль не включён в конфиг?**
A: Он не будет обнаружен, его сервисы не зарегистрируются, и его действия не выполнятся.

**Q: Как протестировать новый модуль?**
A: Создайте unit test, используя FakeModule из существующих тестов.

---

## ✅ Соответствие требованиям

| Требование | Реализация | Статус |
|-----------|-----------|--------|
| Имя, Requires, RegisterServices, InitializeAsync | IAppModule interface | ✅ |
| Чтение конфига | Program.cs:8-9 | ✅ |
| Порядок учитывает зависимости | ModuleCatalog.BuildExecutionOrder() | ✅ |
| Ошибка отсутствующего модуля | ModuleLoadException | ✅ |
| Ошибка циклических зависимостей | ModuleLoadException | ✅ |
| DI сервисов | RegisterServices() | ✅ |
| Выполнение действий | IAppAction | ✅ |
| Множество наборов зависимостей | 3 теста порядка + 2 Edge cases | ✅ |
| Ошибки разных типов | 3 теста ошибок | ✅ |
| Проверка DI внедрения | 2 теста DI | ✅ |
