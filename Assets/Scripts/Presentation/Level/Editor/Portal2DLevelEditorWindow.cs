using System;
using System.Collections.Generic;
using Portal2D.Core.Data;
using Portal2D.Presentation.Gimmicks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace Portal2D.Presentation.Level.Editor
{
    public sealed class Portal2DLevelEditorWindow : EditorWindow
    {
        [SerializeField] private LevelAsset currentLevelAsset, selectedLevel;
        [SerializeField] private LevelObjectCatalog catalog;
        private readonly List<LevelAsset> levels=new();
        private Vector2 scroll, levelScroll;
        private string levelSearch="", objectSearch="";
        private GameObject importPrefab;
        private GUIStyle levelStyle;
        private bool editBounds;
        [MenuItem("Window/Portal2D/Level Editor (关卡编辑器)", false, 20)]
        public static void OpenWindow() => GetWindow<Portal2DLevelEditorWindow>("关卡编辑器").Show();
        private void OnEnable()
        {
            minSize=new Vector2(780,560);
            if(catalog==null) catalog=LevelPrefabLibrary.GetDefaultCatalog();
            RefreshLevels(); EditorApplication.projectChanged+=RefreshLevels;
            SceneView.duringSceneGui+=DrawSceneBounds;
            var root=GameObject.Find("[LevelBuilder_Root]");
            if(root!=null && root.TryGetComponent<LevelAuthoringRoot>(out var binding)) currentLevelAsset=binding.SourceLevel;
            if(selectedLevel==null) selectedLevel=currentLevelAsset!=null ? currentLevelAsset : levels.Count>0 ? levels[0] : null;
        }
        private void OnDisable()
        { EditorApplication.projectChanged-=RefreshLevels; SceneView.duringSceneGui-=DrawSceneBounds; }
        private void OnHierarchyChange()
        {
            var root=GameObject.Find("[LevelBuilder_Root]");
            if(root!=null && root.TryGetComponent<LevelAuthoringRoot>(out var binding)) currentLevelAsset=binding.SourceLevel;
            else currentLevelAsset=null;
            Repaint();
        }
        private void RefreshLevels()
        {
            levels.Clear();
            foreach(var guid in AssetDatabase.FindAssets("t:LevelAsset"))
            { var level=AssetDatabase.LoadAssetAtPath<LevelAsset>(AssetDatabase.GUIDToAssetPath(guid)); if(level!=null) levels.Add(level); }
            levels.Sort((a,b)=>string.Compare(a.LevelDisplayName,b.LevelDisplayName,StringComparison.CurrentCulture)); Repaint();
        }
        private void OnGUI()
        {
            levelStyle ??= new GUIStyle(EditorStyles.miniButton) {alignment=TextAnchor.MiddleLeft,wordWrap=true};
            if(EditorApplication.isPlaying) EditorGUILayout.HelpBox("停止运行后可编辑关卡。",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            using(new EditorGUILayout.HorizontalScope())
            {
                DrawLevelBrowser();
                using(new EditorGUILayout.VerticalScope())
                {
                    scroll=EditorGUILayout.BeginScrollView(scroll);
                    DrawSelectedLevel();
                    var root=GameObject.Find("[LevelBuilder_Root]");
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("当前编辑："+(currentLevelAsset!=null ? currentLevelAsset.LevelDisplayName : "尚未载入关卡"),EditorStyles.boldLabel);
                    using(new EditorGUI.DisabledScope(root==null || currentLevelAsset==null))
                    {
                        if(currentLevelAsset!=null)
                        {
                            var data=new SerializedObject(currentLevelAsset); data.Update();
                            foreach(var field in new[]{("levelId","关卡标识"),("levelDisplayName","显示名称"),("nextLevel","下一关"),
                                ("initialCameraSize","摄像机范围"),("loseCondition","失败条件"),("fallDeathY","掉落死亡高度"),("goalTriggerSize","出口触发范围")})
                                EditorGUILayout.PropertyField(data.FindProperty(field.Item1),new GUIContent(field.Item2));
                            data.ApplyModifiedProperties();
                            DrawEnvironment(root);
                        }
                        EditorGUILayout.Space(); EditorGUILayout.LabelField("瓦片与标记",EditorStyles.boldLabel);
                        if(root!=null) foreach(var map in root.GetComponentsInChildren<Tilemap>())
                            if(GUILayout.Button("编辑瓦片图层 · "+map.name)) { Selection.activeGameObject=map.gameObject; EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette"); }
                        if(GUILayout.Button("添加实心瓦片图层")) AddTilemap(root);
                        using(new EditorGUILayout.HorizontalScope())
                        {
                            if(GUILayout.Button("选择出生点")) SelectMarker("PlayerSpawn_Point");
                            if(GUILayout.Button("选择出口")) SelectMarker("GoalExit_Point");
                        }
                    }
                    EditorGUILayout.Space(); DrawCatalog(root);
                    using(new EditorGUI.DisabledScope(root==null || currentLevelAsset==null))
                        if(GUILayout.Button("保存当前编辑关卡",GUILayout.Height(34))) Save();
                    EditorGUILayout.EndScrollView();
                }
            }
        }
        private void DrawLevelBrowser()
        {
            using(new EditorGUILayout.VerticalScope(GUILayout.Width(245)))
            {
                EditorGUILayout.LabelField("关卡库 · "+levels.Count,EditorStyles.boldLabel);
                levelSearch=EditorGUILayout.TextField(levelSearch,GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField);
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("新建")) CreateLevel();
                    if(GUILayout.Button("刷新")) RefreshLevels();
                }
                levelScroll=EditorGUILayout.BeginScrollView(levelScroll);
                foreach(var level in levels)
                {
                    if(!Matches(level.LevelDisplayName+" "+level.LevelId+" "+AssetDatabase.GetAssetPath(level),levelSearch)) continue;
                    var previous=GUI.backgroundColor; if(selectedLevel==level) GUI.backgroundColor=new Color(0.45f,0.8f,1);
                    if(GUILayout.Button((currentLevelAsset==level?"● ":"")+level.LevelDisplayName+"\n"+level.LevelId+" · "+level.SavedItems.Count+" 个物件",levelStyle,GUILayout.Height(58)))
                    { selectedLevel=level; GUI.FocusControl(null); if(Event.current.clickCount==2) LoadSelected(); }
                    GUI.backgroundColor=previous;
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.LabelField("单击预览，双击载入\n● 表示场景正在编辑的关卡",EditorStyles.wordWrappedMiniLabel);
            }
        }
        private void DrawSelectedLevel()
        {
            selectedLevel=(LevelAsset)EditorGUILayout.ObjectField("浏览关卡",selectedLevel,typeof(LevelAsset),false);
            if(selectedLevel==null) return;
            EditorGUILayout.LabelField(selectedLevel.LevelDisplayName,EditorStyles.boldLabel);
            DrawMapPreview(selectedLevel);
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(selectedLevel),EditorStyles.wordWrappedMiniLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("载入所选关卡",GUILayout.Height(28))) LoadSelected();
                if(GUILayout.Button("在 Project 中定位",GUILayout.Height(28))) EditorGUIUtility.PingObject(selectedLevel);
            }
        }
        private static void DrawMapPreview(LevelAsset level)
        {
            var rect=GUILayoutUtility.GetRect(120,150,GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(rect,level.BackgroundColor);
            var min=Vector2.Min(level.PlayerSpawnPosition,level.GoalPosition); var max=Vector2.Max(level.PlayerSpawnPosition,level.GoalPosition);
            if(level.ConfineCamera) { min=Vector2.Min(min,level.CameraBounds.min); max=Vector2.Max(max,level.CameraBounds.max); }
            foreach(var layer in level.TilemapLayers) if(layer.tiles!=null) foreach(var tile in layer.tiles)
            { min=Vector2.Min(min,(Vector2)(Vector3)tile.position); max=Vector2.Max(max,(Vector2)(Vector3)tile.position+Vector2.one); }
            foreach(var item in level.SavedItems) { min=Vector2.Min(min,item.position); max=Vector2.Max(max,item.position); }
            var extent=max-min+Vector2.one*2; float scale=Mathf.Min((rect.width-16)/extent.x,(rect.height-16)/extent.y);
            Vector2 Point(Vector2 p) => new Vector2(rect.center.x+(p.x-(min.x+max.x)*0.5f)*scale,rect.center.y-(p.y-(min.y+max.y)*0.5f)*scale);
            foreach(var layer in level.TilemapLayers) if(layer.tiles!=null) foreach(var tile in layer.tiles)
            { var p=Point((Vector2)(Vector3)tile.position+Vector2.up); EditorGUI.DrawRect(new Rect(p.x,p.y,Mathf.Max(1,scale),Mathf.Max(1,scale)),new Color(0.55f,0.65f,0.71f)); }
            foreach(var item in level.SavedItems) { var p=Point(item.position); EditorGUI.DrawRect(new Rect(p.x-2,p.y-2,4,4),Color.yellow); }
            var spawn=Point(level.PlayerSpawnPosition); var exit=Point(level.GoalPosition);
            EditorGUI.DrawRect(new Rect(spawn.x-3,spawn.y-5,6,10),Color.green);
            EditorGUI.DrawRect(new Rect(exit.x-4,exit.y-4,8,8),Color.cyan);
            if(level.ConfineCamera)
            {
                var topLeft=Point(new Vector2(level.CameraBounds.xMin,level.CameraBounds.yMax));
                var bottomRight=Point(new Vector2(level.CameraBounds.xMax,level.CameraBounds.yMin));
                var outline=Rect.MinMaxRect(topLeft.x,topLeft.y,bottomRight.x,bottomRight.y); var color=new Color(1,0.75f,0.2f);
                EditorGUI.DrawRect(new Rect(outline.xMin,outline.yMin,outline.width,1),color);
                EditorGUI.DrawRect(new Rect(outline.xMin,outline.yMax-1,outline.width,1),color);
                EditorGUI.DrawRect(new Rect(outline.xMin,outline.yMin,1,outline.height),color);
                EditorGUI.DrawRect(new Rect(outline.xMax-1,outline.yMin,1,outline.height),color);
            }
            GUI.Label(new Rect(rect.x+6,rect.y+3,rect.width-12,18),"地图预览  ·  绿：出生点  青：出口  黄：物件",EditorStyles.whiteMiniLabel);
        }
        private void DrawCatalog(GameObject root)
        {
            EditorGUILayout.LabelField("物件库",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("拖入任意预制体加入物件库。点击物件可摆放到 Scene 中心，再用 Inspector 配置。圆球仍是独立的通关条件。",MessageType.Info);
            using(new EditorGUILayout.HorizontalScope())
            {
                catalog=(LevelObjectCatalog)EditorGUILayout.ObjectField(catalog,typeof(LevelObjectCatalog),false);
                if(GUILayout.Button("编辑分类 / 名称",GUILayout.Width(120)) && catalog!=null) Selection.activeObject=catalog;
            }
            var drop=GUILayoutUtility.GetRect(100,40,GUILayout.ExpandWidth(true)); GUI.Box(drop,"将 Project 中的预制体拖到这里（支持多个）");
            var evt=Event.current;
            if(drop.Contains(evt.mousePosition) && (evt.type==EventType.DragUpdated || evt.type==EventType.DragPerform))
            {
                DragAndDrop.visualMode=DragAndDropVisualMode.Copy;
                if(evt.type==EventType.DragPerform)
                { DragAndDrop.AcceptDrag(); foreach(var obj in DragAndDrop.objectReferences) if(obj is GameObject prefab) Import(prefab); }
                evt.Use();
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                importPrefab=(GameObject)EditorGUILayout.ObjectField(importPrefab,typeof(GameObject),false);
                using(new EditorGUI.DisabledScope(importPrefab==null)) if(GUILayout.Button("导入预制体",GUILayout.Width(100))) Import(importPrefab);
            }
            objectSearch=EditorGUILayout.TextField("搜索名称 / 分类",objectSearch);
            using(new EditorGUI.DisabledScope(root==null || currentLevelAsset==null))
            if(catalog!=null) foreach(var item in catalog.Entries)
            {
                if(item.prefab==null || !Matches(item.displayName+" "+item.category+" "+item.prefab.name,objectSearch)) continue;
                var label=new GUIContent("["+item.category+"] "+item.displayName,AssetPreview.GetMiniThumbnail(item.prefab));
                if(GUILayout.Button(label,GUILayout.Height(32))) PlacePrefab(item.prefab,root,PlacementPosition());
            }
        }
        private void Import(GameObject prefab)
        {
            bool added=LevelPrefabLibrary.RegisterPrefab(prefab,catalog:catalog);
            ShowNotification(new GUIContent(added?"已加入物件库":"请使用尚未导入的预制体资源根对象")); Repaint();
        }
        private static bool Matches(string text,string search) => string.IsNullOrWhiteSpace(search) || text.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0;
        private void DrawEnvironment(GameObject root)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("地图边界与背景",EditorStyles.boldLabel);
            var data=new SerializedObject(currentLevelAsset); data.Update();
            var confined=data.FindProperty("confineCamera");
            EditorGUILayout.PropertyField(confined,new GUIContent("启用地图边界（实体＋相机）"));
            if(confined.boolValue)
            {
                EditorGUILayout.PropertyField(data.FindProperty("cameraBounds"),new GUIContent("地图矩形（世界坐标）"));
                EditorGUILayout.HelpBox("矩形四周生成实体墙，阻止角色和物块越界；墙体厚度在地图外侧。整个摄像机画面也限制在此范围内，视野过大时自动缩小。",MessageType.None);
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("从瓦片计算边界") && TryTileBounds(root,out var bounds)) data.FindProperty("cameraBounds").rectValue=bounds;
                    editBounds=GUILayout.Toggle(editBounds,"在 Scene 中调整",EditorStyles.miniButton);
                }
                var rect=data.FindProperty("cameraBounds").rectValue;
                if(rect.width<=0 || rect.height<=0) EditorGUILayout.HelpBox("边界宽高需要大于 0；无效边界不会生成实体墙或限制摄像机。",MessageType.Warning);
            }
            EditorGUILayout.PropertyField(data.FindProperty("backgroundColor"),new GUIContent("背景底色"));
            EditorGUILayout.PropertyField(data.FindProperty("backgroundSprite"),new GUIContent("背景图片（Sprite）"));
            if(data.FindProperty("backgroundSprite").objectReferenceValue!=null)
            {
                EditorGUILayout.PropertyField(data.FindProperty("backgroundTint"),new GUIContent("图片色调"));
                if(!confined.boolValue) EditorGUILayout.PropertyField(data.FindProperty("backgroundRect"),new GUIContent("图片覆盖矩形"));
                EditorGUILayout.HelpBox(confined.boolValue?"图片铺满地图边界，可在 Scene 中预览。":"图片铺满指定矩形；空白区域使用背景底色。",MessageType.None);
            }
            if(data.ApplyModifiedProperties()) { Dirty(); SceneView.RepaintAll(); }
            if(root!=null) LevelBoundary2D.Ensure(root,currentLevelAsset);
        }
        private static bool TryTileBounds(GameObject root,out Rect bounds)
        {
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity); var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            if(root!=null) foreach(var map in root.GetComponentsInChildren<Tilemap>())
                foreach(var cell in map.cellBounds.allPositionsWithin) if(map.HasTile(cell))
                    for(int x=0;x<=1;x++) for(int y=0;y<=1;y++)
                    { var point=(Vector2)map.CellToWorld(cell+new Vector3Int(x,y)); min=Vector2.Min(min,point); max=Vector2.Max(max,point); }
            bounds=float.IsInfinity(min.x)?default:Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            return bounds.width>0 && bounds.height>0;
        }
        private void DrawSceneBounds(SceneView view)
        {
            if(EditorApplication.isPlaying || currentLevelAsset==null || !currentLevelAsset.ConfineCamera) return;
            var rect=currentLevelAsset.CameraBounds;
            var previous=Handles.color; Handles.color=new Color(1,0.75f,0.2f);
            Handles.DrawWireCube(rect.center,new Vector3(rect.width,rect.height,0));
            Handles.Label(new Vector3(rect.xMin,rect.yMax,0),"地图边界");
            if(editBounds)
            {
                EditorGUI.BeginChangeCheck();
                var min=Handles.PositionHandle(new Vector3(rect.xMin,rect.yMin,0),Quaternion.identity);
                var max=Handles.PositionHandle(new Vector3(rect.xMax,rect.yMax,0),Quaternion.identity);
                if(EditorGUI.EndChangeCheck())
                {
                    var data=new SerializedObject(currentLevelAsset);
                    data.FindProperty("cameraBounds").rectValue=Rect.MinMaxRect(Mathf.Min(min.x,max.x),Mathf.Min(min.y,max.y),
                        Mathf.Max(min.x,max.x)+Mathf.Max(0,0.1f-Mathf.Abs(max.x-min.x)),Mathf.Max(min.y,max.y)+Mathf.Max(0,0.1f-Mathf.Abs(max.y-min.y)));
                    data.ApplyModifiedProperties(); Dirty(); Repaint();
                }
            }
            Handles.color=previous;
        }
        private bool PrepareToLoad()
        {
            if(GameObject.Find("[LevelBuilder_Root]")==null || !UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) return true;
            if(currentLevelAsset==null) return EditorUtility.DisplayDialog("载入关卡","场景中已有地图。载入会替换地图对象，可使用 Undo 撤销。","载入","取消");
            int answer=EditorUtility.DisplayDialogComplex("切换关卡","是否先保存当前编辑的关卡？","保存并载入","取消","直接载入");
            return answer==2 || (answer==0 && Save());
        }
        private void CreateLevel()
        {
            if(!PrepareToLoad()) return;
            string path=EditorUtility.SaveFilePanelInProject("新建关卡","NewLevel","asset","选择保存位置","Assets/Resource/Levels");
            if(string.IsNullOrEmpty(path)) return;
            selectedLevel=CreateInstance<LevelAsset>(); AssetDatabase.CreateAsset(selectedLevel,path);
            var data=new SerializedObject(selectedLevel); data.FindProperty("levelId").stringValue=System.IO.Path.GetFileNameWithoutExtension(path); data.ApplyModifiedProperties();
            LoadSelected(true);
        }
        private void LoadSelected(bool prepared=false)
        {
            if(selectedLevel==null || (!prepared && !PrepareToLoad())) return;
            Undo.IncrementCurrentGroup(); int undo=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("载入关卡");
            foreach(var name in new[]{"[LevelBuilder_Root]","PlayerSpawn_Point","GoalExit_Point"})
            { var existing=GameObject.Find(name); if(existing!=null) Undo.DestroyObjectImmediate(existing); }
            currentLevelAsset=selectedLevel;
            var root=currentLevelAsset.BuildLevelContent(); root.name="[LevelBuilder_Root]";
            root.AddComponent<LevelAuthoringRoot>().SourceLevel=currentLevelAsset;
            Undo.RegisterCreatedObjectUndo(root,"载入关卡");
            if(root.GetComponentsInChildren<Tilemap>().Length==0) AddTilemap(root);
            Marker("PlayerSpawn_Point",currentLevelAsset.PlayerSpawnPosition,Color.green);
            Marker("GoalExit_Point",currentLevelAsset.GoalPosition,Color.cyan);
            Undo.CollapseUndoOperations(undo); Selection.activeGameObject=root; SceneView.lastActiveSceneView?.FrameSelected(); Dirty();
        }
        private static Vector3 PlacementPosition()
        {
            var p=SceneView.lastActiveSceneView!=null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            return new Vector3(Mathf.Round(p.x),Mathf.Round(p.y),0);
        }
        public static GameObject PlacePrefab(GameObject prefab,GameObject root,Vector3 position)
        {
            if(prefab==null || root==null || !PrefabUtility.IsPartOfPrefabAsset(prefab)) return null;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform); go.transform.position=position;
            if(go.TryGetComponent<PuzzleGimmickBase>(out var gimmick))
            { gimmick.GimmickId=Guid.NewGuid().ToString("N"); PrefabUtility.RecordPrefabInstancePropertyModifications(gimmick); }
            Undo.RegisterCreatedObjectUndo(go,"摆放 "+prefab.name); Selection.activeGameObject=go;
            EditorSceneManager.MarkSceneDirty(root.scene); return go;
        }
        private static void AddTilemap(GameObject root)
        {
            if(root==null) return;
            var grid=root.transform.Find("Grid");
            if(grid==null) { grid=new GameObject("Grid",typeof(Grid)).transform; grid.SetParent(root.transform); }
            var go=new GameObject("Ground",typeof(Tilemap),typeof(TilemapRenderer)); go.transform.SetParent(grid,false);
            go.layer=Core.Constants.GameLayers.ObstaclesLayer;
            go.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
            go.AddComponent<CompositeCollider2D>().geometryType=CompositeCollider2D.GeometryType.Polygons;
            go.AddComponent<TilemapCollider2D>().compositeOperation=Collider2D.CompositeOperation.Merge;
            Undo.RegisterCreatedObjectUndo(go,"添加瓦片图层"); Selection.activeGameObject=go; Dirty();
        }
        private static void Marker(string name,Vector2 position,Color color)
        {
            var go=new GameObject(name); go.transform.position=position; go.AddComponent<LevelPointMarker>().Color=color;
            Undo.RegisterCreatedObjectUndo(go,"关卡标记");
        }
        private static void SelectMarker(string name)
        { var go=GameObject.Find(name); if(go!=null) { Selection.activeGameObject=go; SceneView.lastActiveSceneView?.FrameSelected(); } }
        private bool Save()
        {
            var root=GameObject.Find("[LevelBuilder_Root]"); if(root==null || currentLevelAsset==null) return false;
            try
            {
                Undo.RecordObject(currentLevelAsset,"保存关卡"); currentLevelAsset.SaveFromScene(root.transform);
                var data=new SerializedObject(currentLevelAsset);
                var spawn=GameObject.Find("PlayerSpawn_Point"); var exit=GameObject.Find("GoalExit_Point");
                if(spawn!=null) data.FindProperty("playerSpawnPosition").vector2Value=spawn.transform.position;
                if(exit!=null) data.FindProperty("goalPosition").vector2Value=exit.transform.position;
                data.ApplyModifiedProperties(); EditorUtility.SetDirty(currentLevelAsset); AssetDatabase.SaveAssetIfDirty(currentLevelAsset);
                Dirty(); ShowNotification(new GUIContent("关卡已保存")); return true;
            }
            catch(Exception error) { EditorUtility.DisplayDialog("关卡未保存",error.Message,"确定"); return false; }
        }
        private static void Dirty() => EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
