using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item Icon Set")]
public class ItemIconSet : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string itemId;
        public Sprite icon;
    }

    [SerializeField] private Entry[] entries;

    public Sprite Find(string itemId)
    {
        if (entries == null)
            return null;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].itemId == itemId)
                return entries[i].icon;
        }

        return null;
    }
}
