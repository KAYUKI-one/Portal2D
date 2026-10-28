using System;

namespace Portal2D.Core.ECS
{
    /// <summary>
    /// 轻量级实体标识符 (纯值类型)
    /// </summary>
    [Serializable]
    public readonly struct Entity : IEquatable<Entity>
    {
        public static readonly Entity Null = new Entity(0);

        public readonly int Id;

        public bool IsValid => Id > 0;

        public Entity(int id)
        {
            Id = id;
        }

        public bool Equals(Entity other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Entity other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => $"Entity({Id})";

        public static bool operator ==(Entity left, Entity right) => left.Id == right.Id;
        public static bool operator !=(Entity left, Entity right) => left.Id != right.Id;
    }
}
