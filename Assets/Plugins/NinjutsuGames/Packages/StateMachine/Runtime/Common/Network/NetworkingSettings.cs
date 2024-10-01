using System;
using UnityEngine;

namespace NinjutsuGames.StateMachine.Runtime
{
    [Serializable]
    public class NetworkingSettings
    {
        public bool networkSync;
        [HideInInspector] public string nodeId;
        [SerializeReference] public TNetworkConfig config;
        
        public NetworkingSettings(string nodeId)
        {
            networkSync = false;
            this.nodeId = nodeId;
        }
    }
}