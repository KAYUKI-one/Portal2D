namespace Portal2D.Core.Data
{
    // Optional extension for per-instance level configuration (range, path, speed, etc.).
    // Plain prefabs need no adapter; their components and default values are instantiated normally.
    public interface ILevelObjectState
    {
        string CaptureLevelState();
        void RestoreLevelState(string state);
    }
    [System.Serializable]
    public struct SavedLevelObjectState
    {
        public int[] childIndices;
        public string componentType;
        public int componentIndex;
        public string state;
    }
}
