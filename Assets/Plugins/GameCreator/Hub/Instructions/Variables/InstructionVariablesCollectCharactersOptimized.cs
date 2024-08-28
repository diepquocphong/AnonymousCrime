using System;
using System.Collections.Generic;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.VisualScripting
{
    [Version(1, 0, 3)]

    [Title("Collect Characters [Optimized]")]
    [Category("Variables/Collect Characters [Optimized]")]
    [Description("Optimized version of the default 'Collect Characters' instruction")]

    [Image(typeof(IconBust), ColorTheme.Type.Teal, typeof(OverlayBolt))]

    [Serializable]
    public class InstructionVariablesCollectCharactersOptimized : TInstructionVariablesCollect
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private List<ISpatialHash> m_Results = new List<ISpatialHash>();
        [NonSerialized] private List<GameObject> m_Buffer = new List<GameObject>();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        protected override string TitleTarget => "Characters";
        
        // COLLECT METHOD: ------------------------------------------------------------------------

        protected override List<GameObject> Collect(Vector3 origin, float maxRadius, float minRadius)
        {
            this.m_Buffer.Clear();
            SpatialHashCharacters.Find(origin, maxRadius, this.m_Results);

            for (int i = 0; i < m_Results.Count; i++)
            {
                Vector3 offset = this.m_Results[i].Position - origin;
                if (offset.sqrMagnitude <= minRadius * minRadius) continue;
                
                Character character = this.m_Results[i] as Character;
                if (character == null) continue;
                
                this.m_Buffer.Add(character.gameObject);
            }

            return this.m_Buffer;
        }

    }
}