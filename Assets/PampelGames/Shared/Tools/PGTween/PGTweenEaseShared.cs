// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using Unity.Mathematics;

namespace PampelGames.Shared.Tools
{
    public static class PGTweenEaseShared
    {
        public enum Ease
        {
            Linear,
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
            InBounce,
            OutBounce,
            InOutBounce
        }

        public static float GetEase(Ease easeType, float x)
        {
            return easeType switch
            {
                Ease.Linear => EaseLinear(x),
                Ease.InSine => EaseInSine(x),
                Ease.OutSine => EaseOutSine(x),
                Ease.InOutSine => EaseInOutSine(x),
                Ease.InQuad => EaseInQuad(x),
                Ease.OutQuad => EaseOutQuad(x),
                Ease.InOutQuad => EaseInOutQuad(x),
                Ease.InCubic => EaseInCubic(x),
                Ease.OutCubic => EaseOutCubic(x),
                Ease.InOutCubic => EaseInOutCubic(x),
                Ease.InQuart => EaseInQuart(x),
                Ease.OutQuart => EaseOutQuart(x),
                Ease.InOutQuart => EaseInOutQuart(x),
                Ease.InQuint => EaseInQuint(x),
                Ease.OutQuint => EaseOutQuint(x),
                Ease.InOutQuint => EaseInOutQuint(x),
                Ease.InExpo => EaseInExpo(x),
                Ease.OutExpo => EaseOutExpo(x),
                Ease.InOutExpo => EaseInOutExpo(x),
                Ease.InCirc => EaseInCirc(x),
                Ease.OutCirc => EaseOutCirc(x),
                Ease.InOutCirc => EaseInOutCirc(x),
                Ease.InBack => EaseInBack(x),
                Ease.OutBack => EaseOutBack(x),
                Ease.InOutBack => EaseInOutBack(x),
                Ease.InBounce => EaseInBounce(x),
                Ease.OutBounce => EaseOutBounce(x),
                Ease.InOutBounce => EaseInOutBounce(x),
                _ => x
            };
        }

        public static float EaseLinear(float x)
        {
            return x;
        }

        public static float EaseInSine(float x)
        {
            return 1 - math.cos(x * math.PI * 0.5f);
        }

        public static float EaseOutSine(float x)
        {
            return math.sin(x * math.PI * 0.5f);
        }

        public static float EaseInOutSine(float x)
        {
            return -(math.cos(math.PI * x) - 1) * 0.5f;
        }

        public static float EaseInQuad(float x)
        {
            return x * x;
        }

        public static float EaseOutQuad(float x)
        {
            return 1 - (1 - x) * (1 - x);
        }

        public static float EaseInOutQuad(float x)
        {
            return x < 0.5f ? 2 * x * x : 1 - math.pow(-2 * x + 2, 2) * 0.5f;
        }

        public static float EaseInCubic(float x)
        {
            return x * x * x;
        }

        public static float EaseOutCubic(float x)
        {
            return 1 - math.pow(1 - x, 3);
        }

        public static float EaseInOutCubic(float x)
        {
            return x < 0.5 ? 4 * x * x * x : 1 - math.pow(-2 * x + 2, 3) * 0.5f;
        }

        public static float EaseInQuart(float x)
        {
            return x * x * x * x;
        }

        public static float EaseOutQuart(float x)
        {
            return 1 - math.pow(1 - x, 4);
        }

        public static float EaseInOutQuart(float x)
        {
            return x < 0.5 ? 8 * x * x * x * x : 1 - math.pow(-2 * x + 2, 4) * 0.5f;
        }

        public static float EaseInQuint(float x)
        {
            return x * x * x * x * x;
        }

        public static float EaseOutQuint(float x)
        {
            return 1 - math.pow(1 - x, 5);
        }

        public static float EaseInOutQuint(float x)
        {
            return x < 0.5 ? 16 * x * x * x * x * x : 1 - math.pow(-2 * x + 2, 5) * 0.5f;
        }

        public static float EaseInExpo(float x)
        {
            return x == 0 ? 0 : math.pow(2, 10 * x - 10);
        }

        public static float EaseOutExpo(float x)
        {
            return x == 1 ? 1 : 1 - math.pow(2, -10 * x);
        }

        public static float EaseInOutExpo(float x)
        {
            return x == 0 ? 0 : x == 1 ? 1 : x < 0.5f ? math.pow(2, 20 * x - 10) * 0.5f : (2 - math.pow(2, -20 * x + 10)) * 0.5f;
        }

        public static float EaseInCirc(float x)
        {
            return 1 - math.sqrt(1 - math.pow(x, 2));
        }

        public static float EaseOutCirc(float x)
        {
            return math.sqrt(1 - math.pow(x - 1, 2));
        }

        public static float EaseInOutCirc(float x)
        {
            return x < 0.5
                ? (1 - math.sqrt(1 - math.pow(2 * x, 2))) * 0.5f
                : (math.sqrt(1 - math.pow(-2 * x + 2, 2)) + 1) * 0.5f;
        }

        public static float EaseInBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1;
            return c3 * x * x * x - c1 * x * x;
        }

        public static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1;
            return 1 + c3 * math.pow(x - 1, 3) + c1 * math.pow(x - 1, 2);
        }

        public static float EaseInOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c2 = c1 * 1.525f;

            return x < 0.5
                ? math.pow(2 * x, 2) * ((c2 + 1) * 2 * x - c2) * 0.5f
                : (math.pow(2 * x - 2, 2) * ((c2 + 1) * (x * 2 - 2) + c2) + 2) * 0.5f;
        }

        public static float EaseInBounce(float x)
        {
            return 1 - EaseOutBounceOriginal(1 - x);
        }

        public static float EaseOutBounce(float x)
        {
            return EaseOutBounceOriginal(x);
        }

        public static float EaseInOutBounce(float x)
        {
            return x < 0.5
                ? (1 - EaseOutBounceOriginal(1 - 2 * x)) * 0.5f
                : (1 + EaseOutBounceOriginal(2 * x - 1)) * 0.5f;
        }


        public static float EaseOutBounceOriginal(float x)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (x < 1 / d1) return n1 * x * x;
            if (x < 2 / d1) return n1 * (x -= 1.5f / d1) * x + 0.75f;
            if (x < 2.5 / d1) return n1 * (x -= 2.25f / d1) * x + 0.9375f;
            return n1 * (x -= 2.625f / d1) * x + 0.984375f;
        }
    }
}