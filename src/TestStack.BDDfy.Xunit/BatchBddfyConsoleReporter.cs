using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TestStack.BDDfy.Configuration;

namespace TestStack.BDDfy.Xunit
{
	public class BatchBddfyConsoleReporter : FixedConsoleReporter, IBatchProcessor
	{
		public void Process(IEnumerable<Story> stories)
		{
			try
			{
				var metadataToScenarioDictionary = new Dictionary<StoryMetadata, List<Scenario>>();
				var uncorrelatedStories = new List<Story>();
				foreach (var story in stories)
				{
					if (story.Metadata == null)
					{
						uncorrelatedStories.Add(story);
					}
					else
					{
						var existingMetaData =
							metadataToScenarioDictionary.Keys.SingleOrDefault(x => x.IsEqualTo(story.Metadata));
						if (existingMetaData == null)
						{
							metadataToScenarioDictionary.Add(story.Metadata, story.Scenarios.ToList());
						}
						else
						{
							metadataToScenarioDictionary[existingMetaData].AddRange(story.Scenarios);
						}
					}
				}

				var correlatedStories = metadataToScenarioDictionary.OrderBy(x => x.Key.Type.FullName)
					.Select(x => new Story(x.Key, x.Value.OrderBy(s => s.Title).ToArray())).ToArray();

				foreach (var story in correlatedStories.Concat(uncorrelatedStories))
				{
					Process(story);
				}
			}
			catch (Exception exception)
			{
				Console.WriteLine(exception);
				throw;
			}
		}
	}

	public static class StoryMetadataExtensions
	{
		public static bool IsEqualTo(this StoryMetadata storyMetadata, StoryMetadata otherStoryMetadata)
		{
			if (otherStoryMetadata == null)
			{
				return false;
			}

			if (ReferenceEquals(storyMetadata, otherStoryMetadata))
			{
				return true;
			}

			if (otherStoryMetadata.GetType() != storyMetadata.GetType())
			{
				return false;
			}

			return storyMetadata.GetType().GetRuntimeProperties().All(x =>
			{
				var thisValue = x.GetValue(storyMetadata, null);
				var otherValue = x.GetValue(otherStoryMetadata, null);

				if (thisValue == null && otherValue == null)
				{
					return true;
				}

				if (ReferenceEquals(thisValue, otherValue))
				{
					return true;
				}

				return thisValue != null && thisValue.Equals(otherValue);
			});
		}
	}

	// Modified from https://github.com/TestStack/TestStack.BDDfy/blob/master/src/TestStack.BDDfy/Reporters/ConsoleReporter.cs
	public class FixedConsoleReporter : FixedTextReporter
	{
		public override ConsoleColor ForegroundColor
		{
			get => Console.ForegroundColor;
			set => Console.ForegroundColor = value;
		}

		protected override void Write(string text, params object[] args)
		{
			Console.Write(text, args);
		}

		protected override void WriteLine(string text = null)
		{
			Console.WriteLine(text);
		}

		protected override void WriteLine(string text, params object[] args)
		{
			Console.WriteLine(text, args);
		}
	}

	// Modified from https://github.com/TestStack/TestStack.BDDfy/blob/master/src/TestStack.BDDfy/Reporters/TextReporter.cs
	public class FixedTextReporter : IProcessor
	{
		private readonly List<Exception> _exceptions = new List<Exception>();
		private readonly StringBuilder _text = new StringBuilder();
		private int _longestStepSentence;

		public virtual ConsoleColor ForegroundColor { get; set; }

		public void Process(Story story)
		{
			ReportStoryHeader(story);

			var allSteps = story.Scenarios.SelectMany(s => s.Steps)
				.Select(GetStepWithLines)
				.ToList();
			if (allSteps.Any())
			{
				_longestStepSentence = allSteps.SelectMany(s => s.Item2.Select(l => l.Length)).Max();
			}

			foreach (var scenarioGroup in story.Scenarios.GroupBy(s => s.Id))
			{
				if (scenarioGroup.Count() > 1)
				{
					// all scenarios in an example based scenario share the same header and narrative
					var exampleScenario =	scenarioGroup.First(); // Fixed bug here: original iterates story.Scenarios instead of scenarioGroup
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
					foreach (var scenario in scenarioGroup) // Fixed bug here: original iterates story.Scenarios instead of scenarioGroup
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

					var exampleScenario = scenarioGroup.First(); // Fixed bug here: original iterates story.Scenarios instead of scenarioGroup
					if (exampleScenario.Example != null)
					{
						WriteLine();
						WriteExamples(exampleScenario, scenarioGroup);
					}

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

		private void ReportTags(List<string> tags)
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
			if (story.Metadata == null || story.Metadata.Type == null)
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
				var paddedFirstLine = stepAndLines.Item2[0].PadRight(_longestStepSentence + 5);
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

			if (step.Result == Result.Inconclusive || step.Result == Result.NotImplemented)
			{
				ForegroundColor = ConsoleColor.Yellow;
			}
			else if (step.Result == Result.Failed)
			{
				ForegroundColor = ConsoleColor.Red;
			}
			else if (step.Result == Result.NotExecuted)
			{
				ForegroundColor = ConsoleColor.Gray;
			}

			WriteLine(message);
			ForegroundColor = ConsoleColor.White;
		}

		private string CreateExceptionMessage(Step step)
		{
			_exceptions.Add(step.Exception);

			var exceptionReference = $"[Details at {_exceptions.Count} below]";
			if (!string.IsNullOrEmpty(step.Exception.Message))
			{
				return $"[{FlattenExceptionMessage(step.Exception.Message)}] {exceptionReference}";
			}

			return $"{exceptionReference}";
		}

		private void ReportExceptions()
		{
			WriteLine();
			if (_exceptions.Count == 0)
			{
				return;
			}

			Write("Exceptions:");

			for (var index = 0; index < _exceptions.Count; index++)
			{
				var exception = _exceptions[index];
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
			ForegroundColor = ConsoleColor.White;
			WriteLine();
			WriteLine("Scenario: " + scenario.Title);
		}

		public override string ToString()
		{
			return _text.ToString();
		}

		protected virtual void WriteLine(string text = null)
		{
			_text.AppendLine(text);
		}

		protected virtual void WriteLine(string text, params object[] args)
		{
			_text.AppendLine(string.Format(text, args));
		}

		protected virtual void Write(string text, params object[] args)
		{
			_text.AppendFormat(text, args);
		}
	}
}