namespace Microsoft.CST.OpenSource.Tests;

using System.Diagnostics;

public class DockerBuildTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Built_image_runs_cli_help_by_default()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Dockerfile")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        string image = $"ossgadget-test:{Guid.NewGuid():N}";
        string container = $"ossgadget-test-{Guid.NewGuid():N}";
        bool imageBuilt = false;
        bool containerCreated = false;

        try
        {
            await RunDockerAsync("build", "--tag", image, root.FullName);
            imageBuilt = true;
            await RunDockerAsync("create", "--name", container, image);
            containerCreated = true;

            string help = await RunDockerAsync("start", "--attach", container);
            string exitCode = await RunDockerAsync("inspect", "--format", "{{.State.ExitCode}}", container);

            Assert.Equal("0", exitCode.Trim());
            Assert.Contains("ossgadget", help);
            Assert.Contains("download", help);
            Assert.Contains("find-squats", help);
        }
        finally
        {
            try
            {
                if (containerCreated)
                {
                    await RunDockerAsync("rm", "--force", container);
                }
            }
            finally
            {
                if (imageBuilt)
                {
                    await RunDockerAsync("image", "rm", image);
                }
            }
        }
    }

    private static async Task<string> RunDockerAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = new("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        Assert.True(process.Start(), "Failed to start Docker.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(10));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException(
                $"docker {string.Join(" ", arguments)} timed out.\n{await stdout}\n{await stderr}");
        }

        string output = $"{await stdout}\n{await stderr}";
        Assert.True(process.ExitCode == 0,
            $"docker {string.Join(" ", arguments)} exited with code {process.ExitCode}.\n{output}");
        return output;
    }

    private sealed class DockerFactAttribute : FactAttribute
    {
        public DockerFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("RUN_DOCKER_TESTS") != "1")
            {
                Skip = "Set RUN_DOCKER_TESTS=1 to run this test with a Linux Docker daemon.";
            }
        }
    }
}
