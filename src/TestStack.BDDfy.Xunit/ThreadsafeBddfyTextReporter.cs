using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TestStack.BDDfy.Configuration;

namespace TestStack.BDDfy.Xunit
{
	// Modified from https://github.com/TestStack/TestStack.BDDfy/blob/master/src/TestStack.BDDfy/Reporters/TextReporter.cs
	public class ThreadSafeBddfyTextReporter : IProcessor
	{
		private readonly AsyncLocal<List<Exception>> _exceptions = new AsyncLocal<List<Exception>>();
		private readonly AsyncLocal<int> _longestStepSentence = new AsyncLocal<int>();
		protected readonly AsyncLocal<StringBuilder> Text = new AsyncLocal<StringBuilder>();

		public virtual void Process(Story story)
		{
			if (_exceptions.Value == null)
			{
				_exceptions.Value = new List<Exception>();
			}

			if (Text.Value == null)
			{
				Text.Value = new StringBuilder();
			}

			ReportStoryHeader(story);

			var allSteps = story.Scenarios.SelectMany(s => s.Steps)
				.Select(GetStepWithLines)
				.ToList();
			if (allSteps.Any())
			{
				_longestStepSentence.Value = allSteps.SelectMany(s => s.Item2.Select(l => l.Length)).Max();
			}

			foreach (var scenarioGroup in story.Scenarios.GroupBy(s => s.Id))
			{
				if (scenarioGroup.Count() > 1)
				{
					// all scenarios in an example based scenario share the same header and narrative
					var exampleScenario = scenarioGroup.First();
					Report(exampleScenario);

					if (exampleScenario.Steps.Any())
					{
						foreach (var step in exampleScenario.Steps.Where(s => s.ShouldReport))
						{
							ReportOnStep(exampleScenario, GetStepWithLines(step), false);
						}
					}

					WriteLine();
					WriteExamples(exampleScenario, scenarioGroup);
					ReportTags(exampleScenario.Tags);
				}
				else
				{
					foreach (var scenario in scenarioGroup)
					{
						Report(scenario);

						if (scenario.Steps.Any())
						{
							foreach (var step in scenario.Steps.Where(s => s.ShouldReport))
							{
								ReportOnStep(scenario, GetStepWithLines(step), true);
							}
						}
					}

					var exampleScenario = scenarioGroup.First();
					WriteLine();
					WriteExamples(exampleScenario, scenarioGroup);
					ReportTags(exampleScenario.Tags);
				}
			}

			ReportExceptions();
		}

		public ProcessType ProcessType => ProcessType.Report;

		private static Tuple<Step, string[]> GetStepWithLines(Step s)
		{
			return Tuple.Create(s,
			                    s.Title.Replace("\r\n", "\n").Split('\n')
				                    .Select(l => PrefixWithSpaceIfRequired(l, s.ExecutionOrder)).ToArray());
		}

		private void ReportTags(IReadOnlyCollection<string> tags)
		{
			if (!tags.Any())
			{
				return;
			}

			WriteLine();
			WriteLine("Tags: {0}", string.Join(", ", tags));
		}

		private void WriteExamples(Scenario exampleScenario, IEnumerable<Scenario> scenarioGroup)
		{
			WriteLine("Examples: ");
			var scenarios = scenarioGroup.ToArray();
			var allPassed = scenarios.All(s => s.Result == Result.Passed);
			var exampleColumns = exampleScenario.Example.Headers.Length;
			var numberColumns = allPassed ? exampleColumns : exampleColumns + 2;
			var maxWidth = new int[numberColumns];
			var rows = new List<string[]>();

			void AddRow(IEnumerable<string> cells, string result, string error)
			{
				var row = new string[numberColumns];
				var index = 0;

				foreach (var cellText in cells)
				{
					row[index++] = cellText;
				}

				if (!allPassed)
				{
					row[numberColumns - 2] = result;
					row[numberColumns - 1] = error;
				}

				for (var i = 0; i < numberColumns; i++)
				{
					var rowValue = row[i];
					if (rowValue != null && rowValue.Length > maxWidth[i])
					{
						maxWidth[i] = rowValue.Length;
					}
				}

				rows.Add(row);
			}

			AddRow(exampleScenario.Example.Headers, "Result", "Errors");
			foreach (var scenario in scenarios)
			{
				var failingStep = scenario.Steps.FirstOrDefault(s => s.Result == Result.Failed);
				var error = failingStep == null
					? null
					: $"Step: {failingStep.Title} failed with exception: {CreateExceptionMessage(failingStep)}";

				AddRow(scenario.Example.Values.Select(e => e.GetValueAsString()), scenario.Result.ToString(), error);
			}

			foreach (var row in rows)
			{
				WriteExampleRow(row, maxWidth);
			}
		}

		private void WriteExampleRow(string[] row, int[] maxWidth)
		{
			for (var index = 0; index < row.Length; index++)
			{
				var col = row[index];
				Write("| {0} ", (col ?? string.Empty).Trim().PadRight(maxWidth[index]));
			}

			WriteLine("|");
		}

		private void ReportStoryHeader(Story story)
		{
			if (story.Metadata?.Type == null)
			{
				return;
			}

			WriteLine(story.Metadata.TitlePrefix + story.Metadata.Title);
			if (!string.IsNullOrEmpty(story.Metadata.Narrative1))
			{
				WriteLine("\t" + story.Metadata.Narrative1);
			}

			if (!string.IsNullOrEmpty(story.Metadata.Narrative2))
			{
				WriteLine("\t" + story.Metadata.Narrative2);
			}

			if (!string.IsNullOrEmpty(story.Metadata.Narrative3))
			{
				WriteLine("\t" + story.Metadata.Narrative3);
			}
		}

		private static string PrefixWithSpaceIfRequired(string stepTitle, ExecutionOrder executionOrder)
		{
			if (executionOrder == ExecutionOrder.ConsecutiveAssertion ||
			    executionOrder == ExecutionOrder.ConsecutiveSetupState ||
			    executionOrder == ExecutionOrder.ConsecutiveTransition)
			{
				stepTitle = "  " + stepTitle; // add two spaces in the front for indentation.
			}

			return stepTitle;
		}

		private void ReportOnStep(Scenario scenario, Tuple<Step, string[]> stepAndLines, bool includeResults)
		{
			if (!includeResults)
			{
				foreach (var line in stepAndLines.Item2)
				{
					WriteLine("\t{0}", line);
				}

				return;
			}

			var step = stepAndLines.Item1;
			var humanizedResult = Configurator.Scanners.Humanize(step.Result.ToString());

			string message;
			if (scenario.Result == Result.Passed)
			{
				message = $"\t{stepAndLines.Item2[0]}";
			}
			else
			{
				var paddedFirstLine = stepAndLines.Item2[0].PadRight(_longestStepSentence.Value + 5);
				message = $"\t{paddedFirstLine}  [{humanizedResult}] ";
			}

			if (stepAndLines.Item2.Length > 1)
			{
				message = $"{message}\r\n{string.Join("\r\n", stepAndLines.Item2.Skip(1))}";
			}

			if (step.Exception != null)
			{
				message += CreateExceptionMessage(step);
			}

			WriteLine(message);
		}

		private string CreateExceptionMessage(Step step)
		{
			_exceptions.Value.Add(step.Exception);

			var exceptionReference = $"[Details at {_exceptions.Value.Count} below]";
			if (!string.IsNullOrEmpty(step.Exception.Message))
			{
				return $"[{FlattenExceptionMessage(step.Exception.Message)}] {exceptionReference}";
			}

			return $"{exceptionReference}";
		}

		private void ReportExceptions()
		{
			WriteLine();
			if (_exceptions.Value.Count == 0)
			{
				return;
			}

			Write("Exceptions:");

			for (var index = 0; index < _exceptions.Value.Count; index++)
			{
				var exception = _exceptions.Value[index];
				WriteLine();
				Write($"  {index + 1}. ");

				if (!string.IsNullOrEmpty(exception.Message))
				{
					WriteLine(FlattenExceptionMessage(exception.Message));
				}
				else
				{
					WriteLine();
				}

				WriteLine(exception.StackTrace);
			}

			WriteLine();
		}

		private static string FlattenExceptionMessage(string message)
		{
			return string.Join(" ", message
				                   .Replace("\t", " ") // replace tab with one space
				                   .Split(new[] {"\r\n", "\n"}, StringSplitOptions.None)
				                   .Select(s => s.Trim()))
				.TrimEnd(','); // chop any , from the end
		}

		private void Report(Scenario scenario)
		{
			WriteLine();
			WriteLine("Scenario: " + scenario.Title);
		}

		public override string ToString()
		{
			return Text.ToString();
		}

		protected virtual void WriteLine(string text = null)
		{
			Text.Value.AppendLine(text);
		}

		protected virtual void WriteLine(string text, params object[] args)
		{
			Text.Value.AppendLine(string.Format(text, args));
		}

		protected virtual void Write(string text, params object[] args)
		{
			Text.Value.AppendFormat(text, args);
		}
	}
}