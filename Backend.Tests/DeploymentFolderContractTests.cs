using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Backend.Tests;

/// <summary>
/// Keeps the dated deployment snapshots under StoredProcedureMigrations/Deployments honest.
///
/// Stored procedures here are applied by hand in SSMS, so each release is packaged as a dated
/// folder holding a copy of every procedure it ships. A copy that drifts from the file the
/// developers actually edit is worse than no copy at all: whoever runs the folder deploys a
/// stale definition and the report is wrong in a way the repository does not show. These
/// assertions fail the build instead.
/// </summary>
public sealed class DeploymentFolderContractTests
{
    [Fact]
    public void Every_deployment_copy_matches_the_procedure_it_was_taken_from()
    {
        foreach (var folder in DeploymentFolders())
        {
            foreach (var copy in Directory.GetFiles(folder, "??_sp_*.sql"))
            {
                // "01_sp_AmendReport_pagination.sql" -> "sp_AmendReport_pagination.sql"
                var originalName = Path.GetFileName(copy)[3..];
                var original = Path.Combine(MigrationsRoot, originalName);

                Assert.True(File.Exists(original),
                    $"{Rel(copy)} has no original at StoredProcedureMigrations/{originalName}.");
                Assert.True(
                    File.ReadAllBytes(copy).AsSpan().SequenceEqual(File.ReadAllBytes(original)),
                    $"{Rel(copy)} has drifted from StoredProcedureMigrations/{originalName}. "
                    + "Re-copy the file and update checksums.txt, or the deployment ships a stale procedure.");
            }
        }
    }

    [Fact]
    public void Every_deployment_folder_records_the_checksums_of_its_originals()
    {
        foreach (var folder in DeploymentFolders())
        {
            var manifest = Path.Combine(folder, "checksums.txt");
            Assert.True(File.Exists(manifest), $"{Rel(folder)} has no checksums.txt.");

            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(manifest))
            {
                var match = Regex.Match(line, @"^([0-9a-f]{64})  (\S+)$");
                if (!match.Success)
                {
                    Assert.StartsWith("#", line.Length == 0 ? "#" : line);
                    continue;
                }

                var name = match.Groups[2].Value;
                listed.Add(name);

                var original = Path.Combine(MigrationsRoot, name);
                Assert.True(File.Exists(original), $"{Rel(manifest)} lists a missing file: {name}.");
                Assert.Equal(match.Groups[1].Value, Sha256(original));
            }

            foreach (var copy in Directory.GetFiles(folder, "??_sp_*.sql"))
            {
                Assert.Contains(Path.GetFileName(copy)[3..], listed);
            }
        }
    }

    [Fact]
    public void The_run_all_script_applies_every_procedure_in_the_folder()
    {
        foreach (var folder in DeploymentFolders())
        {
            var runAll = Path.Combine(folder, "00_RunAll.sql");
            Assert.True(File.Exists(runAll), $"{Rel(folder)} has no 00_RunAll.sql.");

            var script = File.ReadAllText(runAll);
            foreach (var copy in Directory.GetFiles(folder, "??_sp_*.sql").OrderBy(p => p, StringComparer.Ordinal))
            {
                var procedure = Path.GetFileNameWithoutExtension(copy)[3..];
                Assert.Contains($"CREATE OR ALTER PROCEDURE [dbo].[{procedure}]", script);
            }

            // The procedures belong to the report database; ReportTemplateDB only holds the
            // Excel export job queue and creating them there is a mistake nobody notices.
            Assert.Contains("USE [TradeNetDB];", script);
            Assert.Contains("SET QUOTED_IDENTIFIER ON;", script);
        }
    }

    private static IEnumerable<string> DeploymentFolders()
    {
        var root = Path.Combine(MigrationsRoot, "Deployments");
        Assert.True(Directory.Exists(root), "StoredProcedureMigrations/Deployments is missing.");

        var folders = Directory.GetDirectories(root)
            .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^\d{4}-\d{2}-\d{2}_"))
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(folders);
        return folders;
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Rel(string path)
        => Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

    private static string MigrationsRoot => Path.Combine(RepositoryRoot, "StoredProcedureMigrations");

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null
                && !Directory.Exists(Path.Combine(directory.FullName, "Frontend")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
