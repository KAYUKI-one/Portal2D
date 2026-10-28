namespace Portal2D.Core.ECS
{
    /// <summary>
    /// 系统基接口
    /// </summary>
    public interface ISystem
    {
        void Initialize(World world);
        void Update(float deltaTime);
        void Cleanup();
    }
}
