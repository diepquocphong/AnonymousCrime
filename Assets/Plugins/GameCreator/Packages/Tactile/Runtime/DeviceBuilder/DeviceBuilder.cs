using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using System.Collections.Generic;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class DeviceBuilder : TPolymorphicList<TInputControl>
    {
        public static readonly string LAYOUT_NAME = "Tactile";

        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeReference] 
        private TInputControl[] m_InputControls = new TInputControl[]
        { 
            new InputControlStick("movestick", "Move Stick"),
            new InputControlDelta("lookdelta", "Look Delta"),
            new InputControlDelta("zoomdelta", "Zoom Delta"),
            new InputControlButton("interact", "Interact"),
            new InputControlButton("crouch", "Crouch"),
            new InputControlButton("jump", "Jump"),
            new InputControlButton("sprint", "Sprint"),
            new InputControlButton("attack", "Attack"),
        };

        [NonSerialized] private List<TInputControl> m_SortedInputControls = new ();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override int Length => this.m_InputControls.Length;

        // BUILD LAYOUT: --------------------------------------------------------------------------

        internal void BuildDeviceLayout()
        {
            if (this.Length == 0) return;

            this.m_SortedInputControls.Clear();
            this.m_SortedInputControls.AddRange(this.m_InputControls);
            this.m_SortedInputControls.Sort(SizeInBitsComparer);

            var builder = new InputControlLayout.Builder()
                .WithName("Tactile Device")
                .WithDisplayName("Tactile")
                .WithFormat("TACT")
                .WithType<TactileDevice>();

            int currentByteOffset = 0;
            int currentBitOffset = 0;

            for (int i = 0; i < this.m_SortedInputControls.Count; i++)
            {
                if (!this.m_SortedInputControls[i].IsEnabled) continue;

                this.m_SortedInputControls[i].AddControlToBuilder(
                    builder, ref currentByteOffset, ref currentBitOffset
                );
            }

            #if UNITY_EDITOR
            string jsonLayout = builder.Build()
                                       .ToJson()
                                       .Replace("\"isGenericTypeOfDevice\": false", 
                                                "\"isGenericTypeOfDevice\": true");

            InputControlLayout layout = InputControlLayout.FromJson(jsonLayout);
            #else
            InputControlLayout layout = builder.Build();
            #endif
            
            InputSystem.RegisterLayoutBuilder(() => layout, LAYOUT_NAME);
        }

        // PRIVATE STATIC METHODS: ----------------------------------------------------------------

        private static int SizeInBitsComparer(TInputControl x, TInputControl y)
        {
            return y.ControlSizeInBits.CompareTo(x.ControlSizeInBits);
        }

    }
}