using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
}