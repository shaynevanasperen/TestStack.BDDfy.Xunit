using System;
using System.Threading;
using Xunit.Abstractions;

namespace TestStack.BDDfy.Xunit
{
	public class Xunit2BddfyTextReporter : ThreadsafeBddfyTextReporter
	{
		public static readonly Xunit2BddfyTextReporter Instance = new Xunit2BddfyTextReporter();

		static readonly AsyncLocal<ITestOutputHelper> Output = new AsyncLocal<ITestOutputHelper>();

		Xunit2BddfyTextReporter() { }

		public void RegisterOutput(ITestOutputHelper output)
		{
			Output.Value = output;
		}

		public override void Process(Story story)
		{
			try
			{
				base.Process(story);
				Output.Value?.WriteLine(Text.Value.ToString());
			}
			catch (Exception exception)
			{
				Console.WriteLine(exception);
				throw;
			}
		}
	}
}
