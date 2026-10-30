# 传送门与关卡重构说明

## 使用

- A / D 或方向键移动，W / 空格 / 上方向键跳跃。
- 选中背包第一格的传送门枪：左键放置蓝门 A，右键放置橙门 B。红色预览表示该位置不能放置。角色手上不显示枪模型。
- 靠近圆球按 E 激活；仅激活距离内最近的未激活圆球。所有圆球激活后出口开启，进入出口过关。
- 选中物块后按 F 或点击空位放置；鼠标指向物块按 R 收回。R 收回与右键放门不再冲突。
- 关卡编辑器入口：`Window > Portal2D > Level Editor (关卡编辑器)`。载入地图、编辑 Tilemap、摆放圆球/物块、移动出生点/出口、保存即可，无需配置条件组。

## 文件与组件

- `Assets/Plugins/Portals2D`：独立传送门程序集、A/B 预制体、切片材质/着色器和物理测试。完整复用说明见该目录的 README。
- `Assets/Resources/Gameplay/Player.prefab`：绿色 0.65 × 1.25 矩形，标准 Dynamic Rigidbody2D 和 BoxCollider2D，角色运动与自动立正分属两个组件。
- `Assets/Resources/Gameplay/Pushable_StoneBlock.prefab`：动态物理物块，推动由碰撞求解器处理，无脚本空中阻尼。原 `Assets/Prefab/Gimmicks/Pushable_StoneBlock.prefab` 同步更新。
- `Assets/Resources/Gameplay/ActivationOrb.prefab`：圆球和柔光，接收目标明确的交互事件。关卡规则统计当前关卡下的圆球，不统计其他机关。
- `LevelAsset` 仅保存数据；`LevelContentBuilder` 构建和序列化关卡。圆球保存时生成稳定且唯一的 ID，复制出的重复 ID 在保存时修正。
- `PortalManager` 是本游戏的放置/配对适配层；关卡卸载、读档放门、保存门状态走事件管线。可复用核心不引用游戏命名空间。

## 清理与兼容

已移除旧 PortalEntity、实体类型专用传送路径、动画克隆、旧抛射/预览实现、角色专用 ECS 运动系统和重复物块实现。原开关式通关配置已替换为圆球，现有三个关卡的瓦片和物块数据保留。Amiya 保留在场景中并设为不激活。

同时修复输入重复叠加、右键回收与放门冲突、关卡失败事件递归、事件重入、关卡切换残留传送门、物块放置重叠、运行时仅从 Editor AssetDatabase 加载物块的问题。背包、存档、其他可选机关和原 UI 保留；旧存档中的“出口已解锁”标记不能绕过当前圆球状态。

## 验证记录

- PlayMode：19 / 19 传送门测试通过，其中一个测试包含全部 16 种入口/出口正交方向组合。
- 实际预制体与关卡流程：18 / 18 检查通过，包含走路、跳跃、推箱子、Player 速度/旋转/立正、摄像机方向、材质恢复、物块刚体配置和无空中阻尼、圆球交互范围/逐个激活/全部解锁/重置。
- 实际关卡中的门状态快照、清除、事件恢复、重新配对、失败重生和切换关卡清门均已通过 MCP 验证。
- 已通过 MCP 读取场景组件：无 Missing Script，Player 使用 Dynamic 刚体，与地面及物块物理层发生碰撞。
- 已检查双端精灵切片和圆球柔光的实际摄像机渲染；图像保存于 `Logs/Validation/portal-slicing-validation.png`。
- 重构基线的 Windows x64 开发构建成功，0 错误、0 警告。结果记录在 `Logs/Validation/build-result.txt`；构建产物位于 `Builds/Portal2DValidation`。后续墙角、跨门碰撞与编辑器改动通过 Editor 编译与以下运行验证，未重新生成该 Windows 构建。

### 墙角 Tilemap 掉落修复

复现了截图布局：同一合并 Tilemap 包含地面、柱体和上方平台，入口贴柱体墙角，出口位于平台底面。旧代码在关闭实体与原 Tilemap 的碰撞后，裁剪几何因相邻顶点过近而抛出异常，临时碰撞体没有完成，原碰撞也没有恢复。

修复清理重复顶点和短边；所有副本创建成功后才提交碰撞切换；失败时回滚并清理临时对象。无有效几何的支撑面保持实体，恢复有效后可以再次进入。传送枪手持显示脚本已移除，背包选择与放门操作保留。

- 修复前新增回归测试明确失败于 `PhysicsShapeGroup2D.AddPolygon`；记录：`Logs/Validation/tilemap-corner-before.json`。
- 修复后 12 / 12 物理测试通过：`Logs/Validation/tilemap-corner-physics-after.json`。
- 真实预制体 10 / 10 检查通过：Player 推箱进入墙角门并跟随穿越，两者由顶部出口落回地面；旁观物块承托、碰撞状态恢复、临时碰撞体清理及无手持枪模型均通过。记录：`Logs/Validation/tilemap-corner-gameplay-after.json`。
- `Artifacts/Portals2D.unitypackage` 已同步本次核心修复与回归测试。

重构前备份位于 `C:/Users/Priestess/.codex/tmp/Portal2D-before-portal-refactor-20261030.zip`。未提交 Git；原有 `.vscode` 文件未修改。

## 跨门碰撞与主导权修复

- 主实体的所有碰撞体完全通过门面后才换边，此前重力与输入仍作用于入口侧本体。映射位置、旋转与速度，不附加吸入或出口推进力。
- 远端动态实体使用共享的影子碰撞箱，将接触响应传回原刚体；同侧直接接触、双向镜像接触去重。两侧静态地形合并，消除连续推箱时门口的隐形边。
- 移除角色控制器对整个穿门期间的输入冻结。真实物理阻挡仍按通用刚体规则处理，没有 Player 专用传送或脱困分支。
- `shadow-physics-before.json` 记录修改前 3 项新增用例失败；`shadow-physics-after.json` 记录修改后 17 / 17 测试通过。
- `shadow-wall-push-after.json`、`shadow-ceiling-push-after.json`：真实 Player 与物块在墙对墙、墙对天花板两种场景中分别通过 10 项检查。
- `shadow-floor-input-after.json`：真实 Player 半身处于地面门时的输入与主导权检查 5 / 5 通过。
- `shadow-gameplay-regression-after.json`：本轮改动后重新运行完整游戏流程，18 / 18 检查通过。
- 以上记录位于 `Logs/Validation`。本轮物理改动前备份：`C:/Users/Priestess/.codex/tmp/Portal2D-before-shadow-physics-20261030.zip`。

## 关卡编辑器扩展

打开 `Window > Portal2D > Level Editor (关卡编辑器)`。左侧关卡列表支持按显示名、标识和路径搜索；单击查看地图预览，双击或点击“载入所选关卡”打开。浏览选择与当前编辑关卡分开，保存始终写回当前载入的关卡。

物件库支持从 Project 拖入多个预制体、对象选择框导入，以及预制体右键菜单 `Portal2D > 加入关卡物件库`。默认目录为 `Assets/Resource/LevelObjectCatalog.asset`，可在 Inspector 中编辑分类和显示名。点击物件按钮会在 Scene 中心放置仍保持预制体连接的实例；支持 Undo。新增物件无需继承圆球或通关机关类。

编辑器扩展可调用：

```csharp
LevelPrefabLibrary.RegisterPrefab(prefab, "激光发射器", "机关");
Portal2DLevelEditorWindow.PlacePrefab(prefab, levelRoot, worldPosition);
```

普通预制体的组件与默认参数自动保留；位置、旋转、缩放和现有精灵覆盖由关卡保存。若一个物件需要每关不同的参数（例如激光范围、移动平台路径或速度），其组件可实现 `Portal2D.Core.Data.ILevelObjectState`：

```csharp
public string CaptureLevelState() => JsonUtility.ToJson(configuration);
public void RestoreLevelState(string state)
{
    JsonUtility.FromJsonOverwrite(state, configuration);
    ApplyConfiguration();
}
```

接口保存字符串配置，可用于子节点组件；恢复发生在实例创建之后、Start 之前。它不自动保存任意 Inspector 覆盖或跨物件场景引用：共享默认值请修改预制体，每关参数通过接口保存；路径等空间数据应明确采用物件本地坐标。插件无需引用 Editor 命名空间。圆球通关统计仍独立于物件库。

MCP 验证：自定义预制体导入、重复导入防重、预制体连接、子组件配置、资产持久化、位置缩放和刚体配置完整往返，共 9 项通过。记录：`Logs/Validation/level-editor-extensions.json`。

## 视频中的贴图闪落与体验优化

针对 `QQ20261030-205243.mp4` 和用户补充的“物理正常，仅瞬间跌落贴图”：旧交接流程更新 Rigidbody2D 后立即移除克隆，但渲染 Transform 仍保留入口位置；在新回归场景中两者相差 15.66 单位。现在先同步出口位置和角度，再发出交接事件、移除切片，经过一个出口侧物理步后恢复插值。碰撞规则和动量映射保持原逻辑。记录：`video-render-before.json`、`video-render-physics-after.json`（18 项通过）。

摄像机不再订阅传送事件直接平移并清空速度，而是持续以同一阻尼速度追踪最新目标。重生和载入关卡仍立即定位。滚轮缩放改为按时间计算的指数平滑；相机始终保持竖直。

Player 的地面加速度由 45 提高至 120，并同步到实际 Player 预制体。未改变物块质量、重力、碰撞或空中控制。真实双层标准物块测试：旧版本移动约 0.11 格，修改后移动约 13.85 格且保持堆叠。`experience-after.json` 同时验证六次连续传送、相机无事件瞬移、三个窗口比例下的完整视野边界和切换关卡释放限制，共 8 项通过；`video-wall-push-after.json` 验证真实预制体墙对墙推箱，10 项通过。

### 编辑地图边界与背景

载入关卡后，在“地图边界与背景”中：

- 启用“启用地图边界（实体＋相机）”，填写世界坐标矩形，也可点击“从瓦片计算边界”；“在 Scene 中调整”显示两个角点手柄。矩形四边生成静态实体碰撞箱，厚度 2 单位位于地图外侧，内表面恰好贴合矩形，角色与物块通过普通物理碰撞被挡住。整个相机画面也限制在此矩形内，靠近边缘时停下，视野过大时限制缩放。关卡库缩略图以橙框标出边界。
- 配置“背景底色”；可选 Sprite 图片和色调。启用边界时图片铺满边界，未启用时可独立填写“图片覆盖矩形”。图片在 Scene 中预览，运行时自动生成在游戏物件后面，没有物理组件。
- 配置保存在 LevelAsset 中，按“保存当前编辑关卡”写盘。保留已有 `confineCamera` / `cameraBounds` 序列化字段，已设置的边界无需重新填写；新关卡默认关闭。不同关卡可以使用不同背景。边界和背景都是派生对象，不进入可交互预制体列表，也不参与圆球统计，随关卡卸载自动清理。

保存、重载、非中心图片 Pivot、渲染层级及背景排除序列化共 7 项检查通过：`level-environment-after.json`；预制体扩展的 9 项往返验证也重新通过：`environment-prefab-roundtrip-after.json`。以上记录均位于 `Logs/Validation`。

本轮依据本机 Unity 6000.5.5f1 的 `Rigidbody2D-interpolation.html`、`Rigidbody2D-position.html`、`Vector3.SmoothDamp.html` 和 `Camera-orthographicSize.html` 核对 API 行为。修改前备份：`C:/Users/Priestess/.codex/tmp/Portal2D-before-video-fix-20261030.zip`。独立传送门包已更新；本轮没有重新生成 Windows 可执行构建。

## 推箱出门倾斜闪动与实体边界

针对 `QQ20261030-212938.mp4` 及截图逐帧定位：方块尚未完全通过时，碰撞角度也发生异常变化，底角沉入地面，随后才被纠正。前一轮交接时的渲染同步修复没有覆盖这个阶段。原因是入口地形与出口映射地形的凸片段被放入同一个 PolygonCollider2D 的多条路径；路径允许表示孔洞，重叠支撑区域因此被挖空。现在每个凸片段使用独立的合并操作数，再由 CompositeCollider2D 合并为实体地面。没有冻结物块旋转、增加推进力或添加 Player 专用碰撞分支。

- 新增慢速自由方块回归：修复前底角最低 -0.794、最大倾角 65.39°，修复后通过。完整模块测试 19 / 19 通过，记录 `flicker-slow-floor-before.json`、`flicker-slow-floor-after.json`、`flicker-physics-after.json`。
- 真实预制体快速与慢速推箱分别通过 11 项检查；慢速时底角最低 +0.0132、最大倾角约 0.014°，没有下沉。记录 `flicker-real-wall-after.json`、`flicker-slow-real-wall-after.json`。连续摄像机渲染帧位于 `slow-push-render-after`，缩略图为 `contact-sheet.jpg`。
- `LevelBoundary2D` 使用四个静态非 Trigger BoxCollider2D，随关卡构建、编辑与卸载更新。实际 Player、标准物块和普通圆形刚体以 70 单位/秒冲向四边与四角，均保持在边界内；调整、关闭、无效尺寸及卸载验证共 10 项通过，记录 `solid-boundary-physics-after.json`。
- 边界与相机共用原有持久化矩形，保存/重载以及背景兼容共 9 项通过，记录 `solid-boundary-persistence-after.json`。生成的边界不会被误存为物件预制体。

以上记录位于 `Logs/Validation`。修改前备份：`C:/Users/Priestess/.codex/tmp/Portal2D-before-flicker-boundary-20261030.zip`；测试前已备份磁盘场景并保存当时的场景编辑，未丢弃未保存内容。API 语义核对使用本机 `PolygonCollider2D-pathCount.html`。本轮未重新构建 Windows 可执行文件。
