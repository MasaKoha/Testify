#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniTestify
{
    /// <summary>同期・非同期の双方で同じ引数検証と省略値を使います。</summary>
    internal sealed class AiCommandContext
    {
        private readonly Dictionary<string, string> _members;
        /// <summary>省略値を補った共通引数です。</summary>
        internal AiCommandArguments Arguments { get; }
        /// <summary>要求された操作名です。</summary>
        internal string Operation { get; }

        /// <summary>実行前に JSON と操作固有の引数を検証します。</summary>
        internal AiCommandContext(AiCommandRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Operation = request.op ?? string.Empty;
            var json = string.IsNullOrWhiteSpace(request.args) ? "{}" : request.args;
            _members = AiJsonObject.Parse(json);
            Arguments = new AiCommandArguments();
            JsonUtility.FromJsonOverwrite(json, Arguments);
            AiCommandArguments.ValidateDuration(Arguments.readyTimeoutSeconds, nameof(Arguments.readyTimeoutSeconds), false);
            if (Operation == "capture" || Operation == "agent.observe")
            {
                AiPlayModeViewFocus.Validate(Arguments.view);
            }

            if (Operation == "agent.observe" || Operation == "agent.find")
            {
                UiObservationScope.Validate(Arguments.scope);
            }
        }

        /// <summary>入れ子の JSON オブジェクトを必須条件とともに検証します。</summary>
        internal string GetObject(string name, bool required = false)
        {
            if (!_members.TryGetValue(name, out var json))
            {
                if (required)
                {
                    throw new ArgumentException($"{name} が必要です。");
                }

                return string.Empty;
            }

            AiJsonObject.Parse(json);
            return json;
        }

        /// <summary>単一行動と一括行動を同じ実行単位へ揃えます。</summary>
        internal AgentAction[] GetActions()
        {
            var hasAction = _members.ContainsKey("action");
            var hasSteps = _members.TryGetValue("steps", out var stepsJson);
            if (hasAction == hasSteps)
            {
                throw new ArgumentException("action または steps のどちらか一方が必要です。");
            }

            if (hasAction)
            {
                var action = ReadAction(GetObject("action", true));
                action.expect = action.expect ?? Arguments.expect;
                return new[] { action };
            }

            var steps = AiJsonObject.ParseObjectArray(stepsJson);
            if (steps.Count == 0)
            {
                throw new ArgumentException("steps には 1 件以上の行動オブジェクトが必要です。");
            }

            var actions = new AgentAction[steps.Count];
            for (var actionIndex = 0; actionIndex < steps.Count; actionIndex++)
            {
                actions[actionIndex] = ReadAction(steps[actionIndex]);
            }

            return actions;
        }

        private static AgentAction ReadAction(string json)
        {
            var action = new AgentAction();
            JsonUtility.FromJsonOverwrite(json, action);
            AiCommandArguments.ValidateDuration(action.timeoutSeconds, nameof(action.timeoutSeconds), true);
            return action;
        }
    }
}
#endif
