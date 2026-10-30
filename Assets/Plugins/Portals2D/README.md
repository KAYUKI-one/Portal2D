# Portals2D

独立的二维刚体传送门模块。验证环境：Unity 6000.5.5f1、URP 2D、Rigidbody2D 物理系统。

## 导入与使用

将整个 `Assets/Plugins/Portals2D` 目录复制到另一 Unity 项目，或导入项目根目录 `Artifacts/Portals2D.unitypackage`。模块不引用本游戏的角色、背包、关卡或输入代码。

- 预制体：`Resources/Gameplay/PortalA.prefab`、`PortalB.prefab`。
- 本地 +Y 为出口朝外方向，本地 X 为开口长度方向。门根节点保持单位缩放。
- 默认开口长 2.2 单位；两门长度必须一致。
- 实体只需要可模拟的 Dynamic / Kinematic `Rigidbody2D` 和非 Trigger `Collider2D`。支持子节点上的复合碰撞体，无需给实体添加传送门脚本。
- 静态墙体、单向平台和 Trigger 不作为穿越实体。放置表面需为静态、连续、平直、完整支撑开口的碰撞体；单向平台不接受放置。
- 两端切片显示针对 `SpriteRenderer`。跨门接触由独立的影子碰撞箱处理，不复制 Animator、输入或游戏行为脚本。其他渲染器可通过事件接入自己的表现。

```csharp
using SeamlessPortals2D;
using UnityEngine;

// hit 是瞄准射线首先击中的墙面。mask 仅包含允许放门的墙面层。
if (PortalPlacement2D.TryFit(hit.point, hit.normal, portal.Length, mask, out var fit))
    portal.Place(fit.Position, fit.Normal, fit.Supports);

portalA.Link(portalB);
// 移除 / 更换门时先 Unlink，正在穿越的实体会安全退出并恢复碰撞。
```

直接在场景中配置时，需设置 Partner、Support Colliders 和 Slice Material，或者在启动脚本中调用以上 API。预制体已经配置了 Slice Material；游戏的 `PortalManager` 负责射击放置时的表面验证和配对。

## 物理与事件

每个固定物理步检测实体是否完整处于开口宽度内，并等所有实体碰撞体的最后边缘完全越过门面后才切换主导权。此前继续使用入口侧实体的重力、输入和受力；影子碰撞体不施加第二份重力。保留穿过平面的距离，不使用传送冷却、最低出口速度或吸入加速。位置、线速度和刚体角度仅作刚性旋转映射，保持速度大小与角速度。

开口期间，只为当前实体忽略原支撑碰撞体。局部支撑面扣除门洞后，与映射过来的出口侧静态地形合并为连续碰撞面，消除门口的隐形边界。每个凸形片段作为独立的 PolygonCollider2D 合并操作数，再交给 CompositeCollider2D 做实体合并；不能将重叠片段放进同一个 PolygonCollider2D 的多条路径，否则重叠地面可能被解释成孔洞，导致慢速推箱倾斜下沉。原地图继续阻挡其他实体，同一 Tilemap 上门外地面保持承托。

出口附近的动态 / Kinematic 实体使用只有碰撞箱的影子刚体。一个远端刚体在同一入口只拥有一个共享影子；同侧实体保留直接接触，双向进入的实体对只解算一次镜像接触。动态影子的接触响应映射回原刚体，包括线速度、角速度和接触位移；不克隆游戏脚本或外加推进力。此结构参考 Valve 开发者公开的 [Portal Problems 讲解](https://cs50.harvard.edu/games/2018/weeks/11/)。离开、停用、重新配对或移除时恢复原碰撞忽略状态、插值与材质，并清理影子。

碰撞副本会先清理裁剪产生的重复顶点和极短边；全部副本成功创建后，才切换当前实体的碰撞。创建失败时清理副本并保留原碰撞，避免异常留下整块 Tilemap 对该实体失效的状态。每扇门只裁剪自己的支撑面。顶点间距限制依据 [Unity PhysicsShapeGroup2D 实现](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.2/Modules/Physics2D/ScriptBindings/Physics2D.bindings.cs)。

```csharp
private void OnEnable() => PortalEvents.Transit += OnTransit;
private void OnDisable() => PortalEvents.Transit -= OnTransit;
private void OnTransit(PortalTransitEvent e)
{
    // Entered / Teleported / Exited / Cancelled
    // Body, Entry, Exit, PreviousPosition, Position, Velocity, RotationDelta
}
```

自定义角色控制器在穿门期间仍应响应移动输入，避免使用 `IsTravelling` 冻结操作，也不要用固定速度覆盖传送保留的动量。重生或外部强制移动前调用 `PortalSimulation2D.ReleaseBody(body)`。摄像机不参与刚体检测，由游戏自己的跟随控制器处理。重力仍由所在游戏决定，模块不旋转世界重力。

主导权交接时，在移除双端切片前同步刚体和渲染 Transform 的目标位置与角度；经过一个出口侧物理步后恢复原插值模式，避免旧位置参与新位置的插值而产生一帧的掉落/闪动。该处理不改变速度或碰撞求解。

此版本使用默认 Physics2D 场景和固定物理步。验证覆盖自由刚体、复合碰撞体和静态墙面；跨门的 Joint2D 链条、移动门框、独立 PhysicsScene2D 和非精灵切片需要额外适配。Kinematic 实体若由外部脚本逐帧指定坐标，该脚本应消费 Teleported 事件更新自己的运动目标。

## 验证

Unity Test Runner 的 PlayMode 中运行 `Portals2D.Tests`。19 项测试覆盖 16 种正交方向、线性/切向/角动量、高速圆形实体、复合碰撞体、反向穿回、超宽实体拒绝、短墙和缺口拒绝、贴角、共享 Tilemap 地板、取消恢复、事件回调移除门、无效几何失败保护、完整入门后换边、远端障碍阻挡、动态物块跨门反作用、双向进入不重复施力、天花板出口不提前拉动入口实体、交接时的渲染姿态同步与插值恢复，以及慢速推箱跨门期间的最低点和旋转稳定性。

测试源码随模块提供；使用 Unity Test Framework。游戏专用预制体与关卡流程验证位于项目的 `PortalGameplayValidation` 和 `PortalTilemapCornerValidation` 编辑器验证组件中，不包含在独立模块内。后者使用真实 Player、物块和 A/B 门预制体，验证推箱穿越、两个实体同时穿越、顶部出口落地、旁观物块承托、碰撞恢复与临时碰撞体清理。
