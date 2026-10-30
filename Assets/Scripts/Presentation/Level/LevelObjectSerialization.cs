using System.Collections.Generic;
using Portal2D.Core.Data;
using UnityEngine;

namespace Portal2D.Presentation.Level
{
    public static class LevelObjectSerialization
    {
        public static List<SavedLevelObjectState> Capture(GameObject root)
        {
            var states=new List<SavedLevelObjectState>();
            foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(component is not ILevelObjectState adapter) continue;
                var path=new List<int>(); var t=component.transform;
                while(t!=root.transform) { path.Add(t.GetSiblingIndex()); t=t.parent; }
                path.Reverse(); int index=0;
                foreach(var other in component.GetComponents(component.GetType())) { if(other==component) break; index++; }
                states.Add(new SavedLevelObjectState {childIndices=path.ToArray(),componentType=component.GetType().FullName,
                    componentIndex=index,state=adapter.CaptureLevelState()});
            }
            return states;
        }
        public static void Restore(GameObject root, List<SavedLevelObjectState> states)
        {
            if(states==null) return;
            foreach(var saved in states)
            {
                var target=root.transform;
                foreach(int index in saved.childIndices ?? System.Array.Empty<int>())
                { if(index<0 || index>=target.childCount) { target=null; break; } target=target.GetChild(index); }
                if(target==null) { Debug.LogWarning($"Missing level object child: {root.name}",root); continue; }
                int match=0; bool restored=false;
                foreach(var component in target.GetComponents<MonoBehaviour>())
                    if(component!=null && component.GetType().FullName==saved.componentType && match++==saved.componentIndex && component is ILevelObjectState adapter)
                    { adapter.RestoreLevelState(saved.state); restored=true; break; }
                if(!restored) Debug.LogWarning($"Missing level configuration component {saved.componentType} on {root.name}",root);
            }
        }
    }
}
