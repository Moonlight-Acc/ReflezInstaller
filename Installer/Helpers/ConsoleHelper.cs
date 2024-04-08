using Spectre.Console;

namespace Installer.Helpers;

public class ConsoleHelper
{
    public static void Checked(string text)
    {
        AnsiConsole.MarkupLine("[bold lime][[ \u2714\ufe0f ]][/][white] " + text + "[/]");
    }

    public static void Info(string text)
    {
        AnsiConsole.MarkupLine("[bold deepskyblue1][[ \ud83d\udcac ]][/][white] " + text + "[/]");
    }

    public static void Error(string text)
    {
        AnsiConsole.MarkupLine("[bold red][[ ‼\ufe0f ]][/][white] " + text + "[/]");
    }

    public static async Task Status(string initialText, Func<Action<string>, Task> action)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan bold"))
            .StartAsync($"[bold white]{initialText}[/]", async context =>
            {
                await action.Invoke(s => context.Status($"[white]{s}[/]"));
            });
    }

    public static string Selection(string question, string[] options)
    {
        var actionSelection = new SelectionPrompt<string>();

        actionSelection.AddChoices(options);

        actionSelection.Converter = item => $"[white]{item}[/]";

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[white bold]{question}[/]");

        return AnsiConsole.Prompt(actionSelection);
    }
}