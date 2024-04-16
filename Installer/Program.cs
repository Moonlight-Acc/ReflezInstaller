﻿using System.Text;
using Installer.Installers;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

AnsiConsole.MarkupLine("[aqua]Reflez Panel Installer[/]");
AnsiConsole.MarkupLine("[white]Welcome to the Reflez panel installer. This program will guide you through the installation of the Reflez panel, the daemon and wings[/]");

var installer = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("[white]Please select the software you want to install on this machine[/]")
        .AddChoices(
            "Reflez Panel",
            "Reflez Daemon",
            "Wings"
        )
    );

AnsiConsole.MarkupLine($"[white]Starting installer for: {installer}[/]");

switch (installer)
{
    case "Reflez Panel":
        await PanelInstaller.Install();
        break;
    case "Reflez Daemon":
        await DaemonInstaller.Install();
        break;
    case "Wings":
        await WingsInstaller.Install();
        break;
}
