#r "Newtonsoft.Json"

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Azure.CognitiveServices.ContentModerator;
using Newtonsoft.Json;
using HtmlAgilityPack;
using AngleSharp;
using AngleSharp.Html.Dom;
using AngleSharp.Dom;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("C# HTTP trigger function processed a request.");

    string url = req.Query["url"];
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    dynamic data = JsonConvert.DeserializeObject(requestBody);
    url = url ?? data?.url;

    if (url != null)
    {
        HtmlWeb web = new HtmlWeb();
        var htmlDoc = web.Load(url);

        string SubscriptionKey = "87f2a91f2e2a4554bee9153a279fe10e";
        string Endpoint = "https://contentmoderatorengine.cognitiveservices.azure.com/";

        ContentModeratorClient clientText = Authenticate(SubscriptionKey, Endpoint);
        Dictionary<string, Dictionary<int, string>> indexContentDictionary = GetIndexContentDictionary(url);


        List<string> output = new List<string>();
        int cnt = 0;
        foreach (var dicts in indexContentDictionary)
        {
            log.LogInformation(dicts.Value.Count.ToString());
            foreach (var index in dicts.Value)
            {
                string query = index.Value.Substring(0, Math.Min(1000, index.Value.Length));
                string moderatorOutput = ModerateText(clientText, query);
                output.Add(moderatorOutput);
                cnt++;
            }
        }

        string response = GetFinalRenderingResult(output, indexContentDictionary);
        return (ActionResult)new OkObjectResult(response);
    }

    return new BadRequestObjectResult("Please pass a URL on the query string or in the request body");
}

public static string ModerateText(ContentModeratorClient client, string content)
{
    string output = string.Empty;
    content = content.Replace(Environment.NewLine, " ");
    byte[] textBytes = Encoding.UTF8.GetBytes(content);
    MemoryStream stream = new MemoryStream(textBytes);

    var screenResult = client.TextModeration.ScreenText("text/plain", stream, "eng", true, true, null, true);
    return JsonConvert.SerializeObject(screenResult, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
    //log.LogInformation(output);
}

public static ContentModeratorClient Authenticate(string key, string endpoint)
{
    ContentModeratorClient client = new ContentModeratorClient(new ApiKeyServiceClientCredentials(key));

    client.Endpoint = endpoint;
    return client;
}

public static Dictionary<string, Dictionary<int, string>> GetIndexContentDictionary(string url)
{
    Dictionary<string, Dictionary<int, string>> indexContentDictionary = new Dictionary<string, Dictionary<int, string>>();
    Dictionary<int, string> mainDictionary = new Dictionary<int, string>();
    Dictionary<int, string> asideDictionary = new Dictionary<int, string>();

    var config = Configuration.Default.WithDefaultLoader();
    var context = BrowsingContext.New(config);
    IDocument document = context.OpenAsync(url).Result;
    var eleemtns = document.Body.ChildNodes.Where(x => x.NodeType == NodeType.Element).
        Where(z => z.NodeName.Equals("DIV", StringComparison.InvariantCultureIgnoreCase));
    foreach (var element in eleemtns)
    {
        var div = element as IHtmlDivElement;

        if (div != null && div.Id != null && div.Id.Contains("content"))
        {
            var childNodes = div.ChildNodes;
            foreach (var child in childNodes)
            {
                if (child.NodeName.Equals("MAIN", StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (var ol in child.ChildNodes)
                    {
                        var orderedListElement = ol as IHtmlOrderedListElement;
                        if (orderedListElement != null)
                        {
                            foreach (var listItem in orderedListElement.Children)
                                findTextContentOfInnerMostChild(mainDictionary, listItem, listItem.Index());

                        }
                    }
                }

                if (child.NodeName.Equals("ASIDE", StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (var ol in child.ChildNodes)
                    {
                        var orderedListElement = ol as IHtmlOrderedListElement;
                        if (orderedListElement != null)
                        {
                            foreach (var listItem in orderedListElement.Children)
                                findTextContentOfInnerMostChild(asideDictionary, listItem, listItem.Index());

                        }
                    }
                }
            }
            break;
        }
    }

    indexContentDictionary.Add("MAIN", mainDictionary);
    indexContentDictionary.Add("ASIDE", asideDictionary);
    return indexContentDictionary;
}

private static void findTextContentOfInnerMostChild(Dictionary<int, string> indexContentDictionary, IElement node, int index)
{
    if (node != null)
    {
        if (node is IHtmlHeadingElement || node is IHtmlParagraphElement || node is IHtmlAnchorElement)
        {
            if (!string.IsNullOrEmpty(node.TextContent))
            {
                string text = string.Empty;
                if (node is IHtmlHeadingElement)
                {
                    text = "Heading: ";
                }

                text = text + node.TextContent;
                if (!indexContentDictionary.ContainsKey(index))
                {
                    indexContentDictionary.Add(index, text);
                }
                else
                {
                    text = indexContentDictionary[index] + " " + text;
                    indexContentDictionary[index] = text;
                }
            }
        }

        foreach (var child in node.Children)
        {
            findTextContentOfInnerMostChild(indexContentDictionary, child, index);
        }
    }
}

public class ModeratorResult
{
    public string OriginalText { get; set; }
    public string NormalizedText { get; set; }
    public string AutoCorrectedText { get; set; }

    public string Misrepresentation { get; set; }

    public ClassificationObject Classification { get; set; }

    public StatusObject Status { get; set; }

    public object PII { get; set; }

    public string Language { get; set; }

    public object Terms { get; set; }
}

public class StatusObject
{
    public int Code { get; set; }
    public string Description { get; set; }

    public string Exception { get; set; }
}

public class ScoreObject
{
    public double Score { get; set; }
}

public class ClassificationObject
{
    public ScoreObject Category1 { get; set; }
    public ScoreObject Category2 { get; set; }
    public ScoreObject Category3 { get; set; }

    public bool ReviewRecommended { get; set; }
}


public static string GetFinalRenderingResult(List<string> result, Dictionary<string, Dictionary<int, string>> dictionary)
{
    Dictionary<string, Dictionary<string, List<int>>> renderingResult = new Dictionary<string, Dictionary<string, List<int>>>();
    Dictionary<string, List<int>> mainResult = new Dictionary<string, List<int>>();
    Dictionary<string, List<int>> asideResult = new Dictionary<string, List<int>>();
    mainResult.Add("offensive", new List<int>());
    asideResult.Add("offensive", new List<int>());
    mainResult.Add("safe", new List<int>());
    asideResult.Add("safe", new List<int>());
    List<ModeratorResult> classification = new List<ModeratorResult>();
    foreach (var obj in result)
    {
        classification.Add(JsonConvert.DeserializeObject<ModeratorResult>(obj));
    }

    bool offensive = false;
    int i = 0;
    var mainParsed = dictionary["MAIN"];
    while (i < mainParsed.Count && i < classification.Count)
    {
        var mREsult = classification[i];
        if (mREsult.Classification.Category1.Score >= 0.8 || mREsult.Classification.Category2.Score >= 0.9)
        {
            offensive = true;
        }

        var original = mainParsed[i];
        if (offensive)
        {
            mainResult["offensive"].Add(i);

        }
        else
        {
            mainResult["safe"].Add(i);

        }

        i++;

    }
    var asideParsed = dictionary["ASIDE"];
    int j = 0;
    while (j < asideParsed.Count && i < classification.Count)
    {
        var mREsult = classification[i];
        if (mREsult.Classification.Category1.Score >= 0.75 || mREsult.Classification.Category2.Score >= 0.85)
        {
            offensive = true;
        }

        var original = asideParsed[j];
        if (offensive)
        {
            asideResult["offensive"].Add(j);

        }
        else
        {
            asideResult["safe"].Add(j);

        }

        i++;
        j++;
    }

    renderingResult.Add("MAIN", mainResult);
    renderingResult.Add("ASIDE", asideResult);
    return JsonConvert.SerializeObject(renderingResult);
}

