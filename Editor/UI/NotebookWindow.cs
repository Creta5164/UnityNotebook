using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using static UnityNotebook.Styles;

namespace UnityNotebook
{
    public class NotebookWindow : EditorWindow
    {
        [MenuItem("Window/Notebook")]
        public static void Init()
        {
            var wnd = GetWindow<NotebookWindow>();
            wnd.titleContent = new GUIContent("Notebook");
        }
        
        private static bool _openExternally;

        private static bool IsRunning => NBState.RunningCell >= 0;

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (_openExternally)
            {
                return false;
            }

            var target = EditorUtility.InstanceIDToObject(instanceID);
            if (target is not Notebook)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(instanceID);
            if (Path.GetExtension(path) != ".ipynb")
            {
                return false;
            }

            var notebook = AssetDatabase.LoadAssetAtPath<Notebook>(path);
            Init();
            NBState.OpenedNotebook = notebook;
            return true;
        }

        private static void ChangeNotebook(Notebook notebook)
        {
            if (notebook == NBState.OpenedNotebook)
            {
                return;
            }
            NBState.SaveJson();
            NBState.CloseNotebook();
            NBState.OpenedNotebook = notebook;
        }
        
        private void OnEnable()
        {
            ChangeNotebook(NBState.OpenedNotebook);
            EditorApplication.update += DoRepaint;
            Undo.undoRedoPerformed += DoRepaint;
        }
        
        private void OnDisable()
        {
            Evaluator.Stop();
            EditorApplication.update -= DoRepaint;
            Undo.undoRedoPerformed -= DoRepaint;
        }
        
        private void OnDestroy()
        {
            NBState.SaveJson();
        }

        private int _lastKeyboardControl;
        
        private void OnGUI()
        {
            Styles.Init();
            
            // Save the asset when moving between fields
            if (GUIUtility.keyboardControl != _lastKeyboardControl)
            {
                _lastKeyboardControl = GUIUtility.keyboardControl;
                NBState.SaveScriptableObject();
            }

            DrawWindowToolbar();

            if (NBState.OpenedNotebook == null)
            {
                DrawCreateNotebook();
            }
            else
            {
                DrawNotebook(NBState.OpenedNotebook);
            }
        }

        private void DrawCreateNotebook()
        {
            const int buttonWidth = 200;
            const int buttonHeight = 50;
            var buttonRect = new Rect(
                position.width / 2 - buttonWidth / 2,
                (position.height / 2 - buttonHeight / 2) - 35,
                buttonWidth,
                buttonHeight
            );
            
            if (GUI.Button(buttonRect, "Create Notebook"))
            {
                var path = EditorUtility.SaveFilePanelInProject("Create Notebook", "Notebook", "ipynb",
                    "Create Notebook");
                if (!string.IsNullOrEmpty(path))
                {
                    if (!path.EndsWith(".ipynb"))
                    {
                        path += ".ipynb";
                    }

                    var nb = Notebook.CreateAsset(path);
                    AssetDatabase.Refresh();
                    EditorGUIUtility.PingObject(nb);
                    nb.cells.Add(new Notebook.Cell {cellType = Notebook.CellType.Code});
                    NBState.OpenedNotebook = nb;
                }
            }

            buttonRect.y += buttonHeight + 10;
            if (GUI.Button(buttonRect, "Open Notebook"))
            {
                var path = EditorUtility.OpenFilePanel("Open Notebook", Application.dataPath, "ipynb");
                if (!string.IsNullOrEmpty(path))
                {
                    // Get assets-relative path
                    path = path.Replace(Application.dataPath, "Assets");
                    var nb = AssetDatabase.LoadAssetAtPath<Notebook>(path);
                    ChangeNotebook(nb);
                }
            }

            buttonRect.y += buttonHeight + 10;

            DrawNotebookAssetsPopup(buttonRect);
        }

        private static void DrawNotebookAssetsPopup(Rect rect)
        {
            var guids = AssetDatabase.FindAssets("t:Notebook");
            var notebooks = new List<Notebook>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                notebooks.Add(AssetDatabase.LoadAssetAtPath<Notebook>(path));
            }

            if (notebooks.Count == 0)
            {
                return;
            }

            notebooks.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            var notebookNames = new string[notebooks.Count];
            for (var i = 0; i < notebooks.Count; i++)
            {
                notebookNames[i] = AssetDatabase.GetAssetPath(notebooks[i]);
                // Print forward slashes instead of using them as submenu separators.
                // Replaces the slash with division character.
                notebookNames[i] = notebookNames[i].Replace("/", " \u2215 ");
            }

            var index = EditorGUI.Popup(rect, "Notebooks", -1, notebookNames);
            if (index >= 0)
            {
                ChangeNotebook(notebooks[index]);
            }
        }
        
        private void DrawWindowToolbar()
        {
            using var h = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
            
            var nb = NBState.OpenedNotebook;
            using var _ = new EditorGUI.DisabledScope(nb == null);
            
            if (nb != null && IsRunning)
            {
                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    Evaluator.Stop();
                }
            }
            else
            {
                if (GUILayout.Button("Run", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    GUI.FocusControl(null);
                    // TODO repaint callback doesn't work reliably, the final cell output often isn't drawn
                    Evaluator.ExecuteAllCells(nb, Repaint);
                }
            }

            using (new EditorGUI.DisabledScope(nb == null || IsRunning))
            {
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                {
                    Undo.RecordObject(nb, "Clear All Output");
                    NBState.ClearOutput();
                }

                using (new EditorGUI.DisabledScope(nb != null && NBState.instance.scriptState == null))
                {
                    if (GUILayout.Button("Restart", EditorStyles.toolbarButton))
                    {
                        NBState.instance.scriptState = null;
                    }
                }
                
                EditorGUILayout.Space();

                if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                {
                    NBState.SaveJson();
                }
                // Revert unsaved ScriptableObject changes by reimporting the json file
                if (GUILayout.Button("Revert", EditorStyles.toolbarButton))
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(nb));
                }
                if (GUILayout.Button("Edit", EditorStyles.toolbarButton))
                {
                    _openExternally = true;
                    AssetDatabase.OpenAsset(nb);
                    _openExternally = false;
                }
            }
            
            GUILayout.FlexibleSpace();
            
            // Notebook object field
            using (new EditorGUI.DisabledScope(nb != null && IsRunning))
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var newNotebook = EditorGUILayout.ObjectField(NBState.OpenedNotebook, typeof(Notebook), true) as Notebook;
                    if (check.changed)
                    {
                        ChangeNotebook(newNotebook);
                    }
                }
            }
        }

        private void DoRepaint()
        {
            if (NBState.RunningCell == -1)
            {
                return;
            }

            Repaint();
        }

        private void DrawNotebook(Notebook notebook)
        {
            if (notebook == null)
            {
                return;
            }
            
            if (Shortcuts.ConsumeReturnKey && Event.current.type == EventType.KeyUp)
            {
                Shortcuts.ConsumeReturnKey = false;
            }
            if (Event.current.isKey && Shortcuts.ConsumeReturnKey && Event.current.character == '\n')
            {
                Event.current.Use();
            }

            // Keyboard shortcuts
            if (Shortcuts.HandleKeyboardShortcuts(notebook))
            {
                NBState.SaveScriptableObject();
                Event.current.Use();
                Repaint();
            }

            using var scrollView = new EditorGUILayout.ScrollViewScope(NBState.Scroll, false, false);
            NBState.Scroll = scrollView.scrollPosition;
            
            var cellCount = notebook.cells.Count;
            var cellIndex = 0;
            do
            {
                if (DrawAddCellButtons(notebook, cellIndex, out var headerRect))
                {
                    // added a cell, loop invalidated
                    break;
                }

                if (cellCount > 0 && cellIndex < cellCount)
                {
                    DrawCell(notebook, cellIndex);
                    
                    // called after drawing the cell so it displays in front of the cell
                    if (DrawCellToolbar(notebook, cellIndex, headerRect))
                    {
                        // cells were modified, break out of the draw loop
                        break;
                    }
                    
                    // detect a click inside of the box
                    var cellRect = GUILayoutUtility.GetLastRect();
                    if (Event.current.type == EventType.MouseDown && cellRect.Contains(Event.current.mousePosition))
                    {
                        NBState.SelectedCell = cellIndex;
                        GUI.FocusControl(null);
                        Event.current.Use();
                        Repaint();
                    }
                }

                cellIndex++;
            } while (cellIndex < cellCount + 1);
        }
        
        private static bool DrawAddCellButtons(Notebook notebook, int cellIndex, out Rect rect)
        {
            using var _ = new EditorGUI.DisabledScope(IsRunning);
            
            // Add cell buttons
            rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            var buttonRect = new Rect(rect.x + rect.width / 2 - 60, rect.y, 60, 20);
                
            bool AddButton(string title, Notebook.CellType type)
            {
                if (!GUI.Button(buttonRect, title, EditorStyles.toolbarButton))
                {
                    return false;
                }
                Undo.RecordObject(notebook, "Add Cell");
                var c = new Notebook.Cell { cellType = type };
                notebook.cells.Insert(cellIndex, c);
                NBState.SelectedCell = cellIndex;
                NBState.IsEditMode = true;
                Event.current.Use();
                return true;
            }

            var addCode = AddButton("+ Code", Notebook.CellType.Code);
            buttonRect.x += 60;
            var addText = AddButton("+ Text", Notebook.CellType.Markdown);
                
            return addCode || addText;
        }

        private static bool DrawCellToolbar(Notebook notebook, int cellIndex, Rect rect)
        {
            using var _ = new EditorGUI.DisabledScope(IsRunning);
            
            // Cell toolbar
            const int buttonWidth = 22;
            rect = new Rect(rect.x + rect.width - (buttonWidth * 3) - 10, rect.y + 13, buttonWidth, 16);
            using (new EditorGUI.DisabledScope(cellIndex == 0))
            {
                if (GUI.Button(rect, "▲", EditorStyles.miniButtonLeft))
                {
                    Undo.RecordObject(notebook, "Move Cell Up");
                    notebook.cells.Insert(cellIndex - 1, notebook.cells[cellIndex]);
                    notebook.cells.RemoveAt(cellIndex + 1);
                    NBState.SelectedCell = cellIndex - 1;
                    Event.current.Use();
                    return true;
                }
            }

            rect.x += buttonWidth;
            using (new EditorGUI.DisabledScope(cellIndex == notebook.cells.Count - 1))
            {
                if (GUI.Button(rect, "▼", EditorStyles.miniButtonMid))
                {
                    Undo.RecordObject(notebook, "Move Cell Down");
                    notebook.cells.Insert(cellIndex + 2, notebook.cells[cellIndex]);
                    notebook.cells.RemoveAt(cellIndex);
                    NBState.SelectedCell = cellIndex + 1;
                    Event.current.Use();
                    return true;
                }
            }

            rect.x += buttonWidth;
            if (GUI.Button(rect, "✕", EditorStyles.miniButtonRight))
            {
                Undo.RecordObject(notebook, "Delete Cell");
                notebook.cells.RemoveAt(cellIndex);
                NBState.SelectedCell = Mathf.Max(0, cellIndex - 1);
                Event.current.Use();
                return true;
            }

            return false;
        }

        private static void DrawCell(Notebook notebook, int cell)
        {
            if (cell >= notebook.cells.Count)
            {
                return;
            }
            
            switch (notebook.cells[cell].cellType)
            {
                case Notebook.CellType.Code:
                    using (new GUILayout.VerticalScope(NBState.SelectedCell == cell ? CodeCellBoxSelectedStyle : CodeCellBoxStyle))
                    {
                        DrawCodeCell(notebook, cell);
                        DrawCodeCellOutput(notebook, cell);
                    }
                    break;
                case Notebook.CellType.Markdown:
                    using (new GUILayout.VerticalScope(NBState.SelectedCell == cell ? CellBoxSelectedStyle : CellBoxStyle))
                    {
                        DrawTextCell(notebook, cell);
                    }
                    break;
                case Notebook.CellType.Raw:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void DrawTextCell(Notebook notebook, int cell)
        {
            if (!NBState.IsEditMode || NBState.SelectedCell != cell)
            {
                Markdown.Draw(notebook.cells[cell].source);
                return;
            }
            
            using var _ = new EditorGUILayout.HorizontalScope();
            
            GUILayout.Space(25);
            notebook.cells[cell].rawText = notebook.cells[cell].source == null ? string.Empty : string.Concat(notebook.cells[cell].source);
            GUI.SetNextControlName(CellInputControlName);
            notebook.cells[cell].rawText = EditorGUILayout.TextArea(notebook.cells[cell].rawText, TextStyle);
            UpdateFocusAndMode(cell);
            TryUpdateCellSource(notebook.cells[cell]);
        }

        private static void DrawCodeCell(Notebook notebook, int cell)
        {
            using var _ = new EditorGUILayout.HorizontalScope();
            
            // Run button
            if (IsRunning && NBState.RunningCell == cell)
            {
                if (GUILayout.Button("■", GUILayout.Width(20), GUILayout.Height(20)))
                {
                    Evaluator.Stop();
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(NBState.RunningCell != -1))
                {
                    if (GUILayout.Button("▶", GUILayout.Width(20), GUILayout.Height(20)))
                    {
                        GUI.FocusControl(null);
                        Evaluator.ExecuteCell(notebook, cell);
                    }
                }
            }
            
            DrawCodeCellTextArea(notebook, cell);
        }

        private static void DrawCodeCellTextArea(Notebook notebook, int cell)
        {
            using var _ = new EditorGUILayout.VerticalScope();
            
            var c = notebook.cells[cell];
            c.rawText ??= string.Concat(c.source);
            var syntaxTheme = EditorGUIUtility.isProSkin ? SyntaxHighlighting.Theme.Dark : SyntaxHighlighting.Theme.Light;
            // a horizontal scroll view
            var height = (c.source.Length == 0 ? 1 : c.source.Length) * 14 + 28;
            c.scroll = GUILayout.BeginScrollView(c.scroll, false, false, GUILayout.Height(height));
            GUI.SetNextControlName(CellInputControlName);
            CodeArea.Draw(ref c.rawText, ref c.highlightedText, syntaxTheme, CodeStyle);

            UpdateFocusAndMode(cell);

            GUILayout.EndScrollView();
            // split code area's text into separate lines to store in scriptable object
            TryUpdateCellSource(c);
        }

        private static void DrawCodeCellOutput(Notebook notebook, int cell)
        {
            if (notebook.cells[cell].outputs.Count <= 0)
            {
                return;
            }
            using var _ = new EditorGUILayout.HorizontalScope();
            if (GUILayout.Button("✕", GUILayout.Width(20), GUILayout.Height(20)))
            {
                Undo.RecordObject(notebook, "Clear Output");
                notebook.cells[cell].outputs.Clear();
                NBState.SaveScriptableObject();
            }
            DrawOutput(notebook.cells[cell]);
        }

        private static void DrawOutput(Notebook.Cell cell)
        {
            using var _ = new EditorGUILayout.VerticalScope();
            
            foreach (var output in cell.outputs)
            {
                switch (output.outputType)
                {
                    case Notebook.OutputType.Stream:
                        EditorGUILayout.TextArea(string.Concat(output.text), TextNoBackgroundStyle);
                        break;
                    case Notebook.OutputType.ExecuteResult:
                        foreach (var data in output.data)
                        {
                            EditorGUILayout.TextArea(string.Concat(data.stringData), TextNoBackgroundStyle);
                        }
                        break;
                    case Notebook.OutputType.DisplayData:
                        foreach (var data in output.data)
                        {
                            var renderer = Renderers.GetRendererForMimeType(data.mimeType);
                            renderer.Render(data);
                        }
                        break;
                    // TODO parse terminal control codes, set colors
                    case Notebook.OutputType.Error:
                        var c = GUI.color;
                        GUI.color = Color.red;
                        GUILayout.Label(output.ename);
                        GUILayout.Label(output.evalue);
                        GUI.color = c;
                        // TODO convert ANSI escape sequences to HTML tags
                        var str = string.Join("\n", output.traceback);
                        output.scroll = GUILayout.BeginScrollView(output.scroll);
                        EditorGUILayout.TextArea(str, CodeNoBackgroundStyle, GUILayout.ExpandHeight(false));
                        GUILayout.EndScrollView();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private const string CellInputControlName = "CellInputField";

        private static void UpdateFocusAndMode(int cell)
        {
            if (cell == NBState.SelectedCell && NBState.IsEditMode)
            {
                GUI.FocusControl(CellInputControlName);
            }
            else if (GUI.GetNameOfFocusedControl() == CellInputControlName)
            {
                NBState.IsEditMode = true;
                NBState.SelectedCell = cell;
            }
        }

        // When the cell text is changed via UI, update the cell's source lines
        private static void TryUpdateCellSource(Notebook.Cell cell)
        {
            if (!GUI.changed)
            {
                return;
            }
            NBState.CopyRawTextToSourceLines(cell);
            GUI.changed = false;
        }
    }
}