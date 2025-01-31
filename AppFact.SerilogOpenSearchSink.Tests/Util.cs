using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenSearch.Client;

namespace AppFact.SerilogOpenSearchSink.Tests;

public static class Util
{
    private static string GetFilePath([CallerFilePath] string file = "") =>
        file;


    public static string GetSolutionDirectory()
    {
        var path = GetFilePath();
        path = Path.GetDirectoryName(path);
        path = Path.GetDirectoryName(path);
        return path!;
    }

    public static string HandleSolutionPathsInString(this string value)
    {
        var slnDir = GetSolutionDirectory();
        return value.Replace(slnDir, "{SolutionDirectory}");
    }
}

public static class OpenSearchClientExtensions
{
    public static async Task<IReadOnlyCollection<JsonObject>> SearchAll(this IOpenSearchClient client)
    {
        var result = await client.SearchAsync<JsonObject>(s => s.Size(420).Query(q => q.MatchAll()));
        Assert.True(result.IsValid);
        return result.Documents;
    }


    public static async Task<JsonArray> SearchAllAsJson(this IOpenSearchClient client)
    {
        var docs = await client.SearchAll();
        var arr = new JsonArray();
        foreach (var doc in docs)
        {
            arr.Add(doc);
        }

        var json = arr.ToJsonString();
        json = json.HandleSolutionPathsInString();

        arr = JsonSerializer.Deserialize<JsonArray>(json)!;

        return arr;
    }
}