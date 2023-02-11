// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation.Internal;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Subsystem.Prediction;
using System.Threading;

namespace System.Management.Automation.Subsystem.Feedback
{
    /// <summary>
    /// The class represents a feedback item generated by the feedback provider.
    /// </summary>
    public sealed class FeedbackItem
    {
        /// <summary>
        /// Layout for displaying the recommended actions.
        /// </summary>
        public enum DisplayLayout
        {
            /// <summary>
            /// Display each recommended action in a row.
            /// </summary>
            Portrait,

            /// <summary>
            /// Display all recommended actions in the same row.
            /// </summary>
            Landscape,
        }

        /// <summary>
        /// Gets the description message about this feedback.
        /// </summary>
        public string Header { get; }

        /// <summary>
        /// Gets the footer message about this feedback.
        /// </summary>
        public string? Footer { get; }

        /// <summary>
        /// Gets the recommended actions -- command lines or even code snippets to run.
        /// </summary>
        public List<string>? RecommendedActions { get; }

        /// <summary>
        /// Gets the layout to use for displaying the recommended actions.
        /// </summary>
        public DisplayLayout Layout { get; }

        /// <summary>
        /// Gets or sets the next feedback item, if there is one.
        /// </summary>
        public FeedbackItem? Next { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedbackItem"/> class.
        /// </summary>
        /// <param name="header">The description message (must be not null or empty).</param>
        /// <param name="actions">The recommended actions to take (optional).</param>
        public FeedbackItem(string header, List<string>? actions)
            : this(header, actions, footer: null, DisplayLayout.Portrait)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedbackItem"/> class.
        /// </summary>
        /// <param name="header">The description message (must be not null or empty).</param>
        /// <param name="actions">The recommended actions to take (optional).</param>
        /// <param name="layout">The layout for displaying the actions.</param>
        public FeedbackItem(string header, List<string>? actions, DisplayLayout layout)
            : this(header, actions, footer: null, layout)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedbackItem"/> class.
        /// </summary>
        /// <param name="header">The description message (must be not null or empty).</param>
        /// <param name="actions">The recommended actions to take (optional).</param>
        /// <param name="footer">The footer message (optional).</param>
        /// <param name="layout">The layout for displaying the actions.</param>
        public FeedbackItem(string header, List<string>? actions, string? footer, DisplayLayout layout)
        {
            Requires.NotNullOrEmpty(header, nameof(header));

            Header = header;
            RecommendedActions = actions;
            Footer = footer;
            Layout = layout;
        }
    }

    /// <summary>
    /// Interface for implementing a feedback provider on command failures.
    /// </summary>
    public interface IFeedbackProvider : ISubsystem
    {
        /// <summary>
        /// Default implementation. No function is required for a feedback provider.
        /// </summary>
        Dictionary<string, string>? ISubsystem.FunctionsToDefine => null;

        /// <summary>
        /// Gets feedback based on the given commandline and error record.
        /// </summary>
        /// <param name="commandLine">The command line that was just executed.</param>
        /// <param name="lastError">The error that was triggerd by the command line.</param>
        /// <param name="token">The cancellation token to cancel the operation.</param>
        /// <returns>The feedback item.</returns>
        FeedbackItem? GetFeedback(string commandLine, ErrorRecord lastError, CancellationToken token);
    }

    internal sealed class GeneralCommandErrorFeedback : IFeedbackProvider
    {
        private readonly Guid _guid;

        internal GeneralCommandErrorFeedback()
        {
            _guid = new Guid("A3C6B07E-4A89-40C9-8BE6-2A9AAD2786A4");
        }

        public Guid Id => _guid;

        public string Name => "general";

        public string Description => "The built-in general feedback source for command errors.";

        public FeedbackItem? GetFeedback(string commandLine, ErrorRecord lastError, CancellationToken token)
        {
            var rsToUse = Runspace.DefaultRunspace;
            if (rsToUse is null)
            {
                return null;
            }

            if (lastError.FullyQualifiedErrorId == "CommandNotFoundException")
            {
                EngineIntrinsics context = rsToUse.ExecutionContext.EngineIntrinsics;

                var target = (string)lastError.TargetObject;
                CommandInvocationIntrinsics invocation = context.SessionState.InvokeCommand;

                // See if target is actually an executable file in current directory.
                var localTarget = Path.Combine(".", target);
                var command = invocation.GetCommand(
                    localTarget,
                    CommandTypes.Application | CommandTypes.ExternalScript);

                if (command is not null)
                {
                    return new FeedbackItem(
                        StringUtil.Format(SuggestionStrings.Suggestion_CommandExistsInCurrentDirectory, target),
                        new List<string> { localTarget });
                }

                // Check fuzzy matching command names.
                if (ExperimentalFeature.IsEnabled("PSCommandNotFoundSuggestion"))
                {
                    var pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace);
                    var results = pwsh.AddCommand("Get-Command")
                            .AddParameter("UseFuzzyMatching")
                            .AddParameter("FuzzyMinimumDistance", 1)
                            .AddParameter("Name", target)
                        .AddCommand("Select-Object")
                            .AddParameter("First", 5)
                            .AddParameter("Unique")
                            .AddParameter("ExpandProperty", "Name")
                        .Invoke<string>();

                    if (results.Count > 0)
                    {
                        return new FeedbackItem(
                            SuggestionStrings.Suggestion_CommandNotFound,
                            new List<string>(results),
                            FeedbackItem.DisplayLayout.Landscape);
                    }
                }
            }

            return null;
        }
    }

    internal sealed class UnixCommandNotFound : IFeedbackProvider, ICommandPredictor
    {
        private readonly Guid _guid;
        private List<string>? _candidates;

        internal UnixCommandNotFound()
        {
            _guid = new Guid("47013747-CB9D-4EBC-9F02-F32B8AB19D48");
        }

        Dictionary<string, string>? ISubsystem.FunctionsToDefine => null;

        public Guid Id => _guid;

        public string Name => "cmd-not-found";

        public string Description => "The built-in feedback/prediction source for the Unix command utility.";

        #region IFeedbackProvider

        private static string? GetUtilityPath()
        {
            string cmd_not_found = "/usr/lib/command-not-found";
            bool exist = IsFileExecutable(cmd_not_found);

            if (!exist)
            {
                cmd_not_found = "/usr/share/command-not-found/command-not-found";
                exist = IsFileExecutable(cmd_not_found);
            }

            return exist ? cmd_not_found : null;

            static bool IsFileExecutable(string path)
            {
                var file = new FileInfo(path);
                return file.Exists && file.UnixFileMode.HasFlag(UnixFileMode.OtherExecute);
            }
        }

        public FeedbackItem? GetFeedback(string commandLine, ErrorRecord lastError, CancellationToken token)
        {
            if (Platform.IsWindows || lastError.FullyQualifiedErrorId != "CommandNotFoundException")
            {
                return null;
            }

            var target = (string)lastError.TargetObject;
            if (target is null)
            {
                return null;
            }

            if (target.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string? cmd_not_found = GetUtilityPath();
            if (cmd_not_found is not null)
            {
                var startInfo = new ProcessStartInfo(cmd_not_found);
                startInfo.ArgumentList.Add(target);
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;

                using var process = Process.Start(startInfo);
                if (process is not null)
                {
                    string? header = null;
                    List<string>? actions = null;

                    while (true)
                    {
                        string? line = process.StandardError.ReadLine();
                        if (line is null)
                        {
                            break;
                        }

                        if (line == string.Empty)
                        {
                            continue;
                        }

                        if (line.StartsWith("sudo ", StringComparison.Ordinal))
                        {
                            actions ??= new List<string>();
                            actions.Add(line.TrimEnd());
                        }
                        else if (actions is null)
                        {
                            header = line;
                        }
                    }

                    if (actions is not null && header is not null)
                    {
                        _candidates = actions;

                        var footer = process.StandardOutput.ReadToEnd().Trim();
                        return string.IsNullOrEmpty(footer)
                            ? new FeedbackItem(header, actions)
                            : new FeedbackItem(header, actions, footer, FeedbackItem.DisplayLayout.Portrait);
                    }
                }
            }

            return null;
        }

        #endregion

        #region ICommandPredictor

        public bool CanAcceptFeedback(PredictionClient client, PredictorFeedbackKind feedback)
        {
            return feedback switch
            {
                PredictorFeedbackKind.CommandLineAccepted => true,
                _ => false,
            };
        }

        public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
        {
            if (_candidates is not null)
            {
                string input = context.InputAst.Extent.Text;
                List<PredictiveSuggestion>? result = null;

                foreach (string c in _candidates)
                {
                    if (c.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                    {
                        result ??= new List<PredictiveSuggestion>(_candidates.Count);
                        result.Add(new PredictiveSuggestion(c));
                    }
                }

                if (result is not null)
                {
                    return new SuggestionPackage(result);
                }
            }

            return default;
        }

        public void OnCommandLineAccepted(PredictionClient client, IReadOnlyList<string> history)
        {
            // Reset the candidate state.
            _candidates = null;
        }

        public void OnSuggestionDisplayed(PredictionClient client, uint session, int countOrIndex) { }

        public void OnSuggestionAccepted(PredictionClient client, uint session, string acceptedSuggestion) { }

        public void OnCommandLineExecuted(PredictionClient client, string commandLine, bool success) { }

        #endregion;
    }
}
