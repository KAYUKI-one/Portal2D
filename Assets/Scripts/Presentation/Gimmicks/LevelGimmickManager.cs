using System;
using UnityEngine;

namespace Portal2D.Presentation.Gimmicks
{
    /// <summary>
    /// [已废弃] 关卡机关旧版常驻仲裁器。
    /// 现已由纯事件解耦、挂载于关卡实例生命周期的 LevelRuleController 取代。
    /// 保留此文件仅防外部元数据丢失。
    /// </summary>
    [Obsolete("Use LevelRuleController on the level instance instead.")]
    public class LevelGimmickManager : MonoBehaviour
    {
    }
}
