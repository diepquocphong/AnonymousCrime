using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using GameCreator.Runtime.Characters;

namespace GameCreator.Runtime.VisualScripting.MobileController
{
    [Version(0, 0, 1)]

    [Title("Change Zoom Distance")]
    [Category("MobileController/Change Zoom Distance")]
    [Description("Changes the zoom DIstance")]
    [Parameter("Zoom Speed", "This control the sensitivity of the zoom.")]
    [Parameter("Min Zoom", "This control the minimum value of the zoom.")]
    [Parameter("Max Zoom", "This control the maximum value of the zoom.")]
    
    [Serializable]
    public class MobileControllerZoomInstruction : TInstructionShotZoom
    {

        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private PropertyGetDecimal m_ShotSmoothTime = GetDecimalDecimal.Create(50f);
        [Space]
        [SerializeField] private PropertyGetDecimal m_zoomSpeed = GetDecimalDecimal.Create(1f);


        // PRIVATE MEMBERS: -------------------------------------------------------------------------------

        private PropertySetNumber m_Distance = new PropertySetNumber();
        private PropertyGetDecimal m_minZoom = GetDecimalMathPercent.Create(0.1f);
        private PropertyGetDecimal m_maxZoom = GetDecimalMathPercent.Create(1);
        

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Change {this.m_Shot}[Zoom] Distance";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
           
            //Update Zoom Information
            ShotSystemZoom shotSystem = this.GetShotSystem<ShotSystemZoom>(args);
            if (shotSystem == null) return DefaultResult;
            shotSystem.SmoothTime = (float)m_ShotSmoothTime.Get(args);

            if (ShortcutPlayer.Instance != null)
            {
                Character character = ShortcutPlayer.Instance.Get<Character>();
                if ((Input.touchCount == 2 && character.Player.InputDirection == Vector3.zero) || (Input.touchCount == 3 && character.Player.InputDirection != Vector3.zero))
                {
                    float zoomSpeed = (float)this.m_zoomSpeed.Get(args);
                    float minZoom = (float)this.m_minZoom.Get(args);
                    float maxZoom = (float)this.m_maxZoom.Get(args);

                    Touch touch0; Touch touch1;
                    if (Input.touchCount == 3)
                    {
                       touch0 = Input.GetTouch(1);
                       touch1 = Input.GetTouch(2);
                    }else
                    {
                        touch0 = Input.GetTouch(0);
                        touch1 = Input.GetTouch(1);
                    }
                    

                    // Calculate the current and previous distance between touches
                    float prevDistance = (touch0.position - touch0.deltaPosition - (touch1.position - touch1.deltaPosition)).magnitude;
                    float currentDistance = (touch0.position - touch1.position).magnitude;

                    // Calculate the difference in distances and adjust the zoom level
                    float zoomDelta = (prevDistance - currentDistance) * zoomSpeed;

                    //Determine if zoom in or zoom out
                    if (zoomDelta >= 0.01)
                    {
                        shotSystem.Level = Mathf.Clamp(shotSystem.Level + 0.1f * zoomSpeed, minZoom, maxZoom);
                    }
                    else if(zoomDelta <= -0.01)
                    {
                        shotSystem.Level = Mathf.Clamp(shotSystem.Level - 0.1f * zoomSpeed, minZoom, maxZoom);
                    }

                }
                this.m_Distance.Set(shotSystem.Level, args);

            }
            
            return DefaultResult;
        }

    }

}
