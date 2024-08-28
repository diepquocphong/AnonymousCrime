using System;
using GameCreator.Runtime.Common;
using UnityEngine;
using GameCreator.Runtime.VisualScripting;

[Version(1, 0, 0)]
[Title("On Trigger Enter Layer")]
[Category("Physics/On Trigger Enter Layer")]
[Description("Executed when a game object belongs to any of the layer mask values and enters the Trigger collider")]

[Image(typeof(IconLayers), ColorTheme.Type.Green)]
[Parameter("Layer Mask", "A bitmask of Layer values")]

[Keywords("Pass", "Through", "Touch", "Collision", "Collide", "Mask", "Physics", "Belong", "Has")]

[Serializable]
public class EventTriggerEnterLayer : GameCreator.Runtime.VisualScripting.Event
{
    [SerializeField] private LayerMask m_LayerMask = Physics.DefaultRaycastLayers;
    // METHODS: -------------------------------------------------------------------------------

    protected override void OnAwake(Trigger trigger)
    {
        base.OnAwake(trigger);
        trigger.RequireRigidbody();
    }

    protected override void OnTriggerEnter3D(Trigger trigger, Collider collider)
    {
        base.OnTriggerEnter3D(trigger, collider);

        if (!this.IsActive) return;
        if (!CheckLayerMask(collider.gameObject)) return;
        GetGameObjectLastTriggerEnter.Instance = collider.gameObject;
        _ = this.m_Trigger.Execute(collider.gameObject);
    }

    protected override void OnTriggerEnter2D(Trigger trigger, Collider2D collider)
    {
        base.OnTriggerEnter2D(trigger, collider);

        if (!this.IsActive) return;
        if (!CheckLayerMask(collider.gameObject)) return;
        GetGameObjectLastTriggerEnter.Instance = collider.gameObject;
        _ = this.m_Trigger.Execute(collider.gameObject);
    }

    protected bool CheckLayerMask(GameObject gameObject)
    {
        if (gameObject == null) return false;

        int bitmask = this.m_LayerMask.value & (1 << gameObject.layer);
        return bitmask > 0;
    }
}


