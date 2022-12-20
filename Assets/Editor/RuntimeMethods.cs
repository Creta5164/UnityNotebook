using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEngine;

public static class RuntimeMethods
{
    public static object Delay(float seconds) => new EditorWaitForSeconds(seconds);

    public static void Show(object data)
    {
        if (NotebookWindowData.instance.runningCell != null)
        {
            AddDataToOutput(NotebookWindowData.instance.runningCell, data);
        }
    }
    
    private static void AddDataToOutput(Notebook.Cell cell, object data)
    {
        // TODO get the running cell
        
        switch (data)
        {
            case string s:
                var txtOutput = new Notebook.CellOutput()
                {
                    outputType = Notebook.OutputType.DisplayData,
                    data = new List<Notebook.CellOutputDataEntry>
                    {
                        new()
                        {
                            mimeType = "text/plain",
                            stringData = new List<string>()
                        }
                    },
                    metadata = new List<Notebook.CellOutputMetadataEntry>
                    {
                    }
                };
                break;
            case Vector3 v:
                break;
            case Vector2 v:
                break;
            case Quaternion q:
                break;
            case Matrix4x4 m:
                break;
            case AnimationCurve a:
                break;
            case Color c:
                break;
            case Texture2D t:
                var imgOutput = new Notebook.CellOutput()
                {
                    outputType = Notebook.OutputType.DisplayData,
                    data = new List<Notebook.CellOutputDataEntry>
                    {
                        new()
                        {
                            mimeType = "image/png",
                            imageData = new Texture2D(1,1)
                        }
                    },
                    metadata = new List<Notebook.CellOutputMetadataEntry>
                    {
                        // width, height
                    }
                };
                break;
            case Material m:
                break;
            case Mesh m:
                break;
            default:
                break;
        }
    }
}