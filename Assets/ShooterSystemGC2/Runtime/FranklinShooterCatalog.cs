using System;
using System.Collections.Generic;
using GameCreator.Runtime.Shooter;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Project-local list of GC2 shooter weapons and the Weapons Low props used to display them.
    /// The editor installer creates this asset under Resources/FranklinShooter.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Franklin Shooter Catalog",
        menuName = "Franklin Game/Shooter/Weapon Catalog"
    )]
    public sealed class FranklinShooterCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string m_Id;
            [SerializeField] private string m_DisplayName;
            [SerializeField] private string m_Category;
            [SerializeField] private ShooterWeapon m_Weapon;
            [SerializeField] private GameObject m_PropPrefab;
            [SerializeField] private Sprite m_Icon;
            [SerializeField] private int m_StartingMagazine;
            [SerializeField] private Vector3 m_LocalPosition = new(-0.04f, 0.09f, 0.04f);
            [SerializeField] private Vector3 m_LocalRotation = new(-90f, 0f, 90f);
            [SerializeField] private Vector3 m_LocalScale = Vector3.one;
            [SerializeField] private Vector3 m_ModelLocalPosition = Vector3.zero;
            [SerializeField] private Vector3 m_ModelLocalRotation = Vector3.zero;
            [SerializeField] private Vector3 m_ModelLocalScale = Vector3.one;

            public string Id => this.m_Id;
            public string DisplayName => this.m_DisplayName;
            public string Category => this.m_Category;
            public ShooterWeapon Weapon => this.m_Weapon;
            public GameObject PropPrefab => this.m_PropPrefab;
            public Sprite Icon => this.m_Icon;
            public int StartingMagazine => Mathf.Max(0, this.m_StartingMagazine);
            public Vector3 LocalPosition => this.m_LocalPosition;
            public Vector3 LocalEulerAngles => this.m_LocalRotation;
            public Quaternion LocalRotation => Quaternion.Euler(this.m_LocalRotation);
            public Vector3 LocalScale => this.m_LocalScale;
            public Vector3 ModelLocalPosition => this.m_ModelLocalPosition;
            public Vector3 ModelLocalEulerAngles => this.m_ModelLocalRotation;
            public Quaternion ModelLocalRotation => Quaternion.Euler(this.m_ModelLocalRotation);
            public Vector3 ModelLocalScale => this.m_ModelLocalScale;
        }

        [SerializeField] private Entry[] m_Weapons = Array.Empty<Entry>();
        [SerializeField] private int m_DefaultWeaponIndex;

        public IReadOnlyList<Entry> Weapons => this.m_Weapons;
        public int Count => this.m_Weapons?.Length ?? 0;
        public int DefaultWeaponIndex => this.Count == 0
            ? -1
            : Mathf.Clamp(this.m_DefaultWeaponIndex, 0, this.Count - 1);

        public Entry Get(int index)
        {
            return index >= 0 && index < this.Count ? this.m_Weapons[index] : null;
        }

        public int IndexOf(ShooterWeapon weapon)
        {
            if (weapon == null || this.m_Weapons == null) return -1;
            for (int i = 0; i < this.m_Weapons.Length; ++i)
            {
                if (this.m_Weapons[i]?.Weapon == weapon) return i;
            }

            return -1;
        }
    }
}
