using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Serializable]
    public class SwipeDirection
    {
        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] private bool m_IsActive = true;
        [SerializeField] private InputSimulateButton m_InputSimulate;
        [SerializeField, Min(-1)] private int m_Fingers;
        [SerializeField] private IdString m_Id;
        [SerializeField] private float m_Angle;
        [SerializeField] private float m_Arc;

        // PROPERTIES: ----------------------------------------------------------------------------

        public bool IsActive
        {
            get => this.m_IsActive;
            set => this.m_IsActive = value;
        }

        public string Id => this.m_Id.String;
        public int Hash => this.m_Id.Hash;
        public float Angle => this.m_Angle;
        public float Arc => this.m_Arc;
        public int Fingers => this.m_Fingers;

        public InputSimulateButton InputSimulate => this.m_InputSimulate;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public SwipeDirection()
        { 
            this.m_Id = new IdString();
            this.m_InputSimulate = new InputSimulateButton();
        }

        public SwipeDirection(
            string id, float angle, float arc, int fingers = 1, ControlPathButton path = null)
        { 
            this.m_Id = new IdString(id);
            this.m_Angle = angle;
            this.m_Arc = arc;
            this.m_InputSimulate = new InputSimulateButton(path);
            this.m_Fingers = fingers;
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void Enable(TactileControl control)
        {
            this.m_InputSimulate?.OnEnabled(control);
        }

        public void Disable()
        {
            this.m_InputSimulate?.OnDisabled();
        }

        public bool IsWithinBounds(float inputAngle)
        {
            const float LENGTH = 360f;

            float normalizedAngle = (LENGTH - this.Angle) % LENGTH;
            float normalizedInput = (LENGTH - inputAngle) % LENGTH;

            float arcTolerance = Mathf.Abs(this.Arc / 2f);
            float lowerBound = (normalizedAngle - arcTolerance + LENGTH) % LENGTH;
            float upperBound = (normalizedAngle + arcTolerance) % LENGTH;

            return lowerBound < upperBound
                ? normalizedInput >= lowerBound && normalizedInput <= upperBound
                : normalizedInput >= lowerBound || normalizedInput <= upperBound;
        }

        public void SimulateInputButton()
        {
            if (this.m_InputSimulate == null) return;

            this.m_InputSimulate.SendValueToControl(1f);
            this.m_InputSimulate.SendResetValueToControl();
        }
    }
}