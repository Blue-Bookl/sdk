// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Tool.List;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using NuGet.Versioning;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolListGlobalOrToolPathCommandTests
    {
        private readonly BufferedReporter _reporter;

        public ToolListGlobalOrToolPathCommandTests()
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void GivenNoInstalledPackagesItPrintsEmptyTable()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new IToolPackage[0]);

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenAnInvalidToolPathItThrowsException()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new IToolPackage[0]);

            var toolPath = "tool-path-does-not-exist";
            var command = CreateCommand(store.Object, $"--tool-path {toolPath}", toolPath);

            Action a = () => command.Execute();

            a.Should().Throw<GracefulException>()
             .And
             .Message
             .Should()
             .Be(string.Format(CliCommandStrings.ToolListInvalidToolPathOption, toolPath));
        }

        [Fact]
        public void GivenAToolPathItPassesToolPathToStoreFactory()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new IToolPackage[0]);

            var toolPath = Path.GetTempPath();
            var command = CreateCommand(store.Object, $"--tool-path {toolPath}", toolPath);

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenAToolPathItPassesToolPathToStoreFactoryFromRedirectCommand()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new IToolPackage[0]);

            var toolPath = Path.GetTempPath();
            var result = Parser.Parse("dotnet tool list " + $"--tool-path {toolPath}");
            var toolListGlobalOrToolPathCommand = new ToolListGlobalOrToolPathCommand(
                result,
                toolPath1 =>
                {
                    AssertExpectedToolPath(toolPath1, toolPath);
                    return store.Object;
                },
                _reporter);

            var toolListCommand = new ToolListCommand(
                result,
                toolListGlobalOrToolPathCommand);

            toolListCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenASingleInstalledPackageItPrintsThePackage()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new ToolCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenMultipleInstalledPackagesItPrintsThePackages()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new ToolCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                    ),
                    CreateMockToolPackage(
                        "another.tool",
                        "2.7.3",
                        new ToolCommand(new ToolCommandName("bar"), "dotnet", new FilePath("tool"))
                    ),
                    CreateMockToolPackage(
                        "some.tool",
                        "1.0.0",
                        new ToolCommand(new ToolCommandName("fancy-foo"), "dotnet", new FilePath("tool"))
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }


        [Fact]
        public void GivenMultipleInstalledPackagesItPrintsThePackagesForJsonFormat()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new ToolCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                    ),
                    CreateMockToolPackage(
                        "another.tool",
                        "2.7.3",
                        new ToolCommand(new ToolCommandName("bar"), "dotnet", new FilePath("tool"))
                    )
                });

            var command = CreateCommand(store.Object, "-g --format json");

            command.Execute().Should().Be(0);

            _reporter.Lines.Count.Should().Be(1);

            var versionedData = System.Text.Json.JsonSerializer.Deserialize<VersionedDataContract<ToolListJsonContract[]>>(_reporter.Lines[0]);
            versionedData.Should().NotBeNull();
            versionedData.Version.Should().Be(1);
            versionedData.Data.Length.Should().Be(2);

            // another tool should be the first one, since there's OrderBy by PackageId
            versionedData.Data[0].PackageId.Should().Be("another.tool");
            versionedData.Data[0].Version.Should().Be("2.7.3");
            versionedData.Data[0].Commands[0].Should().Be("bar");

            versionedData.Data[1].PackageId.Should().Be("test.tool");
            versionedData.Data[1].Version.Should().Be("1.3.5-preview");
            versionedData.Data[1].Commands[0].Should().Be("foo");
        }

        [Fact]
        public void GivenAPackageWithMultipleCommandsItListsThem()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new ToolCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool")))
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object));
        }

        [Fact]
        public void GivenABrokenPackageItPrintsWarning()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new ToolCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                    ),
                    CreateMockBrokenPackage("another.tool", "2.7.3"),
                    CreateMockToolPackage(
                        "some.tool",
                        "1.0.0",
                        new ToolCommand(new ToolCommandName("fancy-foo"), "dotnet", new FilePath("tool"))
                    )
                });

            var command = CreateCommand(store.Object, "-g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(
                EnumerateExpectedTableLines(store.Object).Prepend(
                    string.Format(
                        CliCommandStrings.ToolListInvalidPackageWarning,
                        "another.tool",
                        "broken").Yellow()));
        }

        private IToolPackage CreateMockToolPackage(string id, string version, ToolCommand command)
        {
            var package = new Mock<IToolPackage>(MockBehavior.Strict);

            package.SetupGet(p => p.Id).Returns(new PackageId(id));
            package.SetupGet(p => p.Version).Returns(NuGetVersion.Parse(version));
            package.SetupGet(p => p.Command).Returns(command);
            return package.Object;
        }

        [Fact]
        public void GivenPackageIdArgItPrintsThatPackage()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                     CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new ToolCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                    ),
                    CreateMockToolPackage(
                        "another.tool",
                        "2.7.3",
                        new ToolCommand(new ToolCommandName("bar"), "dotnet", new FilePath("tool"))
                    ),
                    CreateMockToolPackage(
                        "some.tool",
                        "1.0.0",
                        new ToolCommand(new ToolCommandName("fancy-foo"), "dotnet", new FilePath("tool"))
                    )
                });

            var command = CreateCommand(store.Object, "test.tool -g");

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object, new PackageId("test.tool")));
        }

        [Fact]
        public void GivenNotInstalledPackageItPrintsEmpty()
        {
            var store = new Mock<IToolPackageStoreQuery>(MockBehavior.Strict);
            store
                .Setup(s => s.EnumeratePackages())
                .Returns(new[] {
                    CreateMockToolPackage(
                        "test.tool",
                        "1.3.5-preview",
                        new ToolCommand(new ToolCommandName("foo"), "dotnet", new FilePath("tool"))
                    )
                });

            var command = CreateCommand(store.Object, "not-installed-package -g");

            command.Execute().Should().Be(1);

            _reporter.Lines.Should().Equal(EnumerateExpectedTableLines(store.Object, new PackageId("not-installed-package")));
        }

        private IToolPackage CreateMockBrokenPackage(string id, string version)
        {
            var package = new Mock<IToolPackage>(MockBehavior.Strict);

            package.SetupGet(p => p.Id).Returns(new PackageId(id));
            package.SetupGet(p => p.Version).Returns(NuGetVersion.Parse(version));
            package.SetupGet(p => p.Command).Throws(new ToolConfigurationException("broken"));
            return package.Object;
        }

        private ToolListGlobalOrToolPathCommand CreateCommand(IToolPackageStoreQuery store, string options = "", string expectedToolPath = null)
        {
            var result = Parser.Parse("dotnet tool list " + options);
            return new ToolListGlobalOrToolPathCommand(
                result,
                toolPath => { AssertExpectedToolPath(toolPath, expectedToolPath); return store; },
                _reporter);
        }

        private void AssertExpectedToolPath(DirectoryPath? toolPath, string expectedToolPath)
        {
            if (expectedToolPath != null)
            {
                toolPath.Should().NotBeNull();
                toolPath.Value.Value.Should().Be(expectedToolPath);
            }
            else
            {
                toolPath.Should().BeNull();
            }
        }

        private IEnumerable<string> EnumerateExpectedTableLines(IToolPackageStoreQuery store, PackageId? targetPackageId = null)
        {
            static string GetCommandString(IToolPackage package) => package.Command.Name.ToString();

            var packages = store.EnumeratePackages().Where(
                (p) => PackageHasCommand(p) && ToolListGlobalOrToolPathCommand.PackageIdMatches(p, targetPackageId)
                ).OrderBy(package => package.Id);
            var columnDelimiter = PrintableTable<IToolPackageStoreQuery>.ColumnDelimiter;

            int packageIdColumnWidth = CliCommandStrings.ToolListPackageIdColumn.Length;
            int versionColumnWidth = CliCommandStrings.ToolListVersionColumn.Length;
            int commandsColumnWidth = CliCommandStrings.ToolListCommandsColumn.Length;
            foreach (var package in packages)
            {
                packageIdColumnWidth = Math.Max(packageIdColumnWidth, package.Id.ToString().Length);
                versionColumnWidth = Math.Max(versionColumnWidth, package.Version.ToNormalizedString().Length);
                commandsColumnWidth = Math.Max(commandsColumnWidth, GetCommandString(package).Length);
            }

            yield return string.Format(
                "{0}{1}{2}{3}{4}",
                CliCommandStrings.ToolListPackageIdColumn.PadRight(packageIdColumnWidth),
                columnDelimiter,
                CliCommandStrings.ToolListVersionColumn.PadRight(versionColumnWidth),
                columnDelimiter,
                CliCommandStrings.ToolListCommandsColumn.PadRight(commandsColumnWidth));

            yield return new string(
                '-',
                packageIdColumnWidth + versionColumnWidth + commandsColumnWidth + (columnDelimiter.Length * 2));

            foreach (var package in packages)
            {
                yield return string.Format(
                    "{0}{1}{2}{3}{4}",
                    package.Id.ToString().PadRight(packageIdColumnWidth),
                    columnDelimiter,
                    package.Version.ToNormalizedString().PadRight(versionColumnWidth),
                    columnDelimiter,
                    GetCommandString(package).PadRight(commandsColumnWidth));
            }
        }

        private static bool PackageHasCommand(IToolPackage package)
        {
            try
            {
                return package.Command is not null;
            }
            catch (Exception ex) when (ex is ToolConfigurationException)
            {
                return false;
            }
        }
    }
}
