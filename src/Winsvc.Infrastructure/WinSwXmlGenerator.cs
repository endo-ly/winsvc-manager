using System.Linq;
using System.Xml.Linq;
using Winsvc.Contracts.Manifest;
using Winsvc.Core;

namespace Winsvc.Infrastructure;

public class WinSwXmlGenerator : IServiceConfigGenerator
{
    public string Generate(ServiceManifest manifest)
    {
        var root = new XElement("service",
            new XElement("id", manifest.Id),
            new XElement("name", manifest.DisplayName),
            new XElement("description", manifest.Description),
            new XElement("executable", manifest.Runtime.Executable),
            new XElement("arguments", string.Join(" ", manifest.Runtime.Arguments)),
            new XElement("log", new XAttribute("mode", "roll")),
            new XElement("workingdirectory", manifest.Runtime.WorkDir),
            GetStartMode(manifest.Service.StartMode)
        );

        foreach (var env in manifest.Env)
        {
            root.Add(new XElement("env", 
                new XAttribute("name", env.Key), 
                new XAttribute("value", env.Value)
            ));
        }

        if (manifest.Service.OnFailure == "restart")
        {
            root.Add(new XElement("onfailure", new XAttribute("action", "restart"), new XAttribute("delay", "10 sec")));
        }

        if (!string.IsNullOrEmpty(manifest.Service.ResetFailure))
        {
            root.Add(new XElement("resetfailure", manifest.Service.ResetFailure));
        }

        var doc = new XDocument(root);
        return doc.ToString();
    }
    
    private object[] GetStartMode(string startMode)
    {
        if (startMode == "delayed-auto") 
            return new object[] { new XElement("startmode", "Automatic"), new XElement("delayedAutoStart", "true") };
        else if (startMode == "manual") 
            return new object[] { new XElement("startmode", "Manual") };
        
        return new object[] { new XElement("startmode", "Automatic") };
    }
}
