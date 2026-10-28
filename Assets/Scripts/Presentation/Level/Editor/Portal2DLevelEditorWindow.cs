using System.Collections.Generic;
using System.IO;
using Portal2D.Core.Data;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Utils;
using Portal2D.Presentation.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Portal2D.Presentation.Level.Editor
{
    /// <summary>
    /// Portal2D 关卡与机关可视化编辑器 (Level & Gimmick Editor Window)
    /// 1. 载入并可视化解包编辑工程已有关卡资源 (Load & Unpack Level Assets)；
    /// 2. 在 Scene 视口中自由拖拽角色起点 (Spawn) 与过关终点 (Goal Exit)；
    /// 3. 基于预制体目录 (Assets/Prefab/Gimmicks) 的动态推块与交互机关调色板 (Pushable Blocks & Gimmick Palette)；
    /// 4. 一键“保存到资产”：纯数据驱动打包 ScriptableObject，并全自动清理场景临时编辑残留！
    /// 5. 纯中英文双语界面呈现 (Bilingual UI)。
    /// </summary>
    public class Portal2DLevelEditorWindow : EditorWindow
    {
        [MenuItem("Window/Portal2D/Level Editor (关卡编辑器)", false, 20)]
        public static void OpenWindow()
        {
            var window = GetWindow<Portal2DLevelEditorWindow>("关卡编辑器 (Level Editor)");
            window.minSize = new Vector2(520, 700);
            window.Show();
        }

        private int _selectedTab = 0;
        private readonly string[] _tabTitles = new string[]
        {
            "1. 规则与胜负 (Rules & Conditions)",
            "2. 地图与调色板 (Tilemap & Palette)",
            "3. 交互物块与机关 (Gimmicks & Blocks)",
            "4. 保存与说明 (Save & Manual)"
        };

        // 当前正在编辑的关卡资产
        [SerializeField] private LevelAsset currentLevelAsset;

        // 关卡配置属性
        private string _levelId = "Level_00_Tutorial";
        private string _levelDisplayName = "0. 教学关：基础移动";
        private Vector2 _playerSpawn = new Vector2(-1.13f, -2f);
        private Vector2 _goalPosition = new Vector2(7.5f, -2f);
        private Vector2 _goalTriggerSize = new Vector2(1.5f, 2.5f);
        private LevelWinCondition _winCondition = LevelWinCondition.ReachExitDirectly;
        private LevelLoseCondition _loseCondition = LevelLoseCondition.FallBelowDeathLine;
        private float _fallDeathY = -12.0f;
        private int _requiredSwitchCount = 0;
        private float _cameraSize = 6.0f;
        private LevelAsset _nextLevel;
        [SerializeField] private List<LevelConditionConfig> _winConditions = new List<LevelConditionConfig>();

        // 机关与推块预制体库目录
        private string _gimmicksFolderPath = "Assets/Prefab/Gimmicks";
        private List<GameObject> _scannedGimmickPrefabs = new List<GameObject>();

        // 关卡资产下拉列表缓存
        private List<LevelAsset> _availableLevelAssets = new List<LevelAsset>();
        private string[] _availableLevelNames = new string[0];
        private int _selectedLevelIndex = 0;

        private Vector2 _scrollPos;
        private Vector2 _gimmickScrollPos;

        private void OnEnable()
        {
            RefreshLevelAssetList();
            ScanGimmickPrefabs();

            if (currentLevelAsset == null && _availableLevelAssets.Count > 0)
            {
                currentLevelAsset = _availableLevelAssets[0];
            }
            LoadAssetPropertiesToMemory();
        }

        private void Update()
        {
            // 实时同步 Scene 视口中被拖拽的起点与终点标记坐标
            SyncSceneMarkersToMemory();
        }

        private void RefreshLevelAssetList()
        {
            _availableLevelAssets.Clear();
            string[] guids = AssetDatabase.FindAssets("t:LevelAsset", new[] { "Assets/Resource/Levels" });
            List<string> names = new List<string>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<LevelAsset>(path);
                if (asset != null)
                {
                    _availableLevelAssets.Add(asset);
                    names.Add($"{asset.LevelDisplayName} ({asset.LevelId})");
                }
            }

            _availableLevelNames = names.ToArray();

            _selectedLevelIndex = 0;
            if (currentLevelAsset != null)
            {
                for (int i = 0; i < _availableLevelAssets.Count; i++)
                {
                    if (_availableLevelAssets[i] == currentLevelAsset)
                    {
                        _selectedLevelIndex = i;
                        break;
                    }
                }
            }
        }

        private void LoadAssetPropertiesToMemory()
        {
            if (currentLevelAsset == null) return;
            _levelId = currentLevelAsset.LevelId;
            _levelDisplayName = currentLevelAsset.LevelDisplayName;
            _playerSpawn = currentLevelAsset.PlayerSpawnPosition;
            _goalPosition = currentLevelAsset.GoalPosition;
            _goalTriggerSize = currentLevelAsset.GoalTriggerSize;
            _winCondition = currentLevelAsset.WinCondition;
            _loseCondition = currentLevelAsset.LoseCondition;
            _fallDeathY = currentLevelAsset.FallDeathY;
            _requiredSwitchCount = currentLevelAsset.RequiredSwitchCount;
            _cameraSize = currentLevelAsset.InitialCameraSize;
            _nextLevel = currentLevelAsset.NextLevel;

            _winConditions.Clear();
            if (currentLevelAsset.WinConditions != null)
            {
                for (int i = 0; i < currentLevelAsset.WinConditions.Count; i++)
                {
                    if (currentLevelAsset.WinConditions[i] != null)
                    {
                        _winConditions.Add(currentLevelAsset.WinConditions[i].Clone());
                    }
                }
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();

            EditorGUILayout.Space(6);
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabTitles, GUILayout.Height(30));
            EditorGUILayout.Space(8);

            switch (_selectedTab)
            {
                case 0:
                    DrawRulesTab();
                    break;
                case 1:
                    DrawMapAndPaletteTab();
                    break;
                case 2:
                    DrawGimmicksTab();
                    break;
                case 3:
                    DrawSaveTab();
                    break;
            }

            EditorGUILayout.Space(14);
            DrawBottomBar();

            EditorGUILayout.EndScrollView();
        }

        #region 顶部关卡管理栏 (Header & Level Manager)

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Portal2D 关卡与机关可视化编辑器 (Portal2D Level & Gimmick Visual Editor)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("支持解包载入、视口拖拽起点终点、自定义机关与推块调色板、一键保存并自动清理层级\n(Supports unpacking, viewport gizmo dragging, custom gimmicks & pushable blocks palette, auto cleanup)", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 1. 关卡快速选择下拉菜单与资产槽
            EditorGUILayout.BeginHorizontal();
            if (_availableLevelNames.Length > 0)
            {
                int newIndex = EditorGUILayout.Popup("选择工程关卡 (Select Level)", _selectedLevelIndex, _availableLevelNames);
                if (newIndex != _selectedLevelIndex && newIndex >= 0 && newIndex < _availableLevelAssets.Count)
                {
                    _selectedLevelIndex = newIndex;
                    currentLevelAsset = _availableLevelAssets[_selectedLevelIndex];
                    LoadAssetPropertiesToMemory();
                }
            }

            EditorGUI.BeginChangeCheck();
            currentLevelAsset = (LevelAsset)EditorGUILayout.ObjectField(currentLevelAsset, typeof(LevelAsset), false, GUILayout.Width(130));
            if (EditorGUI.EndChangeCheck() && currentLevelAsset != null)
            {
                LoadAssetPropertiesToMemory();
            }
            EditorGUILayout.EndHorizontal();

            // 显示并配置“默认起始关卡”
            var levelMgr = FindAnyObjectByType<LevelManager>();
            EditorGUILayout.BeginHorizontal();
            string startLvlName = (levelMgr != null && levelMgr.DefaultStartLevel != null)
                ? $"{levelMgr.DefaultStartLevel.LevelDisplayName} ({levelMgr.DefaultStartLevel.LevelId})"
                : "未指定 (Unset)";
            EditorGUILayout.LabelField($"新游戏默认起始关卡 (Start Level): 【{startLvlName}】", EditorStyles.boldLabel);

            if (currentLevelAsset != null && GUILayout.Button("★ 设为起始关卡 (Set As Start)", GUILayout.Width(180)))
            {
                if (levelMgr != null)
                {
                    levelMgr.SetDefaultStartLevel(currentLevelAsset);
                    EditorUtility.DisplayDialog("设置成功 (Success)", $"新游戏默认起始关卡已设定为:\n[{currentLevelAsset.LevelDisplayName}] ({currentLevelAsset.LevelId})", "好的 (OK)");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 2. 核心操作按钮组
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.25f, 0.7f, 1f);
            if (GUILayout.Button("📂 载入此关卡到场景编辑 (Load to Scene)", GUILayout.Height(32)))
            {
                LoadLevelToScene(currentLevelAsset);
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("➕ 新建关卡模板 (New Level)", GUILayout.Width(150), GUILayout.Height(32)))
            {
                CreateNewLevelTemplate();
            }

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("🧹 清理场景残留 (Clean Scene)", GUILayout.Width(140), GUILayout.Height(32)))
            {
                CleanSceneResidues(true);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void LoadLevelToScene(LevelAsset asset)
        {
            if (asset == null)
            {
                EditorUtility.DisplayDialog("提示 (Prompt)", "请先在上方选择或拖入一个关卡资产！\n(Please select a LevelAsset first!)", "好的 (OK)");
                return;
            }

            LoadAssetPropertiesToMemory();

            // 1. 清理场景中现存的旧编辑物件
            CleanSceneResidues(false);

            // 2. 纯数据动态构建关卡内容到场景
            var rootInstance = asset.BuildLevelContent();
            rootInstance.name = "[LevelBuilder_Root]";

            // 3. 生成并摆放可拖拽的 起点与终点 Marker
            EnsureSpawnAndGoalMarkers(rootInstance.transform, _playerSpawn, _goalPosition);

            // 4. 定位 Scene 视口至关卡中心
            Selection.activeGameObject = rootInstance;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"<color=cyan>[LevelEditor] 关卡 [{asset.LevelDisplayName}] 已载入场景！可在 Scene 视口自由编辑瓦片、摆放交互预制体与拖拽起点终点。</color>");
        }

        private void CreateNewLevelTemplate()
        {
            CleanSceneResidues(false);

            currentLevelAsset = null;
            _levelId = "Level_" + System.DateTime.Now.ToString("MMdd_HHmm");
            _levelDisplayName = "新关卡：机关试炼 (New Puzzle Level)";
            _playerSpawn = new Vector2(-4f, -2f);
            _goalPosition = new Vector2(6f, -2f);
            _winCondition = LevelWinCondition.ActivateAllSwitches;
            _loseCondition = LevelLoseCondition.FallBelowDeathLine;
            _fallDeathY = -12.0f;
            _requiredSwitchCount = 0;
            _cameraSize = 6.0f;
            _nextLevel = null;
            _winConditions.Clear();

            var root = SetupStandardGridHierarchy();
            EnsureSpawnAndGoalMarkers(root.transform, _playerSpawn, _goalPosition);

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"[LevelEditor] 已初始化新关卡模板: {_levelId}");
        }

        #endregion

        #region Tab 1: 规则与胜负 (Rules & Conditions)

        private void DrawRulesTab()
        {
            EditorGUILayout.LabelField("一、 基础信息与相机 (Basic Info & Camera)", EditorStyles.boldLabel);
            _levelId = EditorGUILayout.TextField("关卡 ID (唯一标识 / Level ID)", _levelId);
            _levelDisplayName = EditorGUILayout.TextField("关卡显示名称 (Display Name)", _levelDisplayName);
            _cameraSize = EditorGUILayout.Slider("初始摄像机正交尺寸 (Camera Size)", _cameraSize, 4f, 12f);
            _nextLevel = (LevelAsset)EditorGUILayout.ObjectField("通关进入的下一关 (Next Level)", _nextLevel, typeof(LevelAsset), false);

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("二、 关卡通关胜利规则 (Victory Conditions)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• 直接到达终点 (ReachExitDirectly)：终点自始至终处于开启状态，玩家触碰即可通关。\n" +
                "• 解开全部机关 (ActivateAllSwitches)：踩下所有压力板或达成下方条件组后，终点发光解锁。",
                MessageType.Info
            );
            _winCondition = (LevelWinCondition)EditorGUILayout.EnumPopup("通关胜利条件 (Win Condition)", _winCondition);

            if (_winCondition == LevelWinCondition.ActivateAllSwitches)
            {
                EditorGUI.indentLevel++;
                _requiredSwitchCount = EditorGUILayout.IntField("所需激活机关数 (兼容旧版/0为全部)", _requiredSwitchCount);
                if (_requiredSwitchCount <= 0 && _winConditions.Count == 0)
                {
                    EditorGUILayout.LabelField("提示: 若未配置下方条件组，0 代表自动统计场景内所有开关", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(8);
                DrawWinConditionsDesigner();
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("三、 关卡失败规则 (Failure Condition)", EditorStyles.boldLabel);
            _loseCondition = (LevelLoseCondition)EditorGUILayout.EnumPopup("关卡失败条件 (Lose Condition)", _loseCondition);

            if (_loseCondition == LevelLoseCondition.FallBelowDeathLine)
            {
                EditorGUI.indentLevel++;
                _fallDeathY = EditorGUILayout.FloatField("深渊坠落死亡线 Y 坐标 (Death Line Y)", _fallDeathY);
                EditorGUILayout.LabelField($"提示: 当角色 Y 坐标低于 {_fallDeathY} 时自动判定死亡重置回起点", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawWinConditionsDesigner()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("▼ 通关条件组设计器 (Victory Conditions Designer)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "可自由配置关卡的通关条件组！(Configure custom victory condition groups)\n" +
                "• 例如：条件1设定为“同时压下两块压力板”；条件2设定为“按下主控电闸”；\n" +
                "• 只有当下方的所有条件组均被达成时，终点才会解锁高亮发光并允许通关！",
                MessageType.None
            );

            // 操作按钮栏
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("➕ 添加新通关条件 (Add Condition)", GUILayout.Height(26)))
            {
                _winConditions.Add(new LevelConditionConfig(
                    $"条件 #{_winConditions.Count + 1}",
                    ConditionEvaluateMode.AllSimultaneouslyActive,
                    stay: true
                ));
            }
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("⚡ 智能识别场景机关 (Auto-Detect Gimmicks)", GUILayout.Height(26)))
            {
                AutoGenerateConditionsFromGimmicks();
            }
            GUI.backgroundColor = Color.white;

            if (_winConditions.Count > 0 && GUILayout.Button("🗑 清空 (Clear)", GUILayout.Width(75), GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("确认 (Confirm)", "确定清空当前配置的所有通关条件吗？\n(Clear all victory conditions?)", "确定 (Yes)", "取消 (Cancel)"))
                {
                    _winConditions.Clear();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            if (_winConditions.Count == 0)
            {
                EditorGUILayout.HelpBox("当前尚未配置任何独立条件组。运行时将自动回退为【踩下/激活场景内所有机关】。(Fallback to activating all gimmicks).", MessageType.Warning);
            }
            else
            {
                var availableGimmicks = GetAvailableGimmickIds();

                for (int i = 0; i < _winConditions.Count; i++)
                {
                    var cond = _winConditions[i];
                    if (cond == null) continue;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // 1. 标题行
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"条件 {i + 1}:", GUILayout.Width(50));
                    cond.conditionName = EditorGUILayout.TextField(cond.conditionName);
                    GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                    if (GUILayout.Button("✕ 删除 (Del)", GUILayout.Width(75)))
                    {
                        _winConditions.RemoveAt(i);
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        break;
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();

                    // 2. 模式与持久性
                    cond.evaluateMode = (ConditionEvaluateMode)EditorGUILayout.EnumPopup("判定模式 (Evaluate Mode)", cond.evaluateMode);
                    cond.stayCompletedOnceMet = EditorGUILayout.ToggleLeft("达成后永久保持满足 (Stay completed once met)", cond.stayCompletedOnceMet);

                    // 3. 关联机关勾选列表
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("选择包含在本条件中的机关 (Associated Gimmicks):", EditorStyles.miniBoldLabel);

                    if (availableGimmicks.Count == 0)
                    {
                        EditorGUILayout.HelpBox("当前场景/资产中未检测到机关物体。请在【3. 交互物块与机关】中先摆放机关。", MessageType.Info);
                    }
                    else
                    {
                        EditorGUI.indentLevel++;
                        for (int g = 0; g < availableGimmicks.Count; g++)
                        {
                            string gid = availableGimmicks[g];
                            bool isChecked = cond.requiredGimmickIds.Contains(gid);

                            EditorGUILayout.BeginHorizontal();
                            bool newChecked = EditorGUILayout.ToggleLeft($"机关: {gid}", isChecked);
                            if (newChecked != isChecked)
                            {
                                if (newChecked) cond.requiredGimmickIds.Add(gid);
                                else cond.requiredGimmickIds.Remove(gid);
                            }

                            if (GUILayout.Button("🎯 定位 (Focus)", GUILayout.Width(75)))
                            {
                                FocusGimmickInScene(gid);
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUI.indentLevel--;
                    }

                    string summary = cond.requiredGimmickIds.Count > 0
                        ? $"已关联 {cond.requiredGimmickIds.Count} 项: [{string.Join(", ", cond.requiredGimmickIds)}]"
                        : "<color=red>警告: 尚未勾选任何机关！(No gimmick selected)</color>";
                    EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(4);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private List<string> GetAvailableGimmickIds()
        {
            var list = new List<string>();

            // 1. 从场景 [LevelBuilder_Root] 中获取
            var root = GameObject.Find("[LevelBuilder_Root]");
            if (root != null)
            {
                var gimmicks = root.GetComponentsInChildren<PuzzleGimmickBase>(true);
                for (int i = 0; i < gimmicks.Length; i++)
                {
                    var g = gimmicks[i];
                    if (g != null && !string.IsNullOrEmpty(g.GimmickId) && !list.Contains(g.GimmickId))
                    {
                        list.Add(g.GimmickId);
                    }
                }
                var interactables = root.GetComponentsInChildren<InteractableObject>(true);
                for (int i = 0; i < interactables.Length; i++)
                {
                    var io = interactables[i];
                    string gid = !string.IsNullOrEmpty(io.GimmickId) ? io.GimmickId : io.gameObject.name;
                    if (!list.Contains(gid))
                    {
                        list.Add(gid);
                    }
                }
            }

            // 2. 从资产保存的 SavedItems 中补充
            if (currentLevelAsset != null && currentLevelAsset.SavedItems != null)
            {
                for (int i = 0; i < currentLevelAsset.SavedItems.Count; i++)
                {
                    string gid = currentLevelAsset.SavedItems[i].gimmickId;
                    if (!string.IsNullOrEmpty(gid) && !list.Contains(gid))
                    {
                        list.Add(gid);
                    }
                }
            }

            return list;
        }

        private void FocusGimmickInScene(string gimmickId)
        {
            var root = GameObject.Find("[LevelBuilder_Root]");
            Transform target = null;
            if (root != null)
            {
                target = root.transform.Find(gimmickId);
                if (target == null)
                {
                    var all = root.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i].name == gimmickId)
                        {
                            target = all[i];
                            break;
                        }
                    }
                }
            }
            if (target == null)
            {
                var go = GameObject.Find(gimmickId);
                target = go?.transform;
            }

            if (target != null)
            {
                Selection.activeGameObject = target.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
                EditorGUIUtility.PingObject(target.gameObject);
            }
            else
            {
                Debug.LogWarning($"[LevelEditor] 场景中未找到名为 [{gimmickId}] 的物体，请先载入关卡到场景。");
            }
        }

        private void AutoGenerateConditionsFromGimmicks()
        {
            var gimmicks = GetAvailableGimmickIds();
            if (gimmicks.Count == 0)
            {
                EditorUtility.DisplayDialog("提示 (Prompt)", "当前未找到任何机关，无法自动生成条件！\n(No gimmicks found in scene!)", "好的 (OK)");
                return;
            }

            _winConditions.Clear();
            for (int i = 0; i < gimmicks.Count; i++)
            {
                string gid = gimmicks[i];
                var cond = new LevelConditionConfig(
                    $"条件: 触发 {gid}",
                    gid.Contains("FloorButton") ? ConditionEvaluateMode.AllTriggeredOnce : ConditionEvaluateMode.AllSimultaneouslyActive,
                    stay: true
                );
                cond.requiredGimmickIds.Add(gid);
                _winConditions.Add(cond);
            }

            EditorUtility.DisplayDialog("完成 (Done)", $"已根据关卡中的 {gimmicks.Count} 个机关自动生成了对应的通关条件！\n(Generated conditions for {gimmicks.Count} gimmicks!)", "好的 (OK)");
        }

        #endregion

        #region Tab 2: 地图与调色板 (Tilemap & Palette)

        private void DrawMapAndPaletteTab()
        {
            EditorGUILayout.LabelField("一、 Unity 2D 瓦片调色板快速联动 (Tile Palette Integration)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("点击下方按钮直接呼出 Unity 原生 2D Tile Palette 调色板窗口，自由刷砖画地图。\n(Click below to open native Tile Palette to paint tiles.)", MessageType.Info);

            if (GUILayout.Button("★ 呼出 Unity 2D 瓦片调色板 (Open Tile Palette)", GUILayout.Height(36)))
            {
                EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("二、 工程已有瓦片库快捷通道 (Project Tilemap Asset Folders)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("选中 草地瓦片库 (Grass Tiles)"))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>("Assets/Resource/TilePattles/Grass");
                if (folder != null) EditorGUIUtility.PingObject(folder);
            }
            if (GUILayout.Button("选中 石道瓦片库 (Stone Tiles)"))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>("Assets/Resource/TilePattles/Stone");
                if (folder != null) EditorGUIUtility.PingObject(folder);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("三、 关卡瓦片图层规范与转换 (Tilemap Layers & Conversion)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• 【Ground 地面层】(层级 Obstacles)：实心障碍阻挡，角色无法穿透。\n" +
                "• 【Platforms 单向平台层】(层级 OneWayPlatform)：支持单向穿透，可从下方跳上，按 S 键可向下穿透。\n" +
                "提示：在 Tile Palette 刷地图时，请注意检查 Active Tilemap 是否选在期望的图层上！",
                MessageType.Info);

            if (GUILayout.Button("一键在场景生成/确保标准地图层结构 (Setup Grid & Tilemaps)", GUILayout.Height(30)))
            {
                SetupStandardGridHierarchy();
            }

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.95f, 0.75f, 0.25f);
            if (GUILayout.Button("🔄 若误画在 Platforms：一键转移瓦片至 Ground 实心地面 (Transfer Platforms to Ground)", GUILayout.Height(30)))
            {
                TransferPlatformsTilesToGround();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("四、 快速摆放可推动地块 (Quick Place Pushable Stone Block)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("一键在当前视口中央放置一个可推动、可放置、可获取的石块实体！(Place a pushable stone block in scene)", MessageType.None);

            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.55f);
            if (GUILayout.Button("📦 放置可推动石块到场景 (Place Pushable Stone Block)", GUILayout.Height(32)))
            {
                PlacePushableStoneBlock();
            }
            GUI.backgroundColor = Color.white;
        }

        private void TransferPlatformsTilesToGround()
        {
            var root = GameObject.Find("[LevelBuilder_Root]");
            if (root == null)
            {
                EditorUtility.DisplayDialog("提示 (Prompt)", "场景中未找到 [LevelBuilder_Root]，请先点击上方【📂 载入关卡到场景编辑】！", "好的 (OK)");
                return;
            }

            var groundTm = root.transform.Find("Grid/Ground")?.GetComponent<Tilemap>();
            var platTm = root.transform.Find("Grid/Platforms")?.GetComponent<Tilemap>();

            if (groundTm == null || platTm == null)
            {
                EditorUtility.DisplayDialog("提示 (Prompt)", "未找到完整的 Ground 或 Platforms 瓦片图层！", "好的 (OK)");
                return;
            }

            int count = 0;
            foreach (var pos in platTm.cellBounds.allPositionsWithin)
            {
                if (platTm.HasTile(pos))
                {
                    groundTm.SetTile(pos, platTm.GetTile(pos));
                    count++;
                }
            }
            platTm.ClearAllTiles();

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("转移成功 (Success)", $"已成功将 {count} 块瓦片从 Platforms 转移到 Ground 实心地面层！", "太好了 (Great)");
        }

        private GameObject SetupStandardGridHierarchy()
        {
            var root = GameObject.Find("[LevelBuilder_Root]");
            if (root == null)
            {
                root = new GameObject("[LevelBuilder_Root]");
                Undo.RegisterCreatedObjectUndo(root, "Create LevelBuilder Root");
            }

            var gridObj = root.transform.Find("Grid");
            if (gridObj == null)
            {
                var g = new GameObject("Grid");
                g.transform.SetParent(root.transform, false);
                g.AddComponent<Grid>();
                gridObj = g.transform;
            }

            // 1. 地面层 (Ground)
            int solidLayer = Portal2D.Core.Constants.GameLayers.ObstaclesLayer;

            var ground = gridObj.Find("Ground");
            if (ground == null)
            {
                var gObj = new GameObject("Ground");
                gObj.transform.SetParent(gridObj, false);
                gObj.layer = solidLayer;
                gObj.AddComponent<Tilemap>();
                var tr = gObj.AddComponent<TilemapRenderer>();
                tr.sortingOrder = 0;
                var tc = gObj.AddComponent<TilemapCollider2D>();
                tc.compositeOperation = Collider2D.CompositeOperation.Merge;
                var comp = gObj.AddComponent<CompositeCollider2D>();
                comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
                var rb = gObj.GetComponent<Rigidbody2D>();
                if (rb != null) rb.bodyType = RigidbodyType2D.Static;
            }

            // 2. 单向平台层 (Platforms)
            int oneWayLayer = Portal2D.Core.Constants.GameLayers.OneWayPlatformLayer;
            if (oneWayLayer >= 0)
            {
                var plat = gridObj.Find("Platforms");
                if (plat == null)
                {
                    var pObj = new GameObject("Platforms");
                    pObj.transform.SetParent(gridObj, false);
                    pObj.layer = oneWayLayer;
                    pObj.AddComponent<Tilemap>();
                    var tr = pObj.AddComponent<TilemapRenderer>();
                    tr.sortingOrder = 1;
                    var tc = pObj.AddComponent<TilemapCollider2D>();
                    tc.compositeOperation = Collider2D.CompositeOperation.Merge;
                    var comp = pObj.AddComponent<CompositeCollider2D>();
                    comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
                    var rb = pObj.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.bodyType = RigidbodyType2D.Static;
                }
            }

            return root;
        }

        #endregion

        #region Tab 3: 交互物块与机关 (Gimmicks & Blocks)

        private void DrawGimmicksTab()
        {
            // 1. 起点与终点设置
            EditorGUILayout.LabelField("一、 玩家出生点与过关终点 (Player Spawn & Goal Exit Markers)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("在 Scene 视口中点击【🚩 起点】或【🚪 终点】标记，使用 Unity 移动工具 (W 键) 即可自由拖拽摆放！\n(Click markers in Scene view and press W to move freely)", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _playerSpawn = EditorGUILayout.Vector2Field("起点 (Player Spawn)", _playerSpawn);
            if (EditorGUI.EndChangeCheck())
            {
                SetMarkerPosition("PlayerSpawn_Point", _playerSpawn);
            }
            if (GUILayout.Button("🎯 选中并聚焦起点 (Focus Spawn)", GUILayout.Width(170)))
            {
                FocusMarker("PlayerSpawn_Point", _playerSpawn);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _goalPosition = EditorGUILayout.Vector2Field("终点 (Goal Exit)", _goalPosition);
            if (EditorGUI.EndChangeCheck())
            {
                SetMarkerPosition("GoalExit_Point", _goalPosition);
            }
            if (GUILayout.Button("🎯 选中并聚焦终点 (Focus Goal)", GUILayout.Width(170)))
            {
                FocusMarker("GoalExit_Point", _goalPosition);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("🔄 在场景中重新生成/对齐 起点与终点标记 (Re-align Markers)", GUILayout.Height(26)))
            {
                var root = GameObject.Find("[LevelBuilder_Root]")?.transform;
                EnsureSpawnAndGoalMarkers(root, _playerSpawn, _goalPosition);
            }

            EditorGUILayout.Space(14);

            // 2. 预制体驱动的交互机关与推块调色板
            EditorGUILayout.LabelField("二、 可推动物块与交互机关调色板 (Pushable Blocks & Gimmicks Palette)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "关卡编辑器不再写死机关！任何存入指定目录的 Prefab (如石头推块、压力板、控制台) 都会以调色板呈现，一键放置入场景。\n" +
                "(Any prefab in the folder appears here for one-click placement.)",
                MessageType.None);

            // 快捷创建与放置操作栏
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.55f);
            if (GUILayout.Button("📦 放置可推动石块 (Place Pushable Stone Block)", GUILayout.Height(30)))
            {
                PlacePushableStoneBlock();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("➕ 生成基础按压开关模板 (Create Button Template)", GUILayout.Height(30)))
            {
                CreateInteractiveItemTemplate();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            _gimmicksFolderPath = EditorGUILayout.TextField("预制体存放目录 (Gimmicks Folder)", _gimmicksFolderPath);
            if (GUILayout.Button("📁 打开 (Open)", GUILayout.Width(75)))
            {
                EnsureDirectory(_gimmicksFolderPath);
                var folderObj = AssetDatabase.LoadAssetAtPath<Object>(_gimmicksFolderPath);
                if (folderObj != null) EditorGUIUtility.PingObject(folderObj);
            }
            if (GUILayout.Button("🔄 刷新 (Refresh)", GUILayout.Width(80)))
            {
                ScanGimmickPrefabs();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // 绘制扫描到的预制体调色板
            DrawGimmickPrefabPalette();
        }

        private void ScanGimmickPrefabs()
        {
            _scannedGimmickPrefabs.Clear();
            EnsureDirectory(_gimmicksFolderPath);

            if (AssetDatabase.IsValidFolder(_gimmicksFolderPath))
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { _gimmicksFolderPath });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        _scannedGimmickPrefabs.Add(prefab);
                    }
                }
            }
        }

        private void DrawGimmickPrefabPalette()
        {
            if (_scannedGimmickPrefabs.Count == 0)
            {
                EditorGUILayout.HelpBox($"当前目录 [{_gimmicksFolderPath}] 下暂无预制体。\n点击上方按钮生成模板，或在场景做好物品后拖入该目录成为 Prefab 即可！", MessageType.Warning);
                return;
            }

            _gimmickScrollPos = EditorGUILayout.BeginScrollView(_gimmickScrollPos, GUILayout.Height(180));
            int columns = Mathf.Max(1, (int)(position.width / 155));
            int count = _scannedGimmickPrefabs.Count;

            for (int i = 0; i < count; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < columns && (i + j) < count; j++)
                {
                    var prefab = _scannedGimmickPrefabs[i + j];
                    DrawPrefabCard(prefab);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabCard(GameObject prefab)
        {
            if (prefab == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(145), GUILayout.Height(72));
            Texture2D preview = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);

            EditorGUILayout.BeginHorizontal();
            if (preview != null)
            {
                GUILayout.Label(preview, GUILayout.Width(36), GUILayout.Height(36));
            }
            EditorGUILayout.LabelField(prefab.name, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("+ 放置到场景 (Place)", GUILayout.Height(24)))
            {
                PlacePrefabInScene(prefab);
            }
            EditorGUILayout.EndVertical();
        }

        private void PlacePushableStoneBlock()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Gimmicks/Pushable_StoneBlock.prefab");
            if (prefab != null)
            {
                PlacePrefabInScene(prefab);
            }
            else
            {
                var root = GameObject.Find("[LevelBuilder_Root]")?.transform;
                Vector3 spawnPos = GetSceneViewCenter();
                var pushable = PushableBlockEntity.Spawn(spawnPos, root);
                pushable.name = $"Pushable_StoneBlock_{Random.Range(10, 99)}";
                Selection.activeGameObject = pushable.gameObject;
                Undo.RegisterCreatedObjectUndo(pushable.gameObject, "Place Pushable Stone Block");
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log($"[LevelEditor] 已在场景中放置可推动石块: {pushable.name}");
            }
        }

        private void PlacePrefabInScene(GameObject prefab)
        {
            var root = GameObject.Find("[LevelBuilder_Root]")?.transform;
            Vector3 spawnPos = GetSceneViewCenter();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = $"{prefab.name}_{Random.Range(10, 99)}";
            instance.transform.position = spawnPos;

            if (root != null)
            {
                instance.transform.SetParent(root, true);
            }

            Selection.activeGameObject = instance;
            Undo.RegisterCreatedObjectUndo(instance, $"Place {prefab.name}");
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[LevelEditor] 已在场景中放置预制体: {instance.name}");
        }

        private void CreateInteractiveItemTemplate()
        {
            var root = GameObject.Find("[LevelBuilder_Root]")?.transform;
            Vector3 pos = GetSceneViewCenter();

            var go = new GameObject($"Interactive_Button_{Random.Range(10, 99)}");
            go.transform.position = pos;
            if (root != null) go.transform.SetParent(root, true);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.CreateBorderedBoxSprite(24, new Color(1f, 0.75f, 0.1f), Color.white, 2);
            sr.sortingOrder = 10;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.4f);
            col.isTrigger = true;

            var interactable = go.AddComponent<InteractableObject>();
            interactable.GimmickId = go.name;

            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Interactive Item Template");
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[LevelEditor] 已生成交互物品模板 [{go.name}]！可在 Inspector 调整其属性，然后拖入 {_gimmicksFolderPath} 成为 Prefab！");
        }

        #endregion

        #region Tab 4: 保存与说明 (Save & Manual)

        private void DrawSaveTab()
        {
            EditorGUILayout.LabelField("纯数据驱动关卡资产化说明 (Data-Driven Architecture)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "本系统采用【纯数据驱动 (Data-Driven)】架构：\n" +
                "1. 关卡地图瓦片、交互机关、可推动物块与通关规则全部直接保存在 .asset 资源文件内；\n" +
                "2. ★ 无需生成任何外部 Prefab 预制体文件，零冗余残留！\n" +
                "3. 游戏运行或载入编辑时，直接从 .asset 数据纯动态构建；\n" +
                "4. 点击下方【★ 保存到资产】后，场景临时层级会自动被全部清理干净！\n\n" +
                "(All tiles, pushable blocks, gimmicks, and rules are serialized into .asset files directly without extra prefabs)",
                MessageType.Info
            );
        }

        private void DrawBottomBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f);
            if (GUILayout.Button("★ 保存到资产 (Save to Asset)", GUILayout.Height(46)))
            {
                SaveLevelToAsset();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 核心保存与清理管线 (Core Save & Clean Pipeline)

        private void SaveLevelToAsset()
        {
            if (string.IsNullOrEmpty(_levelId))
            {
                EditorUtility.DisplayDialog("错误 (Error)", "关卡 ID 不能为空！\n(Level ID cannot be empty!)", "确定 (OK)");
                return;
            }

            // 1. 同步 Spawn / Goal 标记位置
            SyncSceneMarkersToMemory();

            // 2. 确保目标目录存在
            string assetDir = "Assets/Resource/Levels";
            EnsureDirectory(assetDir);

            var root = GameObject.Find("[LevelBuilder_Root]");
            if (root == null)
            {
                EditorUtility.DisplayDialog("错误 (Error)", "场景中未找到 [LevelBuilder_Root] 编辑层级！请先点击【载入关卡】或【新建关卡】。\n(No [LevelBuilder_Root] in scene!)", "确定 (OK)");
                return;
            }

            // 3. 创建或更新 LevelAsset (ScriptableObject)
            string assetPath = $"{assetDir}/{_levelId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<LevelAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<LevelAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            // ★ 纯数据驱动：将场景中的所有 Tilemap 瓦片与摆放的交互机关/推块全部提取保存进 LevelAsset 资产
            asset.SaveFromScene(root.transform);

            asset.SetRuntimeData(
                _levelId,
                _levelDisplayName,
                _playerSpawn,
                _goalPosition,
                _nextLevel,
                _cameraSize,
                _winCondition,
                _fallDeathY,
                _loseCondition,
                _requiredSwitchCount,
                _winConditions
            );

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            currentLevelAsset = asset;
            RefreshLevelAssetList();

            // 4. 自动注册到 LevelManager 的关卡库中
            var levelMgr = FindAnyObjectByType<LevelManager>();
            if (levelMgr != null && !levelMgr.AllLevels.Contains(asset))
            {
                levelMgr.AllLevels.Add(asset);
                EditorUtility.SetDirty(levelMgr);
                EditorSceneManager.MarkSceneDirty(levelMgr.gameObject.scene);
            }

            // 5. 彻底清理场景临时层级与残留
            CleanSceneResidues(false);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            EditorUtility.DisplayDialog("保存成功 (Save Successful)",
                $"关卡 [{_levelDisplayName}] (ID: {_levelId}) 已成功保存为独立资源文件！\n\n" +
                $"• 资产路径: {assetPath}\n" +
                $"• 纯数据格式：地图、机关与可推动物块均已打包在 .asset 内！\n" +
                $"• 场景临时层级已自动全部清理干净！",
                "太棒了 (Awesome)");
        }

        private void CleanSceneResidues(bool showDialog)
        {
            int cleanedCount = 0;

            var root = GameObject.Find("[LevelBuilder_Root]");
            if (root != null)
            {
                DestroyImmediate(root);
                cleanedCount++;
            }

            var spawnPoint = GameObject.Find("PlayerSpawn_Point") ?? GameObject.Find("PlayerSpawn_Marker");
            if (spawnPoint != null)
            {
                DestroyImmediate(spawnPoint);
                cleanedCount++;
            }

            var goalPoint = GameObject.Find("GoalExit_Point") ?? GameObject.Find("GoalExit_Marker");
            if (goalPoint != null)
            {
                DestroyImmediate(goalPoint);
                cleanedCount++;
            }

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            if (showDialog)
            {
                EditorUtility.DisplayDialog("清理完成 (Cleaned)", $"已清理场景中的临时关卡编辑对象 ({cleanedCount} 项)，层级恢复清爽！\n(Scene residues cleaned)", "好的 (OK)");
            }
        }

        #endregion

        #region Marker 标记管理与视口同步 (Markers & Viewport Sync)

        private void EnsureSpawnAndGoalMarkers(Transform root, Vector2 spawnPos, Vector2 goalPos)
        {
            // 1. 起点 Marker
            var spawnObj = GameObject.Find("PlayerSpawn_Point");
            if (spawnObj == null)
            {
                spawnObj = new GameObject("PlayerSpawn_Point");
                var sr = spawnObj.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.CreateBorderedBoxSprite(24, new Color(0.18f, 0.65f, 0.95f), Color.white, 2);
                sr.sortingOrder = 25;
                spawnObj.AddComponent<PlayerSpawnMarker>();
            }
            spawnObj.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);

            // 2. 终点 Marker
            var goalObj = GameObject.Find("GoalExit_Point");
            if (goalObj == null)
            {
                goalObj = new GameObject("GoalExit_Point");
                var sr = goalObj.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.CreateBorderedBoxSprite(24, new Color(0.2f, 1f, 0.5f), Color.white, 2);
                sr.sortingOrder = 25;
                var marker = goalObj.AddComponent<LevelGoalMarker>();
                marker.TriggerSize = _goalTriggerSize;
            }
            goalObj.transform.position = new Vector3(goalPos.x, goalPos.y, 0f);
        }

        private void SyncSceneMarkersToMemory()
        {
            var spawnObj = GameObject.Find("PlayerSpawn_Point");
            if (spawnObj != null)
            {
                _playerSpawn = new Vector2(spawnObj.transform.position.x, spawnObj.transform.position.y);
            }

            var goalObj = GameObject.Find("GoalExit_Point");
            if (goalObj != null)
            {
                _goalPosition = new Vector2(goalObj.transform.position.x, goalObj.transform.position.y);
            }
        }

        private void SetMarkerPosition(string markerName, Vector2 pos)
        {
            var marker = GameObject.Find(markerName);
            if (marker != null)
            {
                marker.transform.position = new Vector3(pos.x, pos.y, 0f);
            }
        }

        private void FocusMarker(string markerName, Vector2 defaultPos)
        {
            var marker = GameObject.Find(markerName);
            if (marker == null)
            {
                var root = GameObject.Find("[LevelBuilder_Root]")?.transform;
                EnsureSpawnAndGoalMarkers(root, _playerSpawn, _goalPosition);
                marker = GameObject.Find(markerName);
            }

            if (marker != null)
            {
                Selection.activeGameObject = marker;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        private Vector3 GetSceneViewCenter()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                Vector3 p = sv.pivot;
                p.z = 0;
                return p;
            }
            return Vector3.zero;
        }

        private void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        #endregion
    }
}
