// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using UnityEngine;

namespace PampelGames.Shared.Tools
{
    /// <summary>
    ///     Handles Tween ease methods.
    /// </summary>
    public static class PGTweenEase
    {
        public enum Ease
        {
            Linear,
            AnimationCurve,
            InSine,
            OutSine,
            InOutSine,
            InQuad,
            OutQuad,
            InOutQuad,
            InCubic,
            OutCubic,
            InOutCubic,
            InQuart,
            OutQuart,
            InOutQuart,
            InQuint,
            OutQuint,
            InOutQuint,
            InExpo,
            OutExpo,
            InOutExpo,
            InCirc,
            OutCirc,
            InOutCirc,
            InBack,
            OutBack,
            InOutBack,
            InElastic,
            OutElastic,
            InOutElastic,
            InBounce,
            OutBounce,
            InOutBounce
        }

        /// <summary>
        ///     Methods from https://easings.net/.
        /// </summary>
        public delegate float EaseMethod(float currentTime, float duration, float amplitude, AnimationCurve animationCurve);

        public static EaseMethod GetEaseMethod(Ease easeType)
        {
            switch (easeType)
            {
                case Ease.Linear:
                    return EaseLinear;
                case Ease.AnimationCurve:
                    return EaseAnimationCurve;
                case Ease.InSine:
                    return EaseInSine;
                case Ease.OutSine:
                    return EaseOutSine;
                case Ease.InOutSine:
                    return EaseInOutSine;
                case Ease.InQuad:
                    return EaseInQuad;
                case Ease.OutQuad:
                    return EaseOutQuad;
                case Ease.InOutQuad:
                    return EaseInOutQuad;
                case Ease.InCubic:
                    return EaseInCubic;
                case Ease.OutCubic:
                    return EaseOutCubic;
                case Ease.InOutCubic:
                    return EaseInOutCubic;
                case Ease.InQuart:
                    return EaseInQuart;
                case Ease.OutQuart:
                    return EaseOutQuart;
                case Ease.InOutQuart:
                    return EaseInOutQuart;
                case Ease.InQuint:
                    return EaseInQuint;
                case Ease.OutQuint:
                    return EaseOutQuint;
                case Ease.InOutQuint:
                    return EaseInOutQuint;
                case Ease.InExpo:
                    return EaseInExpo;
                case Ease.OutExpo:
                    return EaseOutExpo;
                case Ease.InOutExpo:
                    return EaseInOutExpo;
                case Ease.InCirc:
                    return EaseInCirc;
                case Ease.OutCirc:
                    return EaseOutCirc;
                case Ease.InOutCirc:
                    return EaseInOutCirc;
                case Ease.InBack:
                    return EaseInBack;
                case Ease.OutBack:
                    return EaseOutBack;
                case Ease.InOutBack:
                    return EaseInOutBack;
                case Ease.InElastic:
                    return EaseInElastic;
                case Ease.OutElastic:
                    return EaseOutElastic;
                case Ease.InOutElastic:
                    return EaseInOutElastic;
                case Ease.InBounce:
                    return EaseInBounce;
                case Ease.OutBounce:
                    return EaseOutBounce;
                case Ease.InOutBounce:
                    return EaseInOutBounce;

                default:
                    return EaseLinear;
            }
        }

        private static float EaseLinear(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseLinear(currentTime / duration);
        }

        private static float EaseAnimationCurve(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            var x = currentTime / duration;
            var normalizedLength = animationCurve[animationCurve.length - 1].time;
            return animationCurve.Evaluate(x * normalizedLength);
        }

        private static float EaseInSine(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInSine(currentTime / duration);
        }

        private static float EaseOutSine(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseOutSine(currentTime / duration);
        }

        private static float EaseInOutSine(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInOutSine(currentTime / duration);
        }

        private static float EaseInQuad(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInQuad(currentTime / duration);
        }

        private static float EaseOutQuad(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseOutQuad(currentTime / duration);
        }

        private static float EaseInOutQuad(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInOutQuad(currentTime / duration);
        }

        private static float EaseInCubic(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInCubic(currentTime / duration);
        }

        private static float EaseOutCubic(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseOutCubic(currentTime / duration);
        }

        private static float EaseInOutCubic(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInOutCubic(currentTime / duration);
        }

        private static float EaseInQuart(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInQuart(currentTime / duration);
        }

        private static float EaseOutQuart(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseOutQuart(currentTime / duration);
        }

        private static float EaseInOutQuart(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInOutQuart(currentTime / duration);
        }

        private static float EaseInQuint(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInQuint(currentTime / duration);
        }

        private static float EaseOutQuint(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseOutQuint(currentTime / duration);
        }

        private static float EaseInOutQuint(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInOutQuint(currentTime / duration);
        }

        private static float EaseInExpo(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInExpo(currentTime / duration);
        }

        private static float EaseOutExpo(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseOutExpo(currentTime / duration);
        }

        private static float EaseInOutExpo(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInOutExpo(currentTime / duration);
        }

        private static float EaseInCirc(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInCirc(currentTime / duration);
        }

        private static float EaseOutCirc(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseOutCirc(currentTime / duration);
        }

        private static float EaseInOutCirc(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInOutCirc(currentTime / duration);
        }

        private static float EaseInBack(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInBack(currentTime / duration);
        }

        private static float EaseOutBack(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseOutBack(currentTime / duration);
        }

        private static float EaseInOutBack(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInOutBack(currentTime / duration);
        }

        private static float EaseInElastic(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            var x = currentTime / duration;
            var c4 = (float) (amplitude * Math.PI / 3f);
            return -Mathf.Pow(2, 10 * x - 10) * Mathf.Sin((float) ((x * 10 - 10.75) * c4));
        }

        private static float EaseOutElastic(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            var x = currentTime / duration;
            var c4 = (float) (amplitude * Math.PI / 3);
            return Mathf.Pow(2, -10 * x) * Mathf.Sin((float) ((x * 10 - 0.75) * c4)) + 1;
        }

        private static float EaseInOutElastic(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            var x = currentTime / duration;
            var c5 = (float) (amplitude * Math.PI / 4.5f);
            return x < 0.5
                ? -(Mathf.Pow(2, 20 * x - 10) * Mathf.Sin((float) ((20 * x - 11.125) * c5))) * 0.5f
                : Mathf.Pow(2, -20 * x + 10) * Mathf.Sin((float) ((20 * x - 11.125) * c5)) * 0.5f + 1;
        }

        private static float EaseInBounce(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInBounce(currentTime / duration);
        }

        private static float EaseOutBounce(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseOutBounce(currentTime / duration);
        }

        private static float EaseInOutBounce(float currentTime, float duration, float amplitude, AnimationCurve animationCurve)
        {
            return PGTweenEaseShared.EaseInOutBounce(currentTime / duration);
        }

        private static float EaseOutBounceOriginal(float x)
        {
            return PGTweenEaseShared.EaseOutBounceOriginal(x);
        }
    }
}