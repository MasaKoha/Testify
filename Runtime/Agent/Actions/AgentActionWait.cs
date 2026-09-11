#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace UniTestify
{
    /// <summary>対話操作の前提条件をシナリオと同じアンカー判定で待ちます。</summary>
    internal sealed class AgentActionWait
    {
        /// <summary>同期経路で未成立のアンカーを読み飛ばさないための案内です。</summary>
        internal const string SynchronousWaitRequiredMessage = "待機条件が未成立です。フレームをまたぐ待機にはメールボックスの agent.act を使ってください。";

        private const int MillisecondsPerSecond = 1000;
        private readonly InputReplayAnchor _anchor;
        private readonly float _timeoutSeconds;

        /// <summary>待機だけの要求も入力要求と同じ上限で扱います。</summary>
        internal AgentActionWait(AgentAction action)
        {
            AiCommandArguments.ValidateDuration(action.timeoutSeconds, nameof(action.timeoutSeconds), true);
            _anchor = CreateAnchor(action);
            _timeoutSeconds = action.timeoutSeconds;
        }

        /// <summary>アンカーが成立した場合だけ行動の送出を許可します。</summary>
        internal bool IsSatisfied { get; private set; }

        /// <summary>対象の自動準備待ちと合算して応答へ残す実時間です。</summary>
        internal int WaitedMilliseconds { get; private set; }

        /// <summary>timeScale に依存せず待ち、タイムアウト時は未成立のまま終了します。</summary>
        internal IEnumerator<object> WaitAsync()
        {
            var startedAt = Time.realtimeSinceStartupAsDouble;
            // perf: アンカーは要求ごとに一度作り、反復中は既存の判定だけを呼ぶ。
            while (!(IsSatisfied = UiInputLocator.IsAnchorSatisfied(_anchor)))
            {
                if (Time.realtimeSinceStartupAsDouble - startedAt > _timeoutSeconds)
                {
                    break;
                }

                yield return null;
            }

            WaitedMilliseconds = (int)((Time.realtimeSinceStartupAsDouble - startedAt) * MillisecondsPerSecond);
        }

        /// <summary>シナリオ側のアンカー生成を共用し、waitFor の解釈を揃えます。</summary>
        internal static InputReplayAnchor CreateAnchor(AgentAction action)
        {
            return UiScenarioStepReader.CreateAnchor(new UiScenarioStep
            {
                waitForText = action.waitForText,
                waitForObject = action.waitForObject,
                waitForFocus = action.waitForFocus,
                waitForScene = action.waitForScene,
            });
        }

        /// <summary>行動を伴わない待機と、空の要求を区別します。</summary>
        internal static bool HasConditions(AgentAction action)
        {
            return action != null && (!string.IsNullOrEmpty(action.waitForText)
                || !string.IsNullOrEmpty(action.waitForObject)
                || !string.IsNullOrEmpty(action.waitForFocus)
                || !string.IsNullOrEmpty(action.waitForScene));
        }
    }
}
#endif
