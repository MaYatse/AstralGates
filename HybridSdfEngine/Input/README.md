# Ввод общего назначения

Модуль получает состояния клавиатуры и мыши и передаёт их потребителю.
Назначение клавиш, действия приложения и управление камерой задаёт вызывающий код.

## Устройство

- `IInputSource` задаёт контракт источника: запуск, чтение состояния, завершение.
- `SharpHookInputSource` реализует его через глобальный хук SharpHook 8.0.1.
- `InputState` накапливает события клавиатуры и мыши с синхронизацией потоков.
- `InputFrame` предоставляет состояния удержания, нажатия, отпускания,
  абсолютную позицию и накопленное смещение мыши.

Коды клавиш и кнопок — `InputKey` и `InputMouseButton` из `HybridSdfEngine.Input`.
Все они доступны через одинаковые методы; зарезервированных клавиш у модуля нет.
Собственный источник может реализовать `IInputSource` и наполнять `InputState`
событиями выбранной оконной библиотеки или тестовыми данными.

## Использование в коде

```csharp
using HybridSdfEngine.Input;

await using IInputSource input = new SharpHookInputSource();
await input.StartAsync(); // Возвращается после готовности хука.

// Один раз за итерацию приложения; тот же снимок передаётся всем потребителям.
InputFrame frame = input.ReadFrame();
bool held = frame.IsDown(InputKey.Enter);
bool pressed = frame.WasPressed(InputKey.Enter);
bool released = frame.WasReleased(InputKey.Enter);
bool mouseHeld = frame.IsDown(InputMouseButton.Button1);
var mousePosition = frame.MousePosition;
var mouseDelta = frame.MouseDelta;
```

Одновременно запускайте только один глобальный хук во всём процессе.
`SharpHookInputSource` проверяет это для своих экземпляров. Не создавайте
отдельные хуки SharpHook параллельно с ним. `StartAsync` вызывается один раз;
`DisposeAsync` останавливает и дожидается завершения. `Completion` позволяет
заметить неожиданную остановку или ошибку хука.

`InputState` принимает события из другого потока под короткой блокировкой.
`ReadFrame` переносит данные в переиспользуемый `InputFrame`: он стабилен до
следующего `ReadFrame`. Вызывайте его один раз за кадр из потока обновления и
не сохраняйте снимок на последующие кадры. `WasPressed`, `WasReleased` и
`MouseDelta` сбрасываются после чтения; удержание сохраняется. Нажатие и
отпускание между кадрами дают оба перехода, поэтому короткий клик не теряется.
Повторы удерживаемой клавиши не дают повторного `WasPressed`.

Для другого источника ввода можно использовать `InputState.SetKey`,
`SetMouseButton`, `SetMousePosition` или `AddMouseDelta`. Абсолютная позиция
при первом событии задаёт базу без скачка. `Reset` освобождает удерживаемые
клавиши/кнопки и забывает базу мыши; его можно вызвать при потере фокуса.

SharpHook получает ввод глобально, включая другие приложения. Модуль не
подавляет события, не определяет фокус чужого окна и не захватывает курсор.
Потребитель сам выбирает, когда обрабатывать ввод. `Reset` сбрасывает состояние,
но не останавливает источник: следующие события продолжат поступать.

Практический потребитель находится в приложении:
[`AstralGates.Input.FlyCameraController`](../../AstralGates/Input/FlyCameraController.cs).
Его назначения WASD, ПКМ и переключатели управления описаны
[отдельно](../../AstralGates/Input/README.md).

## Платформы и проверки

На macOS глобальному хуку требуется разрешение Accessibility. На Linux
возможности зависят от backend; Wayland имеет ограничения и требует повышенных
привилегий. Подробности: [SharpHook](https://github.com/TolikPylypchuk/SharpHook#supported-platforms).

Обычные проверки используют `SharpHook.Testing`, не отправляют события в ОС и
не устанавливают системный хук:

```powershell
dotnet run --project Tests/HybridSdfEngine.Tests/ConsoleDisplayChecks.csproj -c Release
```

Отдельная проверка запускает и останавливает настоящий системный хук без
инъекции клавиш и движений мыши:

```powershell
dotnet run --project Tests/HybridSdfEngine.Tests/ConsoleDisplayChecks.csproj -c Release -- --input-smoke-test
```
