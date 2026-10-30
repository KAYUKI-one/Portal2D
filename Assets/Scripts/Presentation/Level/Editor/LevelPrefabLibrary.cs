using Portal2D.Core.Data;
using UnityEditor;
using UnityEngine;

namespace Portal2D.Presentation.Level.Editor
{
    public static class LevelPrefabLibrary
    {
        public const string DefaultCatalogPath="Assets/Resource/LevelObjectCatalog.asset";
        public static LevelObjectCatalog GetDefaultCatalog()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<LevelObjectCatalog>(DefaultCatalogPath);
            if(catalog!=null) return catalog;
            catalog=ScriptableObject.CreateInstance<LevelObjectCatalog>();
            catalog.Add(Resources.Load<GameObject>("Gameplay/ActivationOrb"),"激活圆球","通关");
            catalog.Add(Resources.Load<GameObject>("Gameplay/Pushable_StoneBlock"),"可移动物块","物理物件");
            AssetDatabase.CreateAsset(catalog,DefaultCatalogPath);
            return catalog;
        }
        public static bool RegisterPrefab(GameObject prefab, string displayName=null, string category="自定义物件", LevelObjectCatalog catalog=null)
        {
            if(prefab==null || !PrefabUtility.IsPartOfPrefabAsset(prefab) ||
                AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(prefab))!=prefab) return false;
            catalog=catalog!=null ? catalog : GetDefaultCatalog();
            Undo.RecordObject(catalog,"导入关卡物件");
            if(!catalog.Add(prefab,displayName,category)) return false;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog); return true;
        }
        [MenuItem("Assets/Portal2D/加入关卡物件库",false,30)]
        private static void ImportSelection()
        {
            foreach(var obj in Selection.objects) if(obj is GameObject prefab) RegisterPrefab(prefab);
            Portal2DLevelEditorWindow.OpenWindow();
        }
        [MenuItem("Assets/Portal2D/加入关卡物件库",true)]
        private static bool CanImport() => Selection.activeObject is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab);
    }
}
