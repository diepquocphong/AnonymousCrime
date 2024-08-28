using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using System.Threading.Tasks;
using Unity.VisualScripting;

//Special thanks to GameMan, this instruction was created with the help of
//their GC2 Instruction Maker GPT

[Version(1, 0, 0)]
[Title("Parse JSON and Extract Multiple Values")]
[Description("Parses a JSON string and extracts multiple values using specified paths.")]
[Category("Network/ParseJsonAndExtractValues")]

[Parameter("JSON Input Text", "The JSON string to parse.")]
[Parameter("Path Maps", "Array of path and output text mappings.")]
[Keywords("Network", "Json", "Parse", "Extract")]

/*
Notes: Pathing right now is for very simple known types, 
coding would be needed for more complicated types.

Pathing uses NewtonSoft.SelectToken(); check on their site for more examples.

//Path example jsonInText
{"Books":[
    {"Name":"BookA","Info":[{"Pages":"100","Author":"Someone"}]},
    {"Name":"BookB","Info":[{"Pages":"200","Author":"YouKnow"}]}
]}
Path for BookB pages(JsonPath) = "Books[1].Info[0].Pages" = returns "200"

The idea is to map these to your local named variable list or other string outputs.
*/

[Serializable]
public class InstructionParseJsonAndExtractValues : Instruction
{
    [Serializable]
    protected class PathMap
    {
        public string Comment;
        public PropertyGetString JsonPath = new PropertyGetString();
        public PropertySetString PathValue = new PropertySetString();
    }

    [SerializeField] private PropertyGetString jsonInText = new PropertyGetString();
    [SerializeField] private PathMap[] pathMaps;

    protected override Task Run(Args args)
    {
        string jsonText = jsonInText.Get(args);

        try
        {
            JObject jsonObj = JObject.Parse(jsonText);

            foreach (var pathMap in pathMaps)
            {
                JToken selectedToken = jsonObj.SelectToken(pathMap.JsonPath.Get(args));
                if (selectedToken != null)
                {
                    pathMap.PathValue.Set(selectedToken.ToString(), args);
                }
                else
                {
                    Debug.LogWarning($"Path not found in JSON: {pathMap.JsonPath.Get(args)}");
                }
            }
        }
        catch (JsonException ex)
        {
            Debug.LogError($"JSON parsing error: {ex.Message}");
        }

        return DefaultResult;
    }
}
