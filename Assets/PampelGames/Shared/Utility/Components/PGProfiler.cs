// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;

namespace PampelGames.Shared.Utility
{
    public class PGProfiler : MonoBehaviour
    {
        public float size = 30;
        public Color color = Color.black;
        public float interval = 0.5f;


        private readonly Rect fpsRect = new(10, 10, 300, 100);
        private GUIStyle fpsStyle;

        private float elapsedTime;
        private int frameCount;
        private float avgFps;
        private float msPerFrame;


        private void Update()
        {
            frameCount++;
            elapsedTime += Time.deltaTime;

            if (elapsedTime >= interval)
            {
                avgFps = frameCount / elapsedTime;
                msPerFrame = elapsedTime * 1000 / frameCount;
                frameCount = 0;
                elapsedTime = 0;
            }
        }

        private void OnGUI()
        {
            fpsStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = (int) size,
                fontStyle = FontStyle.Bold,
                normal = {textColor = color}
            };

            var text = $"FPS: {Mathf.Round(avgFps)} | {msPerFrame:F1}ms";
            GUI.Label(fpsRect, text, fpsStyle);
        }
    }
}