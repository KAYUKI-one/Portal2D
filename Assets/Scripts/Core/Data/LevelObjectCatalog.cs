using System;
using System.Collections.Generic;
using UnityEngine;

namespace Portal2D.Core.Data
{
    [CreateAssetMenu(menuName="Portal2D/Level Object Catalog",fileName="LevelObjectCatalog")]
    public sealed class LevelObjectCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public GameObject prefab;
            public string displayName;
            public string category="自定义物件";
        }
        [SerializeField] private List<Entry> entries=new();
        public IReadOnlyList<Entry> Entries => entries;
        public bool Add(GameObject prefab, string displayName=null, string category="自定义物件")
        {
            if(prefab==null || entries.Exists(e=>e.prefab==prefab)) return false;
            entries.Add(new Entry {prefab=prefab,displayName=string.IsNullOrWhiteSpace(displayName)?prefab.name:displayName,category=category});
            return true;
        }
    }
}
