using System;
using UnityEditor;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>Editor 時のみ撮影対象ウィンドウを選択し、Runtime から Editor の型依存を切り離します。</summary>
    internal static class PlayModeViewFocus
    {
        private const string GameViewTypeName = "UnityEditor.GameView";
        private const string SimulatorWindowTypeName = "UnityEditor.DeviceSimulation.SimulatorWindow";

        [InitializeOnLoadMethod]
        private static void Register()
        {
            AiPlayModeViewFocus.FocusHandler = TryFocus;
        }

        /// <summary>撮影要求と Editor 操作メールボックスで同じウィンドウ選択を共用します。</summary>
        internal static bool TryFocus(string view)
        {
            if (Application.isBatchMode)
            {
                return false;
            }

            var typeName = view == "game" ? GameViewTypeName : SimulatorWindowTypeName;
            var windowType = FindWindowType(typeName);
            if (windowType == null)
            {
                return false;
            }

            try
            {
                var window = EditorWindow.GetWindow(windowType);
                if (window == null)
                {
                    return false;
                }

                window.Focus();
                return true;
            }
            catch (Exception)
            {
                // ウィンドウを生成できない Editor 環境でも、従来の撮影経路を使えるようにする。
                return false;
            }
        }

        private static Type FindWindowType(string typeName)
        {
            // Device Simulator の収録アセンブリは Unity のバージョンで異なるため固定しない。
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var windowType = assembly.GetType(typeName);
                if (windowType != null)
                {
                    return windowType;
                }
            }

            return null;
        }
    }
}
