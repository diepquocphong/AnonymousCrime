using System;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Serializable]
    public class SkillCooldown
    {
        private enum TextType : byte
        {
            Text,
            TMP
        }

        private static readonly decimal[] s_PredefinedPower = 
        {
            0.5m,
            0.05m,
            0.005m,
            0.0005m,
            0.00005m,
            0.000005m,
            0.0000005m,
            0.00000005m,
            0.000000005m,
            0.0000000005m
        };

        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] private bool m_IsManual;

        [SerializeField] private Image m_Fill;
        [SerializeField] private TextType m_TextType = TextType.TMP;
        [SerializeField] private Text m_TextLegacy;
        [SerializeField] private TMP_Text m_TextTMP;
        
        [SerializeField]
        private PropertyGetString m_Format = GetStringTactileSkillCooldownNoDecimal.Create;
        
        [SerializeField]
        private PropertyGetDecimal m_Duration = GetDecimalDecimal.Create(5f);

        [SerializeField]
        private TimeMode m_TimeMode = new();

        // MEMBERS: -------------------------------------------------------------------------------

        private Args m_Args;

        [NonSerialized] private float m_CurrentDuration;
        [NonSerialized] private char[] m_TextCharArray = new char[32];

        // PROPERTIES: ----------------------------------------------------------------------------

        public TimeMode TimeMode
        {
            get => this.m_TimeMode;
            set => this.m_TimeMode = value;
        }

        public bool IsManual
        {
            get => this.m_IsManual;
            set => this.m_IsManual = value;
        }

        public bool IsCooldown => this.Remaining != 0f;

        [field: NonSerialized] public float Ratio { get; private set; }
        [field: NonSerialized] public float Remaining { get; private set; }

        // EVENTS: ------------------------------------------------------------------------------

        public event Action EventStart;
        public event Action EventReset;

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void Setup(Args args)
        {
            this.m_Args = args;

            if (this.m_Fill != null)
            {
                this.m_Fill.type = Image.Type.Filled;
                this.m_Fill.fillAmount = 0f;
            }

            this.SetTextEmpty();
        }

        public void Start(bool force = false, bool invoke = true)
        {
            if (!force)
            {
                if (this.m_IsManual) return;
                if (this.IsCooldown) return;
            }

            float duration = (float) this.m_Duration.Get(this.m_Args);
            if (duration <= 0f) return;

            this.Ratio = 1f;
            this.Remaining = duration;
            this.m_CurrentDuration = duration;

            this.SetText(this.m_Format.Get(this.m_Args), duration);
            if (this.m_Fill) this.m_Fill.fillAmount = this.Ratio;
            if (invoke) this.EventStart?.Invoke();
        }

        public void Reset(bool invoke = true)
        {
            if (!this.IsCooldown) return;

            this.Ratio = 0f;
            this.Remaining = 0f;
            this.SetTextEmpty();
            if (this.m_Fill) this.m_Fill.fillAmount = 0;

            if (invoke) this.EventReset?.Invoke();
        }

        public void Update()
        {
            if (!this.IsCooldown) return;

            float duration = (float) this.m_Duration.Get(this.m_Args);
            if (this.m_CurrentDuration != duration)
                this.Remaining = duration * this.Ratio;

            this.Remaining -= this.m_TimeMode.DeltaTime;
            if (this.Remaining < 0f) this.Remaining = 0f;

            this.m_CurrentDuration = duration;

            this.Ratio = this.m_CurrentDuration > 0f
                ? this.Remaining / this.m_CurrentDuration
                : 0f;

            string format = this.m_Format.Get(this.m_Args);
            if (format.Length > this.m_TextCharArray.Length)
            {
                Array.Resize(ref this.m_TextCharArray, Mathf.NextPowerOfTwo(format.Length + 1));
            }

            this.SetText(this.m_Format.Get(this.m_Args), this.Remaining);

            if (this.m_Fill) this.m_Fill.fillAmount = this.Ratio;
            if (this.IsCooldown) return;

            this.SetTextEmpty();
            this.EventReset?.Invoke();
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void SetTextEmpty()
        {
            switch (this.m_TextType)
            {
                case TextType.Text:
                    if (this.m_TextLegacy != null)
                    {
                        this.m_TextLegacy.text = string.Empty;
                    }

                    break;

                case TextType.TMP:
                    if (this.m_TextTMP != null)
                    {
                        this.m_TextTMP.text = string.Empty;
                    }

                    break;
            }
        }

        private void SetText(string pattern, float arg)
        {
            int readFlag = 0;
            int readIndex = 0;
            int writeIndex = 0;

            int padding = 0;
            int Precision = 0;

            for (; readIndex < pattern.Length; readIndex++)
            {
                char c = pattern[readIndex];

                if (c == '{')
                {
                    readFlag = 1;
                    continue;
                }

                if (c == '}')
                {
                    this.AddFloatToCharArray(arg, padding, Precision, ref writeIndex);

                    readFlag = 0;
                    padding = 0;
                    Precision = 0;
                    continue;
                }

                if (readFlag == 1)
                {
                    if (c == '.')
                    {
                        readFlag = 2;
                        continue;
                    }

                    if (c == '0')
                    {
                        padding += 1;
                        continue;
                    }

                    if (c >= '1' && c <= '9')
                    {
                        Precision = c - 48;
                        continue;
                    }
                }

                if (readFlag == 2)
                {
                    if (c == '0')
                    {
                        Precision += 1;
                        continue;
                    }
                }

                if (readFlag != 0)
                    continue;

                this.m_TextCharArray[writeIndex] = c;
                writeIndex += 1;
            }

            switch (this.m_TextType)
            {
                case TextType.Text:
                    if (this.m_TextLegacy != null)
                    {
                        this.m_TextLegacy.text = new string(this.m_TextCharArray, 0, writeIndex);
                    }

                    break;

                case TextType.TMP:
                    if (this.m_TextTMP != null)
                    {
                        this.m_TextTMP.SetCharArray(this.m_TextCharArray, 0, writeIndex);
                    }

                    break;
            }
        }

        private void AddFloatToCharArray(float value, int padding, int precision, ref int writeIndex)
        {
            if (value < 0)
            {
                this.m_TextCharArray[writeIndex] = '-';
                writeIndex += 1;
                value = -value;
            }

            decimal valueD = (decimal)value;

            if (padding == 0 && precision == 0)
                precision = 9;
            else
                valueD += s_PredefinedPower[Mathf.Min(9, precision)];

            long integer = (long)valueD;
            this.AddIntegerToCharArray(integer, padding, ref writeIndex);

            if (precision <= 0) return;
            
            valueD -= integer;
            if (valueD == 0) return;

            this.m_TextCharArray[writeIndex++] = '.';
            for (int p = 0; p < precision; p++)
            {
                valueD *= 10;
                long d = (long)valueD;

                this.m_TextCharArray[writeIndex++] = (char)(d + 48);
                valueD -= d;

                if (valueD == 0)
                    p = precision;
            }
        }

        private void AddIntegerToCharArray(double number, int padding, ref int writeIndex)
        {
            int integralCount = 0;
            int i = writeIndex;

            do
            {
                this.m_TextCharArray[i++] = (char)(number % 10 + 48);
                number /= 10;
                integralCount += 1;
            } while (number > 0.999999999999999d || integralCount < padding);

            int lastIndex = i;
            while (writeIndex + 1 < i)
            {
                i -= 1;
                char t = this.m_TextCharArray[writeIndex];
                this.m_TextCharArray[writeIndex] = this.m_TextCharArray[i];
                this.m_TextCharArray[i] = t;
                writeIndex += 1;
            }

            writeIndex = lastIndex;
        }
        
    }
}