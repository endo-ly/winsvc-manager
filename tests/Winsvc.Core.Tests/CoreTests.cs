using Xunit;
using Winsvc.Contracts.Manifest;
using Winsvc.Core;
using Winsvc.Infrastructure;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Winsvc.Core.Tests;

public class CoreTests
{
    [Fact]
    public void WinSwXmlGenerator_ShouldGenerateValidXml()
    {
        var generator = new WinSwXmlGenerator();
        var manifest = new ServiceManifest
        {
            Id = "acestep",
            DisplayName = "ACE-Step API",
            Description = "ACE-Step FastAPI service",
            Runtime = new RuntimeConfig
            {
                WorkDir = @"C:\svc\runtimes\acestep\ACE-Step-1.5",
                Executable = @"C:\svc\runtimes\acestep\.venv\Scripts\python.exe",
                Arguments = new() { "-m", "acestep.api_server" }
            },
            Service = new ServiceConfig
            {
                StartMode = "delayed-auto",
                OnFailure = "restart",
                ResetFailure = "1 hour"
            }
        };
        manifest.Env["ACESTEP_API_HOST"] = "127.0.0.1";

        // Act
        var xml = generator.Generate(manifest);

        // Assert
        Assert.Contains("<id>acestep</id>", xml);
        Assert.Contains(@"<executable>C:\svc\runtimes\acestep\.venv\Scripts\python.exe</executable>", xml);
        Assert.Contains("<env name=\"ACESTEP_API_HOST\" value=\"127.0.0.1\" />", xml);
        Assert.Contains("<onfailure action=\"restart\" delay=\"10 sec\" />", xml);
    }

    [Fact]
    public void ManifestValidator_ShouldReturnErrorsForInvalidManifest()
    {
        var validator = new ManifestValidator();
        var manifest = new ServiceManifest(); // empty
        
        var errors = validator.Validate(manifest).ToList();
        
        Assert.Contains("Id is required.", errors);
    }
    
    [Fact]
    public async Task YamlManifestReader_ShouldReadYamlFile()
    {
        // Arrange
        var yaml = @"
id: acestep
displayName: ACE-Step API
description: ACE-Step FastAPI service

runtime:
  workDir: C:\svc\runtimes\acestep\ACE-Step-1.5
  executable: C:\svc\runtimes\acestep\.venv\Scripts\python.exe
  arguments:
    - -m
    - acestep.api_server

service:
  wrapperDir: C:\svc\services\acestep
  startMode: delayed-auto
  onFailure: restart
  resetFailure: 1 hour

env:
  ACESTEP_API_HOST: 127.0.0.1
  ACESTEP_API_PORT: ""8010""

health:
  url: http://127.0.0.1:8010/health
  timeoutSec: 5
";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, yaml);
        
        var reader = new YamlManifestReader();

        // Act
        var manifest = await reader.ReadAsync(tempFile);

        // Assert
        Assert.Equal("acestep", manifest.Id);
        Assert.Equal("ACE-Step API", manifest.DisplayName);
        Assert.Equal(@"C:\svc\runtimes\acestep\ACE-Step-1.5", manifest.Runtime.WorkDir);
        Assert.Equal(2, manifest.Runtime.Arguments.Count);
        Assert.Equal("-m", manifest.Runtime.Arguments[0]);
        Assert.Equal("127.0.0.1", manifest.Env["ACESTEP_API_HOST"]);
        Assert.Equal("http://127.0.0.1:8010/health", manifest.Health.Url);

        // Cleanup
        File.Delete(tempFile);
    }
}
