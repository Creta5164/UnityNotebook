using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEditor;

namespace UnityNotebook
{
    // Implementation of the Jupyter Notebook file format
    // https://nbformat.readthedocs.io/en/latest/format_description.html
    // https://github.com/jupyter/nbformat/blob/main/nbformat/v4/nbformat.v4.schema.json
    [JsonConverter(typeof(NotebookConverter))]
    public class Notebook : ScriptableObject
    {
        public int format = 4;
        public int formatMinor = 2;
        public List<Cell> cells = new();

        public static Notebook CreateAsset(string path)
        {
            var notebook = CreateInstance<Notebook>();
            var json = JsonConvert.SerializeObject(notebook, Formatting.Indented);
            System.IO.File.WriteAllText(path, json);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Notebook>(path);
        }

        [MenuItem("Assets/Create/Notebook", false, 80)]
        public static void CreateAssetMenu()
        {
            // Get the path to the currently selected folder
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
            {
                path = "Assets";
            }
            else if (!string.IsNullOrEmpty(System.IO.Path.GetExtension(path)))
            {
                path = path.Replace(System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
            }

            // Create asset
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(path + "/New Notebook.ipynb");
            var asset = CreateAsset(assetPath);
            Selection.activeObject = asset;
        }
        
        // Saves the current ScriptableObject data back to the underlying json asset file
        public void SaveScriptableObject()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
            EditorUtility.ClearDirty(this);
        }

        public void SaveJson()
        {
            SaveScriptableObject();
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            System.IO.File.WriteAllText(AssetDatabase.GetAssetPath(this), json);
        }
    }

    [Serializable]
    [JsonConverter(typeof(CellConverter))]
    public class Cell
    {
        // common
        public CellType cellType; // markdown, code
        public int executionCount;
            
        // TODO metadata
        // public List<CellMetadataEntry> metadata; // empty object if its a markdown cell
        public string[] source = Array.Empty<string>(); // could be a single string or a list of strings

        // code cell
        [SerializeReference]
        public List<CellOutput> outputs = new();

        // temp UI vars
        [NonSerialized] public string rawText;
        [NonSerialized] public string highlightedText = "";
        [NonSerialized] public Vector2 scroll;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CellType
    {
        [EnumMember(Value = "markdown")]
        Markdown,
        [EnumMember(Value = "code")]
        Code,
        [EnumMember(Value = "raw")]
        Raw
    }

    [Serializable]
    [JsonConverter(typeof(CellOutputConverter))]
    public class CellOutput
    {
        public OutputType outputType;
        [NonSerialized] public Vector2 scroll;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OutputType
    {
        [EnumMember(Value = "stream")]
        Stream,
        [EnumMember(Value = "display_data")]
        DisplayData,
        [EnumMember(Value = "execute_result")]
        ExecuteResult,
        [EnumMember(Value = "error")]
        Error
    }

    [Serializable]
    [JsonConverter(typeof(CellOutputDisplayDataConverter))]
    public class CellOutputDisplayData : CellOutput
    {
        public CellOutputDisplayData() => outputType = OutputType.DisplayData;

        [JsonIgnore]
        public List<ValueWrapper> values = new(); // mime-type -> data, often text/plain, image/png, application/json

        // public List<CellOutputMetadataEntry> metadata = new(); // mime-type -> metadata
    }

    [Serializable]
    [JsonConverter(typeof(CellOutputExecuteResultsConverter))]
    public class CellOutputExecuteResults : CellOutput
    {
        public CellOutputExecuteResults() => outputType = OutputType.ExecuteResult;
        public int executionCount;
            
        [JsonIgnore]
        public ValueWrapper backingValue;
    }

    [Serializable]
    [JsonConverter(typeof(CellOutputErrorConverter))]
    public class CellOutputError : CellOutput
    {
        public CellOutputError() => outputType = OutputType.Error;
            
        public string ename;
        public string evalue;
        public List<string> traceback = new();
    }

    [Serializable]
    [JsonConverter(typeof(CellOutputStreamConverter))]
    public class CellOutputStream : CellOutput
    {
        public CellOutputStream() => outputType = OutputType.Stream;
            
        public string name; // if stream output cell: stdout, stderr
        public List<string> text = new();
    }
}
