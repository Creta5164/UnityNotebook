using JetBrains.Annotations;

namespace UnityNotebook
{
    // Static methods that are available to a running notebook cell
    [UsedImplicitly]
    public static class RuntimeMethods
    {
        public static void Show(object data)
        {
            if (data == null)
            {
                return;
            }
            var notebook = NBState.OpenedNotebook;
            var cell = NBState.RunningCell;
            var output = Renderers.GetCellOutputForObject(data);
            notebook.cells[cell].outputs.Add(output);
        }
    }
}