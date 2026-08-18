using System;
using UnityEngine;

namespace GameCreator.Runtime.Common
{
    [AddComponentMenu("")]
    internal class PoolInstance : MonoBehaviour
    {
        [NonSerialized] private int m_PrefabId;
        [NonSerialized] private bool m_HasDuration;
        [NonSerialized] private float m_DisableAt;

        // INITIALIZERS: --------------------------------------------------------------------------
        
        private void OnDisable()
        {
            this.m_HasDuration = false;
            
            if (ApplicationManager.IsExiting) return;
            PoolManager.Instance.OnDisableInstance(this.m_PrefabId, this);
        }

        private void OnDestroy()
        {
            if (ApplicationManager.IsExiting) return;
            PoolManager.Instance.OnDestroyInstance(this.m_PrefabId, this);
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void OnCreate(int prefabId)
        {
            this.m_PrefabId = prefabId;
        }
        
        public void SetDuration(float duration)
        {
            this.m_HasDuration = true;
            this.m_DisableAt = Time.time + duration;
        }

        // UPDATE METHOD: -------------------------------------------------------------------------

        private void Update()
        {
            if (!this.m_HasDuration || Time.time < this.m_DisableAt) return;

            this.m_HasDuration = false;
            this.gameObject.SetActive(false);
        }
    }
}
