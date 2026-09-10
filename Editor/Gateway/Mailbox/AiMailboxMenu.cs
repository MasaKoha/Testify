using UnityEditor;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>Play 中のメールボックスを Editor メニューから操作します。</summary>
    public static class AiMailboxMenu
    {
        private const string StartMenuPath = "UniTestify/Mailbox/Start";
        private const string StopMenuPath = "UniTestify/Mailbox/Stop";

        [MenuItem(StartMenuPath)]
        private static void StartMailbox()
        {
            AiMailboxServer.Start();
        }

        [MenuItem(StartMenuPath, true)]
        private static bool CanStart()
        {
            return Application.isPlaying && !AiMailboxServer.IsRunning;
        }

        [MenuItem(StopMenuPath)]
        private static void StopMailbox()
        {
            AiMailboxServer.Stop();
        }

        [MenuItem(StopMenuPath, true)]
        private static bool CanStop()
        {
            return AiMailboxServer.IsRunning;
        }
    }
}
