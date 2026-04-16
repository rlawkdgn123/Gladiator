using UnityCliConnector;
using Newtonsoft.Json.Linq;
using UnityEngine;

[UnityCliTool(Name = "ping_game", Description = "Simple connectivity test for this project", Group = "custom")]
public static class PingTool
{
    public static object HandleCommand(JObject parameters)
    {
        return new SuccessResponse("Project tool is working", new
        {
            project = Application.productName,
            unityVersion = Application.unityVersion,
            time = System.DateTime.Now.ToString("O")
        });
    }
}