// ----------------------------------------------------
// Blood Factory
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using PampelGames.Shared.Tools;
using PampelGames.Shared.Utility;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PampelGames.BloodFactory
{
    /// <summary>
    ///     Using reflection to cover both URP and HDRP Decal Projector.
    /// </summary>
    public class DecalHandler : MonoBehaviour, PGIExecutable
    {
        public Component decalComponent;
        public float lifetime = 5f;
        public float fadeIn = 0.5f;
        public float fadeOut = 1f;
        public Vector2 size = new(0.75f, 1.25f);
        public bool fadeInSize = true;
        public float fadeInSizeTime = 0.2f;
        public List<Material> materials = new();

        private bool initialized;

        private Func<Vector3> sizeGetter;
        private Action<Vector3> sizeSetter;
        private Action<Material> materialSetter;
        private Action<float> fadeFactorSetter;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            var decalType = decalComponent.GetType();

            var sizeProperty = decalType.GetProperty("size");
            var materialProperty = decalType.GetProperty("material");
            var fadeFactorProperty = decalType.GetProperty("fadeFactor");

            sizeGetter = (Func<Vector3>) Delegate.CreateDelegate(typeof(Func<Vector3>), decalComponent, sizeProperty!.GetGetMethod());
            sizeSetter = (Action<Vector3>) Delegate.CreateDelegate(typeof(Action<Vector3>), decalComponent, sizeProperty.GetSetMethod());
            materialSetter = (Action<Material>) Delegate.CreateDelegate(typeof(Action<Material>), decalComponent, materialProperty!.GetSetMethod());
            fadeFactorSetter = (Action<float>) Delegate.CreateDelegate(typeof(Action<float>), decalComponent, fadeFactorProperty!.GetSetMethod());

            fadeFactorSetter(0f);
        }

        public void Execute()
        {
            Initialize();

            var randomAngle = Random.Range(0f, 360f);
            transform.Rotate(0f, 0f, randomAngle, Space.Self);

            var randomIndex = Random.Range(0, materials.Count);
            materialSetter(materials[randomIndex]);

            var randomSize = Random.Range(size.x, size.y);
            sizeSetter(Vector3.one * randomSize);

            if (fadeInSize)
            {
                var currentSize = sizeGetter();
                var sizeTween = PGTween.Move(this, 0f, 1f, fadeInSizeTime);

                sizeTween.OnUpdate(() =>
                {
                    var curveEvaluation = sizeTween.currentTime / fadeInSizeTime;
                    sizeSetter(currentSize * curveEvaluation);
                });
            }

            var fadeTween = PGTween.Move(this, 0f, 1f, fadeIn);
            fadeTween.OnUpdate(() => { fadeFactorSetter((float) fadeTween.currentValue); });

            PGScheduler.ScheduleTime(this, lifetime - fadeOut, () =>
            {
                var fadeOutTween = PGTween.Move(this, 1f, 0f, fadeOut);
                fadeOutTween.OnUpdate(() => { fadeFactorSetter((float) fadeOutTween.currentValue); });
            });
        }
    }
}