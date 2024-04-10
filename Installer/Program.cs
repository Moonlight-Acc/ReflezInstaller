using Installer.Abstractions;
using Installer.Helpers;
using Installer.SoftwareImplementations;
using Spectre.Console;

// Configure stuff here

// Title Screen
AnsiConsole.MarkupLine("[white bold]Welcome to the [aqua]Moonlight installer[/] made by [fuchsia]@masuowo[/][/]\n");
ConsoleHelper.Info("Running version: v2.0.0");

if(await BashHelper.ExecuteCommand("whoami") == "root")
    ConsoleHelper.Checked("Running as root");
else
{
    ConsoleHelper.Error("You need to run the installer as root");
    return;
}

if(await BashHelper.ExecuteCommand("which apt") != "")
    ConsoleHelper.Checked("Aptitude found");
else
{
    ConsoleHelper.Error("You need to have the aptitude (apt) package manager installed as the installer only supports apt at the moment");
    return;
}

var callContext = new CallContext(args.ToList());

var arch = await BashHelper.ExecuteCommand("uname -m | grep -q 'x86_64' && echo 'x64' || echo 'arm64'");
callContext.Storage.Set("ARCH", arch);
ConsoleHelper.Info("Detected architecture: " + arch);

var softwareList = new List<ISoftware>();
var installedSoftware = new Dictionary<ISoftware, bool>();

softwareList.Add(new PanelSoftware());
softwareList.Add(new DaemonSoftware());

await ConsoleHelper.Status("Checking installed software", async action =>
{
    foreach (var software in softwareList)
    {
        action.Invoke($"Checking if '{software.Name}' is installed");
        var isInstalled = await software.CheckFulfilled(callContext);
        installedSoftware.Add(software, isInstalled);
    }
});

ISoftware selectedSoftware;

if (callContext.HasParameter("--use-software"))
    selectedSoftware = softwareList.First(x => x.Id == callContext.GetRequiredParameterValue<string>("--use-software"));
else
{
    var selection = new SelectionPrompt<ISoftware>();
    selection.AddChoices(softwareList);

    selection.Converter = software => $"[bold white]{software.Name}[/]";

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[white bold]Select the software you want to install/update/uninstall:[/]");
    
    selectedSoftware = AnsiConsole.Prompt(selection);
}

AnsiConsole.WriteLine();

ConsoleHelper.Info($"Selected software '{selectedSoftware.Name}' with {selectedSoftware.Dependencies.Length} dependencies");

var status = installedSoftware[selectedSoftware] ? "[lime]Installed[/]" : "[red]Not installed[/]";
ConsoleHelper.Info($"Status: {status}");

AnsiConsole.WriteLine();

string action;

if (callContext.HasParameter("--use-action"))
    action = callContext.GetRequiredParameterValue<string>("--use-action");
else
{
    action = ConsoleHelper
        .Selection("Select the action you want to perform", new[] { "Install", "Update", "Uninstall" });
}

try
{
    switch (action)
    {
        case "Install":
            foreach (var dependency in selectedSoftware.Dependencies)
            {
                if(await dependency.CheckFulfilled(callContext))
                    ConsoleHelper.Checked($"'{dependency.Name}' is already installed");
                else
                {
                    ConsoleHelper.Info($"Installing '{dependency.Name}'");
                    
                    await dependency.Fulfill(callContext);
                    
                    ConsoleHelper.Info($"Installed '{dependency.Name}'");
                }
            }
            
            ConsoleHelper.Info($"Installing '{selectedSoftware.Name}'");
            await selectedSoftware.Install(callContext);
            break;
        
        case "Update":
            
            ConsoleHelper.Info($"Updating '{selectedSoftware.Name}'");
            await selectedSoftware.Update(callContext);
            break;
        
        case "Uninstall":
            
            ConsoleHelper.Info($"Uninstalling '{selectedSoftware.Name}'");
            await selectedSoftware.Uninstall(callContext);
            break;
    }
}
catch (Exception e)
{
    ConsoleHelper.Error("Uhh ohh, an error occured while performing action. To help us fix the issue, please open a issue on our github or write us on our discord server (not our dms). See our website for more information");
    ConsoleHelper.Error("Error stacktrace:");
    AnsiConsole.WriteException(e);
}