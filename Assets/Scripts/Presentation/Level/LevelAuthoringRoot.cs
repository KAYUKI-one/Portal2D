using Portal2D.Core.Data;
using UnityEngine;

namespace Portal2D.Presentation.Level
{
    // Keeps the editing scene associated with its asset across editor window/domain reloads.
    [DisallowMultipleComponent]
    public sealed class LevelAuthoringRoot : MonoBehaviour
    {
        public LevelAsset SourceLevel;
    }
}
