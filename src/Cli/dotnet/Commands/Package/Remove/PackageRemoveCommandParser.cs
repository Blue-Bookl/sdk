// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Package.Remove;

internal static class PackageRemoveCommandParser
{
    public static readonly CliArgument<IEnumerable<string>> CmdPackageArgument = new(LocalizableStrings.CmdPackage)
    {
        Description = LocalizableStrings.PackageRemoveAppHelpText,
        Arity = ArgumentArity.OneOrMore,
    };

    public static readonly CliOption<bool> InteractiveOption = CommonOptions.InteractiveOption().ForwardIfEnabled("--interactive");

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        var command = new CliCommand("remove", LocalizableStrings.PackageRemoveAppFullName);

        command.Arguments.Add(CmdPackageArgument);
        command.Options.Add(InteractiveOption);
        command.Options.Add(PackageCommandParser.ProjectOption);

        command.SetAction((parseResult) => new RemovePackageReferenceCommand(parseResult).Execute());

        return command;
    }
}
