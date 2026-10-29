using AstralGates;

switch (args)
{
    case []:
        await new GameLoop().Start();
        break;
    case ["--gif", ..]:
        Environment.ExitCode = await GifRenderCommand.RunAsync(args);
        break;
    default:
        Console.Error.WriteLine($"Usage: AstralGates [{GifRenderCommand.Usage}]");
        Environment.ExitCode = 2;
        break;
}
