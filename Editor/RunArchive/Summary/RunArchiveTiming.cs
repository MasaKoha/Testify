using System;
using System.Globalization;
using System.IO;

namespace UniTestify.Editor
{
    /// <summary>シナリオ時刻・所要時間・更新時刻の優先順位からランの時間範囲を解決します。</summary>
    internal static class RunArchiveTiming
    {
        private const string TimestampFormat = "yyyyMMdd-HHmmss";
        private const string TimestampWithMillisecondsFormat = "yyyyMMdd-HHmmss-fff";

        /// <summary>結果の終了・開始・更新時刻の順に基準時刻を解決します。</summary>
        internal static DateTimeOffset ResolveAnchorTime(string scenarioResultPath, RunArchiveScenarioResult scenarioResult)
        {
            if (TryParseDateTimeOffset(scenarioResult == null ? string.Empty : scenarioResult.finishedAt, out var finishedAt))
            {
                return finishedAt;
            }

            if (TryParseDateTimeOffset(scenarioResult == null ? string.Empty : scenarioResult.startedAt, out var startedAt))
            {
                return startedAt;
            }

            if (!string.IsNullOrEmpty(scenarioResultPath) && File.Exists(scenarioResultPath))
            {
                return File.GetLastWriteTime(scenarioResultPath);
            }

            return DateTimeOffset.Now;
        }

        /// <summary>開始時刻がなければ所要時間から補完します。</summary>
        internal static DateTimeOffset ResolveStartedAt(DateTimeOffset anchorTime, RunArchiveScenarioResult scenarioResult)
        {
            if (TryParseDateTimeOffset(scenarioResult == null ? string.Empty : scenarioResult.startedAt, out var startedAt))
            {
                return startedAt;
            }

            if (scenarioResult != null && scenarioResult.durationSeconds > 0.0f)
            {
                return anchorTime.AddSeconds(-scenarioResult.durationSeconds);
            }

            return anchorTime;
        }

        /// <summary>終了時刻がなければ開始時刻と所要時間から補完します。</summary>
        internal static DateTimeOffset ResolveFinishedAt(DateTimeOffset anchorTime, RunArchiveScenarioResult scenarioResult, DateTimeOffset runStartedAt)
        {
            if (TryParseDateTimeOffset(scenarioResult == null ? string.Empty : scenarioResult.finishedAt, out var finishedAt))
            {
                return finishedAt;
            }

            if (scenarioResult != null && scenarioResult.durationSeconds > 0.0f)
            {
                return runStartedAt.AddSeconds(scenarioResult.durationSeconds);
            }

            return anchorTime;
        }

        private static bool TryParseDateTimeOffset(string text, out DateTimeOffset value)
        {
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value))
            {
                return true;
            }

            if (DateTimeOffset.TryParseExact(text, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value))
            {
                return true;
            }

            return DateTimeOffset.TryParseExact(text, TimestampWithMillisecondsFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value);
        }
    }
}
