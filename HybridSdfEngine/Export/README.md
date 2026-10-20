# Экспорт GIF

`GifWriter` передаёт кадры рендера в FFmpeg через stdin. Промежуточные PNG и
хранение всей анимации в памяти не нужны. Требуется установленный FFmpeg в `PATH`;
в конструкторе можно указать путь через `ffmpegPath`.

## Запуск из корня репозитория

```powershell
dotnet run --project AstralGates/AstralGates.csproj -c Release -- --gif artifacts/render.gif 320 180 25 4
```

Аргументы после `--gif`: файл, ширина, высота, FPS и длительность в секундах.
Дробная длительность записывается через точку. Можно указать только файл или
вообще опустить аргументы: значения по умолчанию — `artifacts/render.gif`,
320×180, 25 FPS, 4 секунды. Для замены существующего файла добавьте в конце
`--overwrite`.

Используется текущий `RenderPipeline`, тени и SSAA4. Сцена обновляется с шагом
`1 / FPS`; число кадров — `ceil(seconds * FPS)`. Скорость воспроизведения
не зависит от времени расчёта. GIF зациклен. Ctrl+C отменяет экспорт;
итоговый файл появляется только после успешного завершения кодировщика.

## Из кода

```csharp
using HybridSdfEngine.Export;

await using var gif = new GifWriter("animation.gif", width, height, framesPerSecond: 25);
for (int frame = 0; frame < 100; frame++)
{
    pipeline.UpdateScene(1f / 25f);
    await gif.WriteFrameAsync(shader.RenderWithGammaSsaa4());
}
await gif.CompleteAsync();
```

В кадре ожидается ровно `width * height` элементов `Vector3`, слева направо,
сверху вниз. Цвета уже должны быть с гамма-коррекцией, как результат
`RenderWithGamma` / `RenderWithGammaSsaa4`. Повторная гамма не применяется;
каналы обрезаются до [0, 1], NaN преобразуется в 0. Прозрачность не поддерживается.
Дождитесь `WriteFrameAsync` перед изменением буфера или следующим рендером:
`Shader` переиспользует свой массив. Один экземпляр `GifWriter` обслуживается
последовательно одним вызывающим кодом. Без `CompleteAsync` экспорт отменяется
при `DisposeAsync`; существующий целевой файл сохраняется.

Палитра рассчитывается отдельно для каждого кадра, используется dithering
`sierra2_4a`: [фильтры FFmpeg](https://ffmpeg.org/ffmpeg-filters.html#paletteuse).
FPS ограничен диапазоном 1–100. GIF хранит задержку в сотых долях секунды,
поэтому для равных интервалов удобно выбирать 10, 20, 25 или 50 FPS.

## Проверка

```powershell
dotnet run --project HybridSdfEngine/Tests/ConsoleDisplayChecks.csproj -c Release -- --gif-checks
```

Требует FFmpeg. Проверяется декодирование двух кадров через StbImageSharp:
размеры, порядок RGB, переиспользование буфера, задержки и зацикливание.
Также проверяются перезапись, отмена, очистка временных файлов и ошибки аргументов.
