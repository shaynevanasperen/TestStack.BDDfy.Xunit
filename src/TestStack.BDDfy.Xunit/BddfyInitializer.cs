using TestStack.BDDfy.Configuration;

namespace TestStack.BDDfy.Xunit
{
	public class BddfyInitializer
	{
		static BddfyInitializer()
		{
			Configurator.IdGenerator = new GuiKeyGenerator();
			Configurator.Processors.Add(() => Xunit2BddfyTextReporter.Instance);
			Configurator.Processors.ConsoleReport.Disable();
			Configurator.BatchProcessors.Add(new BatchBddfyConsoleReporter());
		}

		public static void EnsureInitialized() { }
	}
}