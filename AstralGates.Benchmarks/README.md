# AstralGates Benchmarks

Бенчмарки для проекта AstralGates, использующие BenchmarkDotNet.

## Как запустить

```bash
cd AstralGates.Benchmarks
dotnet run -c Release
```

## Структура бенчмарков

### RenderBenchmarks
Измеряет производительность рендеринга на разных разрешениях:
- **Низкое разрешение** (320×240)
- **Среднее разрешение** (640×480)
- **Высокое разрешение** (1920×1080)

Для каждого разрешения тестируются:
- `Render()` - рендеринг в Vector3[]
- `RenderRgbaToBytes()` - рендеринг в byte[] (RGBA)

### SdfBenchmarks
Измеряет производительность SDF операций:
- `SphereDistance` - вычисление расстояния до сферы
- `PlaneDistance` - вычисление расстояния до плоскости
- `CompositorRender` - полный рендеринг одного луча
- `CompositorRender_1000Rays` - рендеринг 1000 лучей (симуляция работы)

## Параметры бенчмарков

- **Target Framework**: .NET 10.0
- **Memory Diagnostics**: включён (показывает allocations)
- **Статистика**: Min, Max, Mean, Median

## Результаты

Результаты сохраняются в папке `BenchmarkDotNet.Artifacts/results/`.

Форматы:
- Консольный вывод с таблицей
- HTML отчёт
- CSV файлы для анализа
- Markdown таблицы

## Добавление новых бенчмарков

Создайте новый класс с атрибутом `[MemoryDiagnoser]`:

```csharp
[SimpleJob(RuntimeMoniker.Net100)]
[MemoryDiagnoser]
public class MyBenchmarks
{
    [GlobalSetup]
    public void Setup()
    {
        // Инициализация
    }

    [Benchmark]
    public void MyTest()
    {
        // Код для бенчмарка
    }
}
```

Затем добавьте его в `Program.cs`:

```csharp
BenchmarkRunner.Run<MyBenchmarks>();
```
